using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Audio flyout's Now Playing entry: what Windows' media flyout shows, from the current media session, under the
    /// mute switches after a divider, following the session live as the extension signals a change.
    /// </summary>
    public class NowPlayingTests : DiscordTestsBase
    {
        private const string Spotify = "Spotify.exe";

        [Fact]
        public void Without_Discord_there_is_no_Now_Playing_tab()
        {
            OpenGuide();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.ProfileAndSystem }, Guide.Tabs.Select(t => t.Kind));
        }

        [Fact]
        public void With_Discord_there_is_no_Now_Playing_tab()
        {
            ConnectFully();
            OpenGuide();

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.Discord, GuideTabKind.ProfileAndSystem }, Guide.Tabs.Select(t => t.Kind));
        }

        [Fact]
        public void With_no_media_session_the_Audio_flyout_ends_saying_no_music_app_is_running_after_a_divider()
        {
            OpenAudio();

            Assert.Equal(
                new[] { GuideItemKind.AudioOutput, GuideItemKind.AudioInput, GuideItemKind.Volume, GuideItemKind.Mute, GuideItemKind.NothingPlaying },
                Flyout.Select(i => i.Kind));
            var item = Flyout.Last();
            Assert.Equal("No music app is running", item.Title);
            Assert.True(item.HasDividerAbove);
        }

        [Fact]
        public void A_playing_session_shows_its_track_artist_art_and_app_and_that_it_plays_after_Mute()
        {
            var art = new object();
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);

            OpenAudio();
            core.OnMediaArt(media.ArtFetches.Last().Request, art);

            var item = Flyout.Last();
            Assert.Equal(GuideItemKind.Mute, Flyout[Flyout.Count - 2].Kind);
            Assert.Equal(GuideItemKind.NowPlaying, item.Kind);
            Assert.Equal("Blinding Lights", item.Title);
            Assert.Equal("The Weeknd", item.Detail);
            Assert.Equal("Spotify", item.App);
            Assert.Same(art, item.Art);
            Assert.True(item.IsPlaying);
            Assert.True(item.HasDividerAbove);
        }

        [Fact]
        public void In_a_call_it_comes_after_Discord_s_mute_and_deafen()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));

            OpenAudio();

            Assert.Equal(
                new[]
                {
                    GuideItemKind.AudioOutput, GuideItemKind.AudioInput, GuideItemKind.Volume, GuideItemKind.Mute,
                    GuideItemKind.DiscordMute, GuideItemKind.DiscordDeafen, GuideItemKind.NothingPlaying,
                },
                Flyout.Select(i => i.Kind));
        }

        [Fact]
        public void Down_reaches_it_like_any_entry()
        {
            OpenAudio();

            FocusOn(GuideItemKind.NothingPlaying);

            Assert.Same(Flyout.Last(), Guide.FocusedItem);
        }

        [Fact]
        public void A_session_appearing_with_focus_on_no_music_app_is_running_shows_its_card_focus_on_it()
        {
            OpenAudio();
            FocusOn(GuideItemKind.NothingPlaying);

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            MediaChanged();

            var item = Flyout.Last();
            Assert.Equal(GuideItemKind.NowPlaying, item.Kind);
            Assert.Equal("Blinding Lights", item.Title);
            Assert.Same(item, Guide.FocusedItem);
            Assert.Equal(GuideItemKind.Audio, Guide.Flyout.Kind);
        }

        [Fact]
        public void A_session_appearing_with_focus_elsewhere_in_the_flyout_leaves_focus_there()
        {
            OpenAudio();
            FocusOn(GuideItemKind.Mute);

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            MediaChanged();

            Assert.Equal(GuideItemKind.NowPlaying, Flyout.Last().Kind);
            Assert.Equal(GuideItemKind.Mute, Guide.FocusedItem.Kind);
            Assert.Contains(Guide.FocusedItem, Flyout);
        }

        [Fact]
        public void Pausing_and_playing_again_changes_the_play_state_in_place()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenAudio();
            FocusOn(GuideItemKind.NowPlaying);
            var items = Flyout;
            var item = items.Last();

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: false);
            MediaChanged();

            Assert.Same(items, Flyout);
            Assert.False(item.IsPlaying);
            Assert.Same(item, Guide.FocusedItem);

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            MediaChanged();

            Assert.True(item.IsPlaying);
        }

        [Fact]
        public void A_new_track_or_another_app_playing_shows_in_place_of_the_last_focus_on_it()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenAudio();
            FocusOn(GuideItemKind.NowPlaying);

            var item = Flyout.Last();
            var art = new object();

            media.Current = new MediaSession("MSEdge", "Lo-fi beats", "Lofi Girl", isPlaying: true);
            MediaChanged();
            Assert.Equal("MSEdge", media.ArtFetches.Last().AppId);
            core.OnMediaArt(media.ArtFetches.Last().Request, art);

            Assert.Same(item, Flyout.Last());
            Assert.Equal("Lo-fi beats", item.Title);
            Assert.Equal("Lofi Girl", item.Detail);
            Assert.Equal("Edge", item.App);
            Assert.Same(art, item.Art);
            Assert.Same(item, Guide.FocusedItem);
            Assert.Single(Flyout, i => i.Kind == GuideItemKind.NowPlaying);
        }

        [Fact]
        public void A_new_track_with_a_device_list_open_leaves_the_list_open_and_focus_on_its_device()
        {
            audio.Add("speakers", "Speakers", AudioDeviceKind.Output);
            audio.Add("headphones", "Headphones", AudioDeviceKind.Output);
            audio.SetDefault(AudioDeviceKind.Output, "headphones");
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenAudio();
            var items = Flyout;
            Tap(ControllerButton.A);
            var output = Flyout.First(i => i.Kind == GuideItemKind.AudioOutput);
            Assert.True(output.IsExpanded);
            var device = Guide.FocusedItem;

            media.Current = new MediaSession(Spotify, "Save Your Tears", "The Weeknd", isPlaying: true);
            MediaChanged();

            Assert.Same(items, Flyout);
            Assert.True(output.IsExpanded);
            Assert.Same(device, Guide.FocusedItem);
            Assert.Equal("Save Your Tears", Flyout.Last().Title);
        }

        [Fact]
        public void The_session_closing_falls_back_to_no_music_app_is_running_focus_on_it()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenAudio();
            FocusOn(GuideItemKind.NowPlaying);

            media.Current = null;
            MediaChanged();

            var item = Flyout.Last();
            Assert.Equal(GuideItemKind.NothingPlaying, item.Kind);
            Assert.Same(item, Guide.FocusedItem);
        }

        [Fact]
        public void A_change_with_the_Audio_flyout_shut_or_the_Guide_closed_leaves_things_be_and_shows_when_it_opens()
        {
            OpenGuide();
            var landing = Guide.Items;

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            MediaChanged();

            Assert.Same(landing, Guide.Items);
            Assert.Null(Guide.Flyout);
            Assert.Equal(GuideTabKind.Landing, Guide.Tab);

            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(2));
            media.Current = null;
            MediaChanged();

            Assert.False(Guide.IsOpen);

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenAudio();
            Assert.Equal("Blinding Lights", Flyout.Last().Title);
        }

        [Fact]
        public void A_on_it_does_nothing()
        {
            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true);
            OpenAudio();
            FocusOn(GuideItemKind.NowPlaying);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.True(Guide.IsOpen);
            Assert.Equal(GuideItemKind.Audio, Guide.Flyout.Kind);
            Assert.Same(Flyout.Last(), Guide.FocusedItem);
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

        private IReadOnlyList<GuideItem> Flyout => Guide.Flyout.Items;

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

        /// <summary>Down through the flyout until focus is on the entry of <paramref name="kind"/>.</summary>
        private void FocusOn(GuideItemKind kind)
        {
            for (var i = 0; Guide.FocusedItem.Kind != kind; i++)
            {
                if (i > Flyout.Count)
                {
                    throw new InvalidOperationException($"Down never reached {kind}.");
                }

                Tap(ControllerButton.DPadDown);
            }
        }
    }
}
