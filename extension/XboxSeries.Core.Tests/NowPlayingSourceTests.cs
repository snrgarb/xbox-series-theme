using System;
using System.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Settings' Now Playing source (#75): which app the Audio flyout's Now Playing card follows while it has a media
    /// session, else Windows' current; Any for Windows' current always. Its choices are Any, Spotify and every other
    /// app seen this run; the choice applies and saves at once, shared by every profile.
    /// </summary>
    public class NowPlayingSourceTests : DiscordTestsBase
    {
        private const string SpotifyExe = "Spotify.exe";
        private const string SpotifyStore = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";
        private const string ChromeExe = "chrome.exe";
        private const string EdgeId = "MSEdge";

        private readonly MediaSession spotifyPaused = new MediaSession(SpotifyExe, "Blinding Lights", "The Weeknd", isPlaying: false);
        private readonly MediaSession youTube = new MediaSession(ChromeExe, "Lo-fi beats", null, isPlaying: true);

        private GuideSettingsState Options => core.Settings.Guide;

        [Fact]
        public void Spotify_is_chosen_at_first_and_the_choices_are_Any_and_Spotify()
        {
            Assert.Equal("Spotify", Options.NowPlayingSource);
            Assert.Equal(new[] { "Any", "Spotify" }, Options.NowPlayingSources);
        }

        [Fact]
        public void With_Spotify_paused_and_Chrome_current_the_card_shows_Spotify_while_the_source_is_Spotify()
        {
            SpotifyPausedChromePlaying();

            OpenAudio();

            Assert.Equal("Spotify", Card.App);
            Assert.Equal("Blinding Lights", Card.Title);
            Assert.False(Card.IsPlaying);
        }

        [Fact]
        public void With_the_source_Any_the_card_shows_Windows_current_session()
        {
            SpotifyPausedChromePlaying();
            core.SetNowPlayingSource("Any");

            OpenAudio();

            Assert.Equal("Chrome", Card.App);
        }

        [Fact]
        public void With_the_source_Chrome_the_card_shows_Chrome_even_when_Spotify_is_current()
        {
            media.Current = spotifyPaused;
            media.Sessions = new[] { spotifyPaused, youTube };
            MediaChanged();
            core.SetNowPlayingSource("Chrome");

            OpenAudio();

            Assert.Equal("Chrome", Card.App);
        }

        [Fact]
        public void Spotify_s_Store_build_is_Spotify_too()
        {
            var store = new MediaSession(SpotifyStore, "Levitating", "Dua Lipa", isPlaying: false);
            media.Current = youTube;
            media.Sessions = new[] { youTube, store };

            OpenAudio();

            Assert.Equal("Levitating", Card.Title);
        }

        [Fact]
        public void With_the_chosen_app_not_running_the_card_follows_Windows_current_session()
        {
            media.Current = youTube;

            OpenAudio();

            Assert.Equal("Chrome", Card.App);
        }

        [Fact]
        public void The_chosen_app_registering_a_session_switches_the_card_to_it()
        {
            media.Current = youTube;
            OpenAudio();

            media.Sessions = new[] { youTube, spotifyPaused };
            MediaChanged();

            Assert.Equal("Spotify", Card.App);
            Assert.Equal("Blinding Lights", Card.Title);
        }

        [Fact]
        public void The_chosen_app_going_returns_the_card_to_Windows_current_session()
        {
            SpotifyPausedChromePlaying();
            OpenAudio();

            media.Sessions = new[] { youTube };
            MediaChanged();

            Assert.Equal("Chrome", Card.App);
        }

        [Fact]
        public void Changing_the_source_switches_an_open_flyout_s_card_at_once()
        {
            SpotifyPausedChromePlaying();
            OpenAudio();

            core.SetNowPlayingSource("Any");

            Assert.Equal("Chrome", Card.App);
            Assert.Equal(GuideItemKind.NowPlaying, Guide.Flyout.Items.Last().Kind);
        }

        [Fact]
        public void Every_app_seen_this_run_is_a_choice_by_its_name_in_the_order_seen_and_only_once()
        {
            media.Current = youTube;
            MediaChanged();
            media.Sessions = new[] { youTube, spotifyPaused, new MediaSession(EdgeId, "Podcast", null, isPlaying: true) };
            MediaChanged();
            media.Sessions = new[] { new MediaSession(ChromeExe, "Another video", null, isPlaying: true) };
            MediaChanged();

            Assert.Equal(new[] { "Any", "Spotify", "Chrome", "Edge" }, Options.NowPlayingSources);
        }

        [Fact]
        public void An_app_that_has_gone_stays_a_choice()
        {
            media.Current = youTube;
            MediaChanged();
            media.Current = null;
            MediaChanged();

            Assert.Contains("Chrome", Options.NowPlayingSources);
        }

        [Fact]
        public void Apps_with_sessions_as_the_core_is_made_are_choices()
        {
            media.Current = youTube;

            NewCore();

            Assert.Equal(new[] { "Any", "Spotify", "Chrome" }, Options.NowPlayingSources);
        }

        [Fact]
        public void A_source_that_isn_t_a_choice_is_refused()
        {
            core.SetNowPlayingSource("Winamp");
            core.SetNowPlayingSource(null);

            Assert.Equal("Spotify", Options.NowPlayingSource);
            Assert.Equal(0, secrets.Writes);
        }

        [Fact]
        public void The_source_is_saved_at_once_and_survives_a_restart()
        {
            core.SetNowPlayingSource("Any");

            NewCore();

            Assert.Equal("Any", Options.NowPlayingSource);
        }

        [Fact]
        public void A_saved_app_not_seen_yet_this_run_is_still_chosen_and_a_choice()
        {
            media.Current = youTube;
            MediaChanged();
            core.SetNowPlayingSource("Chrome");
            media.Current = null;

            NewCore();

            Assert.Equal("Chrome", Options.NowPlayingSource);
            Assert.Equal(new[] { "Any", "Spotify", "Chrome" }, Options.NowPlayingSources);
        }

        [Fact]
        public void A_saved_app_is_followed_as_soon_as_it_registers_after_a_restart()
        {
            media.Current = youTube;
            MediaChanged();
            core.SetNowPlayingSource("Chrome");
            media.Current = spotifyPaused;
            NewCore();
            OpenAudio();
            Assert.Equal("Spotify", Card.App);

            media.Sessions = new[] { spotifyPaused, youTube };
            MediaChanged();

            Assert.Equal("Chrome", Card.App);
            Assert.Equal(new[] { "Any", "Spotify", "Chrome" }, Options.NowPlayingSources);
        }

        [Fact]
        public void Choosing_the_source_already_chosen_saves_nothing()
        {
            core.SetNowPlayingSource("Spotify");

            Assert.Equal(0, secrets.Writes);
        }

        [Fact]
        public void An_unreadable_saved_source_is_Spotify()
        {
            secrets.Values["Guide"] = "{not json";

            NewCore();

            Assert.Equal("Spotify", Options.NowPlayingSource);
            Assert.Equal(new[] { "Any", "Spotify" }, Options.NowPlayingSources);
        }

        private GuideItem Card => Guide.Flyout.Items.Last();

        /// <summary>Spotify paused and YouTube playing in Chrome, Windows' current; the core told.</summary>
        private void SpotifyPausedChromePlaying()
        {
            media.Current = youTube;
            media.Sessions = new[] { spotifyPaused, youTube };
            MediaChanged();
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
