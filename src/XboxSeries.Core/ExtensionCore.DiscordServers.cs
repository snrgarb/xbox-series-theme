using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Servers: a flyout of the user's Discord servers, most recently joined first, then the rest by name; then one of a
    /// server's voice channels with the avatars of the people in each; A on a channel joins it. What Discord answered is
    /// kept until it goes, so that each opens at once while asked again. Replies for a flyout no longer showing are ignored.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The secret store's name for the servers the user has joined a call in, most recently first, as JSON.</summary>
        private const string RecentServersSecret = "Discord.RecentServers";

        /// <summary>The ids of the servers the user has joined a call in, most recently first, every one kept.</summary>
        private List<string> recentServers = new List<string>();

        /// <summary>The servers' entries, kept from Discord's last answer until it goes; null until it has answered.</summary>
        private IReadOnlyList<GuideItem> serverItems;

        /// <summary>Each server's voice channel entries, with who is in them, kept from Discord's last answers until it goes, by server id.</summary>
        private readonly Dictionary<string, IReadOnlyList<GuideItem>> channelItems = new Dictionary<string, IReadOnlyList<GuideItem>>();

        /// <summary>The nonce of the GET_GUILDS or GET_CHANNELS the flyout showing waits for; a reply under any other is stale.</summary>
        private string flyoutRequest;

        /// <summary>The server whose flyout is open or loading, which B goes back to among the servers.</summary>
        private string openServerId;

        /// <summary>How long a flyout's spinner shows at least, however quickly Discord answers.</summary>
        private static readonly TimeSpan SpinnerShown = TimeSpan.FromSeconds(1);

        /// <summary>When the loading flyout showing put its spinner up.</summary>
        private DateTime spinnerSince;

        /// <summary>The GET_CHANNELs sent to learn who is in a server's voice channels, by nonce.</summary>
        private readonly HashSet<string> occupantRequests = new HashSet<string>();

        /// <summary>The servers joined before, read when the core is made.</summary>
        private void LoadRecentServers()
        {
            var json = secrets.Read(RecentServersSecret);
            try
            {
                recentServers = json == null ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                recentServers = new List<string>();
            }

            recentServers.RemoveAll(id => id == null);
        }

        /// <summary>A call learned in one of <paramref name="guildId"/>'s voice channels: the server moves to the front, saved.</summary>
        private void RememberServer(string guildId)
        {
            recentServers.Remove(guildId);
            recentServers.Insert(0, guildId);
            secrets.Write(RecentServersSecret, JsonConvert.SerializeObject(recentServers));
        }

        /// <summary><paramref name="servers"/> in the flyout's order: those joined, most recently first, then the rest by name.</summary>
        private IReadOnlyList<GuideItem> InServerOrder(IEnumerable<GuideItem> servers) =>
            servers
                .Select(s => new { Server = s, Recent = recentServers.IndexOf(s.GuildId) })
                .OrderBy(s => s.Recent < 0 ? int.MaxValue : s.Recent)
                .ThenBy(s => s.Recent < 0 ? s.Server.Title : null, StringComparer.CurrentCultureIgnoreCase)
                .Select(s => s.Server)
                .ToList();

        /// <summary>
        /// A on Servers: shows the servers Discord answered before at once, focus on the first, or else a flyout that
        /// shows they're coming; then asks Discord for them.
        /// </summary>
        private void OpenServers()
        {
            if (serverItems != null)
            {
                ShowServers(null);
            }
            else
            {
                ShowLoading(GuideItemKind.DiscordServers, "Servers");
            }

            flyoutRequest = SendCommand("GET_GUILDS", new JObject());
        }

        /// <summary>The Servers flyout with the servers already fetched, focus on <paramref name="focused"/>, else the first.</summary>
        private void ShowServers(GuideItem focused)
        {
            serverItems = InServerOrder(serverItems);
            Guide.Flyout = new GuideFlyout(GuideItemKind.DiscordServers, "Servers", serverItems);
            Guide.FocusedItem = focused ?? serverItems.FirstOrDefault();
        }

        /// <summary>
        /// A on a server: a flyout under its name showing the channels Discord answered before at once, focus on the
        /// first, or else that they're coming; then asks Discord for them.
        /// </summary>
        private void OpenServer(GuideItem server)
        {
            openServerId = server.GuildId;
            if (channelItems.TryGetValue(server.GuildId, out var channels))
            {
                Guide.Flyout = new GuideFlyout(GuideItemKind.DiscordServer, server.Title, channels);
                Guide.FocusedItem = channels.FirstOrDefault();
            }
            else
            {
                ShowLoading(GuideItemKind.DiscordServer, server.Title);
            }

            flyoutRequest = SendCommand("GET_CHANNELS", new JObject { ["guild_id"] = server.GuildId });
        }

        /// <summary>A flyout with its spinner up, nothing in it to focus, while Discord is asked for its entries.</summary>
        private void ShowLoading(GuideItemKind kind, string title)
        {
            Guide.Flyout = new GuideFlyout(kind, title, new GuideItem[0], isLoading: true);
            Guide.FocusedItem = null;
            spinnerSince = clock.Now;
        }

        /// <summary>
        /// Shows Discord's answer once the loading flyout showing has had its spinner up a second, at once if it has or
        /// isn't loading; not at all if another flyout, or none, shows by then.
        /// </summary>
        private void AfterSpinner(Action show)
        {
            var flyout = Guide.Flyout;
            var left = SpinnerShown - (clock.Now - spinnerSince);
            if (!flyout.IsLoading || left <= TimeSpan.Zero)
            {
                show();
                return;
            }

            clock.After(left, () =>
            {
                if (Guide.Flyout == flyout)
                {
                    show();
                }
            });
        }

        /// <summary>B in a server's flyout: back to the servers, focus on the one it was.</summary>
        private void BackToServers()
        {
            flyoutRequest = null;
            ShowServers(serverItems.FirstOrDefault(s => s.GuildId == openServerId));
        }

        /// <summary>A on a voice channel: asks Discord to join it, and the flyout closes onto Servers.</summary>
        private void SelectVoiceChannel(GuideItem channel)
        {
            JoinChannel(channel.ChannelId);
            CloseDiscordFlyout();
        }

        /// <summary>Closes the Servers flyout or a server's, focus on the Discord tab's Servers.</summary>
        private void CloseDiscordFlyout()
        {
            flyoutRequest = null;
            Guide.Flyout = null;
            Guide.FocusedItem = Guide.Items.FirstOrDefault(i => i.Kind == GuideItemKind.DiscordServers);
        }

        /// <summary>Discord has gone: the servers and their channels are forgotten, to be asked afresh once it's back.</summary>
        private void ForgetServers()
        {
            flyoutRequest = null;
            serverItems = null;
            channelItems.Clear();
            occupantRequests.Clear();
        }

        /// <summary>Whether a reply under <paramref name="nonce"/> is the one the <paramref name="kind"/> flyout showing waits for.</summary>
        private bool IsAwaitedBy(GuideItemKind kind, string nonce) =>
            nonce == flyoutRequest && Guide.Flyout?.Kind == kind;

        /// <summary>
        /// Shows <paramref name="wanted"/>, Discord's answer, in the flyout showing: in place when it holds the same
        /// entries, else rebuilt, focus staying on the same server or channel, on the first after loading. Returns the
        /// entries now shown.
        /// </summary>
        private IReadOnlyList<GuideItem> ShowAnswered(IReadOnlyList<GuideItem> wanted)
        {
            var flyout = Guide.Flyout;
            if (!flyout.IsLoading && UpdateInPlace(flyout.Items, wanted))
            {
                return flyout.Items;
            }

            var focus = FocusAfterRebuild(flyout.Items, wanted);
            Guide.Flyout = new GuideFlyout(flyout.Kind, flyout.Title, wanted);
            Guide.FocusedItem = focus;
            return wanted;
        }

        /// <summary>GET_GUILDS answered: a server entry each, in the flyout's order, kept for reopening.</summary>
        private void OnGuilds(string nonce, JObject data)
        {
            if (!IsAwaitedBy(GuideItemKind.DiscordServers, nonce))
            {
                return;
            }

            flyoutRequest = null;
            AfterSpinner(() =>
                serverItems = ShowAnswered(InServerOrder((data?["guilds"] as JArray ?? new JArray()).OfType<JObject>()
                    .Select(g => new GuideItem(GuideItemKind.DiscordServer, (string)g["name"], image: (string)g["icon_url"]) { GuildId = (string)g["id"] }))));
        }

        /// <summary>
        /// GET_CHANNELS answered: an entry for each voice or stage channel, in Discord's order, kept for reopening, those
        /// shown before keeping who is in them; then who is in each is asked.
        /// </summary>
        private void OnChannels(string nonce, JObject data)
        {
            if (!IsAwaitedBy(GuideItemKind.DiscordServer, nonce))
            {
                return;
            }

            flyoutRequest = null;
            AfterSpinner(() => ShowChannels(data));
        }

        /// <summary>A server's voice channels, from Discord's answer, in the flyout under its name; then who is in each is asked.</summary>
        private void ShowChannels(JObject data)
        {
            var known = Guide.Flyout.Items;
            var channels = (data?["channels"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(c => (int?)c["type"] == 2 || (int?)c["type"] == 13)
                .Select(c => new GuideItem(GuideItemKind.DiscordVoiceChannel, (string)c["name"])
                {
                    ChannelId = (string)c["id"],
                    Occupants = known.FirstOrDefault(k => k.ChannelId == (string)c["id"])?.Occupants,
                })
                .ToList();
            channelItems[openServerId] = ShowAnswered(channels);
            foreach (var channel in channels)
            {
                occupantRequests.Add(SendCommand("GET_CHANNEL", new JObject { ["channel_id"] = channel.ChannelId }));
            }
        }

        /// <summary>
        /// A channel Discord sent, for whichever reason: if a voice channel showing is it, it shows the avatars of the
        /// people in it, none when it's empty.
        /// </summary>
        private void ShowOccupants(JObject channel)
        {
            var id = (string)channel?["id"];
            var shown = Guide.Flyout?.Kind == GuideItemKind.DiscordServer
                ? Guide.Flyout.Items.FirstOrDefault(i => i.ChannelId == id)
                : null;
            if (shown != null)
            {
                shown.Occupants = (channel["voice_states"] as JArray ?? new JArray()).OfType<JObject>()
                    .Select(DiscordParticipant.FromVoiceState)
                    .Where(p => p != null)
                    .Select(p => p.Avatar)
                    .ToList();
            }
        }

        /// <summary>
        /// Discord refused the servers or a server's channels: a flyout loading them stops, with none; one showing those
        /// answered before keeps them.
        /// </summary>
        private void OnFlyoutRefused(string nonce)
        {
            var kind = Guide.Flyout?.Kind ?? GuideItemKind.DiscordServers;
            if (IsAwaitedBy(kind, nonce))
            {
                flyoutRequest = null;
                if (Guide.Flyout.IsLoading)
                {
                    Guide.Flyout = new GuideFlyout(kind, Guide.Flyout.Title, new GuideItem[0]);
                }
            }
        }
    }
}
