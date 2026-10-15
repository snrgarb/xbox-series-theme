using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Servers: a flyout of the user's Discord servers, then one of a server's voice channels with the avatars of the
    /// people in each, fetched as it opens; A on a channel joins it. Replies for a flyout no longer showing are ignored.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The servers' entries, kept from Discord's answer, so that B out of a server goes back to them without asking again.</summary>
        private IReadOnlyList<GuideItem> serverItems = new GuideItem[0];

        /// <summary>The nonce of the GET_GUILDS or GET_CHANNELS the loading flyout waits for; a reply under any other is stale.</summary>
        private string flyoutRequest;

        /// <summary>The GET_CHANNELs sent to learn who is in a server's voice channels, by nonce.</summary>
        private readonly HashSet<string> occupantRequests = new HashSet<string>();

        /// <summary>A on Servers: asks Discord for the user's servers, in a flyout that shows they're coming.</summary>
        private void OpenServers()
        {
            flyoutRequest = SendCommand("GET_GUILDS", new JObject());
            Guide.Flyout = new GuideFlyout(GuideItemKind.DiscordServers, "Servers", new GuideItem[0], isLoading: true);
            Guide.FocusedItem = null;
        }

        /// <summary>The Servers flyout with the servers already fetched, focus on <paramref name="focused"/>, else the first.</summary>
        private void ShowServers(GuideItem focused)
        {
            Guide.Flyout = new GuideFlyout(GuideItemKind.DiscordServers, "Servers", serverItems);
            Guide.FocusedItem = focused ?? serverItems.FirstOrDefault();
        }

        /// <summary>A on a server: asks Discord for its channels, in a flyout under its name that shows they're coming.</summary>
        private void OpenServer(GuideItem server)
        {
            flyoutRequest = SendCommand("GET_CHANNELS", new JObject { ["guild_id"] = server.GuildId });
            Guide.Flyout = new GuideFlyout(GuideItemKind.DiscordServer, server.Title, new GuideItem[0], isLoading: true);
            Guide.FocusedItem = null;
        }

        /// <summary>B in a server's flyout: back to the servers, focus on the one it was.</summary>
        private void BackToServers()
        {
            var title = Guide.Flyout.Title;
            flyoutRequest = null;
            ShowServers(serverItems.FirstOrDefault(s => s.Title == title));
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

        /// <summary>Whether a reply under <paramref name="nonce"/> is the one the <paramref name="kind"/> flyout showing is loading for.</summary>
        private bool IsAwaitedBy(GuideItemKind kind, string nonce) =>
            nonce == flyoutRequest && Guide.Flyout?.Kind == kind && Guide.Flyout.IsLoading;

        /// <summary>GET_GUILDS answered: a server entry each, in Discord's order, focus on the first.</summary>
        private void OnGuilds(string nonce, JObject data)
        {
            if (!IsAwaitedBy(GuideItemKind.DiscordServers, nonce))
            {
                return;
            }

            flyoutRequest = null;
            serverItems = (data?["guilds"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(g => new GuideItem(GuideItemKind.DiscordServer, (string)g["name"], image: (string)g["icon_url"]) { GuildId = (string)g["id"] })
                .ToList();
            ShowServers(null);
        }

        /// <summary>GET_CHANNELS answered: an entry for each voice channel, in Discord's order, focus on the first; then who is in each is asked.</summary>
        private void OnChannels(string nonce, JObject data)
        {
            if (!IsAwaitedBy(GuideItemKind.DiscordServer, nonce))
            {
                return;
            }

            flyoutRequest = null;
            var channels = (data?["channels"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(c => (int?)c["type"] == 2)
                .Select(c => new GuideItem(GuideItemKind.DiscordVoiceChannel, (string)c["name"]) { ChannelId = (string)c["id"] })
                .ToList();
            Guide.Flyout = new GuideFlyout(GuideItemKind.DiscordServer, Guide.Flyout.Title, channels);
            Guide.FocusedItem = channels.FirstOrDefault();
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

        /// <summary>Discord refused the servers or a server's channels: the flyout stops loading, with none.</summary>
        private void OnFlyoutRefused(string nonce)
        {
            var kind = Guide.Flyout?.Kind ?? GuideItemKind.DiscordServers;
            if (IsAwaitedBy(kind, nonce))
            {
                flyoutRequest = null;
                Guide.Flyout = new GuideFlyout(kind, Guide.Flyout.Title, new GuideItem[0]);
            }
        }
    }
}
