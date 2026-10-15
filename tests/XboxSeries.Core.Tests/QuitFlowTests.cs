using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class QuitFlowTests
    {
        private const string HaloDirectory = @"C:\Games\Halo Infinite";

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 13, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;
        private readonly IntPtr halo;

        public QuitFlowTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeRumble());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), installDirectory: HaloDirectory)
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11))
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            halo = windows.OpenWindow(HaloDirectory + @"\HaloInfinite.exe");
        }

        [Fact]
        public void Menu_on_the_running_game_offers_Quit()
        {
            OpenGuideOn("Halo Infinite");

            Tap(ControllerButton.Start);

            Assert.Equal("Halo Infinite", core.Guide.Dialog.Title);
            Assert.Equal(new[] { "Quit" }, Options());
            Assert.Equal("Quit", core.Guide.Dialog.FocusedOption.Label);
        }

        [Fact]
        public void Menu_elsewhere_on_the_landing_tab_does_nothing()
        {
            OpenGuideOn("Hades");

            Tap(ControllerButton.Start);

            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void The_Guides_menus_and_prompts_give_each_option_its_icon()
        {
            OpenGuideOn("Halo Infinite");
            Tap(ControllerButton.Start);
            Assert.Equal(new[] { DialogIcon.Quit }, Icons());

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal(new[] { DialogIcon.Quit, DialogIcon.Cancel }, Icons());

            Tap(ControllerButton.B);
            Tap(ControllerButton.Guide);
            OpenGuideOn("Hades");
            Tap(ControllerButton.A);
            Assert.Equal(new[] { DialogIcon.Play, DialogIcon.Cancel }, Icons());
        }

        [Fact]
        public void B_closes_the_menu_without_quitting_and_the_Guide_stays_open()
        {
            OpenGuideOn("Halo Infinite");
            Tap(ControllerButton.Start);

            Tap(ControllerButton.B);

            Assert.Null(core.Guide.Dialog);
            Assert.True(core.Guide.IsOpen);
            Assert.Empty(windows.CloseRequests);
        }

        [Fact]
        public void Quit_sends_a_graceful_close_and_the_row_disappears_once_the_game_stops()
        {
            OpenGuideOn("Halo Infinite");
            Tap(ControllerButton.Start);

            Tap(ControllerButton.A);

            Assert.Equal(new[] { halo }, windows.CloseRequests);
            Assert.Null(core.Guide.Dialog);
            Assert.True(core.Guide.IsOpen);
            Assert.Contains(core.Guide.Items, i => i.Kind == GuideItemKind.RunningGame);

            HaloExits();

            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.RunningGame);
            Assert.True(core.Guide.IsOpen);
            Assert.Empty(windows.Killed);
        }

        [Fact]
        public void A_game_that_closes_in_time_gets_no_force_quit_prompt()
        {
            QuitFromGuide();
            HaloExits();

            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void A_game_that_has_not_stopped_by_the_timeout_gets_a_force_quit_prompt()
        {
            QuitFromGuide();

            clock.Advance(TimeSpan.FromSeconds(9.9));
            Assert.Null(core.Guide.Dialog);

            clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Equal("Force quit Halo Infinite?", core.Guide.Dialog.Title);
            Assert.Equal(new[] { "Force quit", "Cancel" }, Options());
            Assert.Equal("Force quit", core.Guide.Dialog.FocusedOption.Label);
        }

        [Fact]
        public void Confirming_the_force_quit_prompt_force_kills_the_game()
        {
            QuitFromGuide();
            clock.Advance(TimeSpan.FromSeconds(10));

            Tap(ControllerButton.A);

            Assert.Equal(new[] { halo }, windows.Killed);
            Assert.Null(core.Guide.Dialog);

            HaloExits();

            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.RunningGame);
        }

        [Fact]
        public void Declining_the_force_quit_prompt_leaves_the_game_running()
        {
            QuitFromGuide();
            clock.Advance(TimeSpan.FromSeconds(10));

            Tap(ControllerButton.DPadDown);
            Assert.Equal("Cancel", core.Guide.Dialog.FocusedOption.Label);
            Tap(ControllerButton.A);

            Assert.Null(core.Guide.Dialog);
            Assert.Empty(windows.Killed);
            Assert.True(windows.Exists(halo));
            Assert.Contains(core.Guide.Items, i => i.Kind == GuideItemKind.RunningGame);

            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void B_on_the_force_quit_prompt_declines_it()
        {
            QuitFromGuide();
            clock.Advance(TimeSpan.FromSeconds(10));

            Tap(ControllerButton.B);

            Assert.Null(core.Guide.Dialog);
            Assert.True(core.Guide.IsOpen);
            Assert.Empty(windows.Killed);
        }

        [Fact]
        public void The_force_quit_prompt_goes_away_if_the_game_stops_after_all()
        {
            QuitFromGuide();
            clock.Advance(TimeSpan.FromSeconds(10));

            HaloExits();

            Assert.Null(core.Guide.Dialog);
            Assert.Empty(windows.Killed);
        }

        [Fact]
        public void The_force_quit_prompt_reopens_the_Guide_if_it_was_closed_meanwhile()
        {
            QuitFromGuide();
            Tap(ControllerButton.B);
            Assert.False(core.Guide.IsOpen);

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);
            Assert.Equal("Force quit Halo Infinite?", core.Guide.Dialog.Title);
        }

        [Fact]
        public void A_game_that_survives_a_force_kill_gets_the_prompt_again()
        {
            QuitFromGuide();
            clock.Advance(TimeSpan.FromSeconds(10));
            Tap(ControllerButton.A);

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal("Force quit Halo Infinite?", core.Guide.Dialog.Title);
        }

        [Fact]
        public void With_no_game_window_to_kill_the_force_quit_prompt_does_not_keep_coming_back()
        {
            windows.CloseWindow(halo);
            QuitFromGuide();
            Assert.Empty(windows.CloseRequests);
            clock.Advance(TimeSpan.FromSeconds(10));

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.Empty(windows.Killed);
            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void A_game_started_from_elsewhere_mid_quit_ends_the_quit()
        {
            core.PlayGame(library.Id("Hades"));
            Tap(ControllerButton.A);

            core.OnGameStarted(library.Id("Tunic"));
            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.Null(core.Guide.Dialog);
            Assert.Empty(windows.Killed);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void A_on_a_recent_game_while_another_runs_asks_Quit_X_and_start_Y()
        {
            OpenGuideOn("Hades");

            Tap(ControllerButton.A);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal("Quit Halo Infinite and start Hades?", core.Guide.Dialog.Title);
            Assert.Equal(new[] { "Quit and start", "Cancel" }, Options());
            Assert.Empty(windows.CloseRequests);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void Declining_Quit_X_and_start_Y_does_nothing()
        {
            OpenGuideOn("Hades");
            Tap(ControllerButton.A);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Assert.Null(core.Guide.Dialog);
            Assert.True(core.Guide.IsOpen);
            Assert.Empty(windows.CloseRequests);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void Confirming_quits_X_and_launches_Y_only_once_X_has_stopped()
        {
            OpenGuideOn("Hades");
            Tap(ControllerButton.A);

            Tap(ControllerButton.A);

            Assert.Equal(new[] { halo }, windows.CloseRequests);
            Assert.Empty(playnite.StartedGames);
            Assert.True(core.Guide.IsOpen);

            HaloExits();

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
            Assert.False(core.Guide.IsOpen);
            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(playnite.Window, windows.Foreground);
        }

        [Fact]
        public void A_hung_X_goes_through_the_force_quit_prompt_before_Y_launches()
        {
            OpenGuideOn("Hades");
            Tap(ControllerButton.A);
            Tap(ControllerButton.A);

            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal("Force quit Halo Infinite?", core.Guide.Dialog.Title);
            Tap(ControllerButton.A);
            Assert.Empty(playnite.StartedGames);

            HaloExits();

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
        }

        [Fact]
        public void Declining_the_force_quit_prompt_cancels_starting_Y()
        {
            OpenGuideOn("Hades");
            Tap(ControllerButton.A);
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));

            Tap(ControllerButton.B);
            HaloExits();

            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void Closing_the_Guide_declines_Quit_X_and_start_Y()
        {
            OpenGuideOn("Hades");
            Tap(ControllerButton.A);

            Tap(ControllerButton.Guide);

            Assert.False(core.Guide.IsOpen);
            Assert.Null(core.Guide.Dialog);
            Assert.Empty(windows.CloseRequests);
            Assert.Equal(halo, windows.Foreground);
        }

        [Fact]
        public void Two_games_are_never_launched_at_once()
        {
            core.PlayGame(library.Id("Hades"));
            Tap(ControllerButton.A);
            HaloExits();
            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);

            // Hades is launching but hasn't started yet.
            core.PlayGame(library.Id("Tunic"));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void A_launch_that_is_cancelled_frees_the_way_for_another()
        {
            core.OnGameStopped(library.Id("Halo Infinite"));
            core.PlayGame(library.Id("Hades"));

            core.OnGameStartupCancelled(library.Id("Hades"));
            core.PlayGame(library.Id("Tunic"));

            Assert.Equal(new[] { library.Id("Hades"), library.Id("Tunic") }, playnite.StartedGames);
        }

        [Fact]
        public void While_a_game_is_quitting_another_switch_is_ignored()
        {
            core.PlayGame(library.Id("Hades"));
            Tap(ControllerButton.A);

            core.PlayGame(library.Id("Tunic"));
            HaloExits();

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
        }

        [Fact]
        public void The_flow_is_exposed_for_the_Game_hub_and_the_tile_menu()
        {
            playnite.BringToFront();

            core.QuitGame(library.Id("Halo Infinite"));

            Assert.Equal(new[] { halo }, windows.CloseRequests);
            Assert.False(core.Guide.IsOpen);

            HaloExits();
            core.OnGameStarted(library.Id("Hades"));
            core.PlayGame(library.Id("Tunic"));

            Assert.True(core.Guide.IsOpen);
            Assert.Equal("Quit Hades and start Tunic?", core.Guide.Dialog.Title);
        }

        [Fact]
        public void Playing_a_game_when_nothing_runs_starts_it_at_once()
        {
            core.OnGameStopped(library.Id("Halo Infinite"));

            core.PlayGame(library.Id("Hades"));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void A_launch_from_the_Dashboard_with_nothing_running_goes_ahead()
        {
            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.True(core.OnGameStarting(library.Id("Hades")));
            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void A_launch_from_the_Dashboard_while_another_game_runs_is_held_and_asks_Quit_X_and_start_Y()
        {
            playnite.BringToFront();

            Assert.False(core.OnGameStarting(library.Id("Hades")));

            Assert.True(core.Guide.IsOpen);
            Assert.Equal("Quit Halo Infinite and start Hades?", core.Guide.Dialog.Title);

            Tap(ControllerButton.A);
            HaloExits();

            // The Guide's own launch comes back through Playnite's starting event and must go ahead.
            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
            Assert.True(core.OnGameStarting(library.Id("Hades")));
        }

        [Fact]
        public void A_second_launch_from_the_Dashboard_while_the_first_is_starting_is_cancelled()
        {
            core.OnGameStopped(library.Id("Halo Infinite"));
            Assert.True(core.OnGameStarting(library.Id("Hades")));

            Assert.False(core.OnGameStarting(library.Id("Tunic")));

            Assert.Null(core.Guide.Dialog);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void A_launch_from_the_Dashboard_while_a_game_is_quitting_is_cancelled()
        {
            core.QuitGame(library.Id("Halo Infinite"));

            Assert.False(core.OnGameStarting(library.Id("Hades")));

            Assert.Null(core.Guide.Dialog);
            HaloExits();
            Assert.Empty(playnite.StartedGames);
        }

        private string[] Options() => core.Guide.Dialog.Options.Select(o => o.Label).ToArray();

        private DialogIcon[] Icons() => core.Guide.Dialog.Options.Select(o => o.Icon).ToArray();

        private void OpenGuideOn(string title)
        {
            Tap(ControllerButton.Guide);
            while (core.Guide.FocusedItem.Title != title)
            {
                Tap(ControllerButton.DPadDown);
            }
        }

        private void QuitFromGuide()
        {
            OpenGuideOn("Halo Infinite");
            Tap(ControllerButton.Start);
            Tap(ControllerButton.A);
        }

        /// <summary>Halo's process exits and Playnite reports it stopped.</summary>
        private void HaloExits()
        {
            windows.CloseWindow(halo);
            core.OnGameStopped(library.Id("Halo Infinite"));
        }

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        /// <summary>Pressed and let go. A Guide tap opens the Guide half a second after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button)
        {
            Press(button);
            Release(button);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(500));
            }
        }
    }
}
