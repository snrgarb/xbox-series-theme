using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>The Discord tab's shortcuts: known DMs and voice channels, learned from the calls the user joins, rejoined in one press.</summary>
    public class DiscordShortcutsTests : DiscordTestsBase
    {
        [Fact]
        public void Idle_with_no_shortcuts_the_tab_ends_in_Servers_under_a_divider_with_focus_on_it()
        {
            NotInCall();

            OpenDiscordTab();

            Assert.Equal(new[] { GuideItemKind.DiscordAccount, GuideItemKind.DiscordIdle, GuideItemKind.DiscordServers }, Kinds());
            Assert.Equal("Servers", Guide.Items[2].Title);
            Assert.True(Guide.Items[2].HasDividerAbove);
            Assert.Same(Guide.Items[2], Guide.FocusedItem);
        }

        [Fact]
        public void A_voice_channel_joined_becomes_a_shortcut_named_after_it_with_its_servers_icon_and_name()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));

            Assert.True(JToken.DeepEquals(new JObject { ["guild_id"] = "9" }, transport.LastCommand("GET_GUILD")["args"]));
            Reply("GET_GUILD", new JObject { ["id"] = "9", ["name"] = "The Lads", ["icon_url"] = "https://cdn.discordapp.com/icons/9/abc.png" });
            Leave();
            OpenDiscordTab();

            var tile = Assert.Single(Shortcuts());
            Assert.Equal("Late night", tile.Title);
            Assert.Equal("The Lads", tile.Detail);
            Assert.Equal("https://cdn.discordapp.com/icons/9/abc.png", tile.Image);
            Assert.False(tile.IsDirectMessage);
        }

        [Fact]
        public void A_voice_channel_whose_server_hasnt_answered_is_still_no_DM()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9"));
            Error("GET_GUILD", "Nope");
            Leave();

            OpenDiscordTab();

            var tile = Assert.Single(Shortcuts());
            Assert.Null(tile.Detail);
            Assert.False(tile.IsDirectMessage);
        }

        [Fact]
        public void The_servers_name_and_icon_arriving_unchanged_save_nothing()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9"));
            var guild = new JObject { ["id"] = "9", ["name"] = "The Lads", ["icon_url"] = "https://cdn.discordapp.com/icons/9/abc.png" };
            Reply("GET_GUILD", guild);
            Leave();
            Join(VoiceChannel("500", "Late night", "9"));
            var writes = secrets.Writes;

            Reply("GET_GUILD", guild);

            Assert.Equal(writes, secrets.Writes);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void A_DM_joined_becomes_a_shortcut_named_after_the_people_in_it_wearing_the_first_ones_avatar(int type)
        {
            NotInCall();
            Join(Dm("600", type, State(Me, "gabe"), State(Ana, "ana", avatar: "a1"), State(Bo, "bo")));
            Leave();

            OpenDiscordTab();

            var tile = Assert.Single(Shortcuts());
            Assert.Equal("ana, bo", tile.Title);
            Assert.Equal(AvatarOf(Ana, "a1"), tile.Image);
            Assert.Null(tile.Detail);
            Assert.True(tile.IsDirectMessage);
            Assert.Null(transport.LastCommand("GET_GUILD"));
        }

        [Fact]
        public void A_named_DM_keeps_its_name_and_avatar_as_people_come_and_go_until_it_is_joined_again()
        {
            NotInCall();
            Join(Dm("600", 3, State(Me, "gabe"), State(Ana, "ana", avatar: "a1"), State(Bo, "bo")));
            OpenDiscordTab();
            var writes = secrets.Writes;

            Dispatch("VOICE_STATE_DELETE", State(Ana, "ana"));

            Assert.Equal(new[] { "ana, bo" }, ShortcutTitles());
            Assert.Equal(AvatarOf(Ana, "a1"), Shortcuts()[0].Image);
            Assert.Equal(writes, secrets.Writes);

            Leave();
            Join(Dm("600", 3, State(Me, "gabe"), State(Bo, "bo", avatar: "b2")));

            Assert.Equal(new[] { "bo" }, ShortcutTitles());
            Assert.Equal(AvatarOf(Bo, "b2"), Shortcuts()[0].Image);
        }

        [Fact]
        public void A_DM_rung_before_anyone_answers_is_named_once_they_join()
        {
            NotInCall();
            Join(Dm("600", 1, State(Me, "gabe")));
            OpenDiscordTab();
            Assert.Equal(new[] { "Call" }, ShortcutTitles());

            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana", avatar: "a1"));
            Leave();

            Assert.Equal(new[] { "ana" }, ShortcutTitles());
            Assert.Equal(AvatarOf(Ana, "a1"), Shortcuts()[0].Image);
        }

        [Fact]
        public void Rejoining_a_DM_alone_keeps_the_name_and_avatar_learned_before()
        {
            NotInCall();
            Join(Dm("600", 1, State(Me, "gabe"), State(Ana, "ana", avatar: "a1")));
            Leave();
            Join(Dm("600", 1, State(Me, "gabe")));

            OpenDiscordTab();

            var tile = Assert.Single(Shortcuts());
            Assert.Equal("ana", tile.Title);
            Assert.Equal(AvatarOf(Ana, "a1"), tile.Image);
        }

        [Fact]
        public void The_call_the_user_is_in_at_sign_in_is_learned_too()
        {
            ConnectFully();
            Reply("GET_SELECTED_VOICE_CHANNEL", Dm("600", 1, State(Me, "gabe"), State(Ana, "ana")));

            OpenDiscordTab();

            Assert.Equal(new[] { "ana" }, ShortcutTitles());
        }

        [Fact]
        public void In_a_call_the_shortcuts_follow_Leave_under_a_divider_then_Servers_and_focus_stays_on_Mute()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));

            OpenDiscordTab();

            Assert.Equal(
                new[]
                {
                    GuideItemKind.DiscordChannel, GuideItemKind.DiscordParticipant, GuideItemKind.DiscordMute, GuideItemKind.DiscordDeafen,
                    GuideItemKind.DiscordLeave, GuideItemKind.DiscordShortcut, GuideItemKind.DiscordServers,
                },
                Kinds());
            Assert.Equal(new[] { false, false, true, false, false, true, true }, Guide.Items.Select(i => i.HasDividerAbove));
            Assert.Equal(GuideItemKind.DiscordMute, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Idle_the_tab_opens_on_the_first_shortcut()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9"));
            Leave();

            OpenDiscordTab();

            Assert.Same(Shortcuts()[0], Guide.FocusedItem);
            Assert.True(Shortcuts()[0].HasDividerAbove);
        }

        [Fact]
        public void Shortcuts_are_most_recently_joined_first_and_rejoining_one_moves_it_to_the_front()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9"));
            Join(VoiceChannel("501", "B", "9"));
            Join(VoiceChannel("502", "C", "9"));
            OpenDiscordTab();
            Assert.Equal(new[] { "C", "B", "A" }, ShortcutTitles());

            Join(VoiceChannel("500", "A", "9"));

            Assert.Equal(new[] { "A", "C", "B" }, ShortcutTitles());
        }

        [Fact]
        public void The_tab_rebuilt_as_someone_joins_keeps_focus_on_the_same_shortcut()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9"));
            Join(VoiceChannel("501", "B", "9", State(Me, "gabe")));
            OpenDiscordTab();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadRight);
            Assert.Equal("A", Guide.FocusedItem.Title);

            Dispatch("VOICE_STATE_CREATE", State(Ana, "ana"));

            Assert.Equal("A", Guide.FocusedItem.Title);
            Assert.Contains(Guide.FocusedItem, Guide.Items);
        }

        [Fact]
        public void A_servers_icon_arriving_while_the_tab_shows_puts_it_on_the_tile()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9"));
            var getGuild = transport.LastCommand("GET_GUILD");
            Leave();
            OpenDiscordTab();

            ReplyTo(getGuild, new JObject { ["id"] = "9", ["name"] = "The Lads", ["icon_url"] = "https://cdn.discordapp.com/icons/9/abc.png" });

            Assert.Equal("https://cdn.discordapp.com/icons/9/abc.png", Shortcuts()[0].Image);
            Assert.Same(Shortcuts()[0], Guide.FocusedItem);
        }

        [Fact]
        public void At_most_six_shortcuts_show()
        {
            NotInCall();
            foreach (var n in Enumerable.Range(1, 8))
            {
                Join(VoiceChannel("50" + n, "Ch" + n, "9"));
            }

            Leave();
            OpenDiscordTab();

            Assert.Equal(new[] { "Ch8", "Ch7", "Ch6", "Ch5", "Ch4", "Ch3" }, ShortcutTitles());
        }

        [Fact]
        public void Voice_channels_beyond_the_six_most_recent_are_forgotten_but_DMs_are_kept()
        {
            NotInCall();
            Join(Dm("600", 1, State(Me, "gabe"), State(Ana, "ana")));
            Join(VoiceChannel("500", "Old channel", "9"));
            foreach (var n in Enumerable.Range(1, 6))
            {
                Join(Dm("60" + n, 1, State(Me, "gabe"), State(Bo, "bo" + n)));
            }

            Leave();
            OpenDiscordTab();
            Assert.Equal(new[] { "bo6", "bo5", "bo4", "bo3", "bo2", "bo1" }, ShortcutTitles());

            RemoveFocusedShortcut();

            Assert.Equal(new[] { "bo5", "bo4", "bo3", "bo2", "bo1", "ana" }, ShortcutTitles());
        }

        [Fact]
        public void Shortcuts_are_kept_in_the_secret_store_and_come_back_when_Playnite_starts_again()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9"));
            Reply("GET_GUILD", new JObject { ["id"] = "9", ["name"] = "The Lads", ["icon_url"] = "https://cdn.discordapp.com/icons/9/abc.png" });
            Join(Dm("600", 1, State(Me, "gabe"), State(Ana, "ana", avatar: "a1")));
            Assert.NotNull(secrets.Read("Discord.Shortcuts"));

            NewCore();
            NotInCall();
            OpenDiscordTab();

            Assert.Equal(new[] { "ana", "Late night" }, ShortcutTitles());
            Assert.Equal(new[] { AvatarOf(Ana, "a1"), "https://cdn.discordapp.com/icons/9/abc.png" }, Shortcuts().Select(i => i.Image));
            Assert.Equal(new[] { null, "The Lads" }, Shortcuts().Select(i => i.Detail));
        }

        [Fact]
        public void Disconnecting_keeps_the_shortcuts()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9"));

            core.DisconnectDiscord();
            ConnectFully();
            Reply("GET_SELECTED_VOICE_CHANNEL", null);
            OpenDiscordTab();

            Assert.Equal(new[] { "Late night" }, ShortcutTitles());
        }

        [Fact]
        public void A_on_a_shortcut_asks_Discord_to_join_it_leaving_the_Guide_open_until_Discord_says_so()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9"));
            Join(VoiceChannel("501", "B", "9"));
            Leave();
            OpenDiscordTab();
            Tap(ControllerButton.DPadRight);
            Assert.Equal("A", Guide.FocusedItem.Title);

            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["channel_id"] = "500", ["force"] = true }, transport.LastCommand("SELECT_VOICE_CHANNEL")["args"]));
            Assert.True(Guide.IsOpen);
            Assert.Equal(GuideItemKind.DiscordAccount, Guide.Items[0].Kind);

            Join(VoiceChannel("500", "A", "9", State(Me, "gabe")));

            Assert.Equal(GuideItemKind.DiscordChannel, Guide.Items[0].Kind);
            Assert.Equal(GuideItemKind.DiscordMute, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_on_the_shortcut_of_the_call_the_user_is_in_sends_nothing()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9", State(Me, "gabe")));
            OpenDiscordTab();
            while (Guide.FocusedItem.Kind != GuideItemKind.DiscordShortcut)
            {
                Tap(ControllerButton.DPadDown);
            }

            var sent = transport.Sent.Count;
            Tap(ControllerButton.A);

            Assert.Equal(sent, transport.Sent.Count);
            Assert.True(Guide.IsOpen);
        }

        [Fact]
        public void A_shortcut_has_a_menu_with_Remove_shortcut_which_forgets_it_for_good()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9"));
            Join(VoiceChannel("501", "B", "9"));
            Join(VoiceChannel("502", "C", "9"));
            Leave();
            OpenDiscordTab();
            Tap(ControllerButton.DPadRight);
            Assert.True(Guide.FocusedItem.HasMenu);
            Assert.Contains(Guide.Hints, h => h.Button == ControllerButton.Start);

            Tap(ControllerButton.Start);

            var menu = Guide.Dialog;
            Assert.Equal("B", menu.Title);
            var option = Assert.Single(menu.Options);
            Assert.Equal("Remove shortcut", option.Label);
            Assert.Equal(DialogIcon.Remove, option.Icon);

            Tap(ControllerButton.A);

            Assert.Null(Guide.Dialog);
            Assert.True(Guide.IsOpen);
            Assert.Equal(new[] { "C", "A" }, ShortcutTitles());
            Assert.Equal("A", Guide.FocusedItem.Title);

            NewCore();
            NotInCall();
            OpenDiscordTab();
            Assert.Equal(new[] { "C", "A" }, ShortcutTitles());
        }

        [Fact]
        public void Removing_the_last_shortcut_moves_focus_to_Servers()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9"));
            Leave();
            OpenDiscordTab();

            RemoveFocusedShortcut();

            Assert.Empty(Shortcuts());
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void B_on_the_shortcut_menu_closes_it_keeping_the_shortcut()
        {
            NotInCall();
            Join(VoiceChannel("500", "A", "9"));
            Leave();
            OpenDiscordTab();
            Tap(ControllerButton.Start);

            Tap(ControllerButton.B);

            Assert.Null(Guide.Dialog);
            Assert.True(Guide.IsOpen);
            Assert.Equal(new[] { "A" }, ShortcutTitles());
        }

        [Fact]
        public void Left_and_right_move_along_a_row_of_shortcuts_held_at_its_ends()
        {
            JoinChannels(5);
            OpenDiscordTab();
            sounds.Played.Clear();

            Tap(ControllerButton.DPadLeft);
            Assert.Equal(0, FocusedTile());
            Assert.DoesNotContain(UiSound.Navigate, sounds.Played);

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadRight);
            Assert.Equal(2, FocusedTile());
            sounds.Played.Clear();
            Tap(ControllerButton.DPadRight);
            Assert.Equal(2, FocusedTile());
            Assert.DoesNotContain(UiSound.Navigate, sounds.Played);

            Tap(ControllerButton.DPadDown);
            Assert.Equal(4, FocusedTile());
            Tap(ControllerButton.DPadRight);
            Assert.Equal(4, FocusedTile());
            Tap(ControllerButton.DPadLeft);
            Assert.Equal(3, FocusedTile());
            Tap(ControllerButton.DPadLeft);
            Assert.Equal(3, FocusedTile());
        }

        [Fact]
        public void Down_from_a_tile_with_none_below_goes_to_the_last_tile_then_on_to_Servers()
        {
            JoinChannels(5);
            OpenDiscordTab();
            Tap(ControllerButton.DPadRight);

            Tap(ControllerButton.DPadDown);
            Assert.Equal(4, FocusedTile());

            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);

            Tap(ControllerButton.DPadUp);
            Assert.Equal(3, FocusedTile());

            Tap(ControllerButton.DPadUp);
            Assert.Equal(0, FocusedTile());
        }

        [Fact]
        public void Down_from_the_tiles_last_row_goes_to_Servers_and_up_from_the_first_row_to_the_entry_above()
        {
            JoinChannels(3);
            OpenDiscordTab();
            Tap(ControllerButton.DPadRight);

            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
            Tap(ControllerButton.DPadUp);
            Assert.Equal(0, FocusedTile());

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadUp);
            Assert.Equal(GuideItemKind.DiscordIdle, Guide.FocusedItem.Kind);

            Tap(ControllerButton.DPadDown);
            Assert.Equal(0, FocusedTile());
        }

        [Fact]
        public void In_a_call_down_from_Leave_reaches_the_first_tile()
        {
            JoinChannels(4);
            Join(VoiceChannel("599", "Now", "9", State(Me, "gabe")));
            OpenDiscordTab();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.DiscordLeave, Guide.FocusedItem.Kind);

            Tap(ControllerButton.DPadDown);

            Assert.Equal(0, FocusedTile());
            Assert.Equal("Now", Guide.FocusedItem.Title);
            Tap(ControllerButton.DPadUp);
            Assert.Equal(GuideItemKind.DiscordLeave, Guide.FocusedItem.Kind);
        }

        /// <summary>Joins and leaves <paramref name="count"/> voice channels, so the idle tab has that many tiles, focus on the first.</summary>
        private void JoinChannels(int count)
        {
            NotInCall();
            foreach (var n in Enumerable.Range(1, count))
            {
                Join(VoiceChannel("50" + n, "Ch" + n, "9"));
            }

            Leave();
        }

        private int FocusedTile() => Shortcuts().ToList().IndexOf(Guide.FocusedItem);

        private void RemoveFocusedShortcut()
        {
            Tap(ControllerButton.Start);
            Tap(ControllerButton.A);
        }
    }
}
