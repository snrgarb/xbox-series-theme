using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class LandingTabTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 13, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public LandingTabTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite);
        }

        [Fact]
        public void Guide_opens_on_the_landing_tab_with_focus_on_Home()
        {
            Tap(ControllerButton.Guide);

            Assert.Equal(new[] { "Home", "My games & apps" }, Titles());
            Assert.Equal("Home", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void Recent_games_are_listed_under_a_divider_newest_first_as_many_as_fit()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Forza Horizon 5", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Never Played", added: new DateTime(2026, 10, 13))
                .WithGame("Hidden Game", lastActivity: new DateTime(2026, 10, 13), hidden: true)
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11))
                .WithGame("Celeste", lastActivity: new DateTime(2026, 9, 1))
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 1))
                .Snapshot());

            Tap(ControllerButton.Guide);

            Assert.Equal(
                new[] { "Home", "My games & apps", "Forza Horizon 5", "Hades", "Halo Infinite", "Tunic" },
                Titles());
            Assert.Equal(new[] { "Forza Horizon 5" }, core.Guide.Items.Where(i => i.HasDividerAbove).Select(i => i.Title));
        }

        [Fact]
        public void The_running_game_is_pinned_first_under_the_divider_and_left_out_of_the_recent_games()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));

            Tap(ControllerButton.Guide);

            Assert.Equal(new[] { "Home", "My games & apps", "Halo Infinite", "Hades" }, Titles());
            var running = core.Guide.Items[2];
            Assert.Equal(GuideItemKind.RunningGame, running.Kind);
            Assert.True(running.HasDividerAbove);
            Assert.False(core.Guide.Items[3].HasDividerAbove);
        }

        [Fact]
        public void Over_the_Dashboard_with_nothing_running_there_is_no_running_game_row()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            core.OnGameStopped(library.Id("Halo Infinite"));
            playnite.BringToFront();

            Tap(ControllerButton.Guide);

            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.RunningGame);
            Assert.Equal(new[] { "Home", "My games & apps", "Halo Infinite" }, Titles());
        }

        [Fact]
        public void D_pad_moves_focus_up_and_down_and_stops_at_the_ends()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .Snapshot());
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.DPadUp);
            Assert.Equal("Home", Focused());

            Tap(ControllerButton.DPadDown);
            Assert.Equal("My games & apps", Focused());

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal("Halo Infinite", Focused());

            Tap(ControllerButton.DPadUp);
            Assert.Equal("My games & apps", Focused());
        }

        [Fact]
        public void Holding_the_D_pad_repeats_focus_after_700_ms_then_every_80_ms_until_released()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 9))
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 8))
                .Snapshot());
            Tap(ControllerButton.Guide);

            Press(ControllerButton.DPadDown);
            Assert.Equal("My games & apps", Focused());

            clock.Advance(TimeSpan.FromMilliseconds(699));
            Assert.Equal("My games & apps", Focused());

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal("Halo Infinite", Focused());

            clock.Advance(TimeSpan.FromMilliseconds(79));
            Assert.Equal("Halo Infinite", Focused());

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal("Hades", Focused());

            Release(ControllerButton.DPadDown);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal("Hades", Focused());
        }

        [Fact]
        public void Hold_repeat_stops_when_the_Guide_closes()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .Snapshot());
            Tap(ControllerButton.Guide);
            Press(ControllerButton.DPadDown);
            Tap(ControllerButton.B);

            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("My games & apps", Focused());
        }

        [Fact]
        public void Reopening_the_Guide_puts_focus_back_on_Home()
        {
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.B);

            Tap(ControllerButton.Guide);

            Assert.Equal("Home", Focused());
        }

        [Fact]
        public void Home_brings_Playnite_to_the_front_on_Home_once_A_is_released_and_the_game_keeps_running()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);

            Press(ControllerButton.A);

            Assert.False(core.Guide.IsOpen);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Release(ControllerButton.A);

            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.Equal(DashboardPage.Home, playnite.Page);
            Assert.True(windows.Exists(game));

            Tap(ControllerButton.Guide);
            Assert.Contains(core.Guide.Items, i => i.Kind == GuideItemKind.RunningGame);
        }

        [Fact]
        public void My_games_and_apps_brings_Playnite_to_the_front_on_that_page()
        {
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.Equal(DashboardPage.MyGamesAndApps, playnite.Page);
        }

        [Fact]
        public void A_on_the_running_game_closes_the_Guide_and_returns_to_the_game()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Press(ControllerButton.A);

            Assert.False(core.Guide.IsOpen);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Release(ControllerButton.A);

            Assert.Equal(game, windows.Foreground);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void A_on_the_running_game_over_the_Dashboard_brings_the_game_to_the_front()
        {
            var library = new FakeLibrary()
                .WithGame("Arma Reforger", lastActivity: new DateTime(2026, 10, 10), installDirectory: @"F:\SteamLibrary\steamapps\common\Arma Reforger");
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Arma Reforger"));
            windows.OpenWindow(@"C:\Program Files (x86)\Steam\steam.exe");
            var game = windows.OpenWindow(@"F:\SteamLibrary\steamapps\common\Arma Reforger\ArmaReforgerSteam.exe");
            playnite.BringToFront();
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void A_on_the_running_game_falls_back_to_the_window_in_front_when_no_game_window_is_found()
        {
            var library = new FakeLibrary()
                .WithGame("Arma Reforger", lastActivity: new DateTime(2026, 10, 10), installDirectory: @"F:\SteamLibrary\steamapps\common\Arma Reforger");
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Arma Reforger"));
            var game = windows.OpenWindow(@"D:\Elsewhere\Arma Reforger Launcher.exe");
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void A_on_a_recent_game_launches_it_once_A_is_released_when_nothing_is_running()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 9));
            core.OnLibraryChanged(library.Snapshot());
            playnite.BringToFront();
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Press(ControllerButton.A);

            Assert.False(core.Guide.IsOpen);
            Assert.Empty(playnite.StartedGames);

            Release(ControllerButton.A);

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
            Assert.Equal(playnite.Window, windows.Foreground);
        }

        [Fact]
        public void A_on_a_recent_game_starts_nothing_while_another_game_is_running()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 9));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal("Hades", Focused());

            Tap(ControllerButton.A);

            Assert.True(core.Guide.IsOpen);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void Pointing_at_an_entry_focuses_it()
        {
            Tap(ControllerButton.Guide);

            core.FocusItem(core.Guide.Items[1]);

            Assert.Equal("My games & apps", Focused());
        }

        [Fact]
        public void Clicking_an_entry_selects_it_and_hands_focus_on_at_once()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10));
            core.OnLibraryChanged(library.Snapshot());
            Tap(ControllerButton.Guide);

            core.ChooseItem(core.Guide.Items[2]);

            Assert.False(core.Guide.IsOpen);
            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(new[] { library.Id("Halo Infinite") }, playnite.StartedGames);
        }

        private string Focused() => core.Guide.FocusedItem.Title;

        private string[] Titles() => core.Guide.Items.Select(i => i.Title).ToArray();

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        private void Tap(ControllerButton button)
        {
            Press(button);
            Release(button);
        }
    }
}
