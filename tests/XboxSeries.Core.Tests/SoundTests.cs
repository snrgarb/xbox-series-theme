using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class SoundTests
    {
        private const string HaloDirectory = @"C:\Games\Halo Infinite";

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public SoundTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, sounds, new FakeDrives(), new FakeUserAccount());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), installDirectory: HaloDirectory)
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11))
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10));
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void Guide_button_opens_the_Guide_with_the_open_menu_sound()
        {
            Tap(ControllerButton.Guide);

            Assert.Equal(new[] { UiSound.OpenMenu }, sounds.Played);
        }

        [Theory]
        [InlineData(ControllerButton.B)]
        [InlineData(ControllerButton.Guide)]
        public void Closing_the_Guide_from_the_pad_is_silent(ControllerButton button)
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            Tap(button);

            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Esc_closing_the_Guide_is_silent()
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            core.DismissGuide();

            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Each_focus_move_in_the_Guide_plays_navigate()
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate }, sounds.Played);
        }

        [Fact]
        public void A_focus_move_past_the_end_is_silent()
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            Tap(ControllerButton.DPadUp);

            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Holding_the_d_pad_plays_navigate_on_each_repeat()
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            Press(ControllerButton.DPadDown);
            clock.Advance(TimeSpan.FromMilliseconds(700));

            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate }, sounds.Played);
        }

        [Fact]
        public void A_on_an_entry_plays_enter()
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal(new[] { UiSound.Enter }, sounds.Played);
        }

        [Fact]
        public void A_click_on_an_entry_plays_enter()
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            core.ChooseItem(core.Guide.Items[0]);

            Assert.Equal(new[] { UiSound.Enter }, sounds.Played);
        }

        [Fact]
        public void The_running_games_menu_opens_with_prompt_open_and_B_closes_it_with_prompt_close()
        {
            HaloRuns();
            OpenGuideOn("Halo Infinite");
            sounds.Played.Clear();

            Tap(ControllerButton.Start);
            Tap(ControllerButton.B);

            Assert.Equal(new[] { UiSound.PromptOpen, UiSound.PromptClose }, sounds.Played);
        }

        [Fact]
        public void Choosing_a_menu_option_plays_enter()
        {
            HaloRuns();
            OpenGuideOn("Halo Infinite");
            Tap(ControllerButton.Start);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal(new[] { UiSound.Enter }, sounds.Played);
        }

        [Fact]
        public void The_tile_menu_opens_with_prompt_open_moves_with_navigate_and_answers_with_enter()
        {
            core.FocusHomeTile(core.Home.RecentRow.Single(t => t.Title == "Hades"));

            Tap(ControllerButton.Start);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Assert.Equal(new[] { UiSound.PromptOpen, UiSound.Navigate, UiSound.Enter }, sounds.Played);
        }

        [Fact]
        public void B_on_the_tile_menu_plays_prompt_close()
        {
            core.FocusHomeTile(core.Home.RecentRow.Single(t => t.Title == "Hades"));
            Tap(ControllerButton.Start);
            sounds.Played.Clear();

            Tap(ControllerButton.B);

            Assert.Equal(new[] { UiSound.PromptClose }, sounds.Played);
        }

        [Fact]
        public void A_prompt_that_opens_the_Guide_plays_only_the_prompt_sound()
        {
            HaloRuns();

            core.PlayGame(library.Id("Hades"));

            Assert.Equal(new[] { UiSound.PromptOpen }, sounds.Played);
        }

        [Theory]
        [InlineData(ControllerButton.A)]
        [InlineData(ControllerButton.B)]
        public void Answering_a_prompt_either_way_plays_prompt_close(ControllerButton button)
        {
            HaloRuns();
            core.PlayGame(library.Id("Hades"));
            sounds.Played.Clear();

            Tap(button);

            Assert.Equal(new[] { UiSound.PromptClose }, sounds.Played);
        }

        [Fact]
        public void A_click_on_a_prompt_option_plays_prompt_close()
        {
            HaloRuns();
            core.PlayGame(library.Id("Hades"));
            sounds.Played.Clear();

            core.ChooseOption(core.Guide.Dialog.Options.Last());

            Assert.Equal(new[] { UiSound.PromptClose }, sounds.Played);
        }

        [Fact]
        public void A_on_a_recent_game_while_another_runs_plays_enter_then_prompt_open()
        {
            HaloRuns();
            OpenGuideOn("Hades");
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal(new[] { UiSound.Enter, UiSound.PromptOpen }, sounds.Played);
        }

        [Fact]
        public void The_force_quit_prompt_plays_prompt_open()
        {
            HaloRuns();
            OpenGuideOn("Halo Infinite");
            Tap(ControllerButton.Start);
            Tap(ControllerButton.A);
            sounds.Played.Clear();

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal(new[] { UiSound.PromptOpen }, sounds.Played);
        }

        [Fact]
        public void A_prompt_closed_by_the_game_stopping_is_silent()
        {
            HaloRuns();
            core.PlayGame(library.Id("Hades"));
            sounds.Played.Clear();

            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.Empty(sounds.Played);
        }

        private void HaloRuns()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            windows.OpenWindow(HaloDirectory + @"\HaloInfinite.exe");
        }

        private void OpenGuideOn(string title)
        {
            Tap(ControllerButton.Guide);
            while (core.Guide.FocusedItem.Title != title)
            {
                Tap(ControllerButton.DPadDown);
            }
        }

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        /// <summary>Pressed and let go. A Guide tap opens the Guide 0.3 s after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button)
        {
            Press(button);
            Release(button);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
