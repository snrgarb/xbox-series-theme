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
    /// call it then follows is in ExtensionCore.DiscordCall.cs, and keeping the tokens good in ExtensionCore.DiscordTokens.cs.
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
        private const string DiscordNotRunning = "Discord isn't running";

        private static readonly string[] Scopes = { "rpc", "identify", "rpc.voice.read", "rpc.voice.write", "rpc.notifications.read" };

        /// <summary>The voice events the Discord tab follows, one SUBSCRIBE each.</summary>
        private static readonly string[] VoiceEvents = { "VOICE_CHANNEL_SELECT", "VOICE_CONNECTION_STATUS", "VOICE_SETTINGS_UPDATE" };

        private readonly IDiscordTransport discord;
        private readonly IDiscordOAuth discordOAuth;
        private readonly ISecretStore secrets;

        private DiscordStep discordStep;

        /// <summary>
        /// The saved access token to sign in with once Discord is READY, or null for Connect, which AUTHORIZEs first. It
        /// stays set through the sign-in, refreshed or not, marking it as one with saved tokens.
        /// </summary>
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

            /// <summary>The saved refresh token sent to the token endpoint, waiting for its answer.</summary>
            Refresh,

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
            SignInWithSavedTokens();
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

        /// <summary>
        /// Settings' Disconnect: closes the connection and forgets the tokens, and with them any need to reconnect. The
        /// Client ID and Secret stay.
        /// </summary>
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
            Settings.Discord.NeedsReconnect = false;
            CloseDiscord(NotConnected);
        }

        /// <summary>
        /// Opens the pipe and says hello; once Discord is READY, signs in with the saved <paramref name="token"/>, refreshed
        /// first when near expiring, or AUTHORIZEs first when null. Discord not running, it is tried again later while
        /// tokens are saved.
        /// </summary>
        private void StartDiscord(string token)
        {
            ForgetRenewal();
            ForgetProbe();
            if (!discord.Open())
            {
                CloseDiscord(DiscordNotRunning);
                ProbeLater();
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
                ProbeLater();
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
                // Refused while connecting, or authorizing again while connected, signing in has failed; once signed in,
                // as a voice setting Discord won't change, the connection stays up and the event that would have
                // followed never comes.
                if (discordStep != DiscordStep.Connected || (sent == "AUTHORIZE" && renewal == DiscordStep.Authorize))
                {
                    OnSignInRefused(sent, (string)data?["message"]);
                }
                else
                {
                    OnDiscordRefused(sent, nonce);
                }

                return;
            }

            if (sent == "AUTHORIZE" && TokenStep == DiscordStep.Authorize)
            {
                SetTokenStep(DiscordStep.TokenExchange);
                if (discordStep != DiscordStep.Connected)
                {
                    Settings.Discord.StatusText = Connecting;
                }

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
                SignInWithSavedToken();
            }
            else
            {
                SendAuthorize();
            }
        }

        /// <summary>
        /// The token endpoint's answer: its HTTP status and body, or status 0 with the error's message when no answer
        /// came. Granted, the tokens are saved and Discord signed in with them; an answer nothing is waiting for, as after
        /// Disconnect, is ignored.
        /// </summary>
        public void OnDiscordTokenResponse(int status, string body)
        {
            var step = TokenStep;
            if (step == DiscordStep.Refresh)
            {
                OnRefreshAnswer(status, status == 0 ? null : Parse(body));
                return;
            }

            if (step != DiscordStep.TokenExchange)
            {
                return;
            }

            if (status == 0)
            {
                OnTokensRefused("The token request failed: " + body);
                return;
            }

            var answer = Parse(body);
            if (status < 200 || status > 299)
            {
                var error = (string)answer?["error_description"] ?? (string)answer?["error"] ?? "HTTP " + status;
                OnTokensRefused("Couldn't connect to Discord: " + error);
                return;
            }

            if ((string)answer?["access_token"] == null)
            {
                OnTokensRefused("Couldn't connect to Discord: no access token came back");
                return;
            }

            OnTokensGranted(answer);
        }

        private void Authenticate(string token)
        {
            discordStep = DiscordStep.Authenticate;
            SendCommand("AUTHENTICATE", new JObject { ["access_token"] = token });
        }

        /// <summary>
        /// AUTHENTICATE answered: the status line says who, any need to reconnect is gone, the Discord tab appears or
        /// shows Discord again, the voice events are followed, Discord is asked for the call the user may already be in,
        /// and the tokens' refresh is set. Signed in again after Discord went, it is all done afresh, as the first time.
        /// </summary>
        private void OnDiscordSignedIn(JObject user)
        {
            discordStep = DiscordStep.Connected;
            Settings.Discord.NeedsReconnect = false;
            ResetProbes();
            ScheduleRefresh();
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

        /// <summary>
        /// Discord's pipe closed by itself: Discord quit, or dropped the connection. With tokens saved, Discord is waited
        /// for, and signed in again once back.
        /// </summary>
        public void OnDiscordClosed()
        {
            if (discordStep != DiscordStep.Closed)
            {
                CloseDiscord(ReconnectsByItself ? DiscordNotRunning : "Discord closed the connection");
                ProbeLater();
            }
        }

        /// <summary>
        /// Ends the connection, or the connect under way, with <paramref name="status"/> on the status line, and stops
        /// trying Discord again; the Discord tab, if showing, says why it can't show Discord.
        /// </summary>
        private void CloseDiscord(string status)
        {
            discord.Close();
            awaitingReplies.Clear();
            ForgetServers();
            ForgetProbe();
            discordStep = DiscordStep.Closed;
            signInToken = null;
            ForgetRenewal();
            Settings.Discord.IsConnecting = false;
            Settings.Discord.StatusText = status;
            Settings.Discord.IsConnected = false;
            ForgetCall();
            OnDiscordTabChanged();
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
        /// Discord signed in, went, or was disconnected with the Guide open: the Discord tab's icon comes or goes. Open on
        /// the Discord tab, the Guide shows it afresh, any flyout closed, unless it shows the same already; as the tab
        /// goes, the Guide falls back to the landing tab.
        /// </summary>
        private void OnDiscordTabChanged()
        {
            if (!Guide.IsOpen)
            {
                return;
            }

            if (Guide.Tab != GuideTabKind.Discord)
            {
                RefreshTabs();
            }
            else if (!Settings.Discord.HasDiscordTab)
            {
                ShowTab(GuideTabKind.Landing);
            }
            else if (Guide.Flyout != null || !UpdateInPlace(Guide.Items, DiscordTabItems()))
            {
                ShowTab(GuideTabKind.Discord);
            }
        }

        /// <summary>
        /// The Discord tab: the call, or, out of one, who is signed in and that they're not in a voice channel; not signed
        /// in, why it can't show Discord.
        /// </summary>
        private void ShowDiscordTab()
        {
            Guide.Items = DiscordTabItems();
            Guide.FocusedItem = DiscordTabFocus(Guide.Items);
        }
    }
}
