using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class GuideTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 13, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public GuideTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount());
        }

        [Fact]
        public void Guide_is_closed_at_start()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), series: new[] { "Halo" }, genres: new[] { "Shooter" }, source: "Steam")
                .WithGame("Forza Horizon 5", added: new DateTime(2026, 9, 1), installed: false, source: "Xbox")
                .WithFilterPreset("Favourites", showInFullscreenQuickSelection: true)
                .WithApp("Discord", @"C:\Discord\Update.exe", "--processStart Discord.exe");

            core.OnLibraryChanged(library.Snapshot());

            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void Guide_button_opens_the_Guide_over_the_game_and_takes_the_foreground_when_tapped()
        {
            windows.OpenWindow();

            Press(ControllerButton.Guide);

            // Held, it may yet be a hold for Power, so only the release opens the Guide.
            Assert.False(core.Guide.IsOverlayShown);

            Release(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);
            Assert.True(core.Guide.IsOverlayShown);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);
        }

        [Fact]
        public void B_closes_the_Guide_but_hands_focus_back_only_when_B_is_released()
        {
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);

            Press(ControllerButton.B);

            Assert.False(core.Guide.IsOpen);
            Assert.True(core.Guide.IsOverlayShown);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Release(ControllerButton.B);

            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void Guide_button_tapped_again_closes_the_Guide_and_hands_focus_back_when_it_is_released()
        {
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);

            Press(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);

            Release(ControllerButton.Guide);

            Assert.False(core.Guide.IsOpen);

            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void Focus_goes_back_after_one_second_when_the_dismiss_button_release_never_arrives()
        {
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);
            Press(ControllerButton.B);

            clock.Advance(TimeSpan.FromMilliseconds(999));
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void Focus_goes_to_Playnite_Fullscreen_when_the_game_quit_while_the_Guide_was_open()
        {
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);

            windows.CloseWindow(game);
            Tap(ControllerButton.B);

            Assert.Equal(playnite.Window, windows.Foreground);
        }

        [Fact]
        public void Guide_still_shows_and_follows_the_pad_when_Windows_refuses_the_foreground()
        {
            var game = windows.OpenWindow();
            windows.RefusesForeground = true;

            Tap(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);
            Assert.True(core.Guide.IsOverlayShown);

            Tap(ControllerButton.B);

            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void Esc_or_a_click_on_the_dim_closes_the_Guide_and_hands_focus_back_at_once()
        {
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);

            core.DismissGuide();

            Assert.False(core.Guide.IsOpen);
            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void Reopening_before_the_dismiss_button_is_released_keeps_the_Guide_up_and_still_returns_to_the_game()
        {
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);
            Press(ControllerButton.B);

            Tap(ControllerButton.Guide);
            Release(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Tap(ControllerButton.B);

            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void Guide_opened_over_the_Dashboard_hands_focus_back_to_Playnite()
        {
            playnite.BringToFront();
            Tap(ControllerButton.Guide);

            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Tap(ControllerButton.B);

            Assert.Equal(playnite.Window, windows.Foreground);
        }

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        private void Tap(ControllerButton button)
        {
            Press(button);
            Release(button);
        }
    }
}
