using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Discord: the user's own application, kept in the secret store, and a hand-rolled client for Discord's local RPC:
    /// the handshake, AUTHORIZE, the token exchange, AUTHENTICATE and SUBSCRIBE, over the transport's raw frames. The
    /// call it then follows is in ExtensionCore.DiscordCall.cs.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        // The secret store's names. Nothing of Discord's goes to Playnite's plain settings.
        private const string ClientIdSecret = "Discord.ClientId";
        private const string ClientSecretSecret = "Discord.ClientSecret";
        private const string AccessTokenSecret = "Discord.AccessToken";
        private const string RefreshTokenSecret = "Discord.RefreshToken";
        private const string ExpiresAtSecret = "Discord.ExpiresAt";

        /// <summary>
        /// The redirect the user adds under OAuth2, which the token exchange has to name. AUTHORIZE leaves it out, as
        /// Discord refuses one there and takes the app's own. Nothing listens there: RPC's AUTHORIZE hands the code back itself.
        /// </summary>
        private const string RedirectUri = "http://127.0.0.1";

        private const string NotConnected = "Not connected";
        private const string Connecting = "Connecting...";

        private static readonly string[] Scopes = { "rpc", "identify", "rpc.voice.read", "rpc.voice.write", "rpc.notifications.read" };

        /// <summary>The voice events the Discord tab follows, one SUBSCRIBE each.</summary>
        private static readonly string[] VoiceEvents = { "VOICE_CHANNEL_SELECT", "VOICE_CONNECTION_STATUS", "VOICE_SETTINGS_UPDATE" };

        private readonly IDiscordTransport discord;
        private readonly IDiscordOAuth discordOAuth;
        private readonly ISecretStore secrets;

        private DiscordStep discordStep;

        /// <summary>The access token to sign in with once Discord is READY: the saved one at start-up, or null to AUTHORIZE first.</summary>
        private string signInToken;

        /// <summary>The commands sent and not yet answered, by nonce, which Discord's replies carry back.</summary>
        private readonly Dictionary<string, string> awaitingReplies = new Dictionary<string, string>();
        private int lastNonce;

        /// <summary>The Discord user signed in, by display name, else username.</summary>
        private string discordUser;

        /// <summary>Where Connect has got to.</summary>
        private enum DiscordStep
        {
            /// <summary>Not connected, nor connecting: frames and token answers are ignored.</summary>
            Closed,

            /// <summary>The handshake sent, waiting for Discord's READY.</summary>
            Handshake,

            /// <summary>AUTHORIZE sent: the user approves the request in Discord.</summary>
            Authorize,

            /// <summary>The code sent to the token endpoint, waiting for its answer.</summary>
            TokenExchange,

            /// <summary>AUTHENTICATE sent.</summary>
            Authenticate,

            /// <summary>Signed in.</summary>
            Connected,
        }

        /// <summary>The Client ID and Secret saved before, and whether tokens are, read when the core is made.</summary>
        private void LoadDiscordApp()
        {
            Settings.Discord.ClientId = secrets.Read(ClientIdSecret);
            Settings.Discord.HasClientSecret = secrets.Read(ClientSecretSecret) != null;
            Settings.Discord.HasTokens = secrets.Read(AccessTokenSecret) != null;
        }

        /// <summary>
        /// Playnite has started: with tokens saved from before, Discord is signed in with them, without asking again. It
        /// waits for this rather than the constructor, so that the transport can reach the core before frames come back.
        /// </summary>
        public void OnApplicationStarted()
        {
            var token = secrets.Read(AccessTokenSecret);
            if (token != null && Settings.Discord.ClientId != null)
            {
                StartDiscord(token);
            }
        }

        /// <summary>Settings' Client ID field, pasted or typed: saved at once, trimmed; empty forgets it.</summary>
        public void SetDiscordClientId(string text)
        {
            Settings.Discord.ClientId = SaveSecret(ClientIdSecret, text);
        }

        /// <summary>Settings' Client Secret field, pasted or typed: saved at once, trimmed, and never shown; empty forgets it.</summary>
        public void SetDiscordClientSecret(string text)
        {
            Settings.Discord.HasClientSecret = SaveSecret(ClientSecretSecret, text) != null;
        }

        private string SaveSecret(string name, string text)
        {
            var value = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            if (value == null)
            {
                secrets.Delete(name);
            }
            else
            {
                secrets.Write(name, value);
            }

            return value;
        }

        /// <summary>
        /// Settings' Connect: the handshake with Discord, then its Authorize prompt for the user to approve, the token
        /// exchange and signing in. Does nothing without a Client ID and Secret (<see cref="DiscordSettingsState.CanConnect"/>).
        /// </summary>
        public void ConnectDiscord()
        {
            if (Settings.Discord.CanConnect)
            {
                StartDiscord(null);
            }
        }

        /// <summary>Settings' Disconnect: closes the connection and forgets the tokens. The Client ID and Secret stay.</summary>
        public void DisconnectDiscord()
        {
            // Greyed with nothing to disconnect: pressed then, it leaves the status line saying what last went wrong.
            if (!Settings.Discord.CanDisconnect)
            {
                return;
            }

            secrets.Delete(AccessTokenSecret);
            secrets.Delete(RefreshTokenSecret);
            secrets.Delete(ExpiresAtSecret);
            Settings.Discord.HasTokens = false;
            CloseDiscord(NotConnected);
        }

        /// <summary>Opens the pipe and says hello; once Discord is READY, signs in with <paramref name="token"/>, or AUTHORIZEs first when null.</summary>
        private void StartDiscord(string token)
        {
            if (!discord.Open())
            {
                CloseDiscord("Discord isn't running");
                return;
            }

            signInToken = token;
            awaitingReplies.Clear();
            discordStep = DiscordStep.Handshake;
            Settings.Discord.IsConnecting = true;
            Settings.Discord.StatusText = Connecting;
            discord.Send(DiscordOpcode.Handshake, new JObject { ["v"] = 1, ["client_id"] = Settings.Discord.ClientId }.ToString(Formatting.None));
        }

        /// <summary>One frame from Discord's pipe, raw: its opcode and JSON.</summary>
        public void OnDiscordFrame(DiscordOpcode opcode, string json)
        {
            if (discordStep == DiscordStep.Closed)
            {
                return;
            }

            if (opcode == DiscordOpcode.Ping)
            {
                discord.Send(DiscordOpcode.Pong, json);
                return;
            }

            var frame = Parse(json);
            if (opcode == DiscordOpcode.Close)
            {
                var message = (string)frame?["message"];
                CloseDiscord(string.IsNullOrEmpty(message) ? "Discord closed the connection" : "Discord closed the connection: " + message);
                return;
            }

            if (opcode != DiscordOpcode.Frame || frame == null)
            {
                return;
            }

            var cmd = (string)frame["cmd"];
            var evt = (string)frame["evt"];
            if (cmd == "DISPATCH")
            {
                if (evt == "READY" && discordStep == DiscordStep.Handshake)
                {
                    OnDiscordReady();
                }
                else if (discordStep == DiscordStep.Connected)
                {
                    OnDiscordEvent(evt, frame["data"] as JObject);
                }

                return;
            }

            var nonce = (string)frame["nonce"];
            if (nonce == null || !awaitingReplies.TryGetValue(nonce, out var sent))
            {
                return;
            }

            awaitingReplies.Remove(nonce);
            var data = frame["data"] as JObject;
            if (evt == "ERROR")
            {
                // Refused while connecting, Connect has failed; once signed in, as a voice setting Discord won't change,
                // the connection stays up and the event that would have followed never comes.
                if (discordStep != DiscordStep.Connected)
                {
                    CloseDiscord("Couldn't connect to Discord: " + (string)data?["message"]);
                }
                else
                {
                    OnDiscordRefused(sent, nonce);
                }

                return;
            }

            if (sent == "AUTHORIZE" && discordStep == DiscordStep.Authorize)
            {
                discordStep = DiscordStep.TokenExchange;
                Settings.Discord.StatusText = Connecting;
                discordOAuth.RequestToken(Settings.Discord.ClientId, secrets.Read(ClientSecretSecret), new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = (string)data?["code"],
                    ["redirect_uri"] = RedirectUri,
                });
            }
            else if (sent == "AUTHENTICATE" && discordStep == DiscordStep.Authenticate)
            {
                OnDiscordSignedIn(data?["user"] as JObject);
            }
            else if (discordStep == DiscordStep.Connected)
            {
                OnDiscordReply(sent, nonce, data);
            }
        }

        private void OnDiscordReady()
        {
            if (signInToken != null)
            {
                Authenticate(signInToken);
                return;
            }

            discordStep = DiscordStep.Authorize;
            Settings.Discord.StatusText = "Approve the request in Discord";
            SendCommand("AUTHORIZE", new JObject
            {
                ["client_id"] = Settings.Discord.ClientId,
                ["scopes"] = new JArray(Scopes),
            });
        }

        /// <summary>
        /// The token endpoint's answer: its HTTP status and body, or status 0 with the error's message when no answer
        /// came. Granted, the tokens are saved and Discord signed in with them.
        /// </summary>
        public void OnDiscordTokenResponse(int status, string body)
        {
            if (discordStep != DiscordStep.TokenExchange)
            {
                return;
            }

            if (status == 0)
            {
                CloseDiscord("The token request failed: " + body);
                return;
            }

            var answer = Parse(body);
            if (status < 200 || status > 299)
            {
                var error = (string)answer?["error_description"] ?? (string)answer?["error"] ?? "HTTP " + status;
                CloseDiscord("Couldn't connect to Discord: " + error);
                return;
            }

            var accessToken = (string)answer?["access_token"];
            if (accessToken == null)
            {
                CloseDiscord("Couldn't connect to Discord: no access token came back");
                return;
            }

            secrets.Write(AccessTokenSecret, accessToken);
            secrets.Write(RefreshTokenSecret, (string)answer["refresh_token"] ?? "");
            var expiresIn = (double?)answer["expires_in"] ?? 0;
            secrets.Write(ExpiresAtSecret, clock.Now.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture));
            Settings.Discord.HasTokens = true;
            Authenticate(accessToken);
        }

        private void Authenticate(string token)
        {
            discordStep = DiscordStep.Authenticate;
            SendCommand("AUTHENTICATE", new JObject { ["access_token"] = token });
        }

        /// <summary>
        /// AUTHENTICATE answered: the status line says who, the Discord tab appears, the voice events are followed, and
        /// Discord is asked for the call the user may already be in.
        /// </summary>
        private void OnDiscordSignedIn(JObject user)
        {
            discordStep = DiscordStep.Connected;
            discordUserId = (string)user?["id"];
            var globalName = (string)user?["global_name"];
            discordUser = string.IsNullOrEmpty(globalName) ? (string)user?["username"] : globalName;
            Settings.Discord.IsConnecting = false;
            Settings.Discord.IsConnected = true;
            Settings.Discord.StatusText = "Connected as " + discordUser;
            OnDiscordTabChanged();
            foreach (var voiceEvent in VoiceEvents)
            {
                SendCommand("SUBSCRIBE", new JObject(), voiceEvent);
            }

            AskForCall();
        }

        /// <summary>Discord's pipe closed by itself: Discord quit, or dropped the connection.</summary>
        public void OnDiscordClosed()
        {
            if (discordStep != DiscordStep.Closed)
            {
                CloseDiscord("Discord closed the connection");
            }
        }

        /// <summary>Ends the connection, or the connect under way, with <paramref name="status"/> on the status line.</summary>
        private void CloseDiscord(string status)
        {
            discord.Close();
            awaitingReplies.Clear();
            ForgetServers();
            discordStep = DiscordStep.Closed;
            signInToken = null;
            Settings.Discord.IsConnecting = false;
            Settings.Discord.StatusText = status;
            ForgetCall();
            if (Settings.Discord.IsConnected)
            {
                Settings.Discord.IsConnected = false;
                OnDiscordTabChanged();
            }
        }

        /// <summary>Sends a command, under a nonce of its own that Discord's reply carries back, and returns the nonce.</summary>
        private string SendCommand(string cmd, JObject args, string evt = null)
        {
            var nonce = (++lastNonce).ToString(CultureInfo.InvariantCulture);
            awaitingReplies[nonce] = cmd;
            var frame = new JObject { ["cmd"] = cmd, ["args"] = args, ["nonce"] = nonce };
            if (evt != null)
            {
                frame["evt"] = evt;
            }

            discord.Send(DiscordOpcode.Frame, frame.ToString(Formatting.None));
            return nonce;
        }

        private static JObject Parse(string json)
        {
            try
            {
                return string.IsNullOrEmpty(json) ? null : JObject.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Discord connected or went with the Guide open: the Discord tab's icon comes or goes. Open on the Discord tab as
        /// it goes, the Guide falls back to the landing tab.
        /// </summary>
        private void OnDiscordTabChanged()
        {
            if (!Guide.IsOpen)
            {
                return;
            }

            if (Guide.Tab == GuideTabKind.Discord && !Settings.Discord.IsConnected)
            {
                ShowTab(GuideTabKind.Landing);
            }
            else
            {
                RefreshTabs();
            }
        }

        /// <summary>The Discord tab: the call, or, out of one, who is signed in and that they're not in a voice channel.</summary>
        private void ShowDiscordTab()
        {
            Guide.Items = DiscordTabItems();
            Guide.FocusedItem = DiscordTabFocus(Guide.Items);
        }
    }
}
