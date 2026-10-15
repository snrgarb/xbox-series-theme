using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Discord's tokens kept good: refreshed before they expire, at start-up and while connected, Discord asked to
    /// authorize again when a refresh won't do, and the user asked to reconnect in Settings when that fails too.
    /// </summary>
    public class DiscordTokenTests : DiscordTestsBase
    {
        private DiscordSettingsState Discord => core.Settings.Discord;

        [Fact]
        public void Start_up_with_tokens_a_day_from_expiring_refreshes_them_first_then_signs_in_with_the_new_ones()
        {
            SaveTokens(clock.Now.AddDays(7));
            clock.Advance(TimeSpan.FromDays(6.5));
            StartUp();

            Assert.Null(transport.LastCommand("AUTHENTICATE"));
            Assert.Null(transport.LastCommand("AUTHORIZE"));
            var request = Assert.Single(oauth.Requests);
            Assert.Equal(ClientId, request.ClientId);
            Assert.Equal(Secret, request.ClientSecret);
            Assert.Equal(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = "saved-refresh" }, request.Form);
            Assert.True(Discord.IsConnecting);

            core.OnDiscordTokenResponse(200, Tokens("access-2", "refresh-2"));

            AssertSaved("access-2", "refresh-2", clock.Now.AddSeconds(604800));
            Assert.Equal("access-2", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);

            SignIn();

            Assert.True(Discord.IsConnected);
            Assert.Equal("Connected as gabe", Discord.StatusText);
        }

        [Theory]
        [InlineData("past")]
        [InlineData(null)]
        [InlineData("not a date")]
        public void Start_up_with_tokens_already_expired_or_of_no_known_expiry_refreshes_them_first(string expiresAt)
        {
            SaveTokens(clock.Now.AddDays(7));
            if (expiresAt == "past")
            {
                clock.Advance(TimeSpan.FromDays(8));
            }
            else if (expiresAt == null)
            {
                secrets.Values.Remove("Discord.ExpiresAt");
            }
            else
            {
                secrets.Values["Discord.ExpiresAt"] = expiresAt;
            }

            StartUp();

            Assert.Equal("refresh_token", Assert.Single(oauth.Requests).Form["grant_type"]);
            Assert.Null(transport.LastCommand("AUTHENTICATE"));
        }

        [Fact]
        public void Start_up_with_tokens_good_for_days_yet_signs_in_without_refreshing()
        {
            SaveTokens(clock.Now.AddDays(7));
            clock.Advance(TimeSpan.FromDays(5));
            StartUp();

            Assert.Equal("saved-access", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);
            Assert.Empty(oauth.Requests);
        }

        [Fact]
        public void Once_signed_in_the_tokens_are_refreshed_a_day_before_they_expire_over_the_open_connection()
        {
            ConnectFully();
            var authenticates = Sent("AUTHENTICATE").Count;

            clock.Advance(TimeSpan.FromDays(6) - TimeSpan.FromMinutes(1));
            Assert.Single(oauth.Requests);

            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Equal(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = "refresh-1" }, oauth.Requests.Last().Form);

            core.OnDiscordTokenResponse(200, Tokens("access-2", "refresh-2"));

            AssertSaved("access-2", "refresh-2", clock.Now.AddSeconds(604800));
            Assert.Equal(authenticates, Sent("AUTHENTICATE").Count);
            Assert.True(transport.IsOpen);
            Assert.True(Discord.IsConnected);
            Assert.Equal("Connected as gabe", Discord.StatusText);

            // The new tokens are refreshed in their turn, a day before they expire.
            clock.Advance(TimeSpan.FromDays(6));
            Assert.Equal(3, oauth.Requests.Count);
            Assert.Equal("refresh-2", oauth.Requests.Last().Form["refresh_token"]);
        }

        [Fact]
        public void Signing_in_after_a_start_up_refresh_refreshes_again_a_day_before_the_new_tokens_expire()
        {
            SaveTokens(clock.Now.AddHours(1));
            StartUp();
            core.OnDiscordTokenResponse(200, Tokens("access-2", "refresh-2"));
            SignIn();

            clock.Advance(TimeSpan.FromDays(6));

            Assert.Equal(2, oauth.Requests.Count);
            Assert.Equal("refresh-2", oauth.Requests.Last().Form["refresh_token"]);
        }

        [Fact]
        public void Disconnect_stops_the_refresh_and_a_refresh_answer_after_it_is_ignored()
        {
            ConnectFully();
            clock.Advance(TimeSpan.FromDays(6));
            Assert.Equal(2, oauth.Requests.Count);

            core.DisconnectDiscord();
            core.OnDiscordTokenResponse(200, Tokens("access-2", "refresh-2"));

            Assert.False(secrets.Values.ContainsKey("Discord.AccessToken"));
            Assert.Equal("Not connected", Discord.StatusText);
            Assert.False(Discord.IsConnected);
        }

        [Fact]
        public void Disconnect_cancels_the_refresh_waiting_to_come()
        {
            ConnectFully();

            core.DisconnectDiscord();
            clock.Advance(TimeSpan.FromDays(8));

            Assert.Single(oauth.Requests);
        }

        [Fact]
        public void Discord_closing_cancels_the_refresh_waiting_to_come()
        {
            ConnectFully();

            core.OnDiscordClosed();
            clock.Advance(TimeSpan.FromDays(8));

            Assert.Single(oauth.Requests);
            Assert.Equal("access-1", secrets.Values["Discord.AccessToken"]);
        }

        [Theory]
        [InlineData(400, "{\"error\":\"invalid_grant\"}")]
        [InlineData(0, "No such host is known.")]
        [InlineData(200, "{\"token_type\":\"Bearer\"}")]
        public void A_refresh_that_fails_at_start_up_asks_Discord_to_authorize_again_then_signs_in(int status, string body)
        {
            SaveTokens(clock.Now.AddHours(1));
            StartUp();

            core.OnDiscordTokenResponse(status, body);

            Assert.Equal(ClientId, (string)transport.LastCommand("AUTHORIZE")["args"]["client_id"]);
            Assert.Equal("Approve the request in Discord", Discord.StatusText);
            Assert.True(Discord.IsConnecting);

            Reply("AUTHORIZE", new JObject { ["code"] = "code-2" });

            Assert.Equal(
                new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["code"] = "code-2", ["redirect_uri"] = "http://127.0.0.1" },
                oauth.Requests.Last().Form);

            core.OnDiscordTokenResponse(200, Tokens("access-3", "refresh-3"));

            AssertSaved("access-3", "refresh-3", clock.Now.AddSeconds(604800));
            Assert.Equal("access-3", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);

            SignIn();

            Assert.True(Discord.IsConnected);
            Assert.False(Discord.NeedsReconnect);
        }

        [Fact]
        public void A_refresh_that_fails_while_connected_asks_Discord_to_authorize_again_and_stays_connected()
        {
            ConnectFully();
            var authenticates = Sent("AUTHENTICATE").Count;
            clock.Advance(TimeSpan.FromDays(6));

            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");

            Assert.Equal(2, Sent("AUTHORIZE").Count);
            Assert.True(Discord.IsConnected);
            Assert.Equal("Connected as gabe", Discord.StatusText);

            Reply("AUTHORIZE", new JObject { ["code"] = "code-2" });
            core.OnDiscordTokenResponse(200, Tokens("access-3", "refresh-3"));

            AssertSaved("access-3", "refresh-3", clock.Now.AddSeconds(604800));
            Assert.Equal(authenticates, Sent("AUTHENTICATE").Count);
            Assert.True(Discord.IsConnected);
            Assert.Equal("Connected as gabe", Discord.StatusText);

            clock.Advance(TimeSpan.FromDays(6));
            Assert.Equal("refresh-3", oauth.Requests.Last().Form["refresh_token"]);
        }

        [Fact]
        public void A_saved_token_Discord_refuses_is_refreshed_then_signed_in_with()
        {
            SaveTokens(clock.Now.AddDays(7));
            StartUp();

            Error("AUTHENTICATE", "Invalid access token");

            Assert.Equal("refresh_token", Assert.Single(oauth.Requests).Form["grant_type"]);
            Assert.True(transport.IsOpen);
            Assert.True(Discord.IsConnecting);

            core.OnDiscordTokenResponse(200, Tokens("access-2", "refresh-2"));

            Assert.Equal("access-2", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);
            SignIn();
            Assert.True(Discord.IsConnected);
        }

        [Fact]
        public void A_refreshed_token_Discord_refuses_asks_Discord_to_authorize_again_rather_than_refreshing_twice()
        {
            SaveTokens(clock.Now.AddDays(7));
            StartUp();
            Error("AUTHENTICATE", "Invalid access token");
            core.OnDiscordTokenResponse(200, Tokens("access-2", "refresh-2"));

            Error("AUTHENTICATE", "Invalid access token");

            Assert.Single(oauth.Requests);
            Assert.NotNull(transport.LastCommand("AUTHORIZE"));
            Assert.Equal("Approve the request in Discord", Discord.StatusText);
        }

        [Theory]
        [InlineData("authorize")]
        [InlineData("exchange")]
        [InlineData("authenticate")]
        public void When_authorizing_again_fails_too_the_user_is_asked_to_reconnect_in_Settings(string failing)
        {
            SaveTokens(clock.Now.AddDays(7));
            StartUp();
            Error("AUTHENTICATE", "Invalid access token");
            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");
            var changed = new List<string>();
            Discord.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            FailAuthorizingAgain(failing);

            AssertNeedsReconnect();
            Assert.Contains(nameof(DiscordSettingsState.NeedsReconnect), changed);
        }

        [Fact]
        public void When_authorizing_again_fails_while_connected_the_connection_closes_and_the_user_is_asked_to_reconnect()
        {
            ConnectFully();
            OpenGuide();
            clock.Advance(TimeSpan.FromDays(6));
            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");

            Error("AUTHORIZE", "OAuth2 Authorization cancelled");

            AssertNeedsReconnect();
            Assert.DoesNotContain(Guide.Tabs, t => t.Kind == GuideTabKind.Discord);
        }

        [Fact]
        public void Connect_from_Settings_after_needing_to_reconnect_authorizes_afresh_and_clears_it()
        {
            NeedReconnect();

            core.ConnectDiscord();

            Assert.True(Discord.NeedsReconnect);
            ConnectFully();
            Assert.False(Discord.NeedsReconnect);
            Assert.Equal("Connected as gabe", Discord.StatusText);
        }

        [Fact]
        public void Disconnect_after_needing_to_reconnect_clears_it_and_forgets_the_tokens()
        {
            NeedReconnect();

            core.DisconnectDiscord();

            Assert.False(Discord.NeedsReconnect);
            Assert.False(secrets.Values.ContainsKey("Discord.AccessToken"));
            Assert.Equal("Not connected", Discord.StatusText);
        }

        [Fact]
        public void Connect_from_Settings_refused_says_why_rather_than_asking_to_reconnect()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);
            core.ConnectDiscord();
            Ready();

            Error("AUTHORIZE", "OAuth2 Authorization cancelled");

            Assert.Equal("Couldn't connect to Discord: OAuth2 Authorization cancelled", Discord.StatusText);
            Assert.False(Discord.NeedsReconnect);
        }

        [Fact]
        public void Signing_in_with_tokens_from_Connect_that_Discord_refuses_says_why_rather_than_refreshing()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);
            core.ConnectDiscord();
            Ready();
            Reply("AUTHORIZE", new JObject { ["code"] = "the-code" });
            core.OnDiscordTokenResponse(200, Tokens("access-1", "refresh-1"));

            Error("AUTHENTICATE", "Invalid access token");

            Assert.Single(oauth.Requests);
            Assert.Equal("Couldn't connect to Discord: Invalid access token", Discord.StatusText);
            Assert.False(Discord.NeedsReconnect);
        }

        private void SaveTokens(DateTime expiresAt)
        {
            secrets.Values["Discord.ClientId"] = ClientId;
            secrets.Values["Discord.ClientSecret"] = Secret;
            secrets.Values["Discord.AccessToken"] = "saved-access";
            secrets.Values["Discord.RefreshToken"] = "saved-refresh";
            secrets.Values["Discord.ExpiresAt"] = expiresAt.ToString("o", CultureInfo.InvariantCulture);
        }

        /// <summary>Playnite starting again over the saved tokens, as far as Discord's READY.</summary>
        private void StartUp()
        {
            NewCore();
            core.OnApplicationStarted();
            Ready();
        }

        private void Ready() =>
            core.OnDiscordFrame(DiscordOpcode.Frame, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1,\"config\":{}},\"nonce\":null}");

        private void SignIn() => Reply("AUTHENTICATE", new JObject
        {
            ["user"] = new JObject { ["id"] = Me, ["username"] = "gabe", ["global_name"] = null },
            ["scopes"] = new JArray("rpc", "identify"),
        });

        /// <summary>Authorizing again, after a refused token and a failed refresh, failing at <paramref name="failing"/>.</summary>
        private void FailAuthorizingAgain(string failing)
        {
            if (failing == "authorize")
            {
                Error("AUTHORIZE", "OAuth2 Authorization cancelled");
                return;
            }

            Reply("AUTHORIZE", new JObject { ["code"] = "code-2" });
            if (failing == "exchange")
            {
                core.OnDiscordTokenResponse(401, "{\"error\":\"invalid_client\"}");
                return;
            }

            core.OnDiscordTokenResponse(200, Tokens("access-3", "refresh-3"));
            Error("AUTHENTICATE", "Invalid access token");
        }

        private void NeedReconnect()
        {
            SaveTokens(clock.Now.AddDays(7));
            StartUp();
            Error("AUTHENTICATE", "Invalid access token");
            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");
            Error("AUTHORIZE", "OAuth2 Authorization cancelled");
        }

        private void AssertNeedsReconnect()
        {
            Assert.True(Discord.NeedsReconnect);
            Assert.Equal("Reconnect Discord in settings", Discord.StatusText);
            Assert.False(transport.IsOpen);
            Assert.False(Discord.IsConnecting);
            Assert.False(Discord.IsConnected);
            Assert.True(Discord.CanConnect);
            Assert.True(Discord.CanDisconnect);
            Assert.True(secrets.Values.ContainsKey("Discord.AccessToken"));
        }

        private void AssertSaved(string access, string refresh, DateTime expiresAt)
        {
            Assert.Equal(access, secrets.Values["Discord.AccessToken"]);
            Assert.Equal(refresh, secrets.Values["Discord.RefreshToken"]);
            Assert.Equal(expiresAt.ToString("o", CultureInfo.InvariantCulture), secrets.Values["Discord.ExpiresAt"]);
        }

        private static string Tokens(string access, string refresh) => new JObject
        {
            ["access_token"] = access,
            ["token_type"] = "Bearer",
            ["expires_in"] = 604800,
            ["refresh_token"] = refresh,
            ["scope"] = "rpc identify",
        }.ToString();
    }
}
