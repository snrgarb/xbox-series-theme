using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Discord's tokens kept good: refreshed a day before they expire, at sign-in and while connected; Discord asked to
    /// authorize again when a refresh won't do or a saved token is refused; and, when that fails too, the user asked to
    /// reconnect in Settings.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>How long before the tokens expire they are refreshed. Discord's last a week.</summary>
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromDays(1);

        private const string ReconnectInSettings = "Reconnect Discord in settings";

        /// <summary>
        /// Signed in, how far renewing the tokens in the background has got: <see cref="DiscordStep.Refresh"/>,
        /// <see cref="DiscordStep.Authorize"/> or <see cref="DiscordStep.TokenExchange"/>, and Connected while nothing is
        /// under way. The connection stays up throughout, so <see cref="discordStep"/> stays Connected.
        /// </summary>
        private DiscordStep renewal = DiscordStep.Connected;

        /// <summary>Whether this sign-in, or this renewal, has refreshed the tokens already, so a refused token isn't refreshed twice.</summary>
        private bool refreshTried;

        /// <summary>Whether AUTHORIZE was sent to stand in for tokens that wouldn't do, rather than by Connect: failing, the user has to reconnect.</summary>
        private bool reauthorizing;

        /// <summary>The refresh due a day before the tokens expire, while connected.</summary>
        private IDisposable refreshTimer;

        /// <summary>Where the tokens have got to: the step of signing in, or, once signed in, of renewing them in the background.</summary>
        private DiscordStep TokenStep => discordStep == DiscordStep.Connected ? renewal : discordStep;

        private void SetTokenStep(DiscordStep step)
        {
            if (discordStep == DiscordStep.Connected)
            {
                renewal = step;
            }
            else
            {
                discordStep = step;
            }
        }

        /// <summary>
        /// Signs in with the tokens saved from before, without asking the user again: false, doing nothing, without them or
        /// a Client ID. Discord READY, they are refreshed first when a day or less from expiring.
        /// </summary>
        private bool SignInWithSavedTokens()
        {
            var token = secrets.Read(AccessTokenSecret);
            if (token == null || Settings.Discord.ClientId == null)
            {
                return false;
            }

            StartDiscord(token);
            return true;
        }

        /// <summary>Discord READY for the saved token: refreshed first when it is a day or less from expiring, or of no known expiry.</summary>
        private void SignInWithSavedToken()
        {
            if (ExpiresAt() is DateTime expiresAt && expiresAt - clock.Now > RefreshMargin)
            {
                Authenticate(signInToken);
            }
            else
            {
                RefreshTokens();
            }
        }

        /// <summary>The saved tokens' expiry, or null for none, or none that reads as a date.</summary>
        private DateTime? ExpiresAt()
        {
            var text = secrets.Read(ExpiresAtSecret);
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt) ? expiresAt : (DateTime?)null;
        }

        /// <summary>Asks the token endpoint for new tokens for the saved refresh token; without one, Discord is asked to authorize again.</summary>
        private void RefreshTokens()
        {
            refreshTried = true;
            var refreshToken = secrets.Read(RefreshTokenSecret);
            if (string.IsNullOrEmpty(refreshToken))
            {
                Reauthorize();
                return;
            }

            SetTokenStep(DiscordStep.Refresh);
            discordOAuth.RequestToken(Settings.Discord.ClientId, secrets.Read(ClientSecretSecret), new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            });
        }

        /// <summary>Sends AUTHORIZE: Discord shows its prompt for the user to approve, and the code it gives back is exchanged for tokens.</summary>
        private void SendAuthorize()
        {
            SetTokenStep(DiscordStep.Authorize);
            if (discordStep != DiscordStep.Connected)
            {
                Settings.Discord.StatusText = "Approve the request in Discord";
            }

            SendCommand("AUTHORIZE", new JObject
            {
                ["client_id"] = Settings.Discord.ClientId,
                ["scopes"] = new JArray(Scopes),
            });
        }

        /// <summary>The tokens won't do and a refresh hasn't helped: Discord is asked to authorize again, as Connect does.</summary>
        private void Reauthorize()
        {
            reauthorizing = true;
            SendAuthorize();
        }

        /// <summary>
        /// Signing in, or AUTHORIZE while connected, refused with <paramref name="message"/>. A saved token refused is
        /// refreshed, or, refreshed already, authorized again; authorizing again refused, the user has to reconnect.
        /// </summary>
        private void OnSignInRefused(string sent, string message)
        {
            if (reauthorizing)
            {
                NeedReconnect();
            }
            else if (sent == "AUTHENTICATE" && signInToken != null)
            {
                if (refreshTried)
                {
                    Reauthorize();
                }
                else
                {
                    RefreshTokens();
                }
            }
            else
            {
                CloseDiscord("Couldn't connect to Discord: " + message);
            }
        }

        /// <summary>
        /// The token endpoint's answer to a refresh. Granted, the tokens are saved and Discord signed in with them, or,
        /// signed in already, the next refresh set; refused, or not answered, Discord is asked to authorize again.
        /// </summary>
        private void OnRefreshAnswer(int status, JObject answer)
        {
            var accessToken = status >= 200 && status <= 299 ? (string)answer?["access_token"] : null;
            if (accessToken == null)
            {
                Reauthorize();
                return;
            }

            OnTokensGranted(answer);
        }

        /// <summary>Tokens granted, by the code exchange or a refresh: saved, then signed in with, or, signed in already, refreshed in their turn.</summary>
        private void OnTokensGranted(JObject answer)
        {
            var accessToken = (string)answer["access_token"];
            secrets.Write(AccessTokenSecret, accessToken);
            secrets.Write(RefreshTokenSecret, (string)answer["refresh_token"] ?? secrets.Read(RefreshTokenSecret) ?? "");
            var expiresIn = (double?)answer["expires_in"] ?? 0;
            secrets.Write(ExpiresAtSecret, clock.Now.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture));
            Settings.Discord.HasTokens = true;
            if (discordStep == DiscordStep.Connected)
            {
                renewal = DiscordStep.Connected;
                ScheduleRefresh();
            }
            else
            {
                Authenticate(accessToken);
            }
        }

        /// <summary>The token exchange, or authorizing again, failed with <paramref name="status"/>: standing in for the tokens, the user has to reconnect.</summary>
        private void OnTokensRefused(string status)
        {
            if (reauthorizing)
            {
                NeedReconnect();
            }
            else
            {
                CloseDiscord(status);
            }
        }

        /// <summary>Sets the refresh due a day before the saved tokens expire, at once when they are nearer than that.</summary>
        private void ScheduleRefresh()
        {
            refreshTimer?.Dispose();
            refreshTried = false;
            reauthorizing = false;
            var due = (ExpiresAt() ?? clock.Now) - RefreshMargin - clock.Now;
            refreshTimer = clock.After(due > TimeSpan.Zero ? due : TimeSpan.Zero, () =>
            {
                refreshTimer = null;
                if (discordStep == DiscordStep.Connected && renewal == DiscordStep.Connected)
                {
                    RefreshTokens();
                }
            });
        }

        /// <summary>
        /// Neither the saved tokens nor authorizing again would do: the connection closes, Discord isn't tried again by
        /// itself, and Settings and the Discord tab ask the user to reconnect. The tokens stay saved until Connect
        /// replaces them or Disconnect forgets them.
        /// </summary>
        private void NeedReconnect()
        {
            Settings.Discord.NeedsReconnect = true;
            CloseDiscord(ReconnectInSettings);
        }

        /// <summary>Forgets the renewal under way and the refresh due, as the connection closes.</summary>
        private void ForgetRenewal()
        {
            refreshTimer?.Dispose();
            refreshTimer = null;
            renewal = DiscordStep.Connected;
            refreshTried = false;
            reauthorizing = false;
        }
    }
}
