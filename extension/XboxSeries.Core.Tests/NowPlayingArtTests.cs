using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Changing track never makes the Now Playing card flicker: a burst of changes is let settle and only the last shown,
    /// the last track's art stays up until the new track's loads, and an art fetch overtaken by a newer one is dropped.
    /// </summary>
    public class NowPlayingArtTests : DiscordTestsBase
    {
        private const string Spotify = "Spotify.exe";

        private readonly object blindingLightsArt = new object();
        private readonly object saveYourTearsArt = new object();

        [Fact]
        public void Opening_the_flyout_reads_the_session_at_once_and_fetches_its_art()
        {
            media.Current = Track("Blinding Lights");

            OpenAudio();

            Assert.Equal("Blinding Lights", Card.Title);
            Assert.Null(Card.Art);
            Assert.Equal(Spotify, media.ArtFetches.Single().AppId);

            core.OnMediaArt(media.ArtFetches.Single().Request, blindingLightsArt);

            Assert.Same(blindingLightsArt, Card.Art);
        }

        [Fact]
        public void A_burst_of_changes_shows_only_the_last_once_they_settle_and_fetches_its_art_once()
        {
            ShowingBlindingLights();
            var card = Card;
            var titles = new List<string>();
            card.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GuideItem.Title))
                {
                    titles.Add(card.Title);
                }
            };
            var fetches = media.ArtFetches.Count;

            foreach (var title in new[] { "Save Your Tears", "In Your Eyes", "Starboy", "Die For You", "After Hours" })
            {
                media.Current = Track(title);
                core.OnMediaSessionChanged();
                clock.Advance(TimeSpan.FromMilliseconds(60));
            }

            Assert.Equal("Blinding Lights", card.Title);
            Assert.Empty(titles);

            clock.Advance(TimeSpan.FromMilliseconds(200));

            Assert.Equal(new[] { "After Hours" }, titles);
            Assert.Same(card, Card);
            Assert.Equal(fetches + 1, media.ArtFetches.Count);
        }

        [Fact]
        public void A_change_shows_once_it_has_settled_for_200_ms()
        {
            ShowingBlindingLights();

            media.Current = Track("Save Your Tears");
            core.OnMediaSessionChanged();
            clock.Advance(TimeSpan.FromMilliseconds(199));

            Assert.Equal("Blinding Lights", Card.Title);

            clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal("Save Your Tears", Card.Title);
        }

        [Fact]
        public void On_a_track_change_the_old_art_stays_until_the_new_art_arrives_then_swaps_once()
        {
            ShowingBlindingLights();
            var card = Card;
            var arts = new List<object>();
            card.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GuideItem.Art))
                {
                    arts.Add(card.Art);
                }
            };

            media.Current = Track("Save Your Tears");
            MediaChanged();

            Assert.Equal("Save Your Tears", card.Title);
            Assert.Same(blindingLightsArt, card.Art);

            core.OnMediaArt(media.ArtFetches.Last().Request, saveYourTearsArt);

            Assert.Same(saveYourTearsArt, card.Art);
            Assert.Equal(new[] { saveYourTearsArt }, arts);
        }

        [Fact]
        public void Art_arriving_after_the_title_replaces_the_old_art_in_a_later_change()
        {
            ShowingBlindingLights();

            // The title first, with no art yet: the old art stays up.
            media.Current = Track("Save Your Tears");
            MediaChanged();
            core.OnMediaArt(media.ArtFetches.Last().Request, null);

            Assert.Same(blindingLightsArt, Card.Art);

            // Then the art, in a change of its own: fetched again, and it replaces the old.
            var fetches = media.ArtFetches.Count;
            MediaChanged();

            Assert.Equal(fetches + 1, media.ArtFetches.Count);
            core.OnMediaArt(media.ArtFetches.Last().Request, saveYourTearsArt);

            Assert.Same(saveYourTearsArt, Card.Art);
        }

        [Fact]
        public void A_new_track_that_never_gets_art_lets_the_old_art_go_after_a_second()
        {
            ShowingBlindingLights();

            media.Current = Track("Save Your Tears");
            MediaChanged();
            core.OnMediaArt(media.ArtFetches.Last().Request, null);
            clock.Advance(TimeSpan.FromMilliseconds(900));

            Assert.Same(blindingLightsArt, Card.Art);

            clock.Advance(TimeSpan.FromMilliseconds(100));

            Assert.Null(Card.Art);
        }

        [Fact]
        public void Play_and_pause_changes_keep_the_track_s_art_without_fetching_it_again()
        {
            ShowingBlindingLights();
            var fetches = media.ArtFetches.Count;

            media.Current = Track("Blinding Lights", isPlaying: false);
            MediaChanged();

            Assert.False(Card.IsPlaying);
            Assert.Same(blindingLightsArt, Card.Art);
            Assert.Equal(fetches, media.ArtFetches.Count);
        }

        [Fact]
        public void An_older_fetch_arriving_after_a_newer_one_is_dropped()
        {
            ShowingBlindingLights();

            media.Current = Track("Save Your Tears");
            MediaChanged();
            var older = media.ArtFetches.Last().Request;
            media.Current = Track("After Hours");
            MediaChanged();
            var newer = media.ArtFetches.Last().Request;
            Assert.True(newer > older);

            var afterHoursArt = new object();
            core.OnMediaArt(newer, afterHoursArt);
            core.OnMediaArt(older, saveYourTearsArt);

            Assert.Same(afterHoursArt, Card.Art);
        }

        [Fact]
        public void An_older_fetch_arriving_before_the_newer_one_leaves_the_old_art_up()
        {
            ShowingBlindingLights();

            media.Current = Track("Save Your Tears");
            MediaChanged();
            var older = media.ArtFetches.Last().Request;
            media.Current = Track("After Hours");
            MediaChanged();

            core.OnMediaArt(older, saveYourTearsArt);

            Assert.Equal("After Hours", Card.Title);
            Assert.Same(blindingLightsArt, Card.Art);
        }

        [Fact]
        public void Reopening_on_the_same_track_shows_its_art_at_once()
        {
            ShowingBlindingLights();
            var fetches = media.ArtFetches.Count;
            Tap(ControllerButton.B);
            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(2));

            OpenAudio();

            Assert.Same(blindingLightsArt, Card.Art);
            Assert.Equal(fetches, media.ArtFetches.Count);
        }

        [Fact]
        public void Reopening_on_a_new_track_shows_no_art_until_its_own_loads()
        {
            ShowingBlindingLights();
            Tap(ControllerButton.B);
            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(2));
            media.Current = Track("Save Your Tears");

            OpenAudio();

            Assert.Equal("Save Your Tears", Card.Title);
            Assert.Null(Card.Art);

            core.OnMediaArt(media.ArtFetches.Last().Request, saveYourTearsArt);

            Assert.Same(saveYourTearsArt, Card.Art);
        }

        [Fact]
        public void A_fetch_for_a_track_left_while_the_flyout_was_shut_is_dropped()
        {
            ShowingBlindingLights();
            media.Current = Track("Save Your Tears");
            MediaChanged();
            var left = media.ArtFetches.Last().Request;
            Tap(ControllerButton.B);
            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(2));
            media.Current = Track("Blinding Lights");
            OpenAudio();

            core.OnMediaArt(left, saveYourTearsArt);

            Assert.Same(blindingLightsArt, Card.Art);
        }

        private static MediaSession Track(string title, bool isPlaying = true) =>
            new MediaSession(Spotify, title, "The Weeknd", isPlaying);

        private GuideItem Card => Guide.Flyout.Items.Last();

        /// <summary>The Audio flyout open on Blinding Lights, its art loaded.</summary>
        private void ShowingBlindingLights()
        {
            media.Current = Track("Blinding Lights");
            OpenAudio();
            core.OnMediaArt(media.ArtFetches.Last().Request, blindingLightsArt);
            Assert.Same(blindingLightsArt, Card.Art);
        }

        /// <summary>Opens the Guide and, from the landing tab's utilities, the Audio flyout.</summary>
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
