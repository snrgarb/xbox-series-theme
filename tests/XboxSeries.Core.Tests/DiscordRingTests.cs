using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Incoming calls: a known DM someone else joins rings, with a Toast and the Discord tab's Answer, which joins it,
    /// asking first in a call.
    /// </summary>
    public class DiscordRingTests : DiscordTestsBase
    {
        private const string AnaAvatar = "a1";

        [Fact]
        public void Signing_in_follows_who_comes_and_goes_in_each_known_DM_and_asks_who_is_in_it_but_not_a_servers_voice_channel()
        {
            NotInCall();
            KnowDm("600");
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            Leave();
            NewCore();

            ConnectFully();

            Assert.Equal(new[] { "VOICE_STATE_CREATE", "VOICE_STATE_DELETE" }, ChannelCommands("SUBSCRIBE", "600"));
            Assert.Empty(ChannelCommands("SUBSCRIBE", "500"));
            Assert.Contains(Sent("GET_CHANNEL"), c => (string)c["args"]["channel_id"] == "600");
            Assert.DoesNotContain(Sent("GET_CHANNEL"), c => (string)c["args"]["channel_id"] == "500");
        }

        [Fact]
        public void Someone_joining_a_known_DM_rings_with_a_Toast_naming_it()
        {
            NotInCall();
            KnowDm("600");

            Ring("600");

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);
            Assert.Equal("ana is calling", core.Toast.Text);
        }

        [Fact]
        public void The_call_Toast_carries_the_callers_avatar_for_its_tile()
        {
            NotInCall();
            KnowDm("600");

            Ring("600");

            Assert.Equal(AvatarOf(Ana, AnaAvatar), core.Toast.Image);
        }

        [Fact]
        public void A_DM_learned_while_signed_in_is_followed_for_rings_at_once()
        {
            NotInCall();
            Join(Dm("600", 1, State(Me, "gabe"), State(Ana, "ana", AnaAvatar)));

            Assert.Equal(new[] { "VOICE_STATE_CREATE", "VOICE_STATE_DELETE" }, ChannelCommands("SUBSCRIBE", "600").Take(2));
        }

        [Fact]
        public void Without_a_known_DM_nothing_rings_and_someone_joining_is_not_looked_for()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            Leave();
            var fetches = Sent("GET_CHANNEL").Count;

            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana"));

            Assert.Equal(fetches, Sent("GET_CHANNEL").Count);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_known_DM_someone_sat_in_at_sign_in_doesnt_ring_when_more_join()
        {
            NotInCall();
            KnowDm("600");
            NewCore();
            ConnectFully();
            ReplyTo(GetChannel("600"), Dm("600", 1, State(Ana, "ana", AnaAvatar)));
            Reply("GET_SELECTED_VOICE_CHANNEL", null);

            var fetches = Sent("GET_CHANNEL").Count;

            Dispatch("VOICE_STATE_CREATE", State(Bo, "bo"));

            Assert.Equal(fetches, Sent("GET_CHANNEL").Count);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Someone_joining_out_of_a_call_asks_only_the_known_DMs_nobody_else_is_in()
        {
            NotInCall();
            KnowDm("600");
            KnowDm("601");
            Join(Dm("602", 1, State(Me, "gabe"), State(Bo, "bo")));
            Leave();
            var fetches = Sent("GET_CHANNEL").Count;

            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana", AnaAvatar));

            Assert.Equal(new[] { "600", "601" }, Sent("GET_CHANNEL").Skip(fetches).Select(c => (string)c["args"]["channel_id"]));
        }

        [Fact]
        public void A_known_DM_left_by_whoever_sat_in_it_rings_when_they_come_back()
        {
            NotInCall();
            KnowDm("600");
            NewCore();
            ConnectFully();
            ReplyTo(GetChannel("600"), Dm("600", 1, State(Ana, "ana", AnaAvatar)));
            Reply("GET_SELECTED_VOICE_CHANNEL", null);
            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel("600"), Dm("600", 1));

            Ring("600");

            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void Someone_joining_the_call_still_shows_in_it_and_rings_nothing()
        {
            NotInCall();
            KnowDm("600");
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            var fetches = Sent("GET_CHANNEL").Count;

            Dispatch("VOICE_STATE_CREATE", State(Bo, "bo"));
            ReplyTo(GetChannel("500"), VoiceChannel("500", "Late night", "9", State(Me, "gabe"), State(Bo, "bo")));

            Assert.Equal(new[] { "500" }, Sent("GET_CHANNEL").Skip(fetches).Select(c => (string)c["args"]["channel_id"]));
            OpenDiscordTab();

            Assert.Equal(
                new[] { "gabe", "bo" },
                Guide.Items.Where(i => i.Kind == GuideItemKind.DiscordParticipant).Select(i => i.Title));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Someone_joining_a_known_DM_during_a_call_asks_the_call_first_then_the_DMs_and_rings()
        {
            NotInCall();
            KnowDm("600");
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            var fetches = Sent("GET_CHANNEL").Count;

            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana", AnaAvatar));

            Assert.Equal(new[] { "500" }, Sent("GET_CHANNEL").Skip(fetches).Select(c => (string)c["args"]["channel_id"]));

            ReplyTo(GetChannel("500"), VoiceChannel("500", "Late night", "9", State(Me, "gabe")));

            Assert.Equal(new[] { "500", "600" }, Sent("GET_CHANNEL").Skip(fetches).Select(c => (string)c["args"]["channel_id"]));

            ReplyTo(GetChannel("600"), Dm("600", 1, State(Ana, "ana", AnaAvatar)));

            Assert.True(core.Toast.IsShown);
            Assert.Equal("ana is calling", core.Toast.Text);
            OpenGuide();
            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
            Assert.DoesNotContain(Guide.Items, i => i.Kind == GuideItemKind.DiscordParticipant && i.Title == "ana");
        }

        [Fact]
        public void The_call_Toast_shows_for_at_most_30_seconds()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");

            clock.Advance(TimeSpan.FromSeconds(29.9));
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_call_Toast_goes_once_the_caller_hangs_up()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel("600"), Dm("600", 1));

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_call_Toast_cuts_in_before_a_notification_which_waits_behind_it_and_shows_after()
        {
            core.OnNotificationsChanged(new Notification[0]);
            NotInCall();
            KnowDm("600");
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            Assert.Equal("Library updated", core.Toast.Text);

            Ring("600");
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Update available") });

            Assert.Equal("ana is calling", core.Toast.Text);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.True(core.Toast.IsShown);
            Assert.Equal("ana is calling", core.Toast.Text);

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel("600"), Dm("600", 1));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.5));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.Equal("Library updated", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal("Update available", core.Toast.Text);
        }

        [Fact]
        public void A_Guide_tap_during_the_call_Toast_opens_the_Discord_tab_with_focus_on_Answer()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");

            OpenGuide();

            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
            var answer = Guide.Items[0];
            Assert.Equal(GuideItemKind.DiscordIncomingCall, answer.Kind);
            Assert.Equal("Answer", answer.Title);
            Assert.Equal("ana is calling", answer.Detail);
            Assert.Equal(AvatarOf(Ana, AnaAvatar), answer.Image);
            Assert.Same(answer, Guide.FocusedItem);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_Guide_tap_after_the_call_Toast_has_gone_opens_the_Guide_as_usual()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");
            clock.Advance(TimeSpan.FromSeconds(30));

            OpenGuide();

            Assert.Equal(GuideTabKind.Landing, Guide.Tab);
        }

        [Fact]
        public void A_Guide_tap_with_the_Guide_open_during_the_call_Toast_goes_to_Answer_closing_what_was_over_it()
        {
            NotInCall();
            KnowDm("600");
            OpenGuide();
            foreach (var _ in Guide.Items)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            Assert.Equal(GuideItemKind.Audio, Guide.Flyout.Kind);
            Ring("600");
            Assert.True(core.Toast.IsShown);

            Tap(ControllerButton.Guide);

            Assert.True(Guide.IsOpen);
            Assert.Null(Guide.Flyout);
            Assert.Null(Guide.Dialog);
            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
            Assert.Equal(GuideItemKind.DiscordIncomingCall, Guide.FocusedItem.Kind);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_Guide_tap_with_the_Guide_open_and_no_call_Toast_closes_the_Guide()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");
            clock.Advance(TimeSpan.FromSeconds(30));
            OpenGuide();

            Tap(ControllerButton.Guide);

            Assert.False(Guide.IsOpen);
        }

        [Fact]
        public void A_ring_with_the_Discord_tab_open_adds_Answer_at_the_top_with_the_Toast_focus_staying_put()
        {
            NotInCall();
            KnowDm("600");
            OpenDiscordTab();
            var focused = Guide.FocusedItem;

            Ring("600");

            Assert.Equal(GuideItemKind.DiscordIncomingCall, Guide.Items[0].Kind);
            Assert.True(core.Toast.IsShown);
            Assert.Equal(focused.Kind, Guide.FocusedItem.Kind);
            Assert.Equal(focused.Title, Guide.FocusedItem.Title);

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel("600"), Dm("600", 1));

            Assert.NotEqual(GuideItemKind.DiscordIncomingCall, Guide.Items[0].Kind);
            Assert.Equal(focused.Kind, Guide.FocusedItem.Kind);
            Assert.Equal(focused.Title, Guide.FocusedItem.Title);
        }

        [Fact]
        public void The_ring_ending_with_focus_on_Answer_moves_focus_to_the_tabs_first_entry()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");
            OpenGuide();

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel("600"), Dm("600", 1));

            Assert.Equal(GuideItemKind.DiscordShortcut, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Answer_out_of_a_call_joins_the_DM_and_the_ring_ends()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");
            OpenGuide();

            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["channel_id"] = "600", ["force"] = true }, transport.LastCommand("SELECT_VOICE_CHANNEL")["args"]));
            Assert.True(Guide.IsOpen);

            Join(Dm("600", 1, State(Me, "gabe"), State(Ana, "ana", AnaAvatar)));

            Assert.DoesNotContain(Guide.Items, i => i.Kind == GuideItemKind.DiscordIncomingCall);
            Assert.Equal(GuideItemKind.DiscordChannel, Guide.Items[0].Kind);
        }

        [Fact]
        public void Answer_in_a_call_asks_first_and_switches_only_on_Leave_and_answer()
        {
            NotInCall();
            KnowDm("600");
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            RingDuringCall("600", "500");
            OpenGuide();
            var selects = Sent("SELECT_VOICE_CHANNEL").Count;

            Tap(ControllerButton.A);

            var prompt = Guide.Dialog;
            Assert.Equal("Leave Late night and answer?", prompt.Title);
            Assert.Equal(new[] { "Leave and answer", "Cancel" }, prompt.Options.Select(o => o.Label));
            Assert.Equal(new[] { DialogIcon.Play, DialogIcon.Cancel }, prompt.Options.Select(o => o.Icon));
            Assert.Equal(selects, Sent("SELECT_VOICE_CHANNEL").Count);

            Tap(ControllerButton.A);

            Assert.Null(Guide.Dialog);
            Assert.True(JToken.DeepEquals(new JObject { ["channel_id"] = "600", ["force"] = true }, transport.LastCommand("SELECT_VOICE_CHANNEL")["args"]));
        }

        [Fact]
        public void Cancel_on_Leave_and_answer_does_nothing()
        {
            NotInCall();
            KnowDm("600");
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            RingDuringCall("600", "500");
            OpenGuide();
            var sent = transport.Sent.Count;
            Tap(ControllerButton.A);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Assert.Null(Guide.Dialog);
            Assert.True(Guide.IsOpen);
            Assert.Equal(sent, transport.Sent.Count);
            Assert.Equal(GuideItemKind.DiscordIncomingCall, Guide.Items[0].Kind);
        }

        [Fact]
        public void The_ring_ending_closes_Leave_and_answer()
        {
            NotInCall();
            KnowDm("600");
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            RingDuringCall("600", "500");
            OpenGuide();
            Tap(ControllerButton.A);

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel("600"), Dm("600", 1));

            Assert.Null(Guide.Dialog);
            Assert.True(Guide.IsOpen);
        }

        [Fact]
        public void A_newer_ring_replaces_the_one_before()
        {
            NotInCall();
            KnowDm("600");
            Join(Dm("601", 1, State(Me, "gabe"), State(Bo, "bo")));
            Dispatch("VOICE_STATE_DELETE", State(Bo, "bo"));
            Leave();
            Ring("600");

            Dispatch("VOICE_STATE_CREATE", State(Bo, "bo"));
            ReplyTo(GetChannel("601"), Dm("601", 1, State(Bo, "bo")));

            Assert.Equal("bo is calling", core.Toast.Text);
            OpenGuide();
            Assert.Single(Guide.Items, i => i.Kind == GuideItemKind.DiscordIncomingCall);
            Assert.Equal("bo is calling", Guide.Items[0].Detail);
        }

        [Fact]
        public void Discord_going_ends_the_ring()
        {
            NotInCall();
            KnowDm("600");
            Ring("600");

            core.OnDiscordClosed();

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Leaving_a_known_DMs_call_keeps_following_it_for_rings_and_joining_it_didnt_follow_twice()
        {
            NotInCall();

            KnowDm("600");

            Assert.Equal(1, ChannelCommands("SUBSCRIBE", "600").Count(e => e == "VOICE_STATE_CREATE"));
            Assert.Equal(1, ChannelCommands("SUBSCRIBE", "600").Count(e => e == "VOICE_STATE_DELETE"));
            Assert.Equal(new[] { "VOICE_STATE_UPDATE", "SPEAKING_START", "SPEAKING_STOP" }, ChannelCommands("UNSUBSCRIBE", "600"));

            Ring("600");
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void Remove_shortcut_on_a_DM_stops_following_it_for_rings()
        {
            NotInCall();
            KnowDm("600");
            OpenDiscordTab();
            Assert.Equal(GuideItemKind.DiscordShortcut, Guide.FocusedItem.Kind);

            Tap(ControllerButton.Start);
            Tap(ControllerButton.A);

            Assert.Equal(
                new[] { "VOICE_STATE_UPDATE", "SPEAKING_START", "SPEAKING_STOP", "VOICE_STATE_CREATE", "VOICE_STATE_DELETE" },
                ChannelCommands("UNSUBSCRIBE", "600"));
            var fetches = Sent("GET_CHANNEL").Count;
            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana", AnaAvatar));
            Assert.Equal(fetches, Sent("GET_CHANNEL").Count);
        }

        /// <summary>A DM with Ana, joined before and so known, named "ana" and wearing her avatar; she has hung up and the user left.</summary>
        private void KnowDm(string id)
        {
            Join(Dm(id, 1, State(Me, "gabe"), State(Ana, "ana", AnaAvatar)));
            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana", AnaAvatar));
            Leave();
        }

        /// <summary>Ana joins the DM <paramref name="id"/>, out of a call: Discord is asked who is in it, and answers.</summary>
        private void Ring(string id)
        {
            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel(id), Dm(id, 1, State(Ana, "ana", AnaAvatar)));
        }

        /// <summary>
        /// Ana joins the DM <paramref name="id"/> during the call in <paramref name="callId"/>: Discord is asked first
        /// who is in the call, which she isn't, then who is in the DM.
        /// </summary>
        private void RingDuringCall(string id, string callId)
        {
            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana", AnaAvatar));
            ReplyTo(GetChannel(callId), VoiceChannel(callId, "Late night", "9", State(Me, "gabe")));
            ReplyTo(GetChannel(id), Dm(id, 1, State(Ana, "ana", AnaAvatar)));
        }

        private JObject GetChannel(string id) =>
            transport.Commands.Last(c => (string)c["cmd"] == "GET_CHANNEL" && (string)c["args"]["channel_id"] == id);

        /// <summary>The events <paramref name="cmd"/>, SUBSCRIBE or UNSUBSCRIBE, was sent for with channel <paramref name="id"/>, in order.</summary>
        private string[] ChannelCommands(string cmd, string id) =>
            transport.Commands.Where(c => (string)c["cmd"] == cmd && (string)c["args"]["channel_id"] == id).Select(c => (string)c["evt"]).ToArray();
    }
}
