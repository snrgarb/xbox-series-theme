using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class DiscordTests
    {
        private const string ClientId = "1290000000000000001";
        private const string Secret = "s3cr3t-s3cr3t-s3cr3t-s3cr3t-0001";

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeDiscordTransport transport = new FakeDiscordTransport();
        private readonly FakeDiscordOAuth oauth = new FakeDiscordOAuth();
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private ExtensionCore core;

        public DiscordTests()
        {
            core = NewCore();
        }

        private DiscordSettingsState Discord => core.Settings.Discord;

        [Fact]
        public void Settings_Discord_starts_not_connected_with_the_checklist_and_Connect_unavailable()
        {
            Assert.Equal("https://discord.com/developers/applications", Discord.PortalUrl);
            Assert.Contains(Discord.Steps, s => s.Contains("discord.com/developers/applications"));
            Assert.Contains(Discord.Steps, s => s.Contains("OAuth2") && s.Contains("http://127.0.0.1"));
            Assert.Contains(Discord.Steps, s => s.Contains("App Testers"));
            Assert.Null(Discord.ClientId);
            Assert.False(Discord.HasClientSecret);
            Assert.Equal("Not connected", Discord.StatusText);
            Assert.False(Discord.IsConnected);
            Assert.False(Discord.IsConnecting);
            Assert.False(Discord.CanConnect);
            Assert.False(Discord.CanDisconnect);
        }

        [Fact]
        public void Connect_needs_both_the_Client_ID_and_the_Client_Secret()
        {
            core.SetDiscordClientId(ClientId);
            Assert.False(Discord.CanConnect);

            core.ConnectDiscord();
            Assert.Equal(0, transport.OpenCount);

            core.SetDiscordClientSecret(Secret);
            Assert.True(Discord.CanConnect);
        }

        [Fact]
        public void The_Client_ID_and_Secret_are_trimmed_and_saved_to_the_secret_store_and_the_Secret_is_never_shown()
        {
            core.SetDiscordClientId("  " + ClientId + "\r\n");
            core.SetDiscordClientSecret(" " + Secret + "\n");

            Assert.Equal(ClientId, Discord.ClientId);
            Assert.True(Discord.HasClientSecret);
            Assert.Equal(ClientId, secrets.Values["Discord.ClientId"]);
            Assert.Equal(Secret, secrets.Values["Discord.ClientSecret"]);
            Assert.DoesNotContain(typeof(DiscordSettingsState).GetProperties(), p => p.GetValue(Discord) as string == Secret);
        }

        [Fact]
        public void Pasting_nothing_forgets_the_Client_ID_and_Secret()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);

            core.SetDiscordClientId("  ");
            core.SetDiscordClientSecret(null);

            Assert.Null(Discord.ClientId);
            Assert.False(Discord.HasClientSecret);
            Assert.Empty(secrets.Values);
        }

        [Fact]
        public void The_Client_ID_and_Secret_come_back_from_the_secret_store_next_time()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);

            core = NewCore();

            Assert.Equal(ClientId, Discord.ClientId);
            Assert.True(Discord.HasClientSecret);
            Assert.True(Discord.CanConnect);
        }

        [Fact]
        public void Connect_with_Discord_not_running_says_so()
        {
            transport.IsRunning = false;
            EnterApp();

            core.ConnectDiscord();

            Assert.Equal("Discord isn't running", Discord.StatusText);
            Assert.False(Discord.IsConnecting);
            Assert.True(Discord.CanConnect);
            Assert.Empty(transport.Sent);
        }

        [Fact]
        public void Connect_says_hello_with_the_Client_ID_then_asks_for_authorization_once_Discord_is_ready()
        {
            EnterApp();

            core.ConnectDiscord();

            Assert.True(Discord.IsConnecting);
            Assert.Equal("Connecting...", Discord.StatusText);
            Assert.False(Discord.CanConnect);
            Assert.True(Discord.CanDisconnect);
            var hello = Assert.Single(transport.Sent);
            Assert.Equal(DiscordOpcode.Handshake, hello.Opcode);
            Assert.True(JToken.DeepEquals(JObject.Parse("{\"v\":1,\"client_id\":\"" + ClientId + "\"}"), JObject.Parse(hello.Json)));

            Ready();

            var authorize = transport.LastCommand("AUTHORIZE");
            Assert.Equal(ClientId, (string)authorize["args"]["client_id"]);
            Assert.Equal(
                new[] { "rpc", "identify", "rpc.voice.read", "rpc.voice.write", "rpc.notifications.read" },
                authorize["args"]["scopes"].Select(s => (string)s));
            Assert.Equal("http://127.0.0.1", (string)authorize["args"]["redirect_uri"]);
            Assert.False(string.IsNullOrEmpty((string)authorize["nonce"]));
            Assert.Equal("Approve the request in Discord", Discord.StatusText);
        }

        [Fact]
        public void Approving_in_Discord_exchanges_the_code_for_tokens_as_the_users_own_app()
        {
            EnterApp();
            core.ConnectDiscord();
            Ready();

            Reply("AUTHORIZE", new JObject { ["code"] = "the-code" });

            var request = Assert.Single(oauth.Requests);
            Assert.Equal(ClientId, request.ClientId);
            Assert.Equal(Secret, request.ClientSecret);
            Assert.Equal(
                new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["code"] = "the-code", ["redirect_uri"] = "http://127.0.0.1" },
                request.Form);
            Assert.True(Discord.IsConnecting);
            Assert.Equal("Connecting...", Discord.StatusText);
        }

        [Fact]
        public void The_tokens_are_saved_and_used_to_sign_in_then_the_status_says_who_is_connected()
        {
            AuthorizeAndAwaitToken();

            core.OnDiscordTokenResponse(200, Tokens("access-1", "refresh-1", 604800));

            Assert.Equal("access-1", secrets.Values["Discord.AccessToken"]);
            Assert.Equal("refresh-1", secrets.Values["Discord.RefreshToken"]);
            Assert.Equal(clock.Now.AddSeconds(604800).ToString("o", CultureInfo.InvariantCulture), secrets.Values["Discord.ExpiresAt"]);
            Assert.Equal("access-1", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);
            Assert.False(Discord.IsConnected);

            Reply("AUTHENTICATE", User("gabe", "Gabe"));

            Assert.Equal("Connected as Gabe", Discord.StatusText);
            Assert.True(Discord.IsConnected);
            Assert.False(Discord.IsConnecting);
            Assert.False(Discord.CanConnect);
            Assert.True(Discord.CanDisconnect);
        }

        [Fact]
        public void Connected_as_falls_back_to_the_username_without_a_display_name()
        {
            ConnectFully(User("gabe", null));

            Assert.Equal("Connected as gabe", Discord.StatusText);
        }

        [Fact]
        public void Once_signed_in_it_subscribes_to_the_voice_events_one_frame_each()
        {
            ConnectFully();

            var subscribes = transport.Commands.Where(c => (string)c["cmd"] == "SUBSCRIBE").ToList();
            Assert.Equal(new[] { "VOICE_CHANNEL_SELECT", "VOICE_CONNECTION_STATUS", "VOICE_SETTINGS_UPDATE" }, subscribes.Select(c => (string)c["evt"]));
        }

        [Fact]
        public void Every_command_carries_its_own_nonce()
        {
            ConnectFully();

            var nonces = transport.Commands.Select(c => (string)c["nonce"]).ToList();
            Assert.Equal(5, nonces.Count);
            Assert.Equal(nonces.Count, nonces.Distinct().Count());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Discords_pings_are_answered_with_the_same_payload(bool connected)
        {
            if (connected)
            {
                ConnectFully();
            }
            else
            {
                EnterApp();
                core.ConnectDiscord();
            }

            core.OnDiscordFrame(DiscordOpcode.Ping, "{\"t\":42}");

            Assert.Equal((DiscordOpcode.Pong, "{\"t\":42}"), transport.Sent.Last());
        }

        [Fact]
        public void A_reply_to_a_command_never_sent_is_ignored()
        {
            EnterApp();
            core.ConnectDiscord();
            Ready();

            core.OnDiscordFrame(DiscordOpcode.Frame, "{\"cmd\":\"AUTHORIZE\",\"evt\":null,\"data\":{\"code\":\"stray\"},\"nonce\":\"not-ours\"}");

            Assert.Empty(oauth.Requests);
        }

        [Fact]
        public void Cancelling_the_Authorize_prompt_in_Discord_shows_why_and_closes_the_connection()
        {
            EnterApp();
            core.ConnectDiscord();
            Ready();

            Error("AUTHORIZE", "OAuth2 Authorization cancelled");

            AssertFailed("Couldn't connect to Discord: OAuth2 Authorization cancelled");
            Assert.Empty(oauth.Requests);
        }

        [Fact]
        public void A_refused_token_request_shows_Discords_description_of_the_error()
        {
            AuthorizeAndAwaitToken();

            core.OnDiscordTokenResponse(401, "{\"error\":\"invalid_client\",\"error_description\":\"Invalid client secret\"}");

            AssertFailed("Couldn't connect to Discord: Invalid client secret");
            Assert.False(secrets.Values.ContainsKey("Discord.AccessToken"));
        }

        [Fact]
        public void A_refused_token_request_without_a_description_shows_the_error_code()
        {
            AuthorizeAndAwaitToken();

            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");

            AssertFailed("Couldn't connect to Discord: invalid_grant");
        }

        [Fact]
        public void A_token_request_that_got_no_answer_says_it_failed()
        {
            AuthorizeAndAwaitToken();

            core.OnDiscordTokenResponse(0, "No such host is known.");

            AssertFailed("The token request failed: No such host is known.");
        }

        [Fact]
        public void Discord_closing_the_pipe_mid_handshake_shows_it_and_stops()
        {
            EnterApp();
            core.ConnectDiscord();

            core.OnDiscordClosed();

            AssertFailed("Discord closed the connection");
        }

        [Fact]
        public void A_close_frame_from_Discord_shows_its_message()
        {
            EnterApp();
            core.ConnectDiscord();

            core.OnDiscordFrame(DiscordOpcode.Close, "{\"code\":4000,\"message\":\"Invalid Client ID\"}");

            AssertFailed("Discord closed the connection: Invalid Client ID");
        }

        [Fact]
        public void A_token_answer_after_Discord_closed_is_ignored()
        {
            AuthorizeAndAwaitToken();
            core.OnDiscordClosed();

            core.OnDiscordTokenResponse(200, Tokens("access-1", "refresh-1", 604800));

            Assert.False(secrets.Values.ContainsKey("Discord.AccessToken"));
            Assert.Equal("Discord closed the connection", Discord.StatusText);
        }

        [Fact]
        public void Disconnect_closes_the_connection_and_forgets_the_tokens_but_keeps_the_app()
        {
            ConnectFully();

            core.DisconnectDiscord();

            Assert.False(transport.IsOpen);
            Assert.Equal(new[] { "Discord.ClientId", "Discord.ClientSecret" }, secrets.Values.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal("Not connected", Discord.StatusText);
            Assert.False(Discord.IsConnected);
            Assert.True(Discord.CanConnect);
            Assert.False(Discord.CanDisconnect);
        }

        [Fact]
        public void Frames_after_Disconnect_are_ignored()
        {
            EnterApp();
            core.ConnectDiscord();
            core.DisconnectDiscord();

            Ready();

            Assert.Null(transport.LastCommand("AUTHORIZE"));
            Assert.Equal("Not connected", Discord.StatusText);
        }

        [Fact]
        public void Start_up_with_saved_tokens_signs_in_with_them_without_asking_again()
        {
            SaveTokens();
            core = NewCore();
            Assert.Equal(0, transport.OpenCount);

            core.OnApplicationStarted();

            Assert.True(Discord.IsConnecting);
            Assert.Equal(DiscordOpcode.Handshake, transport.Sent.Single().Opcode);
            Ready();
            Assert.Null(transport.LastCommand("AUTHORIZE"));
            Assert.Equal("saved-access", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);

            Reply("AUTHENTICATE", User("gabe", "Gabe"));

            Assert.Equal("Connected as Gabe", Discord.StatusText);
            Assert.True(Discord.IsConnected);
            Assert.Equal(3, transport.Commands.Count(c => (string)c["cmd"] == "SUBSCRIBE"));
            Assert.Empty(oauth.Requests);
        }

        [Fact]
        public void Start_up_without_saved_tokens_leaves_Discord_alone()
        {
            EnterApp();

            core.OnApplicationStarted();

            Assert.Equal(0, transport.OpenCount);
            Assert.Equal("Not connected", Discord.StatusText);
        }

        [Fact]
        public void Start_up_with_Discord_not_running_says_so_and_keeps_the_tokens()
        {
            SaveTokens();
            transport.IsRunning = false;
            core = NewCore();

            core.OnApplicationStarted();

            Assert.Equal("Discord isn't running", Discord.StatusText);
            Assert.False(Discord.IsConnected);
            Assert.Equal("saved-access", secrets.Values["Discord.AccessToken"]);
            Assert.True(Discord.CanDisconnect);
            Assert.DoesNotContain(GuideTabKind.Discord, OpenGuideTabs());
        }

        [Fact]
        public void Start_up_with_a_token_Discord_refuses_says_why_and_keeps_the_tokens()
        {
            SaveTokens();
            core = NewCore();
            core.OnApplicationStarted();
            Ready();

            Error("AUTHENTICATE", "Invalid access token");

            AssertFailed("Couldn't connect to Discord: Invalid access token");
            Assert.Equal("saved-access", secrets.Values["Discord.AccessToken"]);
            Assert.DoesNotContain(GuideTabKind.Discord, OpenGuideTabs());
        }

        [Fact]
        public void Disconnect_after_a_failed_start_up_forgets_the_tokens()
        {
            SaveTokens();
            transport.IsRunning = false;
            core = NewCore();
            core.OnApplicationStarted();

            core.DisconnectDiscord();

            Assert.False(secrets.Values.ContainsKey("Discord.AccessToken"));
            Assert.False(secrets.Values.ContainsKey("Discord.RefreshToken"));
            Assert.False(secrets.Values.ContainsKey("Discord.ExpiresAt"));
            Assert.False(Discord.CanDisconnect);
        }

        [Fact]
        public void The_Guide_has_no_Discord_tab_until_connected()
        {
            EnterApp();
            core.ConnectDiscord();
            Ready();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.ProfileAndSystem }, OpenGuideTabs());
        }

        [Fact]
        public void Once_connected_the_Discord_tab_sits_between_the_landing_tab_and_Profile_and_system()
        {
            ConnectFully();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.Discord, GuideTabKind.ProfileAndSystem }, OpenGuideTabs());
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
        }

        [Fact]
        public void RB_and_LB_move_through_the_Discord_tab()
        {
            ConnectFully();
            OpenGuideTabs();

            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.Discord, core.Guide.Tab);
            Assert.Equal(new[] { false, true, false }, core.Guide.Tabs.Select(t => t.IsSelected));

            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.ProfileAndSystem, core.Guide.Tab);

            Tap(ControllerButton.LeftShoulder);
            Assert.Equal(GuideTabKind.Discord, core.Guide.Tab);

            Tap(ControllerButton.LeftShoulder);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
        }

        [Fact]
        public void The_Discord_tab_says_who_is_connected_and_that_they_are_not_in_a_voice_channel()
        {
            ConnectFully(User("gabe", "Gabe"));
            OpenGuideTabs();

            Tap(ControllerButton.RightShoulder);

            Assert.Equal(
                new[] { (GuideItemKind.DiscordAccount, "Gabe"), (GuideItemKind.DiscordIdle, "You're not in a voice channel") },
                core.Guide.Items.Select(i => (i.Kind, i.Title)));
            Assert.Empty(core.Guide.Utilities);
            Assert.Equal(GuideItemKind.DiscordAccount, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_on_the_Discord_tabs_entries_does_nothing()
        {
            ConnectFully();
            OpenGuideTabs();
            Tap(ControllerButton.RightShoulder);
            sounds.Played.Clear();

            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.Discord, core.Guide.Tab);
            Assert.DoesNotContain(UiSound.Enter, sounds.Played);
        }

        [Fact]
        public void Connecting_with_the_Guide_open_adds_the_tab_without_moving_focus()
        {
            EnterApp();
            core.ConnectDiscord();
            OpenGuideTabs();
            var focused = core.Guide.FocusedItem;

            ConnectFully();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.Discord, GuideTabKind.ProfileAndSystem }, core.Guide.Tabs.Select(t => t.Kind));
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.Same(focused, core.Guide.FocusedItem);
        }

        [Fact]
        public void Disconnecting_with_the_Guide_on_the_Discord_tab_goes_back_to_the_landing_tab()
        {
            ConnectFully();
            OpenGuideTabs();
            Tap(ControllerButton.RightShoulder);

            core.DisconnectDiscord();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.ProfileAndSystem }, core.Guide.Tabs.Select(t => t.Kind));
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.True(core.Guide.Tabs[0].IsSelected);
            Assert.Equal("Home", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void Discord_closing_with_the_Guide_on_Profile_and_system_drops_the_tab_and_stays_put()
        {
            ConnectFully();
            OpenGuideTabs();
            Tap(ControllerButton.RightShoulder);
            Tap(ControllerButton.RightShoulder);

            core.OnDiscordClosed();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.ProfileAndSystem }, core.Guide.Tabs.Select(t => t.Kind));
            Assert.Equal(GuideTabKind.ProfileAndSystem, core.Guide.Tab);
            Assert.Equal("Discord closed the connection", Discord.StatusText);
        }

        private ExtensionCore NewCore() =>
            new ExtensionCore(clock, windows, new FakePlayniteHost(windows), sounds, new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), transport, oauth, secrets);

        private void EnterApp()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);
        }

        private void SaveTokens()
        {
            secrets.Values["Discord.ClientId"] = ClientId;
            secrets.Values["Discord.ClientSecret"] = Secret;
            secrets.Values["Discord.AccessToken"] = "saved-access";
            secrets.Values["Discord.RefreshToken"] = "saved-refresh";
            secrets.Values["Discord.ExpiresAt"] = clock.Now.AddDays(6).ToString("o", CultureInfo.InvariantCulture);
        }

        private void AuthorizeAndAwaitToken()
        {
            EnterApp();
            core.ConnectDiscord();
            Ready();
            Reply("AUTHORIZE", new JObject { ["code"] = "the-code" });
        }

        /// <summary>Connect, all the way through: the Authorize prompt approved, the tokens granted and signed in.</summary>
        private void ConnectFully(JObject user = null)
        {
            if (!Discord.IsConnecting)
            {
                EnterApp();
                core.ConnectDiscord();
            }

            Ready();
            Reply("AUTHORIZE", new JObject { ["code"] = "the-code" });
            core.OnDiscordTokenResponse(200, Tokens("access-1", "refresh-1", 604800));
            Reply("AUTHENTICATE", user ?? User("gabe", "Gabe"));
        }

        private void AssertFailed(string status)
        {
            Assert.Equal(status, Discord.StatusText);
            Assert.False(transport.IsOpen);
            Assert.False(Discord.IsConnecting);
            Assert.False(Discord.IsConnected);
            Assert.True(Discord.CanConnect);
        }

        /// <summary>Taps the Guide button and gives back the tabs the Guide opened with.</summary>
        private IReadOnlyList<GuideTabKind> OpenGuideTabs()
        {
            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            return core.Guide.Tabs.Select(t => t.Kind).ToList();
        }

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }

        private void Ready() =>
            core.OnDiscordFrame(DiscordOpcode.Frame, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1,\"config\":{}},\"nonce\":null}");

        /// <summary>Discord's reply to the last <paramref name="cmd"/> sent, matched to it by nonce.</summary>
        private void Reply(string cmd, JObject data) => core.OnDiscordFrame(DiscordOpcode.Frame, new JObject
        {
            ["cmd"] = cmd,
            ["evt"] = null,
            ["data"] = data,
            ["nonce"] = transport.LastCommand(cmd)["nonce"],
        }.ToString());

        /// <summary>Discord's error reply to the last <paramref name="cmd"/> sent.</summary>
        private void Error(string cmd, string message) => core.OnDiscordFrame(DiscordOpcode.Frame, new JObject
        {
            ["cmd"] = cmd,
            ["evt"] = "ERROR",
            ["data"] = new JObject { ["code"] = 5000, ["message"] = message },
            ["nonce"] = transport.LastCommand(cmd)["nonce"],
        }.ToString());

        private static JObject User(string username, string globalName) => new JObject
        {
            ["user"] = new JObject { ["id"] = "1", ["username"] = username, ["global_name"] = globalName },
            ["scopes"] = new JArray("rpc", "identify"),
        };

        private static string Tokens(string access, string refresh, int expiresIn) => new JObject
        {
            ["access_token"] = access,
            ["token_type"] = "Bearer",
            ["expires_in"] = expiresIn,
            ["refresh_token"] = refresh,
            ["scope"] = "rpc identify",
        }.ToString();
    }
}
