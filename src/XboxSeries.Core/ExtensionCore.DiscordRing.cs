using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Incoming calls: a known DM someone else joins while the user isn't in it rings, with a Toast and the Discord tab's
    /// Answer, which joins it, asking first in a call. Only known DMs ring: who comes and goes in each is followed, and
    /// Discord's answers to GET_CHANNEL tell which one someone joined, as its voice events don't say.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The events of a known DM followed for rings, one SUBSCRIBE each with its channel_id.</summary>
        private static readonly string[] RingEvents = { "VOICE_STATE_CREATE", "VOICE_STATE_DELETE" };

        /// <summary>How long the call's Toast shows at most; the ring itself goes on, on the Discord tab, until it ends.</summary>
        private static readonly TimeSpan RingToastDuration = TimeSpan.FromSeconds(30);

        /// <summary>The known DMs followed for rings on this connection.</summary>
        private readonly HashSet<string> ringFollowed = new HashSet<string>();

        /// <summary>The GET_CHANNELs sent to learn who is in the known DMs, by nonce.</summary>
        private readonly HashSet<string> ringRequests = new HashSet<string>();

        /// <summary>
        /// The GET_CHANNELs of the call's channel sent when someone not in the call came into a channel followed, by
        /// nonce, with who came: its answer tells whether it was the call they came into, before any DM is asked.
        /// </summary>
        private readonly Dictionary<string, string> joinChecks = new Dictionary<string, string>();

        /// <summary>
        /// Whether someone other than the user was in each known DM, as last learned; a DM not yet learned doesn't ring
        /// on its first answer, as someone sitting in a DM call for hours isn't calling.
        /// </summary>
        private readonly Dictionary<string, bool> dmOccupied = new Dictionary<string, bool>();

        /// <summary>The DM ringing, or null; one at a time, a newer ring replacing it.</summary>
        private string ringingChannelId;

        /// <summary>Who is calling: the DM's name, else the caller's, else "Someone".</summary>
        private string ringCaller;

        /// <summary>The ringing DM's picture, else the caller's avatar.</summary>
        private string ringImage;

        /// <summary>Hides the call's Toast once it has shown long enough; null while it isn't showing.</summary>
        private IDisposable ringToastTimer;

        /// <summary>"Leave X and answer?", while it may be showing, so that the ring ending takes it away.</summary>
        private Dialog answerPrompt;

        /// <summary>Signed in: each known DM is followed for rings, and Discord asked who is in it already.</summary>
        private void WatchForRings()
        {
            var dms = knownChannels.Where(c => c.GuildId == null).Select(c => c.Id).ToList();
            foreach (var id in dms)
            {
                FollowForRings(id);
            }

            AskWhoIsIn(dms);
        }

        /// <summary>A DM become known: who comes and goes in it is followed, unless it is already.</summary>
        private void FollowForRings(string channelId)
        {
            if (!ringFollowed.Add(channelId))
            {
                return;
            }

            foreach (var ringEvent in RingEvents)
            {
                SendCommand("SUBSCRIBE", new JObject { ["channel_id"] = channelId }, ringEvent);
            }
        }

        /// <summary>
        /// A DM forgotten: no longer followed for rings, unless the call is in it, which still follows its people, and
        /// a ring in it ends.
        /// </summary>
        private void UnfollowForRings(string channelId)
        {
            dmOccupied.Remove(channelId);
            if (!ringFollowed.Remove(channelId))
            {
                return;
            }

            if (channelId != call?.Id)
            {
                foreach (var ringEvent in RingEvents)
                {
                    SendCommand("UNSUBSCRIBE", new JObject { ["channel_id"] = channelId }, ringEvent);
                }
            }

            if (channelId == ringingChannelId)
            {
                EndRing();
            }
        }

        /// <summary>Discord is asked who is in each of <paramref name="channelIds"/>, known DMs, its answers telling which ring.</summary>
        private void AskWhoIsIn(IEnumerable<string> channelIds)
        {
            foreach (var id in channelIds)
            {
                ringRequests.Add(SendCommand("GET_CHANNEL", new JObject { ["channel_id"] = id }));
            }
        }

        /// <summary>
        /// Someone came into a channel followed, VOICE_STATE_CREATE, which doesn't say which. Followed for rings as well
        /// as the call, it may be a DM ringing. In a call, Discord is asked first who is in the call's channel, which
        /// then shows them if they came there, and only if they didn't, who is in the DMs; out of one, at once. Only the
        /// known DMs nobody else was in are asked, as only those can start ringing. False when it can only be the call's,
        /// which takes them at once.
        /// </summary>
        private bool AsksWhereTheyCame(JObject state)
        {
            var userId = (string)state?["user"]?["id"];
            if (userId == null || userId == discordUserId || call?.Has(userId) == true || !ringFollowed.Any(id => id != call?.Id))
            {
                return false;
            }

            if (call != null)
            {
                joinChecks[SendCommand("GET_CHANNEL", new JObject { ["channel_id"] = call.Id })] = userId;
            }
            else
            {
                AskEmptyDms();
            }

            return true;
        }

        /// <summary>
        /// The call's channel answered, or refused, after <paramref name="userId"/> came into a channel followed: unless
        /// the call now has them, they came into a DM, which Discord is asked about.
        /// </summary>
        private void OnJoinChecked(string userId)
        {
            if (call?.Has(userId) != true)
            {
                AskEmptyDms();
            }
        }

        /// <summary>Discord is asked who is in each known DM nobody else was in, besides the call's, the only ones that can start ringing.</summary>
        private void AskEmptyDms() =>
            AskWhoIsIn(ringFollowed.Where(id => id != call?.Id && dmOccupied.TryGetValue(id, out var occupied) && !occupied).ToList());

        /// <summary>
        /// Someone not in the call left a channel followed, VOICE_STATE_DELETE: Discord is asked who is in the known DMs
        /// someone was in, the ringing one among them, to learn which they left.
        /// </summary>
        private void OnLeftElsewhere(JObject state, bool wasInCall)
        {
            var userId = (string)state?["user"]?["id"];
            if (userId == null || userId == discordUserId || wasInCall)
            {
                return;
            }

            AskWhoIsIn(ringFollowed.Where(id => id != call?.Id && dmOccupied.TryGetValue(id, out var occupied) && occupied).ToList());
        }

        /// <summary>
        /// Who is in a known DM, as Discord answered: someone else come into one nobody else was in rings, unless the
        /// user is in it or on the way; the ringing one left empty stops ringing.
        /// </summary>
        private void OnRingChannel(JObject channel)
        {
            var id = (string)channel?["id"];
            if (id == null || !ringFollowed.Contains(id))
            {
                return;
            }

            var caller = (channel["voice_states"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(DiscordParticipant.FromVoiceState)
                .FirstOrDefault(p => p != null && p.UserId != discordUserId);
            var wasOccupied = dmOccupied.TryGetValue(id, out var occupied) ? occupied : (bool?)null;
            dmOccupied[id] = caller != null;
            if (caller == null)
            {
                if (id == ringingChannelId)
                {
                    EndRing();
                }
            }
            else if (wasOccupied == false && id != selectedChannelId)
            {
                StartRing(id, caller);
            }
        }

        /// <summary>The call left a channel: a known DM it was in keeps whether anyone else is still there, who then isn't calling.</summary>
        private void RememberDmLeft(DiscordCall left)
        {
            if (ringFollowed.Contains(left.Id))
            {
                dmOccupied[left.Id] = left.HasOthers;
            }
        }

        /// <summary>A known DM rings: the call's Toast shows, and the Discord tab gets Answer at its top.</summary>
        private void StartRing(string channelId, DiscordParticipant caller)
        {
            var known = knownChannels.FirstOrDefault(c => c.Id == channelId);
            ringingChannelId = channelId;
            ringCaller = known?.Name ?? (string.IsNullOrEmpty(caller.Name) ? "Someone" : caller.Name);
            ringImage = known?.Image ?? caller.Avatar;
            CloseAnswerPrompt();
            ShowRingToast(ringCaller, ringImage);
            OnCallChanged();
        }

        /// <summary>The ring ends: the caller hung up, the user joined, or the DM was forgotten.</summary>
        private void EndRing()
        {
            if (ringingChannelId != null)
            {
                StopRinging();
                OnCallChanged();
            }
        }

        /// <summary>Nothing rings any longer: the call's Toast and "Leave X and answer?" go, without rebuilding the Discord tab.</summary>
        private void StopRinging()
        {
            ringingChannelId = null;
            ringCaller = null;
            ringImage = null;
            HideRingToast();
            CloseAnswerPrompt();
        }

        /// <summary>Discord has gone: nothing rings, and the DMs are followed afresh once signed in again.</summary>
        private void ForgetRings()
        {
            StopRinging();
            ringFollowed.Clear();
            ringRequests.Clear();
            joinChecks.Clear();
            dmOccupied.Clear();
        }

        /// <summary>
        /// "<paramref name="caller"/> is calling", with <paramref name="image"/> on its tile, for up to
        /// <see cref="RingToastDuration"/>, even over the Guide, with the notification sound. It cuts in before a notification's Toast, which waits
        /// to show again after it with those behind it. During Start-up and the greeting it waits, and shows as the greeting goes.
        /// </summary>
        private void ShowRingToast(string caller, string image)
        {
            if (IsStartupShown || greetingTimer != null)
            {
                waitingRingToast = () => ShowRingToast(caller, image);
                return;
            }

            PutBackShownToast();

            ringToastTimer?.Dispose();
            Toast.Show(caller + " is calling", ToastKind.Call, image);
            sounds.Play(UiSound.Notification);
            ringToastTimer = clock.After(RingToastDuration, HideRingToast);
        }

        /// <summary>The call's Toast goes, or no longer waits to show, and any notifications waiting behind it follow.</summary>
        private void HideRingToast()
        {
            waitingRingToast = null;
            if (ringToastTimer == null)
            {
                return;
            }

            ringToastTimer.Dispose();
            ringToastTimer = null;
            Toast.IsShown = false;
            if (waitingToasts.Count > 0)
            {
                toastTimer = clock.After(ToastGap, ShowNextToast);
            }
        }

        /// <summary>The Discord tab's Answer while a DM rings, the first entry; none otherwise.</summary>
        private IEnumerable<GuideItem> RingItems()
        {
            if (ringingChannelId == null)
            {
                return new GuideItem[0];
            }

            return new[]
            {
                new GuideItem(GuideItemKind.DiscordIncomingCall, "Answer", image: ringImage, detail: ringCaller + " is calling")
                {
                    ChannelId = ringingChannelId,
                },
            };
        }

        /// <summary>
        /// A on Answer: joins the DM ringing; in a call in another channel, asks first "Leave X and answer?", Discord
        /// moving the user over on Leave and answer.
        /// </summary>
        private void Answer(string channelId)
        {
            if (call == null || call.Id == channelId)
            {
                JoinChannel(channelId);
                return;
            }

            answerPrompt = new Dialog(
                $"Leave {call.Title} and answer?",
                null,
                isPrompt: true,
                CloseDialog,
                new DialogOption("Leave and answer", DialogIcon.Play, () =>
                {
                    CloseDialog();
                    JoinChannel(channelId);
                }),
                new DialogOption("Cancel", DialogIcon.Cancel, CloseDialog));
            ShowDialog(answerPrompt);
        }

        /// <summary>"Leave X and answer?" goes, if it is still showing, leaving the Guide open.</summary>
        private void CloseAnswerPrompt()
        {
            if (answerPrompt != null && Guide.Dialog == answerPrompt)
            {
                CloseDialog();
            }

            answerPrompt = null;
        }
    }
}
