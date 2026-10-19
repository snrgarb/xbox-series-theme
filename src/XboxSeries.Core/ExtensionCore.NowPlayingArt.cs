using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// Changing track never makes the Now Playing card flicker (#74). An app fires several changes in under 100 ms on a
    /// track change, and often tells of the title before the art: the changes are let settle and only the last shown,
    /// and the art is the core's to keep, asked for with <see cref="IMediaSessions.FetchArt"/>. The last track's art
    /// stays up until the new track's has loaded, then swaps once; an app that never gives the new track art has the
    /// old let go after <see cref="ArtPatience"/>. Only the latest fetch's answer counts, an older one being dropped as
    /// the Servers flyout drops an older reply.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>How long the media session's changes have to stop for before the last of them is shown.</summary>
        private static readonly TimeSpan MediaSettle = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// How long the last track's art stays up once the new track's fetch has found none, for the art the app tells
        /// of after the title: a later change for the track fetches it again. After that the card shows none.
        /// </summary>
        private static readonly TimeSpan ArtPatience = TimeSpan.FromSeconds(1);

        /// <summary>The wait for the media session's changes to settle; restarted by each change, null when none waits.</summary>
        private IDisposable mediaSettleTimer;

        /// <summary>The art last loaded, the track it is for (<see cref="TrackOf"/>); null art for none yet, or none at all.</summary>
        private object nowPlayingArt;
        private string nowPlayingArtTrack;

        /// <summary>The latest art fetch, by its request number, and the track it is for; an answer to any other is dropped.</summary>
        private int artRequest;
        private string artRequestTrack;

        /// <summary>The end of the last track's art staying up for a new track that has found none so far.</summary>
        private IDisposable artPatienceTimer;

        /// <summary>Runs <paramref name="show"/> once the media session has gone <see cref="MediaSettle"/> without changing again.</summary>
        private void AfterMediaSettles(Action show)
        {
            mediaSettleTimer?.Dispose();
            mediaSettleTimer = clock.After(MediaSettle, () =>
            {
                mediaSettleTimer = null;
                show();
            });
        }

        /// <summary>
        /// The art <paramref name="card"/> shows for <paramref name="session"/>: its track's, once loaded. Until then
        /// it is fetched, again on each change until some comes, and the card keeps the last track's art if that is on
        /// the card showing now, so a track change swaps it once; a flyout opening on a new track shows none until it loads.
        /// </summary>
        private void ShowArt(GuideItem card, MediaSession session)
        {
            var track = TrackOf(session);
            if (track == nowPlayingArtTrack && nowPlayingArt != null)
            {
                // Loaded already: a fetch still out, for a track since left, is dropped as it answers.
                ForgetArtPatience();
                artRequest++;
                artRequestTrack = null;
                card.Art = nowPlayingArt;
                return;
            }

            if (track != artRequestTrack)
            {
                ForgetArtPatience();
            }

            var keep = track == nowPlayingArtTrack || IsArtShowing;
            artRequest++;
            artRequestTrack = track;
            mediaSessions.FetchArt(session.AppId, artRequest);
            card.Art = keep ? nowPlayingArt : null;
        }

        /// <summary>
        /// The art asked for by <see cref="IMediaSessions.FetchArt"/> as <paramref name="request"/>; null for none. Called
        /// on the UI thread. The latest fetch's art is shown now; none found keeps the last track's art up for
        /// <see cref="ArtPatience"/>, in case the app tells of it in a later change. Any other fetch's answer is dropped.
        /// </summary>
        public void OnMediaArt(int request, object art)
        {
            if (request != artRequest || artRequestTrack == null)
            {
                return;
            }

            var track = artRequestTrack;
            if (art != null)
            {
                ForgetArtPatience();
                ShowLoadedArt(art, track);
                return;
            }

            if (track != nowPlayingArtTrack && nowPlayingArt != null && artPatienceTimer == null)
            {
                artPatienceTimer = clock.After(ArtPatience, () =>
                {
                    artPatienceTimer = null;
                    ShowLoadedArt(null, track);
                });
            }
            else if (nowPlayingArt == null)
            {
                nowPlayingArtTrack = track;
            }
        }

        /// <summary>
        /// Whether the open Audio flyout's Now Playing card shows the art last loaded. Always the latest one built: a
        /// card built since asks for its art afresh.
        /// </summary>
        private bool IsArtShowing => nowPlayingArt != null && ShownNowPlayingCard()?.Art == nowPlayingArt;

        /// <summary>The open Audio flyout's Now Playing card; null with the Guide closed, another flyout or no music app.</summary>
        private GuideItem ShownNowPlayingCard()
        {
            if (!Guide.IsOpen || Guide.Flyout?.Kind != GuideItemKind.Audio)
            {
                return null;
            }

            var last = Guide.Flyout.Items[Guide.Flyout.Items.Count - 1];
            return last.Kind == GuideItemKind.NowPlaying ? last : null;
        }

        /// <summary><paramref name="art"/> as <paramref name="track"/>'s, on the card showing it if the flyout is open.</summary>
        private void ShowLoadedArt(object art, string track)
        {
            nowPlayingArt = art;
            nowPlayingArtTrack = track;
            var card = ShownNowPlayingCard();
            if (card != null)
            {
                card.Art = art;
            }
        }

        /// <summary>Forgets the wait to let the last track's art go, as another track's art is asked for or loads.</summary>
        private void ForgetArtPatience()
        {
            artPatienceTimer?.Dispose();
            artPatienceTimer = null;
        }

        /// <summary>The track <paramref name="session"/> plays, as far as its art goes: the app, the title and the artist.</summary>
        private static string TrackOf(MediaSession session) => session.AppId + "\n" + session.Title + "\n" + session.Artist;
    }
}
