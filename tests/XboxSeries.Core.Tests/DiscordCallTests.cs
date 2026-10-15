using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>A call: the Discord tab's channel and people, its Mute, Deafen and Leave, the Audio flyout's toggles and the status icon.</summary>
    public class DiscordCallTests
    {
        private const string ClientId = "1290000000000000001";
        private const string Secret = "s3cr3t-s3cr3t-s3cr3t-s3cr3t-0001";
        private const string Me = "100000000000000001";
        private const string Ana = "200000000000000002";
        private const string Bo = "300000000000000003";
        private const string Cy = "400000000000000004";

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeDiscordTransport transport = new FakeDiscordTransport();
        private readonly FakeDiscordOAuth oauth = new FakeDiscordOAuth();
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private readonly ExtensionCore core;

        public DiscordCallTests()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), sounds, new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), transport, oauth, secrets);
        }

        private StatusState Status => core.Status;

        [Fact]
        public void Signing_in_asks_Discord_which_voice_channel_the_user_is_in_and_their_voice_settings()
        {
            ConnectFully();

            Assert.True(JToken.DeepEquals(new JObject(), transport.LastCommand("GET_SELECTED_VOICE_CHANNEL")["args"]));
            Assert.True(JToken.DeepEquals(new JObject(), transport.LastCommand("GET_VOICE_SETTINGS")["args"]));
        }

        [Fact]
        public void Not_in_a_call_at_sign_in_the_Discord_tab_stays_idle_and_the_status_area_shows_no_Discord_icon()
        {
            ConnectFully();
            Reply("GET_SELECTED_VOICE_CHANNEL", null);
            Reply("GET_VOICE_SETTINGS", Settings(mute: true, deaf: false));

            OpenDiscordTab();

            Assert.Equal(new[] { GuideItemKind.DiscordAccount, GuideItemKind.DiscordIdle, GuideItemKind.DiscordServers }, Kinds());
            Assert.False(Status.IsInDiscordCall);
            Assert.False(Status.IsDiscordMuted);
            Assert.Equal(DiscordBadge.None, Status.DiscordBadge);
        }

        [Fact]
        public void In_a_call_at_sign_in_the_Discord_tab_shows_the_channel_then_the_people_self_first_then_Mute_Deafen_and_Leave()
        {
            ConnectFully();
            Reply("GET_SELECTED_VOICE_CHANNEL", Channel("500", "Late night", 2, State(Ana, "ana"), State(Me, "gabe"), State(Bo, "bo")));

            OpenDiscordTab();

            Assert.Equal(
                new[]
                {
                    (GuideItemKind.DiscordChannel, "Late night"),
                    (GuideItemKind.DiscordParticipant, "gabe"),
                    (GuideItemKind.DiscordParticipant, "ana"),
                    (GuideItemKind.DiscordParticipant, "bo"),
                    (GuideItemKind.DiscordMute, "Mute"),
                    (GuideItemKind.DiscordDeafen, "Deafen"),
                    (GuideItemKind.DiscordLeave, "Leave"),
                    (GuideItemKind.DiscordShortcut, "Late night"),
                    (GuideItemKind.DiscordServers, "Servers"),
                },
                core.Guide.Items.Select(i => (i.Kind, i.Title)));
            Assert.Equal(new[] { false, false, false, false, true, false, false, true, true }, core.Guide.Items.Select(i => i.HasDividerAbove));
            Assert.True(Status.IsInDiscordCall);
        }

        [Fact]
        public void The_Discord_tab_opens_in_a_call_with_focus_on_Mute_the_first_entry_A_does_something_on()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana")));

            OpenDiscordTab();

            Assert.Equal(GuideItemKind.DiscordMute, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_participant_is_named_by_nickname_then_display_name_then_username_and_wears_their_avatar_or_Discords_default()
        {
            var nicked = State(Ana, "ana", globalName: "Ana G", avatar: "a1b2");
            nicked["nick"] = "Annie";
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), nicked, State(Bo, "bo", globalName: "Bo B"), State(Cy, "cy")));

            OpenDiscordTab();

            var people = Participants();
            Assert.Equal(new[] { "gabe", "Annie", "Bo B", "cy" }, people.Select(p => p.Title));
            Assert.Equal("https://cdn.discordapp.com/avatars/" + Ana + "/a1b2.png?size=64", people[1].Image);
            Assert.Equal("https://cdn.discordapp.com/embed/avatars/" + ((ulong.Parse(Bo) >> 22) % 6) + ".png", people[2].Image);
        }

        [Fact]
        public void A_participant_shows_muted_or_deafened_whether_they_did_it_or_the_server_did()
        {
            InCall(Channel("500", "Late night", 2,
                State(Me, "gabe"),
                State(Ana, "ana", selfMute: true),
                State(Bo, "bo", serverMute: true),
                State(Cy, "cy", selfDeaf: true, selfMute: true)));

            OpenDiscordTab();

            Assert.Equal(new[] { false, true, true, true }, Participants().Select(p => p.IsMuted));
            Assert.Equal(new[] { false, false, false, true }, Participants().Select(p => p.IsDeafened));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void A_DM_without_a_name_is_named_after_the_other_people_in_it(int type)
        {
            InCall(Channel("600", "", type, State(Me, "gabe"), State(Ana, "ana"), State(Bo, "bo")));

            OpenDiscordTab();

            Assert.Equal("ana, bo", core.Guide.Items[0].Title);
        }

        [Fact]
        public void Joining_a_channel_fetches_it_and_the_call_begins_once_it_arrives_following_its_voice_events()
        {
            NotInCall();

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "500", ["guild_id"] = "9" });

            Assert.Equal("500", (string)transport.LastCommand("GET_CHANNEL")["args"]["channel_id"]);
            Assert.False(Status.IsInDiscordCall);

            Reply("GET_CHANNEL", Channel("500", "Late night", 2, State(Me, "gabe")));

            Assert.True(Status.IsInDiscordCall);
            var subscribes = transport.Commands.Where(c => (string)c["cmd"] == "SUBSCRIBE" && c["args"]["channel_id"] != null).ToList();
            Assert.Equal(
                new[] { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP" },
                subscribes.Select(c => (string)c["evt"]));
            Assert.All(subscribes, c => Assert.Equal("500", (string)c["args"]["channel_id"]));
        }

        [Fact]
        public void Leaving_the_call_stops_following_its_voice_events_and_the_status_icon_goes()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            Dispatch("VOICE_SETTINGS_UPDATE", Settings(mute: true, deaf: true));
            Dispatch("SPEAKING_START", new JObject { ["user_id"] = Me });

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = null, ["guild_id"] = null });

            var unsubscribes = transport.Commands.Where(c => (string)c["cmd"] == "UNSUBSCRIBE").ToList();
            Assert.Equal(
                new[] { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP" },
                unsubscribes.Select(c => (string)c["evt"]));
            Assert.All(unsubscribes, c => Assert.Equal("500", (string)c["args"]["channel_id"]));
            Assert.False(Status.IsInDiscordCall);
            Assert.False(Status.IsDiscordMuted);
            Assert.False(Status.IsDiscordDeafened);
            Assert.False(Status.IsDiscordSpeaking);
            Assert.Equal(DiscordBadge.None, Status.DiscordBadge);
        }

        [Fact]
        public void Switching_channels_moves_the_voice_events_from_the_old_channel_to_the_new_one()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            OpenDiscordTab();

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "501", ["guild_id"] = "9" });
            Reply("GET_CHANNEL", Channel("501", "Raid", 2, State(Me, "gabe"), State(Ana, "ana")));

            Assert.Equal(
                new[] { "500" },
                transport.Commands.Where(c => (string)c["cmd"] == "UNSUBSCRIBE").Select(c => (string)c["args"]["channel_id"]).Distinct());
            Assert.Equal(5, transport.Commands.Count(c => (string)c["cmd"] == "SUBSCRIBE" && (string)c["args"]["channel_id"] == "501"));
            Assert.Equal("Raid", core.Guide.Items[0].Title);
            Assert.Equal(new[] { "gabe", "ana" }, Participants().Select(p => p.Title));
        }

        [Fact]
        public void A_channel_that_arrives_after_the_user_has_moved_on_is_ignored()
        {
            NotInCall();
            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "500" });
            var stale = transport.LastCommand("GET_CHANNEL");
            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = null });

            core.OnDiscordFrame(DiscordOpcode.Frame, new JObject
            {
                ["cmd"] = "GET_CHANNEL",
                ["data"] = Channel("500", "Late night", 2, State(Me, "gabe")),
                ["nonce"] = stale["nonce"],
            }.ToString());

            Assert.False(Status.IsInDiscordCall);
        }

        [Fact]
        public void Someone_joining_adds_them_after_the_others_keeping_focus_where_it_was()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana")));
            OpenDiscordTab();
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.DiscordDeafen, core.Guide.FocusedItem.Kind);

            Dispatch("VOICE_STATE_CREATE", State(Bo, "bo"));

            Assert.Equal(new[] { "gabe", "ana", "bo" }, Participants().Select(p => p.Title));
            Assert.Equal(GuideItemKind.DiscordDeafen, core.Guide.FocusedItem.Kind);
            Assert.True(core.Guide.FocusedItem.IsFocused);
        }

        [Fact]
        public void Someone_leaving_keeps_focus_on_the_person_it_was_on()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana"), State(Bo, "bo")));
            OpenDiscordTab();
            Tap(ControllerButton.DPadUp);
            Assert.Equal("bo", core.Guide.FocusedItem.Title);

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana"));

            Assert.Equal(new[] { "gabe", "bo" }, Participants().Select(p => p.Title));
            Assert.Equal("bo", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void The_person_with_focus_leaving_moves_focus_to_the_next_entry()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana"), State(Bo, "bo")));
            OpenDiscordTab();
            Tap(ControllerButton.DPadUp);
            Tap(ControllerButton.DPadUp);
            Assert.Equal("ana", core.Guide.FocusedItem.Title);

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana"));

            Assert.Equal("bo", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void A_DM_without_a_name_is_renamed_as_people_join()
        {
            InCall(Channel("600", "", 3, State(Me, "gabe"), State(Ana, "ana")));
            OpenDiscordTab();

            Dispatch("VOICE_STATE_CREATE", State(Bo, "bo"));

            Assert.Equal("ana, bo", core.Guide.Items[0].Title);
        }

        [Fact]
        public void Speaking_lights_the_person_in_place_without_rebuilding_the_tab()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana")));
            OpenDiscordTab();
            var items = core.Guide.Items;
            var ana = Participants()[1];

            Dispatch("SPEAKING_START", new JObject { ["user_id"] = Ana, ["channel_id"] = "500" });

            Assert.Same(items, core.Guide.Items);
            Assert.True(ana.IsSpeaking);
            Assert.False(Status.IsDiscordSpeaking);

            Dispatch("SPEAKING_STOP", new JObject { ["user_id"] = Ana });

            Assert.False(ana.IsSpeaking);
        }

        [Fact]
        public void The_user_speaking_lights_the_status_icon()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana")));

            Dispatch("SPEAKING_START", new JObject { ["user_id"] = Me });
            Assert.True(Status.IsDiscordSpeaking);

            Dispatch("SPEAKING_STOP", new JObject { ["user_id"] = Me });
            Assert.False(Status.IsDiscordSpeaking);
        }

        [Fact]
        public void Someone_muting_shows_in_place_without_rebuilding_the_tab()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana")));
            OpenDiscordTab();
            var items = core.Guide.Items;
            var focused = core.Guide.FocusedItem;

            Dispatch("VOICE_STATE_UPDATE", State(Ana, "ana", selfMute: true));
            Dispatch("VOICE_STATE_UPDATE", State(Me, "gabe", selfDeaf: true));

            Assert.Same(items, core.Guide.Items);
            Assert.Same(focused, core.Guide.FocusedItem);
            Assert.True(Participants()[1].IsMuted);
            Assert.True(Participants()[0].IsDeafened);
        }

        [Fact]
        public void Mute_on_the_Discord_tab_asks_Discord_and_shows_muted_only_once_Discord_says_so()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            OpenDiscordTab();
            var mute = core.Guide.FocusedItem;
            Assert.Equal(GuideItemKind.DiscordMute, mute.Kind);

            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["mute"] = true }, transport.LastCommand("SET_VOICE_SETTINGS")["args"]));
            Assert.Contains(UiSound.Enter, sounds.Played);
            Assert.False(mute.IsOn);
            Assert.False(Status.IsDiscordMuted);

            Dispatch("VOICE_SETTINGS_UPDATE", Settings(mute: true, deaf: false));

            Assert.True(mute.IsOn);
            Assert.True(Status.IsDiscordMuted);
            Assert.Equal(DiscordBadge.Muted, Status.DiscordBadge);
            Assert.True(core.Guide.IsOpen);

            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["mute"] = false }, transport.LastCommand("SET_VOICE_SETTINGS")["args"]));
        }

        [Fact]
        public void Deafen_on_the_Discord_tab_asks_Discord_and_the_deafened_badge_wins_over_muted()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")), mute: true);
            OpenDiscordTab();
            Tap(ControllerButton.DPadDown);
            var deafen = core.Guide.FocusedItem;
            Assert.Equal(GuideItemKind.DiscordDeafen, deafen.Kind);

            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["deaf"] = true }, transport.LastCommand("SET_VOICE_SETTINGS")["args"]));
            Assert.False(deafen.IsOn);

            Dispatch("VOICE_SETTINGS_UPDATE", Settings(mute: true, deaf: true));

            Assert.True(deafen.IsOn);
            Assert.True(Status.IsDiscordDeafened);
            Assert.Equal(DiscordBadge.Deafened, Status.DiscordBadge);
        }

        [Fact]
        public void Leave_asks_Discord_to_leave_and_the_tab_goes_idle_only_once_Discord_says_so()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            OpenDiscordTab();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.DiscordLeave, core.Guide.FocusedItem.Kind);

            Tap(ControllerButton.A);

            var leave = transport.LastCommand("SELECT_VOICE_CHANNEL");
            Assert.True(JToken.DeepEquals(new JObject { ["channel_id"] = null }, leave["args"]));
            Assert.Equal(GuideItemKind.DiscordChannel, core.Guide.Items[0].Kind);
            Assert.True(core.Guide.IsOpen);

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = null });

            Assert.Equal(
                new[] { GuideItemKind.DiscordAccount, GuideItemKind.DiscordIdle, GuideItemKind.DiscordShortcut, GuideItemKind.DiscordServers },
                Kinds());
            Assert.Equal(GuideItemKind.DiscordShortcut, core.Guide.FocusedItem.Kind);
            Assert.Equal(GuideTabKind.Discord, core.Guide.Tab);
        }

        [Fact]
        public void A_on_the_channel_and_the_people_does_nothing()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe"), State(Ana, "ana")));
            OpenDiscordTab();
            var sent = transport.Sent.Count;
            sounds.Played.Clear();

            Tap(ControllerButton.DPadUp);
            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadUp);
            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadUp);
            Assert.Equal(GuideItemKind.DiscordChannel, core.Guide.FocusedItem.Kind);
            Tap(ControllerButton.A);

            Assert.DoesNotContain(UiSound.Enter, sounds.Played);
            Assert.Equal(sent, transport.Sent.Count);
            Assert.True(core.Guide.IsOpen);
        }

        [Fact]
        public void A_refused_voice_command_leaves_the_connection_up()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            OpenDiscordTab();
            Tap(ControllerButton.A);

            Error("SET_VOICE_SETTINGS", "Nope");

            Assert.True(core.Settings.Discord.IsConnected);
            Assert.True(transport.IsOpen);
            Assert.True(Status.IsInDiscordCall);
        }

        [Fact]
        public void Muted_out_of_a_call_shows_no_badge_until_a_call_begins()
        {
            NotInCall();
            Dispatch("VOICE_SETTINGS_UPDATE", Settings(mute: true, deaf: false));
            Assert.False(Status.IsDiscordMuted);

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "500" });
            Reply("GET_CHANNEL", Channel("500", "Late night", 2, State(Me, "gabe", selfMute: true)));

            Assert.True(Status.IsDiscordMuted);
            Assert.Equal(DiscordBadge.Muted, Status.DiscordBadge);
        }

        [Fact]
        public void Discord_closing_mid_call_ends_it()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            Dispatch("VOICE_SETTINGS_UPDATE", Settings(mute: true, deaf: false));
            Dispatch("SPEAKING_START", new JObject { ["user_id"] = Me });

            core.OnDiscordClosed();

            Assert.False(Status.IsInDiscordCall);
            Assert.False(Status.IsDiscordMuted);
            Assert.False(Status.IsDiscordSpeaking);
        }

        [Fact]
        public void Out_of_a_call_the_Audio_flyout_has_no_Discord_toggles()
        {
            NotInCall();

            OpenAudio();

            Assert.Equal(new[] { GuideItemKind.Volume, GuideItemKind.Mute }, core.Guide.Flyout.Items.Select(i => i.Kind));
        }

        [Fact]
        public void In_a_call_the_Audio_flyout_has_Discord_Mute_and_Deafen_after_Windows_own_which_ask_Discord()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")), deaf: true);

            OpenAudio();

            var items = core.Guide.Flyout.Items;
            Assert.Equal(new[] { GuideItemKind.Volume, GuideItemKind.Mute, GuideItemKind.DiscordMute, GuideItemKind.DiscordDeafen }, items.Select(i => i.Kind));
            Assert.Equal(new[] { false, true }, items.Skip(2).Select(i => i.IsOn));

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["mute"] = true }, transport.LastCommand("SET_VOICE_SETTINGS")["args"]));
            Assert.NotNull(core.Guide.Flyout);

            Dispatch("VOICE_SETTINGS_UPDATE", Settings(mute: true, deaf: true));

            Assert.Same(items, core.Guide.Flyout.Items);
            Assert.True(items[2].IsOn);
        }

        [Fact]
        public void A_call_beginning_with_the_Audio_flyout_open_adds_the_toggles_keeping_focus()
        {
            NotInCall();
            OpenAudio();
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.Mute, core.Guide.FocusedItem.Kind);

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "500" });
            Reply("GET_CHANNEL", Channel("500", "Late night", 2, State(Me, "gabe")));

            Assert.Equal(4, core.Guide.Flyout.Items.Count);
            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);
            Assert.Equal(GuideItemKind.Mute, core.Guide.FocusedItem.Kind);
            Assert.Contains(core.Guide.FocusedItem, core.Guide.Flyout.Items);
        }

        [Fact]
        public void A_call_ending_with_focus_on_a_Discord_toggle_in_the_Audio_flyout_moves_focus_to_Windows_mute()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            OpenAudio();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.DiscordDeafen, core.Guide.FocusedItem.Kind);

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = null });

            Assert.Equal(new[] { GuideItemKind.Volume, GuideItemKind.Mute }, core.Guide.Flyout.Items.Select(i => i.Kind));
            Assert.Equal(GuideItemKind.Mute, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_call_beginning_with_the_Guide_on_the_idle_Discord_tab_shows_it_focus_on_Mute()
        {
            NotInCall();
            OpenDiscordTab();

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "500" });
            Reply("GET_CHANNEL", Channel("500", "Late night", 2, State(Me, "gabe")));

            Assert.Equal(GuideItemKind.DiscordChannel, core.Guide.Items[0].Kind);
            Assert.Equal(GuideItemKind.DiscordMute, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_sign_in_answer_arriving_after_the_user_has_left_doesnt_bring_the_call_back()
        {
            ConnectFully();
            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = null });

            Reply("GET_SELECTED_VOICE_CHANNEL", Channel("500", "Late night", 2, State(Me, "gabe")));

            Assert.False(core.Status.IsInDiscordCall);
        }

        [Fact]
        public void Discord_refusing_the_new_channel_drops_the_old_call_rather_than_showing_it_as_current()
        {
            InCall(Channel("500", "Late night", 2, State(Me, "gabe")));
            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "501" });

            Error("GET_CHANNEL", "No access");

            Assert.False(core.Status.IsInDiscordCall);
            Assert.Equal("UNSUBSCRIBE", (string)transport.Commands.Last()["cmd"]);
            Assert.Equal("500", (string)transport.Commands.Last()["args"]["channel_id"]);
            Assert.True(core.Settings.Discord.IsConnected);
        }

        private void ConnectFully()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);
            core.ConnectDiscord();
            core.OnDiscordFrame(DiscordOpcode.Frame, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1,\"config\":{}},\"nonce\":null}");
            Reply("AUTHORIZE", new JObject { ["code"] = "the-code" });
            core.OnDiscordTokenResponse(200, new JObject { ["access_token"] = "access-1", ["refresh_token"] = "refresh-1", ["expires_in"] = 604800 }.ToString());
            Reply("AUTHENTICATE", new JObject
            {
                ["user"] = new JObject { ["id"] = Me, ["username"] = "gabe", ["global_name"] = null },
                ["scopes"] = new JArray("rpc", "identify"),
            });
        }

        private void NotInCall()
        {
            ConnectFully();
            Reply("GET_SELECTED_VOICE_CHANNEL", null);
            Reply("GET_VOICE_SETTINGS", Settings(mute: false, deaf: false));
        }

        private void InCall(JObject channel, bool mute = false, bool deaf = false)
        {
            ConnectFully();
            Reply("GET_VOICE_SETTINGS", Settings(mute, deaf));
            Reply("GET_SELECTED_VOICE_CHANNEL", channel);
        }

        private void OpenGuide()
        {
            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(300));
        }

        private void OpenDiscordTab()
        {
            OpenGuide();
            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.Discord, core.Guide.Tab);
        }

        private void OpenAudio()
        {
            OpenGuide();
            foreach (var _ in core.Guide.Items)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);
        }

        private IReadOnlyList<GuideItemKind> Kinds() => core.Guide.Items.Select(i => i.Kind).ToList();

        private IReadOnlyList<GuideItem> Participants() => core.Guide.Items.Where(i => i.Kind == GuideItemKind.DiscordParticipant).ToList();

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }

        /// <summary>An event from Discord, one the core has subscribed to.</summary>
        private void Dispatch(string evt, JObject data) => core.OnDiscordFrame(DiscordOpcode.Frame, new JObject
        {
            ["cmd"] = "DISPATCH",
            ["evt"] = evt,
            ["data"] = data,
            ["nonce"] = null,
        }.ToString());

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

        private static JObject Settings(bool mute, bool deaf) => new JObject
        {
            ["mute"] = mute,
            ["deaf"] = deaf,
            ["input"] = new JObject { ["volume"] = 100 },
            ["output"] = new JObject { ["volume"] = 100 },
        };

        private static JObject Channel(string id, string name, int type, params JObject[] states) => new JObject
        {
            ["id"] = id,
            ["name"] = name,
            ["type"] = type,
            ["guild_id"] = type == 2 ? "9" : null,
            ["voice_states"] = new JArray(states),
        };

        /// <summary>A voice state, as a channel's voice_states and the VOICE_STATE_* events carry.</summary>
        private static JObject State(
            string id, string username, string globalName = null, string avatar = null,
            bool selfMute = false, bool selfDeaf = false, bool serverMute = false) => new JObject
        {
            ["nick"] = null,
            ["mute"] = false,
            ["volume"] = 100,
            ["pan"] = new JObject { ["left"] = 1.0, ["right"] = 1.0 },
            ["user"] = new JObject { ["id"] = id, ["username"] = username, ["global_name"] = globalName, ["avatar"] = avatar, ["bot"] = false },
            ["voice_state"] = new JObject
            {
                ["mute"] = serverMute,
                ["deaf"] = false,
                ["self_mute"] = selfMute,
                ["self_deaf"] = selfDeaf,
                ["suppress"] = false,
            },
        };
    }
}
