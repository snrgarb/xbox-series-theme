using System;
using System.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Guide's Now Playing tab: what Windows' media flyout shows, from the current media session, following it live
    /// as the extension signals a change.
    /// </summary>
    public class NowPlayingTests : DiscordTestsBase
    {
        private const string Spotify = "Spotify.exe";

        [Fact]
        public void Without_Discord_Now_Playing_sits_between_the_landing_tab_and_Profile_and_system()
        {
            OpenGuide();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.NowPlaying, GuideTabKind.ProfileAndSystem }, Guide.Tabs.Select(t => t.Kind));
            Assert.Equal(GuideTabKind.Landing, Guide.Tab);
        }

        [Fact]
        public void With_Discord_Now_Playing_sits_between_the_Discord_tab_and_Profile_and_system()
        {
            ConnectFully();
            OpenGuide();

            Assert.Equal(
                new[] { GuideTabKind.Landing, GuideTabKind.Discord, GuideTabKind.NowPlaying, GuideTabKind.ProfileAndSystem },
                Guide.Tabs.Select(t => t.Kind));
        }

        [Fact]
        public void RB_and_LB_move_through_Now_Playing()
        {
            ConnectFully();
            OpenGuide();

            Tap(ControllerButton.RightShoulder);
            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.NowPlaying, Guide.Tab);
            Assert.Equal(new[] { false, false, true, false }, Guide.Tabs.Select(t => t.IsSelected));

            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.ProfileAndSystem, Guide.Tab);

            Tap(ControllerButton.LeftShoulder);
            Assert.Equal(GuideTabKind.NowPlaying, Guide.Tab);
        }

        [Fact]
        public void With_no_media_session_the_tab_says_nothing_is_playing()
        {
            OpenNowPlaying();

            var item = Assert.Single(Guide.Items);
            Assert.Equal(GuideItemKind.NothingPlaying, item.Kind);
            Assert.Equal("Nothing's playing", item.Title);
            Assert.Same(item, Guide.FocusedItem);
            Assert.Empty(Guide.Utilities);
        }

        [Fact]
        public void A_playing_session_shows_its_track_artist_art_and_app_and_that_it_plays()
        {
            var art = new object();
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true, art: art);

            OpenNowPlaying();

            var item = Assert.Single(Guide.Items);
            Assert.Equal(GuideItemKind.NowPlaying, item.Kind);
            Assert.Equal("Blinding Lights", item.Title);
            Assert.Equal("The Weeknd", item.Detail);
            Assert.Equal("Spotify", item.App);
            Assert.Same(art, item.Art);
            Assert.True(item.IsPlaying);
            Assert.Same(item, Guide.FocusedItem);
        }

        [Fact]
        public void A_session_appearing_with_the_tab_open_shows_on_it()
        {
            OpenNowPlaying();

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            core.OnMediaSessionChanged();

            var item = Assert.Single(Guide.Items);
            Assert.Equal(GuideItemKind.NowPlaying, item.Kind);
            Assert.Equal("Blinding Lights", item.Title);
            Assert.Same(item, Guide.FocusedItem);
            Assert.Equal(GuideTabKind.NowPlaying, Guide.Tab);
        }

        [Fact]
        public void Pausing_and_playing_again_changes_the_play_state_in_place()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenNowPlaying();
            var item = Guide.Items.Single();

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: false);
            core.OnMediaSessionChanged();

            Assert.Same(item, Guide.Items.Single());
            Assert.False(item.IsPlaying);
            Assert.Same(item, Guide.FocusedItem);

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            core.OnMediaSessionChanged();

            Assert.True(item.IsPlaying);
        }

        [Fact]
        public void A_new_track_or_another_app_playing_shows_in_place_of_the_last()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenNowPlaying();

            media.Current = new MediaSession("MSEdge", "Lo-fi beats", "Lofi Girl", isPlaying: true);
            core.OnMediaSessionChanged();

            var item = Assert.Single(Guide.Items);
            Assert.Equal("Lo-fi beats", item.Title);
            Assert.Equal("Edge", item.App);
            Assert.Same(item, Guide.FocusedItem);
        }

        [Fact]
        public void The_session_closing_falls_back_to_nothing_playing()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenNowPlaying();

            media.Current = null;
            core.OnMediaSessionChanged();

            var item = Assert.Single(Guide.Items);
            Assert.Equal(GuideItemKind.NothingPlaying, item.Kind);
            Assert.Same(item, Guide.FocusedItem);
        }

        [Fact]
        public void A_change_with_the_Guide_on_another_tab_or_closed_leaves_it_be_and_shows_when_the_tab_opens()
        {
            OpenGuide();
            var landing = Guide.Items;

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            core.OnMediaSessionChanged();

            Assert.Same(landing, Guide.Items);
            Assert.Equal(GuideTabKind.Landing, Guide.Tab);

            Tap(ControllerButton.RightShoulder);
            Assert.Equal("Blinding Lights", Guide.Items.Single().Title);

            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(2));
            media.Current = null;
            core.OnMediaSessionChanged();

            Assert.False(Guide.IsOpen);
        }

        [Fact]
        public void A_on_the_tab_does_nothing()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenNowPlaying();
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.True(Guide.IsOpen);
            Assert.Equal(GuideTabKind.NowPlaying, Guide.Tab);
            Assert.Empty(sounds.Played);
        }

        [Theory]
        [InlineData("Spotify.exe", "Spotify")]
        [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
        [InlineData("Chrome", "Chrome")]
        [InlineData("chrome.exe", "Chrome")]
        [InlineData("MSEdge", "Edge")]
        [InlineData("308046B0AF4A39CB", "Firefox")]
        [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
        [InlineData(@"C:\Program Files\VideoLAN\VLC\vlc.exe", "VLC")]
        [InlineData("foobar2000.exe", "Foobar2000")]
        [InlineData("Contoso.Player_abc123!App", "Contoso Player")]
        [InlineData("", "")]
        public void The_app_is_named_as_people_know_it_rather_than_by_its_raw_id(string appId, string name)
        {
            Assert.Equal(name, MediaSession.FriendlyAppName(appId));
        }

        private void OpenNowPlaying()
        {
            OpenGuide();
            while (Guide.Tab != GuideTabKind.NowPlaying && Guide.Tab != GuideTabKind.ProfileAndSystem)
            {
                Tap(ControllerButton.RightShoulder);
            }

            if (Guide.Tab != GuideTabKind.NowPlaying)
            {
                throw new InvalidOperationException("The Now Playing tab didn't open.");
            }
        }
    }
}
