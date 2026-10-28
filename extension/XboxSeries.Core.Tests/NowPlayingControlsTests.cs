using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Now Playing card's row of Previous, Play/Pause and Next: A asks the playing app, whose own change events then
    /// show what it did; an entry the app has turned off is dimmed and A does nothing on it.
    /// </summary>
    public class NowPlayingControlsTests : DiscordTestsBase
    {
        private const string Spotify = "Spotify.exe";

        [Fact]
        public void The_card_has_Previous_Play_Pause_and_Next_under_it_and_stays_the_flyout_s_last_entry()
        {
            media.Current = Playing();

            OpenAudio();

            var card = Flyout.Last();
            Assert.Equal(GuideItemKind.NowPlaying, card.Kind);
            Assert.Equal(new[] { GuideItemKind.MediaPrevious, GuideItemKind.MediaPlayPause, GuideItemKind.MediaNext }, card.Controls.Select(c => c.Kind));
            Assert.All(card.Controls, c => Assert.True(c.IsEnabled));
            Assert.DoesNotContain(Flyout, i => i.Kind == GuideItemKind.MediaPlayPause);
        }

        [Fact]
        public void With_no_music_app_running_there_are_no_controls()
        {
            OpenAudio();

            Assert.Equal(GuideItemKind.NothingPlaying, Flyout.Last().Kind);
            Assert.Null(Flyout.Last().Controls);
        }

        [Fact]
        public void Play_Pause_shows_whether_the_session_plays()
        {
            media.Current = Playing(isPlaying: false);

            OpenAudio();

            Assert.False(PlayPause.IsPlaying);
        }

        [Fact]
        public void The_flyout_s_first_focus_stays_on_Output()
        {
            media.Current = Playing();

            OpenAudio();

            Assert.Equal(GuideItemKind.AudioOutput, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Down_from_the_card_reaches_Play_Pause_and_Up_returns_to_the_card()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOn(GuideItemKind.NowPlaying);
            sounds.Played.Clear();

            Tap(ControllerButton.DPadDown);

            Assert.Same(PlayPause, Guide.FocusedItem);
            Assert.True(PlayPause.IsFocused);
            Assert.Equal(new[] { UiSound.Navigate }, sounds.Played);

            Tap(ControllerButton.DPadUp);

            Assert.Same(Flyout.Last(), Guide.FocusedItem);
            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate }, sounds.Played);
        }

        [Fact]
        public void Left_and_right_move_along_the_row_held_at_either_end()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnPlayPause();
            sounds.Played.Clear();

            Tap(ControllerButton.DPadLeft);
            Assert.Equal(GuideItemKind.MediaPrevious, Guide.FocusedItem.Kind);
            Tap(ControllerButton.DPadLeft);
            Assert.Equal(GuideItemKind.MediaPrevious, Guide.FocusedItem.Kind);
            Assert.Equal(new[] { UiSound.Navigate }, sounds.Played);

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadRight);
            Assert.Equal(GuideItemKind.MediaNext, Guide.FocusedItem.Kind);
            Tap(ControllerButton.DPadRight);
            Assert.Equal(GuideItemKind.MediaNext, Guide.FocusedItem.Kind);
            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate, UiSound.Navigate }, sounds.Played);
        }

        [Fact]
        public void Down_from_the_row_stays_at_the_flyout_s_end()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnPlayPause();
            Tap(ControllerButton.DPadRight);
            sounds.Played.Clear();

            Tap(ControllerButton.DPadDown);

            Assert.Equal(GuideItemKind.MediaNext, Guide.FocusedItem.Kind);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Up_from_any_control_returns_to_the_card()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnPlayPause();
            Tap(ControllerButton.DPadLeft);

            Tap(ControllerButton.DPadUp);

            Assert.Same(Flyout.Last(), Guide.FocusedItem);
        }

        [Theory]
        [InlineData(GuideItemKind.MediaPrevious, MediaCommand.Previous)]
        [InlineData(GuideItemKind.MediaPlayPause, MediaCommand.TogglePlayPause)]
        [InlineData(GuideItemKind.MediaNext, MediaCommand.Next)]
        public void A_on_a_control_asks_the_playing_app_with_the_Enter_sound(GuideItemKind kind, MediaCommand command)
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnControl(kind);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal(new[] { (command, Spotify) }, media.Sent);
            Assert.Equal(new[] { UiSound.Enter }, sounds.Played);
            Assert.True(Guide.IsOpen);
            Assert.Equal(GuideItemKind.Audio, Guide.Flyout.Kind);
            Assert.Equal(kind, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Nothing_changes_until_the_app_reports_it()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnPlayPause();
            var items = Flyout;
            var card = items.Last();
            var playPause = PlayPause;

            Tap(ControllerButton.A);

            Assert.Same(items, Flyout);
            Assert.True(card.IsPlaying);
            Assert.True(playPause.IsPlaying);
            Assert.Equal("Blinding Lights", card.Title);

            media.Current = Playing(isPlaying: false);
            MediaChanged();

            Assert.Same(items, Flyout);
            Assert.False(card.IsPlaying);
            Assert.False(playPause.IsPlaying);
            Assert.Same(playPause, PlayPause);
            Assert.Same(playPause, Guide.FocusedItem);
        }

        [Fact]
        public void A_command_the_app_refuses_leaves_the_flyout_as_it_was()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnControl(GuideItemKind.MediaNext);
            var items = Flyout;
            var next = Guide.FocusedItem;

            Tap(ControllerButton.A);

            // A refusal, or the app closing mid-command, reaches the core as no change at all.
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Single(media.Sent);
            Assert.Same(items, Flyout);
            Assert.Equal("Blinding Lights", Flyout.Last().Title);
            Assert.Same(next, Guide.FocusedItem);
        }

        [Fact]
        public void A_sends_to_the_app_the_card_shows_now()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnPlayPause();

            media.Current = new MediaSession("MSEdge", "Lo-fi beats", "Lofi Girl", isPlaying: true);
            MediaChanged();
            Tap(ControllerButton.A);

            Assert.Equal(new[] { (MediaCommand.TogglePlayPause, "MSEdge") }, media.Sent);
        }

        [Theory]
        [InlineData(GuideItemKind.MediaPrevious)]
        [InlineData(GuideItemKind.MediaPlayPause)]
        [InlineData(GuideItemKind.MediaNext)]
        public void A_control_the_app_has_turned_off_is_disabled_and_A_does_nothing_on_it(GuideItemKind kind)
        {
            media.Current = new MediaSession(
                Spotify, "Blinding Lights", "The Weeknd", isPlaying: true,
                canTogglePlayPause: kind != GuideItemKind.MediaPlayPause,
                canGoPrevious: kind != GuideItemKind.MediaPrevious,
                canGoNext: kind != GuideItemKind.MediaNext);
            OpenAudio();
            FocusOnControl(kind);
            sounds.Played.Clear();

            Assert.False(Guide.FocusedItem.IsEnabled);
            Assert.All(Flyout.Last().Controls.Where(c => c.Kind != kind), c => Assert.True(c.IsEnabled));

            Tap(ControllerButton.A);

            Assert.Empty(media.Sent);
            Assert.Empty(sounds.Played);
            Assert.Equal(kind, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void The_controls_turn_off_and_on_in_place_as_the_app_says()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnControl(GuideItemKind.MediaNext);
            var next = Guide.FocusedItem;

            media.Current = new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying: true, canGoNext: false, canGoPrevious: false);
            MediaChanged();

            Assert.Same(next, Guide.FocusedItem);
            Assert.False(next.IsEnabled);
            Assert.False(Flyout.Last().Controls[0].IsEnabled);
            Assert.True(PlayPause.IsEnabled);

            media.Current = Playing();
            MediaChanged();

            Assert.Same(next, Guide.FocusedItem);
            Assert.True(next.IsEnabled);
        }

        [Fact]
        public void A_new_track_or_app_leaves_focus_on_the_same_control()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnControl(GuideItemKind.MediaNext);
            var card = Flyout.Last();
            var next = Guide.FocusedItem;

            media.Current = new MediaSession("MSEdge", "Lo-fi beats", "Lofi Girl", isPlaying: false);
            MediaChanged();

            Assert.Same(card, Flyout.Last());
            Assert.Equal("Lo-fi beats", card.Title);
            Assert.Same(next, Guide.FocusedItem);
            Assert.Same(next, card.Controls[2]);
            Assert.False(PlayPause.IsPlaying);
        }

        [Fact]
        public void The_session_closing_with_focus_on_a_control_puts_focus_on_no_music_app_is_running()
        {
            media.Current = Playing();
            OpenAudio();
            FocusOnPlayPause();

            media.Current = null;
            MediaChanged();

            var item = Flyout.Last();
            Assert.Equal(GuideItemKind.NothingPlaying, item.Kind);
            Assert.Same(item, Guide.FocusedItem);
        }

        [Fact]
        public void A_call_beginning_with_focus_on_a_control_keeps_focus_on_that_control_of_the_rebuilt_card()
        {
            NotInCall();
            media.Current = Playing();
            OpenAudio();
            FocusOnControl(GuideItemKind.MediaNext);

            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));

            Assert.Contains(Flyout, i => i.Kind == GuideItemKind.DiscordMute);
            Assert.Equal(GuideItemKind.MediaNext, Guide.FocusedItem.Kind);
            Assert.Same(Flyout.Last().Controls[2], Guide.FocusedItem);
        }

        [Fact]
        public void Down_from_Mute_stops_on_no_music_app_is_running()
        {
            OpenAudio();
            FocusOn(GuideItemKind.Mute);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Assert.Equal(GuideItemKind.NothingPlaying, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void In_a_call_down_from_Discord_s_deafen_stops_on_no_music_app_is_running()
        {
            NotInCall();
            Join(VoiceChannel("500", "Late night", "9", State(Me, "gabe")));
            OpenAudio();
            FocusOn(GuideItemKind.DiscordDeafen);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Assert.Equal(GuideItemKind.NothingPlaying, Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_click_on_a_control_asks_the_app()
        {
            media.Current = Playing();
            OpenAudio();

            core.ChooseItem(Flyout.Last().Controls[0]);

            Assert.Equal(new[] { (MediaCommand.Previous, Spotify) }, media.Sent);
            Assert.Equal(GuideItemKind.MediaPrevious, Guide.FocusedItem.Kind);
        }

        private static MediaSession Playing(bool isPlaying = true) =>
            new MediaSession(Spotify, "Blinding Lights", "The Weeknd", isPlaying);

        private IReadOnlyList<GuideItem> Flyout => Guide.Flyout.Items;

        private GuideItem PlayPause => Flyout.Last().Controls[1];

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

        /// <summary>Down to the card, and down again onto Play/Pause.</summary>
        private void FocusOnPlayPause()
        {
            FocusOn(GuideItemKind.NowPlaying);
            Tap(ControllerButton.DPadDown);
        }

        /// <summary>Onto Play/Pause, then left or right to the control of <paramref name="kind"/>.</summary>
        private void FocusOnControl(GuideItemKind kind)
        {
            FocusOnPlayPause();
            if (kind == GuideItemKind.MediaPrevious)
            {
                Tap(ControllerButton.DPadLeft);
            }
            else if (kind == GuideItemKind.MediaNext)
            {
                Tap(ControllerButton.DPadRight);
            }

            if (Guide.FocusedItem.Kind != kind)
            {
                throw new InvalidOperationException($"The row never reached {kind}.");
            }
        }
    }
}
