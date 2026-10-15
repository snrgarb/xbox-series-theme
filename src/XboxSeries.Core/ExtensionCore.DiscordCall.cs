using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The call: which voice channel or DM the user is in and who is there, followed from Discord's replies and events,
    /// shown on the Discord tab, in the Audio flyout and in the status area; and Mute, Deafen and Leave, asked of Discord,
    /// which shows them done only once its events say so.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The events of the call's channel the Discord tab follows, one SUBSCRIBE each with its channel_id.</summary>
        private static readonly string[] CallEvents = { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP" };

        /// <summary>The signed-in Discord user's id, which tells the user among the people in a call.</summary>
        private string discordUserId;

        /// <summary>The channel Discord last said the user selected, fetched or being fetched; null out of a call.</summary>
        private string selectedChannelId;

        /// <summary>The call shown, once its channel has arrived; null out of one.</summary>
        private DiscordCall call;

        /// <summary>The user's own Discord mute and deafen, as Discord's voice settings last said, in a call or out of one.</summary>
        private bool discordSelfMute;
        private bool discordSelfDeaf;

        /// <summary>Signed in: asks which channel the user is already in, and their voice settings.</summary>
        private void AskForCall()
        {
            SendCommand("GET_SELECTED_VOICE_CHANNEL", new JObject());
            SendCommand("GET_VOICE_SETTINGS", new JObject());
        }

        /// <summary>Discord's answer to a command sent once signed in.</summary>
        private void OnDiscordReply(string sent, JObject data)
        {
            switch (sent)
            {
                case "GET_SELECTED_VOICE_CHANNEL":
                    selectedChannelId = (string)data?["id"];
                    ShowCall(DiscordCall.FromChannel(data, discordUserId));
                    break;
                case "GET_CHANNEL":
                    // Fetched for a channel the user has since left, it's stale.
                    if (selectedChannelId != null && (string)data?["id"] == selectedChannelId)
                    {
                        ShowCall(DiscordCall.FromChannel(data, discordUserId));
                    }

                    break;
                case "GET_VOICE_SETTINGS":
                    OnVoiceSettings(data);
                    break;
            }
        }

        /// <summary>An event Discord dispatched once signed in.</summary>
        private void OnDiscordEvent(string evt, JObject data)
        {
            switch (evt)
            {
                case "VOICE_CHANNEL_SELECT":
                    selectedChannelId = (string)data?["channel_id"];
                    if (selectedChannelId == null)
                    {
                        ShowCall(null);
                    }
                    else if (call?.Id != selectedChannelId)
                    {
                        // The call moves over once the channel arrives, with the people in it.
                        SendCommand("GET_CHANNEL", new JObject { ["channel_id"] = selectedChannelId });
                    }

                    break;
                case "VOICE_SETTINGS_UPDATE":
                    OnVoiceSettings(data);
                    break;
                case "VOICE_STATE_CREATE":
                case "VOICE_STATE_UPDATE":
                    call?.Upsert(data);
                    OnCallChanged();
                    break;
                case "VOICE_STATE_DELETE":
                    call?.Remove(data);
                    OnCallChanged();
                    break;
                case "SPEAKING_START":
                case "SPEAKING_STOP":
                    var channelId = (string)data?["channel_id"];
                    if (channelId == null || channelId == call?.Id)
                    {
                        call?.SetSpeaking((string)data?["user_id"], evt == "SPEAKING_START");
                        OnCallChanged();
                    }

                    break;
            }
        }

        private void OnVoiceSettings(JObject data)
        {
            discordSelfMute = (bool?)data?["mute"] ?? discordSelfMute;
            discordSelfDeaf = (bool?)data?["deaf"] ?? discordSelfDeaf;
            OnCallChanged();
        }

        /// <summary>
        /// Shows <paramref name="next"/>, or no call for null: the old channel's events are no longer followed and the
        /// new one's are.
        /// </summary>
        private void ShowCall(DiscordCall next)
        {
            if (call != null && call.Id != next?.Id)
            {
                FollowCallEvents("UNSUBSCRIBE", call.Id);
            }

            if (next != null && call?.Id != next.Id)
            {
                FollowCallEvents("SUBSCRIBE", next.Id);
            }

            call = next;
            OnCallChanged();
        }

        private void FollowCallEvents(string cmd, string channelId)
        {
            foreach (var callEvent in CallEvents)
            {
                SendCommand(cmd, new JObject { ["channel_id"] = channelId }, callEvent);
            }
        }

        /// <summary>Discord has gone: no call, and its voice settings forgotten until it says them again.</summary>
        private void ForgetCall()
        {
            selectedChannelId = null;
            call = null;
            discordSelfMute = false;
            discordSelfDeaf = false;
            OnCallChanged();
        }

        /// <summary>
        /// The call changed: the status area follows it, and the Discord tab and the Audio flyout if showing, in place
        /// when only states changed, rebuilt keeping focus when entries came or went.
        /// </summary>
        private void OnCallChanged()
        {
            Status.IsInDiscordCall = call != null;
            Status.IsDiscordMuted = call != null && discordSelfMute;
            Status.IsDiscordDeafened = call != null && discordSelfDeaf;
            Status.IsDiscordSpeaking = call != null && call.IsUserSpeaking;
            if (!Guide.IsOpen)
            {
                return;
            }

            if (Guide.Flyout?.Kind == GuideItemKind.Audio)
            {
                var items = AudioItems();
                if (!UpdateInPlace(Guide.Flyout.Items, items))
                {
                    var focus = FocusAfterRebuild(Guide.Flyout.Items, items);
                    Guide.Flyout = new GuideFlyout(GuideItemKind.Audio, "Audio", items);
                    Guide.FocusedItem = focus;
                }
            }
            else if (Guide.Tab == GuideTabKind.Discord && Guide.Flyout == null)
            {
                var items = DiscordTabItems();
                if (UpdateInPlace(Guide.Items, items))
                {
                    return;
                }

                // Into a call or out of one, focus starts afresh; otherwise it stays on the same entry or person.
                var wasInCall = Guide.Items.Any(i => i.Kind == GuideItemKind.DiscordChannel);
                var focus = wasInCall == (call != null) ? FocusAfterRebuild(Guide.Items, items) : DiscordTabFocus(items);
                Guide.Items = items;
                Guide.FocusedItem = focus;
            }
        }

        /// <summary>
        /// The Discord tab: in a call, its heading, the people in it, the user first, then Mute, Deafen and Leave under a
        /// divider; out of one, who is signed in and that they're not in a voice channel.
        /// </summary>
        private IReadOnlyList<GuideItem> DiscordTabItems()
        {
            if (call == null)
            {
                return new[]
                {
                    new GuideItem(GuideItemKind.DiscordAccount, discordUser),
                    new GuideItem(GuideItemKind.DiscordIdle, "You're not in a voice channel"),
                };
            }

            return new[] { new GuideItem(GuideItemKind.DiscordChannel, call.Title) }
                .Concat(call.UserFirst.Select(p => new GuideItem(GuideItemKind.DiscordParticipant, p.Name, image: p.Avatar)
                {
                    UserId = p.UserId,
                    IsSpeaking = call.IsSpeaking(p.UserId),
                    IsMuted = p.IsMuted,
                    IsDeafened = p.IsDeafened,
                }))
                .Concat(new[]
                {
                    new GuideItem(GuideItemKind.DiscordMute, "Mute", hasDividerAbove: true) { IsOn = discordSelfMute },
                    new GuideItem(GuideItemKind.DiscordDeafen, "Deafen") { IsOn = discordSelfDeaf },
                    new GuideItem(GuideItemKind.DiscordLeave, "Leave"),
                })
                .ToList();
        }

        /// <summary>The Discord tab's first focus: Mute in a call, the first entry A does something on; the account card out of one.</summary>
        private static GuideItem DiscordTabFocus(IReadOnlyList<GuideItem> items) =>
            items.FirstOrDefault(i => i.Kind == GuideItemKind.DiscordMute) ?? items[0];

        /// <summary>The Audio flyout: Windows' volume and mute, then, in a call, the user's Discord mute and deafen.</summary>
        private IReadOnlyList<GuideItem> AudioItems()
        {
            var items = new List<GuideItem>
            {
                new GuideItem(GuideItemKind.Volume, "Volume") { Level = audio.Volume },
                new GuideItem(GuideItemKind.Mute, "Mute") { IsOn = audio.IsMuted },
            };
            if (call != null)
            {
                items.Add(new GuideItem(GuideItemKind.DiscordMute, "Mute in Discord") { IsOn = discordSelfMute });
                items.Add(new GuideItem(GuideItemKind.DiscordDeafen, "Deafen in Discord") { IsOn = discordSelfDeaf });
            }

            return items;
        }

        /// <summary>
        /// A on Discord's Mute, Deafen or Leave: asks Discord, whose events then show it done. False for any other entry.
        /// </summary>
        private bool SelectDiscordAction(GuideItemKind kind)
        {
            switch (kind)
            {
                case GuideItemKind.DiscordMute:
                    SendCommand("SET_VOICE_SETTINGS", new JObject { ["mute"] = !discordSelfMute });
                    return true;
                case GuideItemKind.DiscordDeafen:
                    SendCommand("SET_VOICE_SETTINGS", new JObject { ["deaf"] = !discordSelfDeaf });
                    return true;
                case GuideItemKind.DiscordLeave:
                    SendCommand("SELECT_VOICE_CHANNEL", new JObject { ["channel_id"] = null });
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether <paramref name="wanted"/>, built afresh, holds the same entries as <paramref name="shown"/>; if so, the
        /// shown ones take its states in place, so that views animate rather than rebuild.
        /// </summary>
        private static bool UpdateInPlace(IReadOnlyList<GuideItem> shown, IReadOnlyList<GuideItem> wanted)
        {
            if (shown.Count != wanted.Count || shown.Where((item, i) => !item.IsSameEntry(wanted[i])).Any())
            {
                return false;
            }

            for (var i = 0; i < shown.Count; i++)
            {
                shown[i].TakeStateFrom(wanted[i]);
            }

            return true;
        }

        /// <summary>
        /// Where focus goes as <paramref name="shown"/> is replaced by <paramref name="wanted"/>: the same kind of entry,
        /// for the same person, else the entry now at the same place.
        /// </summary>
        private GuideItem FocusAfterRebuild(IReadOnlyList<GuideItem> shown, IReadOnlyList<GuideItem> wanted)
        {
            var focused = Guide.FocusedItem;
            if (wanted.Count == 0)
            {
                return null;
            }

            if (focused == null)
            {
                return wanted[0];
            }

            return wanted.FirstOrDefault(i => i.Kind == focused.Kind && i.UserId == focused.UserId)
                ?? wanted[Clamp(shown.ToList().IndexOf(focused), wanted.Count)];
        }
    }
}
