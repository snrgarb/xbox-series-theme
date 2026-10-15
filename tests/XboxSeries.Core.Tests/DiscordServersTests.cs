using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>The Discord tab's Servers: the user's servers, then a server's voice channels with who is in them, joined in one press.</summary>
    public class DiscordServersTests : DiscordTestsBase
    {
        private const string LadsIcon = "https://cdn.discordapp.com/icons/9/abc.png";

        [Fact]
        public void A_on_Servers_asks_Discord_for_the_servers_and_opens_a_flyout_loading_them()
        {
            OpenServers();

            Assert.True(JToken.DeepEquals(new JObject(), transport.LastCommand("GET_GUILDS")["args"]));
            Assert.Equal(GuideItemKind.DiscordServers, Guide.Flyout.Kind);
            Assert.Equal("Servers", Guide.Flyout.Title);
            Assert.True(Guide.Flyout.IsLoading);
            Assert.Empty(Guide.Flyout.Items);
            Assert.Null(Guide.FocusedItem);
        }

        [Fact]
        public void The_servers_arrive_one_entry_each_with_its_icon_focus_on_the_first()
        {
            OpenServers();

            ReplyGuilds();

            Assert.False(Guide.Flyout.IsLoading);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.Flyout.Kind);
            Assert.Equal(new[] { "The Lads", "Book club" }, Guide.Flyout.Items.Select(i => i.Title));
            Assert.All(Guide.Flyout.Items, i => Assert.Equal(GuideItemKind.DiscordServer, i.Kind));
            Assert.Equal(new[] { LadsIcon, null }, Guide.Flyout.Items.Select(i => i.Image));
            Assert.Same(Guide.Flyout.Items[0], Guide.FocusedItem);
        }

        [Fact]
        public void Discord_refusing_the_servers_stops_loading_with_none()
        {
            OpenServers();

            Error("GET_GUILDS", "Nope");

            Assert.False(Guide.Flyout.IsLoading);
            Assert.Empty(Guide.Flyout.Items);
            Assert.True(core.Settings.Discord.IsConnected);
        }

        [Fact]
        public void B_in_the_servers_flyout_closes_it_focus_back_on_Servers()
        {
            OpenServers();
            ReplyGuilds();

            Tap(ControllerButton.B);

            Assert.Null(Guide.Flyout);
            Assert.True(Guide.IsOpen);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Servers_arriving_after_the_flyout_closed_are_ignored()
        {
            OpenServers();
            Tap(ControllerButton.B);

            ReplyGuilds();

            Assert.Null(Guide.Flyout);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_on_a_server_asks_for_its_channels_and_opens_its_flyout_loading_them()
        {
            OpenServers();
            ReplyGuilds();

            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["guild_id"] = "9" }, transport.LastCommand("GET_CHANNELS")["args"]));
            Assert.Equal(GuideItemKind.DiscordServer, Guide.Flyout.Kind);
            Assert.Equal("The Lads", Guide.Flyout.Title);
            Assert.True(Guide.Flyout.IsLoading);
            Assert.Empty(Guide.Flyout.Items);
        }

        [Fact]
        public void A_servers_voice_channels_list_in_Discords_order_then_each_is_fetched_for_who_is_in_it()
        {
            OpenLads();

            ReplyChannels();

            Assert.False(Guide.Flyout.IsLoading);
            Assert.Equal(new[] { "Late night", "Raid" }, Guide.Flyout.Items.Select(i => i.Title));
            Assert.All(Guide.Flyout.Items, i => Assert.Equal(GuideItemKind.DiscordVoiceChannel, i.Kind));
            Assert.All(Guide.Flyout.Items, i => Assert.Null(i.Occupants));
            Assert.Same(Guide.Flyout.Items[0], Guide.FocusedItem);
            Assert.Equal(new[] { "500", "501" }, Sent("GET_CHANNEL").Select(c => (string)c["args"]["channel_id"]));
        }

        [Fact]
        public void A_voice_channel_shows_the_avatars_of_who_is_in_it_or_none_when_empty()
        {
            OpenLads();
            ReplyChannels();
            var fetches = Sent("GET_CHANNEL");
            var lateNight = Guide.Flyout.Items[0];

            ReplyTo(fetches[1], VoiceChannel("501", "Raid", "9"));
            ReplyTo(fetches[0], VoiceChannel("500", "Late night", "9", State(Ana, "ana", avatar: "a1"), State(Bo, "bo", avatar: "b2")));

            Assert.Equal(new[] { AvatarOf(Ana, "a1"), AvatarOf(Bo, "b2") }, Guide.Flyout.Items[0].Occupants);
            Assert.Empty(Guide.Flyout.Items[1].Occupants);
            Assert.Same(lateNight, Guide.Flyout.Items[0]);
        }

        [Fact]
        public void Occupants_change_with_a_property_change_the_views_follow()
        {
            OpenLads();
            ReplyChannels();
            var changed = new System.Collections.Generic.List<string>();
            Guide.Flyout.Items[0].PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            ReplyTo(Sent("GET_CHANNEL")[0], VoiceChannel("500", "Late night", "9", State(Ana, "ana")));

            Assert.Contains("Occupants", changed);
        }

        [Fact]
        public void B_in_a_servers_flyout_goes_back_to_the_servers_without_asking_again_focus_on_that_server()
        {
            OpenServers();
            ReplyGuilds();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);
            Assert.Equal("Book club", Guide.Flyout.Title);
            var asked = Sent("GET_GUILDS").Count;

            Tap(ControllerButton.B);

            Assert.Equal(GuideItemKind.DiscordServers, Guide.Flyout.Kind);
            Assert.False(Guide.Flyout.IsLoading);
            Assert.Equal(new[] { "The Lads", "Book club" }, Guide.Flyout.Items.Select(i => i.Title));
            Assert.Equal("Book club", Guide.FocusedItem.Title);
            Assert.Equal(asked, Sent("GET_GUILDS").Count);
        }

        [Fact]
        public void B_in_a_servers_flyout_goes_back_to_that_server_among_others_of_the_same_name()
        {
            OpenServers();
            Reply("GET_GUILDS", new JObject
            {
                ["guilds"] = new JArray(
                    new JObject { ["id"] = "9", ["name"] = "The Lads" },
                    new JObject { ["id"] = "8", ["name"] = "The Lads" }),
            });
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Tap(ControllerButton.B);

            Assert.Same(Guide.Flyout.Items[1], Guide.FocusedItem);
        }

        [Fact]
        public void Stage_channels_list_among_the_voice_channels_in_Discords_order()
        {
            OpenLads();

            Reply("GET_CHANNELS", new JObject
            {
                ["channels"] = new JArray(
                    new JObject { ["id"] = "490", ["name"] = "general", ["type"] = 0 },
                    new JObject { ["id"] = "510", ["name"] = "Town hall", ["type"] = 13 },
                    new JObject { ["id"] = "500", ["name"] = "Late night", ["type"] = 2 }),
            });

            Assert.Equal(new[] { "Town hall", "Late night" }, Guide.Flyout.Items.Select(i => i.Title));
        }

        [Fact]
        public void Who_is_in_a_channel_the_user_has_just_joined_arriving_first_fills_it_but_the_call_waits_for_its_own_reply()
        {
            OpenLads();
            ReplyChannels();
            var fetch = Sent("GET_CHANNEL")[0];
            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "500", ["guild_id"] = "9" });
            var own = transport.LastCommand("GET_CHANNEL");

            ReplyTo(fetch, VoiceChannel("500", "Late night", "9", State(Me, "gabe"), State(Ana, "ana")));

            Assert.False(core.Status.IsInDiscordCall);
            Assert.Equal(2, Guide.Flyout.Items[0].Occupants.Count);

            ReplyTo(own, VoiceChannel("500", "Late night", "9", State(Me, "gabe"), State(Ana, "ana")));

            Assert.True(core.Status.IsInDiscordCall);
            Assert.Equal(GuideItemKind.DiscordChannel, Guide.Items[0].Kind);
        }

        [Fact]
        public void Channels_arriving_after_the_user_backed_out_of_the_server_are_ignored()
        {
            OpenLads();
            var getChannels = transport.LastCommand("GET_CHANNELS");
            Tap(ControllerButton.B);

            ReplyTo(getChannels, Channels());

            Assert.Equal(GuideItemKind.DiscordServers, Guide.Flyout.Kind);
            Assert.Empty(Sent("GET_CHANNEL"));
        }

        [Fact]
        public void Channels_of_a_server_left_for_another_are_ignored()
        {
            OpenLads();
            var lads = transport.LastCommand("GET_CHANNELS");
            Tap(ControllerButton.B);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            ReplyTo(lads, Channels());

            Assert.Equal("Book club", Guide.Flyout.Title);
            Assert.True(Guide.Flyout.IsLoading);
            Assert.Empty(Guide.Flyout.Items);
        }

        [Fact]
        public void Who_is_in_a_channel_arriving_after_the_user_backed_out_is_ignored()
        {
            OpenLads();
            ReplyChannels();
            var fetch = Sent("GET_CHANNEL")[0];
            var lateNight = Guide.Flyout.Items[0];
            Tap(ControllerButton.B);

            ReplyTo(fetch, VoiceChannel("500", "Late night", "9", State(Ana, "ana")));

            Assert.Equal(GuideItemKind.DiscordServers, Guide.Flyout.Kind);
            Assert.Null(lateNight.Occupants);
        }

        [Fact]
        public void Discord_refusing_a_servers_channels_stops_loading_with_none()
        {
            OpenLads();

            Error("GET_CHANNELS", "Nope");

            Assert.False(Guide.Flyout.IsLoading);
            Assert.Empty(Guide.Flyout.Items);
        }

        [Fact]
        public void A_on_a_voice_channel_asks_Discord_to_join_it_and_the_flyout_closes_focus_on_Servers()
        {
            OpenLads();
            ReplyChannels();
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.True(JToken.DeepEquals(new JObject { ["channel_id"] = "501", ["force"] = true }, transport.LastCommand("SELECT_VOICE_CHANNEL")["args"]));
            Assert.Null(Guide.Flyout);
            Assert.True(Guide.IsOpen);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_on_the_voice_channel_the_user_is_in_asks_nothing_and_closes_the_flyout()
        {
            NotInCall();
            Join(VoiceChannel("501", "Raid", "9", State(Me, "gabe")));
            OpenDiscordTab();
            while (Guide.FocusedItem.Kind != GuideItemKind.DiscordServers)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.A);
            ReplyGuilds();
            Tap(ControllerButton.A);
            ReplyChannels();
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.Null(transport.LastCommand("SELECT_VOICE_CHANNEL"));
            Assert.Null(Guide.Flyout);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_call_beginning_while_a_server_is_open_rebuilds_the_tab_behind_and_the_flyout_closes_onto_Servers()
        {
            OpenLads();
            ReplyChannels();

            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));

            Assert.Equal(GuideItemKind.DiscordServer, Guide.Flyout.Kind);
            Assert.Contains(Guide.FocusedItem, Guide.Flyout.Items);
            Assert.Equal(GuideItemKind.DiscordChannel, Guide.Items[0].Kind);

            Tap(ControllerButton.A);

            Assert.Null(Guide.Flyout);
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
            Assert.Contains(Guide.FocusedItem, Guide.Items);
        }

        [Fact]
        public void The_call_still_follows_the_channel_the_user_moved_to_while_a_server_is_open()
        {
            OpenLads();
            ReplyChannels();

            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = "501", ["guild_id"] = "9" });
            Reply("GET_CHANNEL", VoiceChannel("501", "Raid", "9", State(Me, "gabe")));

            Assert.True(core.Status.IsInDiscordCall);
            Assert.Single(Guide.Flyout.Items[1].Occupants);
            Assert.Null(Guide.Flyout.Items[0].Occupants);
        }

        [Fact]
        public void Discord_going_away_with_a_server_open_falls_back_to_the_landing_tab()
        {
            OpenLads();

            core.OnDiscordClosed();

            Assert.Null(Guide.Flyout);
            Assert.Equal(GuideTabKind.Landing, Guide.Tab);
        }

        [Fact]
        public void LB_and_RB_stay_put_while_a_servers_flyout_is_open()
        {
            OpenServers();

            Tap(ControllerButton.RightShoulder);

            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
            Assert.NotNull(Guide.Flyout);
        }

        private void OpenServers()
        {
            NotInCall();
            OpenDiscordTab();
            Assert.Equal(GuideItemKind.DiscordServers, Guide.FocusedItem.Kind);
            Tap(ControllerButton.A);
        }

        private void OpenLads()
        {
            OpenServers();
            ReplyGuilds();
            Tap(ControllerButton.A);
        }

        private void ReplyGuilds() => Reply("GET_GUILDS", new JObject
        {
            ["guilds"] = new JArray(
                new JObject { ["id"] = "9", ["name"] = "The Lads", ["icon_url"] = LadsIcon },
                new JObject { ["id"] = "8", ["name"] = "Book club", ["icon_url"] = null }),
        });

        private void ReplyChannels() => Reply("GET_CHANNELS", Channels());

        /// <summary>The Lads' channels, as GET_CHANNELS answers: text channels among the voice ones.</summary>
        private static JObject Channels() => new JObject
        {
            ["channels"] = new JArray(
                new JObject { ["id"] = "490", ["name"] = "general", ["type"] = 0 },
                new JObject { ["id"] = "500", ["name"] = "Late night", ["type"] = 2 },
                new JObject { ["id"] = "491", ["name"] = "memes", ["type"] = 0 },
                new JObject { ["id"] = "501", ["name"] = "Raid", ["type"] = 2 }),
        };
    }
}
