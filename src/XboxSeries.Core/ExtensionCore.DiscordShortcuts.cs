using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Shortcuts: the DMs and voice channels the user has joined, on this PC or another device, learned from the calls
    /// Discord reports, most recently joined first, kept in the secret store. The Discord tab shows the first few as
    /// tiles that rejoin a call in one press; Remove shortcut forgets one.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The secret store's name for the known channels, as JSON. Nothing of Discord's goes to Playnite's plain settings.</summary>
        private const string ShortcutsSecret = "Discord.Shortcuts";

        /// <summary>How many shortcuts the Discord tab shows, and how many voice channels are kept.</summary>
        private const int ShortcutCount = 6;

        /// <summary>
        /// The known channels, most recently joined first. DMs and group DMs are kept however far down they fall; a
        /// server's voice channel only while among the first <see cref="ShortcutCount"/>.
        /// </summary>
        private List<KnownChannel> knownChannels = new List<KnownChannel>();

        /// <summary>The known channels saved before, read when the core is made.</summary>
        private void LoadShortcuts()
        {
            var json = secrets.Read(ShortcutsSecret);
            try
            {
                knownChannels = json == null ? new List<KnownChannel>() : JsonConvert.DeserializeObject<List<KnownChannel>>(json) ?? new List<KnownChannel>();
            }
            catch (JsonException)
            {
                knownChannels = new List<KnownChannel>();
            }

            knownChannels.RemoveAll(c => c?.Id == null);
        }

        private void SaveShortcuts() => secrets.Write(ShortcutsSecret, JsonConvert.SerializeObject(knownChannels));

        /// <summary>
        /// A call shown for a channel it wasn't showing before: the channel moves to the front, taking the name and a DM's
        /// picture now known, else keeping those learned before, and a voice channel's server moves to the front of the
        /// servers and is asked for its name and icon.
        /// </summary>
        private void LearnChannel(DiscordCall joined)
        {
            var known = knownChannels.FirstOrDefault(c => c.Id == joined.Id) ?? new KnownChannel { Id = joined.Id };
            knownChannels.Remove(known);
            knownChannels.Insert(0, known);
            known.GuildId = joined.GuildId;
            known.Name = joined.HasName ? joined.Title : known.Name;
            known.Image = joined.GuildId == null ? joined.OtherAvatar ?? known.Image : known.Image;
            for (var i = knownChannels.Count - 1; i >= ShortcutCount; i--)
            {
                if (knownChannels[i].GuildId != null)
                {
                    knownChannels.RemoveAt(i);
                }
            }

            SaveShortcuts();
            if (joined.GuildId != null)
            {
                RememberServer(joined.GuildId);
                SendCommand("GET_GUILD", new JObject { ["guild_id"] = joined.GuildId });
            }
        }

        /// <summary>
        /// The call changed: its shortcut, if it still has one and lacks them, takes the name now known, and a DM the
        /// avatar of the first person in it, as when someone answers a DM rung; one already named keeps its name and
        /// picture as people come and go. It doesn't move, and a shortcut removed during the call isn't brought back.
        /// </summary>
        private void RememberCall()
        {
            var known = call == null ? null : knownChannels.FirstOrDefault(c => c.Id == call.Id);
            if (known == null)
            {
                return;
            }

            var name = known.Name ?? (call.HasName ? call.Title : null);
            var image = known.Image ?? (call.GuildId == null ? call.OtherAvatar : null);
            if (name != known.Name || image != known.Image)
            {
                known.Name = name;
                known.Image = image;
                SaveShortcuts();
            }
        }

        /// <summary>GET_GUILD answered: the server's voice channels take its icon and name, saved and shown only if they changed.</summary>
        private void OnGuild(JObject guild)
        {
            var id = (string)guild?["id"];
            var image = (string)guild?["icon_url"];
            var serverName = (string)guild?["name"];
            var changed = false;
            foreach (var known in knownChannels.Where(c => id != null && c.GuildId == id && (c.Image != image || c.ServerName != serverName)))
            {
                known.Image = image;
                known.ServerName = serverName;
                changed = true;
            }

            if (changed)
            {
                SaveShortcuts();
                OnCallChanged();
            }
        }

        /// <summary>Remove shortcut: the channel is forgotten, gone from the saved list too, and the Discord tab rebuilt keeping focus in place.</summary>
        private void ForgetChannel(string channelId)
        {
            knownChannels.RemoveAll(c => c.Id == channelId);
            SaveShortcuts();
            OnCallChanged();
        }

        /// <summary>The Discord tab's shortcut tiles, the first under a divider; a DM nobody else has been seen in is "Call".</summary>
        private IEnumerable<GuideItem> ShortcutItems() =>
            knownChannels.Take(ShortcutCount).Select((c, i) =>
                new GuideItem(
                    GuideItemKind.DiscordShortcut, c.Name ?? "Call", hasDividerAbove: i == 0, image: c.Image, detail: c.ServerName,
                    isDirectMessage: c.GuildId == null) { ChannelId = c.Id });

        /// <summary>A on a shortcut or a server's voice channel: asks Discord to join it, unless it is the call's already.</summary>
        private void JoinChannel(string channelId)
        {
            if (channelId != call?.Id)
            {
                SendCommand("SELECT_VOICE_CHANNEL", new JObject { ["channel_id"] = channelId, ["force"] = true });
            }
        }

        /// <summary>Menu on a shortcut: a menu under its name with Remove shortcut.</summary>
        private void ShowShortcutMenu(GuideItem item)
        {
            var channelId = item.ChannelId;
            ShowDialog(new Dialog(
                item.Title,
                null,
                isPrompt: false,
                CloseDialog,
                new DialogOption("Remove shortcut", DialogIcon.Remove, () =>
                {
                    CloseDialog();
                    ForgetChannel(channelId);
                })));
        }

        /// <summary>
        /// The D-pad among the Discord tab's shortcut tiles, rows of <see cref="GuideState.DiscordShortcutColumns"/>: left
        /// and right along a row, held at its ends; down a row, else to the last tile on a shorter row below, else on
        /// past the tiles; up a row, else to the entry above them. Down onto the tiles lands on the first, up onto them
        /// on the last row's first. False when focus isn't on or next to the tiles, for the tab's own moves.
        /// </summary>
        private bool MoveAmongShortcuts(ControllerButton direction)
        {
            var items = Guide.Items.ToList();
            var tiles = items.Where(i => i.Kind == GuideItemKind.DiscordShortcut).ToList();
            if (tiles.Count == 0)
            {
                return false;
            }

            const int columns = GuideState.DiscordShortcutColumns;
            var focused = Guide.FocusedItem;
            var first = items.IndexOf(tiles[0]);
            var after = first + tiles.Count;
            var lastRow = (tiles.Count - 1) / columns;
            var index = tiles.IndexOf(focused);
            GuideItem next;
            if (index < 0)
            {
                if (direction == ControllerButton.DPadDown && items.IndexOf(focused) == first - 1)
                {
                    next = tiles[0];
                }
                else if (direction == ControllerButton.DPadUp && items.IndexOf(focused) == after)
                {
                    next = tiles[lastRow * columns];
                }
                else
                {
                    return false;
                }
            }
            else
            {
                var row = index / columns;
                var column = index % columns;
                switch (direction)
                {
                    case ControllerButton.DPadLeft:
                        next = column > 0 ? tiles[index - 1] : focused;
                        break;
                    case ControllerButton.DPadRight:
                        next = column < columns - 1 && index + 1 < tiles.Count ? tiles[index + 1] : focused;
                        break;
                    case ControllerButton.DPadUp:
                        next = row > 0 ? tiles[index - columns] : first > 0 ? items[first - 1] : focused;
                        break;
                    default:
                        next = index + columns < tiles.Count ? tiles[index + columns]
                            : row < lastRow ? tiles[tiles.Count - 1]
                            : after < items.Count ? items[after]
                            : focused;
                        break;
                }
            }

            if (next != focused)
            {
                sounds.Play(UiSound.Navigate);
                Guide.FocusedItem = next;
            }

            return true;
        }

        /// <summary>A DM, group DM or voice channel the user has joined, as saved.</summary>
        private sealed class KnownChannel
        {
            /// <summary>The channel's id, which SELECT_VOICE_CHANNEL takes.</summary>
            public string Id { get; set; }

            /// <summary>The server a voice channel is on; null for a DM or group DM.</summary>
            public string GuildId { get; set; }

            /// <summary>The call's heading when last joined: the channel's name, or a DM's from the people in it; null while a DM has no real one.</summary>
            public string Name { get; set; }

            /// <summary>A DM's first other person's avatar, or a voice channel's server icon; null for none yet.</summary>
            public string Image { get; set; }

            /// <summary>A voice channel's server's name; null for a DM or until the server has said.</summary>
            public string ServerName { get; set; }
        }
    }
}
