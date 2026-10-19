using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Now Playing card's progress bar (#76): how far into its track the session is, and the track's length. An app
    /// tells its place only now and then, true when it tells it, so while the track plays the core moves the card on
    /// from there itself, once a second as the elapsed time's seconds turn, and only while the Guide shows the Audio
    /// flyout; it never asks the app. Every change the app tells of, a new place, a pause or a new track, sets the card
    /// afresh from what the app says.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The session the card's place was last set from, which the ticks move on from.</summary>
        private MediaSession timelineSession;

        /// <summary>The next tick moving the card on; null with none waiting.</summary>
        private IDisposable timelineTimer;

        /// <summary>
        /// Puts <paramref name="session"/>'s place in its track and its length on <paramref name="card"/>, and, while it
        /// plays a track with a length, waits for the elapsed time's next second to move the shown card on, any tick
        /// waiting from an earlier session cancelled first.
        /// </summary>
        private void ShowTimeline(GuideItem card, MediaSession session)
        {
            timelineTimer?.Dispose();
            timelineTimer = null;
            timelineSession = session;
            card.Duration = session.Duration;
            card.Position = TimelinePosition(session);
            WaitForTimelineTick(card.Position);
        }

        /// <summary>
        /// Moves the shown card on to where its track should be by now, and waits for the next second, while the Guide
        /// still shows the Audio flyout with the card; otherwise it stops, waiting for nothing, until the flyout shows the
        /// card again.
        /// </summary>
        private void TickTimeline()
        {
            timelineTimer = null;
            var shown = ShownNowPlayingCard();
            if (shown == null)
            {
                return;
            }

            shown.Position = TimelinePosition(timelineSession);
            WaitForTimelineTick(shown.Position);
        }

        /// <summary>
        /// Waits for the second after <paramref name="position"/>, as the elapsed time turns, while
        /// <see cref="timelineSession"/> plays a track with a length and hasn't reached its end.
        /// </summary>
        private void WaitForTimelineTick(TimeSpan position)
        {
            var session = timelineSession;
            if (!session.IsPlaying || session.Duration <= TimeSpan.Zero || position >= session.Duration)
            {
                return;
            }

            var intoSecond = position.Ticks % TimeSpan.TicksPerSecond;
            timelineTimer = clock.After(TimeSpan.FromTicks(TimeSpan.TicksPerSecond - intoSecond), TickTimeline);
        }

        /// <summary>
        /// Where <paramref name="session"/> should be in its track now: while it plays, its place when the app told it
        /// moved on by the time since, held within the track; paused, or told at no time, just the place it told.
        /// </summary>
        private TimeSpan TimelinePosition(MediaSession session)
        {
            var position = session.Position;
            if (session.IsPlaying && session.LastUpdated != default)
            {
                var since = clock.Now - session.LastUpdated;
                if (since > TimeSpan.Zero)
                {
                    position += since;
                }
            }

            if (session.Duration > TimeSpan.Zero && position > session.Duration)
            {
                position = session.Duration;
            }

            return position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }
    }
}
