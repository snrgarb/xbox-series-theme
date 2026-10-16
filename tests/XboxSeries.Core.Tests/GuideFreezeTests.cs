using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// A running game is frozen while the Guide is open over it, so a game that reads the pad in the background can't
    /// take its input, and thawed before anything could need it again.
    /// </summary>
    public class GuideFreezeTests
    {
        private const string HaloDirectory = @"C:\Games\Halo Infinite";

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 16, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;
        private IntPtr halo;

        public GuideFreezeTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), installDirectory: HaloDirectory)
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void Opening_the_Guide_over_a_running_game_freezes_it()
        {
            HaloRuns();

            Tap(ControllerButton.Guide);

            Assert.Equal(new[] { (HaloDirectory, halo) }, windows.Freezes);
            Assert.True(windows.IsFrozen);
        }

        [Fact]
        public void Opening_the_Guide_with_no_game_running_freezes_nothing()
        {
            Tap(ControllerButton.Guide);

            Assert.Empty(windows.Freezes);
        }

        [Fact]
        public void A_game_with_no_install_directory_isnt_frozen()
        {
            core.OnGameStarted(library.Id("Hades"));
            windows.OpenWindow(@"C:\Elsewhere\Hades.exe");

            Tap(ControllerButton.Guide);

            Assert.Empty(windows.Freezes);
        }

        [Fact]
        public void Reopening_the_Guide_before_it_has_handed_focus_back_doesnt_freeze_again()
        {
            HaloRuns();
            Tap(ControllerButton.Guide);

            // B closes the Guide, but focus waits for B's release; the Guide comes back before then.
            Press(ControllerButton.B);
            Tap(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);
            Assert.Single(windows.Freezes);
            Assert.True(windows.IsFrozen);
        }

        [Fact]
        public void Closing_the_Guide_thaws_the_game_before_focus_is_handed_back()
        {
            HaloRuns();
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.B);

            Assert.False(windows.IsFrozen);
            Assert.Equal(1, windows.Thaws);
            Assert.Equal(halo, windows.Foreground);
            Assert.Empty(windows.HandedForegroundWhileFrozen);
        }

        [Fact]
        public void Returning_to_the_game_from_the_Guide_thaws_it_first()
        {
            HaloRuns();
            playnite.BringToFront();
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal("Halo Infinite", core.Guide.FocusedItem.Title);

            Tap(ControllerButton.A);

            Assert.False(windows.IsFrozen);
            Assert.Equal(halo, windows.Foreground);
            Assert.Empty(windows.HandedForegroundWhileFrozen);
        }

        [Fact]
        public void Quitting_from_the_Guide_thaws_the_game_before_asking_it_to_close()
        {
            HaloRuns();
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.Start);
            Tap(ControllerButton.A);

            Assert.Equal(new[] { halo }, windows.CloseRequests);
            Assert.Empty(windows.QuitWhileFrozen);
            Assert.False(windows.IsFrozen);
        }

        [Fact]
        public void Quit_and_start_thaws_the_game_before_asking_it_to_close()
        {
            HaloRuns();
            core.PlayGame(library.Id("Hades"));
            Assert.True(windows.IsFrozen);

            Tap(ControllerButton.A);

            Assert.Equal(new[] { halo }, windows.CloseRequests);
            Assert.Empty(windows.QuitWhileFrozen);
        }

        [Fact]
        public void A_quitting_game_stays_thawed_with_the_Guide_open_and_through_its_force_quit()
        {
            HaloRuns();
            core.QuitGame(library.Id("Halo Infinite"));

            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal("Force quit Halo Infinite?", core.Guide.Dialog.Title);
            Tap(ControllerButton.A);

            Assert.Empty(windows.Freezes);
            Assert.Equal(new[] { halo }, windows.Killed);
            Assert.Empty(windows.QuitWhileFrozen);
        }

        [Fact]
        public void A_game_that_exits_under_the_Guide_is_thawed_and_not_again_as_the_Guide_closes()
        {
            HaloRuns();
            Tap(ControllerButton.Guide);

            windows.CloseWindow(halo);
            core.OnGameStopped(library.Id("Halo Infinite"));
            Assert.False(windows.IsFrozen);

            Tap(ControllerButton.B);

            Assert.Equal(1, windows.Thaws);
        }

        [Fact]
        public void Playnite_stopping_thaws_the_game()
        {
            HaloRuns();
            Tap(ControllerButton.Guide);

            core.OnApplicationStopped();

            Assert.False(windows.IsFrozen);
        }

        [Fact]
        public void Sleep_from_Power_on_its_own_thaws_the_game_before_handing_it_the_foreground()
        {
            HaloRuns();
            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(0.7));
            Release(ControllerButton.Guide);
            Assert.True(windows.IsFrozen);

            core.ChooseOption(core.Guide.Dialog.Tiles.Single(o => o.Label == "Sleep"));

            Assert.Equal(new[] { PowerCommand.Sleep }, playnite.PowerCommands);
            Assert.False(windows.IsFrozen);
            Assert.Equal(halo, windows.Foreground);
            Assert.Empty(windows.HandedForegroundWhileFrozen);
        }

        [Fact]
        public void Settings_opened_from_the_Guide_thaws_the_game_before_Playnite_comes_to_the_front()
        {
            HaloRuns();
            core.OpenProfile();
            Assert.True(windows.IsFrozen);
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.Settings, core.Guide.FocusedItem.Kind);

            Tap(ControllerButton.A);

            Assert.True(core.Settings.IsShown);
            Assert.False(windows.IsFrozen);
            Assert.Empty(windows.HandedForegroundWhileFrozen);
        }

        [Fact]
        public void Start_up_covering_the_Guide_on_waking_from_sleep_thaws_the_game()
        {
            core.SetAskWhosPlaying(true);
            HaloRuns();
            Tap(ControllerButton.Guide);

            core.OnResumedFromSleep();

            Assert.True(core.Startup.IsShown);
            Assert.False(core.Guide.IsOpen);
            Assert.False(windows.IsFrozen);
        }

        private void HaloRuns()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            halo = windows.OpenWindow(HaloDirectory + @"\HaloInfinite.exe");
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
