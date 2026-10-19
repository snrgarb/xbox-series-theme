using System;
using System.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Now Playing card's progress bar: how far into its track the session is, moved on once a second from the app's
    /// last word while it plays and the Guide shows the Audio flyout, and set afresh by every change the app tells of.
    /// Positions are compared in whole seconds, as the card shows them.
    /// </summary>
    public class NowPlayingProgressTests : DiscordTestsBase
    {
        private const string Spotify = "Spotify.exe";

        [Fact]
        public void A_playing_card_shows_the_length_and_the_place_moved_on_from_when_the_app_last_told_it()
        {
            media.Current = Playing(position: Seconds(60), told: Seconds(5));

            OpenAudio();

            Assert.Equal(Seconds(180), Card.Duration);
            Assert.Equal(65, Shown());
        }

        [Fact]
        public void While_it_plays_the_place_moves_on_once_a_second()
        {
            media.Current = Playing(position: Seconds(60));
            OpenAudio();
            var start = Shown();

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(start + 1, Shown());

            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal(start + 4, Shown());
        }

        [Fact]
        public void A_paused_card_shows_the_app_s_place_however_long_ago_it_was_told_and_stays_there()
        {
            media.Current = Paused(position: Seconds(42), told: Seconds(30));
            OpenAudio();

            Assert.Equal(42, Shown());

            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(42, Shown());
        }

        [Fact]
        public void Pausing_stops_the_place_where_the_app_paused_and_resuming_carries_on_from_the_app_s_place()
        {
            media.Current = Playing(position: Seconds(60));
            OpenAudio();
            clock.Advance(TimeSpan.FromSeconds(3));

            media.Current = Paused(position: Seconds(63));
            MediaChanged();
            Assert.Equal(63, Shown());
            Assert.False(Card.IsPlaying);

            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal(63, Shown());

            media.Current = Playing(position: Seconds(63));
            MediaChanged();
            var resumed = Shown();
            Assert.Equal(63, resumed);

            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(resumed + 2, Shown());
        }

        [Fact]
        public void Scrubbing_in_the_app_moves_the_place_to_the_app_s_new_one_and_it_carries_on_from_there()
        {
            media.Current = Playing(position: Seconds(60));
            OpenAudio();
            var card = Card;
            clock.Advance(TimeSpan.FromSeconds(2));

            media.Current = Playing(position: Seconds(150));
            MediaChanged();

            Assert.Same(card, Card);
            Assert.Equal(150, Shown());

            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(152, Shown());
        }

        [Fact]
        public void A_new_track_starts_the_place_at_its_beginning_with_its_own_length()
        {
            media.Current = Playing(position: Seconds(170));
            OpenAudio();

            media.Current = new MediaSession(
                Spotify, "Save Your Tears", "The Weeknd", isPlaying: true,
                position: TimeSpan.Zero, duration: Seconds(215), lastUpdated: clock.Now);
            MediaChanged();

            Assert.Equal("Save Your Tears", Card.Title);
            Assert.Equal(Seconds(215), Card.Duration);
            Assert.Equal(0, Shown());
        }

        [Fact]
        public void The_place_stops_at_the_track_s_end_however_long_the_app_says_nothing()
        {
            media.Current = Playing(position: Seconds(178));
            OpenAudio();

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal(Seconds(180), Card.Position);
        }

        [Fact]
        public void A_session_with_no_length_has_no_bar_and_nothing_moves()
        {
            media.Current = new MediaSession(
                Spotify, "Live radio", null, isPlaying: true,
                position: Seconds(30), duration: TimeSpan.Zero, lastUpdated: clock.Now);
            OpenAudio();
            var position = Card.Position;

            Assert.Equal(TimeSpan.Zero, Card.Duration);

            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(position, Card.Position);
        }

        [Fact]
        public void Nothing_moves_with_the_Audio_flyout_shut_or_the_Guide_closed()
        {
            media.Current = Playing(position: Seconds(60));
            OpenAudio();
            var card = Card;

            Tap(ControllerButton.B);
            Assert.Null(Guide.Flyout);
            var shut = card.Position;
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(shut, card.Position);

            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(2));
            OpenAudio();
            card = Card;
            Tap(ControllerButton.Guide);
            Assert.False(Guide.IsOpen);
            var closed = card.Position;
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(closed, card.Position);
        }

        [Fact]
        public void Closing_the_Guide_leaves_no_tick_waiting_on_the_clock()
        {
            media.Current = Paused(position: Seconds(60));
            OpenAudio();
            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(5));
            var withoutTicking = clock.Pending;

            media.Current = Playing(position: Seconds(60));
            OpenAudio();
            clock.Advance(TimeSpan.FromSeconds(2));
            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(withoutTicking, clock.Pending);
        }

        [Fact]
        public void Opening_the_Guide_again_shows_the_place_moved_on_by_the_time_it_was_closed()
        {
            var told = clock.Now;
            media.Current = Playing(position: Seconds(60));
            OpenAudio();
            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(20));

            OpenAudio();

            Assert.Equal(60 + (int)(clock.Now - told).TotalSeconds, Shown());
            var reopened = Shown();
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(reopened + 1, Shown());
        }

        private GuideItem Card => Guide.Flyout.Items.Last();

        private static TimeSpan Seconds(int seconds) => TimeSpan.FromSeconds(seconds);

        /// <summary>The card's place in whole seconds, as its elapsed time shows it.</summary>
        private int Shown() => (int)Card.Position.TotalSeconds;

        /// <summary>A three-minute track playing, at <paramref name="position"/> when the app told it <paramref name="told"/> ago.</summary>
        private MediaSession Playing(TimeSpan position, TimeSpan told = default) => new MediaSession(
            Spotify, "Blinding Lights", "The Weeknd", isPlaying: true,
            position: position, duration: Seconds(180), lastUpdated: clock.Now - told);

        /// <summary>The same track paused at <paramref name="position"/>, told <paramref name="told"/> ago.</summary>
        private MediaSession Paused(TimeSpan position, TimeSpan told = default) => new MediaSession(
            Spotify, "Blinding Lights", "The Weeknd", isPlaying: false,
            position: position, duration: Seconds(180), lastUpdated: clock.Now - told);

        private void OpenAudio()
        {
            OpenGuide();
            foreach (var _ in Guide.Items)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            if (Guide.Flyout?.Kind != GuideItemKind.Audio)
            {
                throw new InvalidOperationException("The Audio flyout didn't open.");
            }
        }
    }
}
