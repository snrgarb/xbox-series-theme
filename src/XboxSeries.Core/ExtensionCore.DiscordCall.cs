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

        /// <summary>
        /// Whether Discord has said which channel the user selected since signing in, which outdates the sign-in's
        /// GET_SELECTED_VOICE_CHANNEL answer should it come later.
        /// </summary>
        private bool channelSelectSeen;

        /// <summary>The user's own Discord mute, as Discord's voice settings last said, in a call or out of one.</summary>
        private bool discordSelfMute;

        /// <summary>The user's own Discord deafen, as Discord's voice settings last said, in a call or out of one.</summary>
        private bool discordSelfDeaf;

        /// <summary>Signed in: asks which channel the user is already in, and their voice settings.</summary>
        private void AskForCall()
        {
            SendCommand("GET_SELECTED_VOICE_CHANNEL", new JObject());
            SendCommand("GET_VOICE_SETTINGS", new JObject());
        }

        /// <summary>Discord's answer to a command sent once signed in, under <paramref name="nonce"/>.</summary>
        private void OnDiscordReply(string sent, string nonce, JObject data)
        {
            switch (sent)
            {
                case "GET_SELECTED_VOICE_CHANNEL":
                    // Answered after Discord has said the user moved, it's stale.
                    if (channelSelectSeen)
                    {
                        break;
                    }

                    selectedChannelId = (string)data?["id"];
                    ShowCall(DiscordCall.FromChannel(data, discordUserId));
                    break;
                case "GET_CHANNEL":
                    // Fetched to learn who is in a known DM, it may ring.
                    if (ringRequests.Remove(nonce))
                    {
                        OnRingChannel(data);
                        break;
                    }

                    // Fetched for a server's flyout, it only shows who is in the channel: the call comes from its own.
                    // Fetched for a channel the user has since left, it's stale.
                    if (!occupantRequests.Remove(nonce) && selectedChannelId != null && (string)data?["id"] == selectedChannelId)
                    {
                        ShowCall(DiscordCall.FromChannel(data, discordUserId));
                    }

                    ShowOccupants(data);

                    // Fetched as someone came into a channel followed, it tells whether it was the call they came into.
                    if (joinChecks.TryGetValue(nonce, out var joiner))
                    {
                        joinChecks.Remove(nonce);
                        OnJoinChecked(joiner);
                    }

                    break;
                case "GET_GUILD":
                    OnGuild(data);
                    break;
                case "GET_GUILDS":
                    OnGuilds(nonce, data);
                    break;
                case "GET_CHANNELS":
                    OnChannels(nonce, data);
                    break;
                case "GET_VOICE_SETTINGS":
                    OnVoiceSettings(data);
                    break;
            }
        }

        /// <summary>
        /// Discord refused a command sent once signed in. Refused the channel the user moved to, the call shown is no
        /// longer theirs, so it goes; the user is in a call Discord won't show. Refused who is in a server's voice
        /// channel or a known DM, it stays unknown; refused the servers or a server's channels, the flyout shows none.
        /// Refused the call's channel as someone came into a channel followed, the DMs they may have come into are asked.
        /// </summary>
        private void OnDiscordRefused(string sent, string nonce)
        {
            if (sent == "GET_CHANNEL" && ringRequests.Remove(nonce))
            {
                return;
            }

            string joiner = null;
            var refusedJoinCheck = sent == "GET_CHANNEL" && joinChecks.TryGetValue(nonce, out joiner);

            if (sent == "GET_CHANNEL" && !occupantRequests.Remove(nonce) && call != null && call.Id != selectedChannelId)
            {
                ShowCall(null);
            }
            else if (sent == "GET_GUILDS" || sent == "GET_CHANNELS")
            {
                OnFlyoutRefused(nonce);
            }

            if (refusedJoinCheck)
            {
                joinChecks.Remove(nonce);
                OnJoinChecked(joiner);
            }
        }

        /// <summary>An event Discord dispatched once signed in.</summary>
        private void OnDiscordEvent(string evt, JObject data)
        {
            switch (evt)
            {
                case "VOICE_CHANNEL_SELECT":
                    channelSelectSeen = true;
                    selectedChannelId = (string)data?["channel_id"];
                    if (selectedChannelId != null && selectedChannelId == ringingChannelId)
                    {
                        EndRing();
                    }

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
                    if (!AsksWhereTheyCame(data))
                    {
                        call?.Upsert(data);
                        OnCallChanged();
                    }

                    break;
                case "VOICE_STATE_UPDATE":
                    call?.Upsert(data);
                    OnCallChanged();
                    break;
                case "VOICE_STATE_DELETE":
                    var wasInCall = call?.Has((string)data?["user"]?["id"]) == true;
                    call?.Remove(data);
                    OnCallChanged();
                    OnLeftElsewhere(data, wasInCall);
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
        /// new one's are, a known DM's comings and goings staying followed for rings. The same channel fetched afresh
        /// keeps who was speaking.
        /// </summary>
        private void ShowCall(DiscordCall next)
        {
            if (call != null && call.Id != next?.Id)
            {
                RememberDmLeft(call);
                FollowCallEvents("UNSUBSCRIBE", call.Id);
            }

            if (next != null && call?.Id != next.Id)
            {
                LearnChannel(next);
                FollowCallEvents("SUBSCRIBE", next.Id);
            }
            else if (next != null && call != null)
            {
                next.KeepSpeakingFrom(call);
            }

            call = next;
            OnCallChanged();
        }

        /// <summary>
        /// SUBSCRIBEs or UNSUBSCRIBEs the call's events for <paramref name="channelId"/>; a known DM's comings and goings
        /// are left alone, followed for rings already.
        /// </summary>
        private void FollowCallEvents(string cmd, string channelId)
        {
            foreach (var callEvent in CallEvents.Where(e => !ringFollowed.Contains(channelId) || !RingEvents.Contains(e)))
            {
                SendCommand(cmd, new JObject { ["channel_id"] = channelId }, callEvent);
            }
        }

        /// <summary>Discord has gone: no call, nothing ringing, and its voice settings forgotten until it says them again.</summary>
        private void ForgetCall()
        {
            ForgetRings();
            selectedChannelId = null;
            channelSelectSeen = false;
            call = null;
            discordSelfMute = false;
            discordSelfDeaf = false;
            OnCallChanged();
        }

        /// <summary>
        /// The call or the shortcuts changed: the call's shortcut takes its name, the status area follows the call, and
        /// the Discord tab and the Audio flyout if showing, in place when only states changed, rebuilt keeping focus when
        /// entries came or went. Behind a Servers flyout the tab is rebuilt, focus staying in the flyout.
        /// </summary>
        private void OnCallChanged()
        {
            RememberCall();
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
                RefreshAudioFlyout();
            }
            else if (Guide.Tab == GuideTabKind.Discord)
            {
                var items = DiscordTabItems();
                if (UpdateInPlace(Guide.Items, items))
                {
                    return;
                }

                if (Guide.Flyout != null)
                {
                    Guide.Items = items;
                    return;
                }

                // Into a call or out of one, or Answer gone from under focus, focus starts afresh; otherwise it stays on
                // the same entry or person.
                var wasInCall = Guide.Items.Any(i => i.Kind == GuideItemKind.DiscordChannel);
                var answerGone = Guide.FocusedItem?.Kind == GuideItemKind.DiscordIncomingCall
                    && items.All(i => i.Kind != GuideItemKind.DiscordIncomingCall);
                var focus = wasInCall == (call != null) && !answerGone ? FocusAfterRebuild(Guide.Items, items) : DiscordTabFocus(items);
                Guide.Items = items;
                Guide.FocusedItem = focus;
            }
        }

        /// <summary>
        /// The Discord tab: Answer first while a known DM rings; in a call, its heading, the people in it, the user first, then Mute, Deafen and Leave under a
        /// divider; out of one, who is signed in and that they're not in a voice channel. Then, either way, the shortcuts
        /// and Servers, each under a divider. Not signed in, only a line saying why: Connecting to Discord, while a connect
        /// is under way; Discord isn't running, while it is waited for; otherwise, nothing trying Discord again, the user
        /// has to reconnect in Settings.
        /// </summary>
        private IReadOnlyList<GuideItem> DiscordTabItems()
        {
            if (!Settings.Discord.IsConnected)
            {
                var why = Settings.Discord.NeedsReconnect ? ReconnectInSettings
                    : Settings.Discord.IsConnecting ? ConnectingToDiscord
                    : probeTimer != null ? DiscordNotRunning
                    : ReconnectInSettings;
                return new[] { new GuideItem(GuideItemKind.DiscordUnavailable, why) };
            }

            return RingItems()
                .Concat(CallItems())
                .Concat(ShortcutItems())
                .Concat(new[] { new GuideItem(GuideItemKind.DiscordServers, "Servers", hasDividerAbove: true) })
                .ToList();
        }

        private IEnumerable<GuideItem> CallItems()
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
                });
        }

        /// <summary>
        /// The Discord tab's first focus, the first entry A does something on: Answer while a known DM rings; else Mute
        /// in a call; out of one, the first shortcut, else Servers. Not signed in, the line saying why, its only entry,
        /// on which A does nothing.
        /// </summary>
        private static GuideItem DiscordTabFocus(IReadOnlyList<GuideItem> items) =>
            items.FirstOrDefault(i => i.Kind == GuideItemKind.DiscordIncomingCall)
            ?? items.FirstOrDefault(i => i.Kind == GuideItemKind.DiscordMute)
            ?? items.FirstOrDefault(i => i.Kind == GuideItemKind.DiscordShortcut)
            ?? items.FirstOrDefault(i => i.Kind == GuideItemKind.DiscordServers)
            ?? items[0];

        /// <summary>
        /// The Audio flyout: the Output and Input dropdowns, Windows' volume and mute, then, in a call, the user's Discord
        /// mute and deafen, and last, under the mute switches after a divider, the Now Playing entry.
        /// </summary>
        private IReadOnlyList<GuideItem> AudioItems()
        {
            var items = AudioDropdowns().ToList();
            items.Add(new GuideItem(GuideItemKind.Volume, "Volume") { Level = audio.Volume });
            items.Add(new GuideItem(GuideItemKind.Mute, "Mute") { IsOn = audio.IsMuted });
            if (call != null)
            {
                items.Add(new GuideItem(GuideItemKind.DiscordMute, "Mute in Discord") { IsOn = discordSelfMute });
                items.Add(new GuideItem(GuideItemKind.DiscordDeafen, "Deafen in Discord") { IsOn = discordSelfDeaf });
            }

            items.Add(NowPlayingItem());
            return items;
        }

        /// <summary>
        /// A on Discord's Answer, Mute, Deafen or Leave, a shortcut or a server's voice channel: asks Discord, whose events
        /// then show it done. On Servers or a server, opens its flyout. False for any other entry.
        /// </summary>
        private bool SelectDiscordAction(GuideItem item)
        {
            switch (item.Kind)
            {
                case GuideItemKind.DiscordIncomingCall:
                    Answer(item.ChannelId);
                    return true;
                case GuideItemKind.DiscordShortcut:
                    JoinChannel(item.ChannelId);
                    return true;
                case GuideItemKind.DiscordServers:
                    StopRepeat();
                    OpenServers();
                    return true;
                case GuideItemKind.DiscordServer:
                    OpenServer(item);
                    return true;
                case GuideItemKind.DiscordVoiceChannel:
                    SelectVoiceChannel(item);
                    return true;
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
        /// for the same person, channel or server, else the entry now at the same place.
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

            return wanted.FirstOrDefault(i => i.Kind == focused.Kind && i.UserId == focused.UserId && i.ChannelId == focused.ChannelId
                    && i.GuildId == focused.GuildId)
                ?? wanted[Clamp(shown.ToList().IndexOf(focused), wanted.Count)];
        }
    }
}
