using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Discord out of reach with tokens saved: the Discord tab says so, the pipe is tried again with backoff until Discord
    /// is back, and signing in again follows everything the first sign-in did; the user asked to reconnect, it says that.
    /// </summary>
    public class DiscordReconnectTests : DiscordTestsBase
    {
        private DiscordSettingsState Discord => core.Settings.Discord;

        [Fact]
        public void Start_up_with_Discord_not_running_shows_the_Discord_tab_saying_so()
        {
            StartUpWithDiscordNotRunning();
            OpenDiscordTab();

            var line = Assert.Single(Guide.Items);
            Assert.Equal(GuideItemKind.DiscordUnavailable, line.Kind);
            Assert.Equal("Discord isn't running", line.Title);
            Assert.Same(line, Guide.FocusedItem);
            Assert.Equal("Discord isn't running", Discord.StatusText);
        }

        [Fact]
        public void A_and_the_d_pad_do_nothing_on_the_line_and_LB_and_RB_still_move_between_tabs()
        {
            StartUpWithDiscordNotRunning();
            OpenDiscordTab();
            var line = Guide.FocusedItem;
            sounds.Played.Clear();

            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadUp);

            Assert.True(Guide.IsOpen);
            Assert.Same(line, Guide.FocusedItem);
            Assert.Empty(sounds.Played);

            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.ProfileAndSystem, Guide.Tab);
            Tap(ControllerButton.LeftShoulder);
            Tap(ControllerButton.LeftShoulder);
            Assert.Equal(GuideTabKind.Landing, Guide.Tab);
        }

        [Fact]
        public void The_Guide_has_no_Discord_tab_before_the_first_Connect()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);
            core.OnApplicationStarted();

            OpenGuide();

            Assert.DoesNotContain(GuideTabKind.Discord, Guide.Tabs.Select(t => t.Kind));
        }

        [Fact]
        public void Discord_not_running_is_tried_again_after_2_4_8_16_and_30_seconds_then_every_minute()
        {
            StartUpWithDiscordNotRunning();
            Assert.Equal(1, transport.OpenCount);

            foreach (var seconds in new[] { 2, 4, 8, 16, 30, 60, 60, 60 })
            {
                var opens = transport.OpenCount;
                clock.Advance(TimeSpan.FromSeconds(seconds) - TimeSpan.FromMilliseconds(1));
                Assert.Equal(opens, transport.OpenCount);
                clock.Advance(TimeSpan.FromMilliseconds(1));
                Assert.Equal(opens + 1, transport.OpenCount);
            }

            Assert.Equal("Discord isn't running", Discord.StatusText);
            Assert.False(Discord.IsConnecting);
        }

        [Fact]
        public void Discord_starting_later_is_signed_in_with_the_saved_tokens_by_itself()
        {
            StartUpWithDiscordNotRunning();
            transport.IsRunning = true;

            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(DiscordOpcode.Handshake, transport.Sent.Single().Opcode);
            Assert.True(Discord.IsConnecting);
            Ready();
            Assert.Equal("saved-access", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);
            SignIn();

            Assert.True(Discord.IsConnected);
            Assert.Equal("Connected as gabe", Discord.StatusText);
            Assert.Null(transport.LastCommand("AUTHORIZE"));
            Assert.Empty(oauth.Requests);
            var opens = transport.OpenCount;
            clock.Advance(TimeSpan.FromMinutes(10));
            Assert.Equal(opens, transport.OpenCount);
        }

        [Fact]
        public void Signing_in_with_the_Guide_on_the_Discord_tab_shows_its_entries_with_the_usual_first_focus()
        {
            StartUpWithDiscordNotRunning();
            OpenDiscordTab();
            transport.IsRunning = true;

            clock.Advance(TimeSpan.FromSeconds(2));
            Ready();
            SignIn();

            Assert.Equal(new[] { GuideItemKind.DiscordAccount, GuideItemKind.DiscordIdle, GuideItemKind.DiscordServers }, Kinds());
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
        }

        [Fact]
        public void Discord_closing_keeps_the_Discord_tab_saying_Discord_isnt_running()
        {
            NotInCall();
            OpenDiscordTab();

            core.OnDiscordClosed();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.Discord, GuideTabKind.ProfileAndSystem }, Guide.Tabs.Select(t => t.Kind));
            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
            var line = Assert.Single(Guide.Items);
            Assert.Equal((GuideItemKind.DiscordUnavailable, "Discord isn't running"), (line.Kind, line.Title));
            Assert.Same(line, Guide.FocusedItem);
            Assert.Equal("Discord isn't running", Discord.StatusText);
            Assert.False(Discord.IsConnected);
        }

        [Fact]
        public void Discord_closing_with_the_Servers_flyout_open_closes_it_and_the_tab_says_so()
        {
            NotInCall();
            OpenDiscordTab();
            Tap(ControllerButton.A);
            Assert.NotNull(Guide.Flyout);

            core.OnDiscordClosed();

            Assert.Null(Guide.Flyout);
            Assert.Equal(GuideItemKind.DiscordUnavailable, Assert.Single(Guide.Items).Kind);
            Assert.Same(Guide.Items[0], Guide.FocusedItem);
        }

        [Fact]
        public void Discord_restarting_is_signed_in_again_with_every_subscription_back()
        {
            NotInCall();
            var first = SubscriptionsSince(0);
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            var shortcuts = ShortcutTitlesOnTheTab();

            core.OnDiscordClosed();
            Assert.Equal(1, transport.OpenCount);
            var sent = transport.Commands.Count;
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(2, transport.OpenCount);
            Ready();
            Assert.Equal("access-1", (string)transport.LastCommand("AUTHENTICATE")["args"]["access_token"]);
            SignIn();

            Assert.Equal(first, SubscriptionsSince(sent));
            Assert.Contains(transport.Commands.Skip(sent), c => (string)c["cmd"] == "GET_SELECTED_VOICE_CHANNEL");

            var afterSignIn = transport.Commands.Count;
            Reply("GET_SELECTED_VOICE_CHANNEL", VoiceChannel("500", "Late night", "9", State(Me, "gabe")));

            Assert.Equal(
                new[] { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP" }
                    .Select(e => "SUBSCRIBE " + e + " 500"),
                transport.Commands.Skip(afterSignIn).Where(c => (string)c["cmd"] == "SUBSCRIBE")
                    .Select(c => "SUBSCRIBE " + c["evt"] + " " + c["args"]["channel_id"]));
            Assert.True(core.Status.IsInDiscordCall);
            Assert.Equal("Connected as gabe", Discord.StatusText);
            Assert.Equal(shortcuts, ShortcutTitlesOnTheTab());
        }

        [Fact]
        public void Signing_in_again_starts_the_backoff_afresh()
        {
            StartUpWithDiscordNotRunning();
            clock.Advance(TimeSpan.FromSeconds(2 + 4 + 8));
            Assert.Equal(4, transport.OpenCount);
            transport.IsRunning = true;
            clock.Advance(TimeSpan.FromSeconds(16));
            Ready();
            SignIn();

            core.OnDiscordClosed();
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(6, transport.OpenCount);
        }

        [Fact]
        public void A_pipe_that_opens_but_closes_again_before_signing_in_is_tried_again_with_backoff()
        {
            SaveTokens();
            NewCore();
            core.OnApplicationStarted();

            core.OnDiscordClosed();
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(2, transport.OpenCount);
            core.OnDiscordClosed();
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(2, transport.OpenCount);
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(3, transport.OpenCount);
            Assert.True(Discord.IsConnecting);
        }

        [Fact]
        public void A_pipe_that_opens_at_start_up_but_never_says_READY_is_closed_after_10_seconds_and_tried_again_with_backoff()
        {
            SaveTokens();
            NewCore();
            core.OnApplicationStarted();

            clock.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromMilliseconds(1));
            Assert.True(transport.IsOpen);
            Assert.True(Discord.IsConnecting);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.False(transport.IsOpen);
            Assert.False(Discord.IsConnecting);
            Assert.Equal("Discord isn't running", Discord.StatusText);

            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(2, transport.OpenCount);
            Assert.True(transport.IsOpen);
        }

        [Fact]
        public void A_pipe_a_try_opens_that_never_says_READY_is_closed_after_10_seconds_and_the_backoff_goes_on()
        {
            StartUpWithDiscordNotRunning();
            transport.IsRunning = true;
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.True(transport.IsOpen);

            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.False(transport.IsOpen);
            clock.Advance(TimeSpan.FromSeconds(4) - TimeSpan.FromMilliseconds(1));
            Assert.Equal(2, transport.OpenCount);
            clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(3, transport.OpenCount);
        }

        [Fact]
        public void READY_in_time_keeps_the_pipe_open()
        {
            SaveTokens();
            NewCore();
            core.OnApplicationStarted();
            clock.Advance(TimeSpan.FromSeconds(5));
            Ready();

            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.True(transport.IsOpen);
            Assert.Equal(1, transport.OpenCount);
            Assert.NotNull(transport.LastCommand("AUTHENTICATE"));
        }

        [Fact]
        public void The_wait_for_READY_is_forgotten_as_the_pipe_closes_or_Disconnect_is_pressed()
        {
            SaveTokens();
            NewCore();
            core.OnApplicationStarted();
            clock.Advance(TimeSpan.FromSeconds(5));
            core.OnDiscordClosed();
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(2, transport.OpenCount);

            // Past when the first pipe's wait would have ended, the second is still waited for.
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.True(transport.IsOpen);

            core.DisconnectDiscord();
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Equal(2, transport.OpenCount);
            Assert.Equal("Not connected", Discord.StatusText);
        }

        [Fact]
        public void Connect_from_Settings_waits_for_READY_however_long_it_takes()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);
            core.ConnectDiscord();

            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.True(transport.IsOpen);
            Assert.True(Discord.IsConnecting);
        }

        [Fact]
        public void A_try_opening_the_pipe_with_the_Guide_on_the_Discord_tab_says_Connecting_to_Discord_until_signed_in()
        {
            StartUpWithDiscordNotRunning();
            OpenDiscordTab();
            transport.IsRunning = true;

            clock.Advance(TimeSpan.FromSeconds(2));

            var line = Assert.Single(Guide.Items);
            Assert.Equal((GuideItemKind.DiscordUnavailable, "Connecting to Discord"), (line.Kind, line.Title));
            Assert.Same(line, Guide.FocusedItem);
            Ready();
            Assert.Equal("Connecting to Discord", Assert.Single(Guide.Items).Title);
            SignIn();
            Assert.Equal(new[] { GuideItemKind.DiscordAccount, GuideItemKind.DiscordIdle, GuideItemKind.DiscordServers }, Kinds());
        }

        [Fact]
        public void The_Discord_tab_opened_while_signing_in_with_the_saved_tokens_says_Connecting_to_Discord()
        {
            SaveTokens();
            NewCore();
            core.OnApplicationStarted();

            OpenDiscordTab();

            Assert.Equal("Connecting to Discord", Assert.Single(Guide.Items).Title);
        }

        [Fact]
        public void Connect_failing_with_tokens_saved_leaves_the_Discord_tab_saying_to_reconnect_in_settings()
        {
            StartUpWithDiscordNotRunning();
            OpenDiscordTab();
            transport.IsRunning = true;

            core.ConnectDiscord();
            Ready();
            Error("AUTHORIZE", "OAuth2 Authorization cancelled");
            clock.Advance(TimeSpan.FromMinutes(10));

            Assert.Equal(2, transport.OpenCount);
            var line = Assert.Single(Guide.Items);
            Assert.Equal((GuideItemKind.DiscordUnavailable, "Reconnect Discord in settings"), (line.Kind, line.Title));
            Assert.Same(line, Guide.FocusedItem);
            Assert.False(Discord.NeedsReconnect);
        }

        [Fact]
        public void A_close_frame_with_tokens_saved_shows_its_message_and_tries_again()
        {
            NotInCall();

            core.OnDiscordFrame(DiscordOpcode.Close, "{\"code\":1000,\"message\":\"Shutting down\"}");

            Assert.Equal("Discord closed the connection: Shutting down", Discord.StatusText);
            Assert.False(Discord.IsConnected);
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(2, transport.OpenCount);
        }

        [Fact]
        public void Disconnect_stops_trying_and_takes_the_Discord_tab_away()
        {
            StartUpWithDiscordNotRunning();
            OpenDiscordTab();

            core.DisconnectDiscord();
            clock.Advance(TimeSpan.FromMinutes(10));

            Assert.Equal(1, transport.OpenCount);
            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.ProfileAndSystem }, Guide.Tabs.Select(t => t.Kind));
            Assert.Equal(GuideTabKind.Landing, Guide.Tab);
            Assert.Equal("Not connected", Discord.StatusText);
        }

        [Fact]
        public void Connect_while_waiting_for_Discord_replaces_the_try_waiting_to_come()
        {
            StartUpWithDiscordNotRunning();
            transport.IsRunning = true;

            core.ConnectDiscord();
            Assert.Equal(2, transport.OpenCount);
            clock.Advance(TimeSpan.FromMinutes(10));

            Assert.Equal(2, transport.OpenCount);
            Ready();
            Assert.NotNull(transport.LastCommand("AUTHORIZE"));
        }

        [Fact]
        public void Connect_failing_without_tokens_saved_doesnt_try_again()
        {
            transport.IsRunning = false;
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);

            core.ConnectDiscord();
            clock.Advance(TimeSpan.FromMinutes(10));

            Assert.Equal(1, transport.OpenCount);
            Assert.Equal("Discord isn't running", Discord.StatusText);
        }

        [Fact]
        public void Needing_to_reconnect_stops_trying_and_the_Discord_tab_says_to_reconnect_in_settings()
        {
            SaveTokens();
            NewCore();
            core.OnApplicationStarted();
            OpenDiscordTab();
            Assert.Equal("Connecting to Discord", Guide.Items[0].Title);

            Ready();
            Error("AUTHENTICATE", "Invalid access token");
            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");
            Error("AUTHORIZE", "OAuth2 Authorization cancelled");
            clock.Advance(TimeSpan.FromMinutes(10));

            Assert.Equal(1, transport.OpenCount);
            Assert.Equal("Reconnect Discord in settings", Discord.StatusText);
            var line = Assert.Single(Guide.Items);
            Assert.Equal((GuideItemKind.DiscordUnavailable, "Reconnect Discord in settings"), (line.Kind, line.Title));
            Assert.Same(line, Guide.FocusedItem);
        }

        [Fact]
        public void Needing_to_reconnect_while_signed_in_rebuilds_the_Discord_tab_to_say_so()
        {
            NotInCall();
            OpenDiscordTab();

            clock.Advance(TimeSpan.FromDays(6));
            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");
            Error("AUTHORIZE", "OAuth2 Authorization cancelled");

            var line = Assert.Single(Guide.Items);
            Assert.Equal((GuideItemKind.DiscordUnavailable, "Reconnect Discord in settings"), (line.Kind, line.Title));
            Assert.Same(line, Guide.FocusedItem);
            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
        }

        [Fact]
        public void Discord_closing_after_needing_to_reconnect_doesnt_try_again()
        {
            SaveTokens();
            NewCore();
            core.OnApplicationStarted();
            Ready();
            Error("AUTHENTICATE", "Invalid access token");
            core.OnDiscordTokenResponse(400, "{\"error\":\"invalid_grant\"}");
            Error("AUTHORIZE", "OAuth2 Authorization cancelled");

            core.ConnectDiscord();
            core.OnDiscordClosed();
            clock.Advance(TimeSpan.FromMinutes(10));

            Assert.Equal(2, transport.OpenCount);
            Assert.True(Discord.NeedsReconnect);
        }

        private void StartUpWithDiscordNotRunning()
        {
            SaveTokens();
            NewCore();
            transport.IsRunning = false;
            core.OnApplicationStarted();
        }

        private void SaveTokens()
        {
            secrets.Values["Discord.ClientId"] = ClientId;
            secrets.Values["Discord.ClientSecret"] = Secret;
            secrets.Values["Discord.AccessToken"] = "saved-access";
            secrets.Values["Discord.RefreshToken"] = "saved-refresh";
            secrets.Values["Discord.ExpiresAt"] = clock.Now.AddDays(6).ToString("o", CultureInfo.InvariantCulture);
        }

        private void Ready() =>
            core.OnDiscordFrame(DiscordOpcode.Frame, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1,\"config\":{}},\"nonce\":null}");

        private void SignIn() => Reply("AUTHENTICATE", new JObject
        {
            ["user"] = new JObject { ["id"] = Me, ["username"] = "gabe", ["global_name"] = null },
            ["scopes"] = new JArray("rpc", "identify"),
        });

        /// <summary>The SUBSCRIBEs sent from the <paramref name="from"/>th command on, as "event channel".</summary>
        private IReadOnlyList<string> SubscriptionsSince(int from) => transport.Commands.Skip(from)
            .Where(c => (string)c["cmd"] == "SUBSCRIBE")
            .Select(c => c["evt"] + " " + c["args"]?["channel_id"])
            .ToList();

        /// <summary>The shortcuts the Discord tab shows, opened for the purpose and closed again.</summary>
        private IReadOnlyList<string> ShortcutTitlesOnTheTab()
        {
            OpenDiscordTab();
            var titles = ShortcutTitles();
            Tap(ControllerButton.Guide);
            return titles;
        }
    }
}
