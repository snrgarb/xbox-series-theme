using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Idle screen's timers: with no input the screen dims after Dim after and shows things after Show things after,
    /// both counted from the last input; the first input after only wakes it.
    /// </summary>
    public class IdleTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeInputActivity input = new FakeInputActivity();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public IdleTests()
        {
            // Playnite's Fullscreen window opens in front.
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads(), inputActivity: input);
        }

        private void Wait(double minutes) => clock.Advance(TimeSpan.FromMinutes(minutes));

        [Fact]
        public void With_no_input_the_screen_dims_at_two_minutes()
        {
            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);

            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            Assert.True(core.Idle.IsDimmed);
        }

        [Fact]
        public void The_Idle_screen_shows_things_at_five_minutes_with_the_dim_still_on()
        {
            Wait(4.99);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            Wait(0.01);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
            Assert.True(core.Idle.IsDimmed);
        }

        [Fact]
        public void Input_wakes_the_screen_and_the_timers_count_again_from_it()
        {
            Wait(1);
            core.OnUserInput();

            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            core.OnUserInput();
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(4.99);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void Dim_after_Never_turns_the_Idle_screen_off()
        {
            core.SetDimAfter(null);

            Wait(60);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
        }

        [Fact]
        public void Show_things_after_Never_stays_dimmed()
        {
            core.SetShowThingsAfter(null);

            Wait(60);

            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void Never_on_both_never_idles()
        {
            core.SetDimAfter(null);
            core.SetShowThingsAfter(null);

            Wait(60);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
        }

        [Fact]
        public void Show_things_after_equal_to_Dim_after_shows_things_and_the_dim_together()
        {
            core.SetShowThingsAfter(TimeSpan.FromMinutes(2));

            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
            Wait(5);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void New_times_count_from_when_they_were_set()
        {
            Wait(1);
            core.SetDimAfter(TimeSpan.FromMinutes(1));

            Wait(0.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void A_pad_press_counts_as_input_too()
        {
            Wait(1);
            Press(ControllerButton.DPadDown);

            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void The_first_key_or_mouse_input_while_dimmed_only_wakes_the_screen()
        {
            Assert.False(core.OnUserInput());
            Wait(2);

            Assert.True(core.OnUserInput());
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.False(core.IsStartupShown);
            Assert.False(core.OnUserInput());
        }

        [Fact]
        public void Showing_things_wakes_the_same_way()
        {
            Wait(5);

            Assert.True(core.OnUserInput());
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
        }

        [Fact]
        public void A_routed_to_a_tile_as_it_wakes_the_screen_is_swallowed_and_focus_stays_on_the_tile()
        {
            var tile = FocusRecentTile();
            Wait(2);

            Assert.True(core.OnUserInput(ControllerButton.A));
            Press(ControllerButton.A);
            Wait(0.1);
            Assert.Empty(playnite.OpenedHubs);

            Press(ControllerButton.A);
            Wait(0.1);
            Assert.Equal(new[] { tile.GameId }, playnite.OpenedHubs);
        }

        [Fact]
        public void A_wake_press_the_pad_reports_before_Playnite_routes_it_is_swallowed_as_routed_too()
        {
            FocusRecentTile();
            Wait(2);

            core.OnControllerButton(ControllerButton.A, ButtonState.Pressed);
            Assert.True(core.OnUserInput(ControllerButton.A));
            core.OnControllerButton(ControllerButton.A, ButtonState.Released);
            Wait(0.1);

            Assert.Empty(playnite.OpenedHubs);
            Assert.False(core.OnUserInput(ControllerButton.A));
        }

        [Fact]
        public void The_Guide_button_tapped_while_dimmed_wakes_the_screen_without_opening_the_Guide()
        {
            Wait(2);

            Press(ControllerButton.Guide);
            Wait(0.1);
            Assert.False(core.Guide.IsOpen);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);

            Press(ControllerButton.Guide);
            Wait(0.1);
            Assert.True(core.Guide.IsOpen);
        }

        [Fact]
        public void B_pressed_while_dimmed_over_the_Guide_leaves_the_Guide_up()
        {
            Press(ControllerButton.Guide);
            Wait(0.1);
            Assert.True(core.Guide.IsOpen);
            Wait(2);
            Assert.True(core.Idle.IsDimmed);

            Assert.True(core.OnUserInput(ControllerButton.B));
            Press(ControllerButton.B);

            Assert.True(core.Guide.IsOpen);
        }

        [Fact]
        public void A_wake_press_that_never_reaches_the_pad_events_swallows_nothing_after_another_button()
        {
            var tile = FocusRecentTile();
            Wait(2);
            core.OnUserInput(ControllerButton.A);

            Press(ControllerButton.DPadRight);
            Press(ControllerButton.A);
            Wait(0.1);

            Assert.Equal(new[] { tile.GameId }, playnite.OpenedHubs);
        }

        [Fact]
        public void A_wake_press_that_never_reaches_the_pad_events_swallows_nothing_a_moment_later()
        {
            var tile = FocusRecentTile();
            Wait(2);
            core.OnUserInput(ControllerButton.A);

            clock.Advance(TimeSpan.FromSeconds(2));
            Press(ControllerButton.A);
            Wait(0.1);

            Assert.Equal(new[] { tile.GameId }, playnite.OpenedHubs);
        }

        [Fact]
        public void Over_a_running_game_the_screen_dims_then_shows_things_counted_from_the_Launch_screen_going()
        {
            var game = FocusRecentTile().GameId;
            Wait(1);
            core.OnGameStarting(game);
            Wait(1.5);
            core.OnGameStarted(game);
            windows.OpenWindow(@"C:\Games\Halo\halo.exe");
            Assert.False(core.LaunchScreen.IsShown);

            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            Wait(3);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void A_game_stopping_neither_wakes_the_screen_nor_resets_the_timers()
        {
            var game = FocusRecentTile().GameId;
            core.OnGameStarting(game);
            core.OnGameStarted(game);
            Wait(1);
            core.OnGameStopped(game);

            Wait(1);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            core.OnGameStarted(game);
            core.OnGameStopped(game);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void A_game_starting_while_dimmed_wakes_the_screen_as_its_Launch_screen_shows()
        {
            var game = FocusRecentTile().GameId;
            Wait(2);

            core.OnGameStarting(game);

            Assert.True(core.LaunchScreen.IsShown);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
        }

        [Fact]
        public void No_idle_while_the_Launch_screen_shows_and_the_timers_count_from_zero_once_it_goes()
        {
            var game = FocusRecentTile().GameId;
            core.SetDimAfter(TimeSpan.FromMinutes(1));
            core.OnGameStarting(game);
            Wait(1.2);
            Assert.True(core.LaunchScreen.IsShown);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);

            core.OnGameStartupCancelled(game);
            Wait(0.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void No_idle_during_Start_up_and_the_timers_count_from_zero_once_it_ends()
        {
            core.OnStartup();
            Wait(10);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);

            core.OnStartupSignedIn(core.Settings.StartUp.Profiles[0]);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.False(core.IsStartupShown);
            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void With_another_app_in_front_the_screen_dims_then_shows_things_all_the_same()
        {
            Wait(1);
            windows.OpenWindow(@"C:\Apps\Browser\browser.exe");

            Wait(0.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            Wait(3);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);

            playnite.BringToFront();
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void Input_outside_Playnite_has_the_timers_count_again_from_it()
        {
            windows.OpenWindow(@"C:\Apps\Browser\browser.exe");
            Wait(1.5);
            input.Use();

            // The core looks for it every second, so it counts from the next look.
            clock.Advance(TimeSpan.FromSeconds(1));
            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void Input_outside_Playnite_wakes_a_dimmed_screen_swallowing_nothing()
        {
            windows.OpenWindow(@"C:\Games\Halo\halo.exe");
            Wait(5);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);

            // Seen at the next look, it wakes the screen once Playnite has had its while to hand on a press of it.
            input.Use();
            clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.False(core.OnUserInput());
            Press(ControllerButton.Guide);
            Wait(0.1);
            Assert.True(core.Guide.IsOpen);
        }

        [Fact]
        public void A_pad_held_outside_Playnite_never_lets_the_screen_dim()
        {
            windows.OpenWindow(@"C:\Games\Forza\forza.exe");
            input.IsHeld = true;

            Wait(60);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
        }

        [Fact]
        public void Input_outside_Playnite_just_before_the_dim_keeps_the_screen_awake()
        {
            // Dim after set half a second after a look, so the dim falls due between two looks.
            windows.OpenWindow(@"C:\Apps\Browser\browser.exe");
            clock.Advance(TimeSpan.FromMilliseconds(500));
            core.SetDimAfter(TimeSpan.FromMinutes(1));
            clock.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromMilliseconds(300));
            input.Use();
            clock.Advance(TimeSpan.FromMilliseconds(300));

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
        }

        [Fact]
        public void A_wake_press_in_Playnite_seen_outside_it_too_is_still_swallowed_whole()
        {
            var tile = FocusRecentTile();
            Wait(2);
            clock.Advance(TimeSpan.FromMilliseconds(500));

            Assert.True(core.OnUserInput(ControllerButton.A));
            input.Use();
            clock.Advance(TimeSpan.FromMilliseconds(600));
            Press(ControllerButton.A);
            Wait(0.1);
            Assert.Empty(playnite.OpenedHubs);

            Press(ControllerButton.A);
            Wait(0.1);
            Assert.Equal(new[] { tile.GameId }, playnite.OpenedHubs);
        }

        [Fact]
        public void Input_seen_outside_Playnite_before_Playnite_hands_it_on_leaves_Playnite_to_wake_the_screen()
        {
            FocusRecentTile();
            Wait(2);
            clock.Advance(TimeSpan.FromMilliseconds(500));

            input.Use();
            clock.Advance(TimeSpan.FromMilliseconds(600));
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            Assert.True(core.OnUserInput(ControllerButton.A));
            Press(ControllerButton.A);
            Wait(0.1);
            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void The_Overlay_in_front_is_Playnites_so_the_screen_dims_over_the_Guide()
        {
            Press(ControllerButton.Guide);
            Wait(2);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void Toasts_neither_wake_the_screen_nor_reset_the_timers()
        {
            core.OnNotificationsChanged(new Notification[0]);
            Wait(1);
            core.OnNotificationsChanged(new[] { new Notification("1", "Library updated") });
            Assert.True(core.Toast.IsShown);
            Wait(1);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            core.OnNotificationsChanged(new[] { new Notification("1", "Library updated"), new Notification("2", "Update ready") });
            Assert.True(core.Toast.IsShown);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        /// <summary>A game on the Recent row, with Home's focus on its tile.</summary>
        [Fact]
        public void A_pad_held_still_while_dimmed_with_Playnite_in_front_keeps_things_from_showing_and_wakes_the_screen()
        {
            FocusRecentTile();
            Wait(2);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            input.IsHeld = true;
            Wait(4);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Null(core.Idle.Card);
        }

        [Fact]
        public void A_timer_falling_due_while_a_wake_seen_outside_Playnite_waits_on_Playnite_leaves_the_stage_alone()
        {
            FocusRecentTile();
            Wait(2);

            // Just before Show things after falls due, the look sees input Playnite has yet to hand on.
            clock.Advance(TimeSpan.FromMinutes(3) - TimeSpan.FromMilliseconds(1100));
            input.Use();
            clock.Advance(TimeSpan.FromMilliseconds(1150));

            Assert.NotEqual(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void The_Guide_buttons_wake_press_over_a_game_seen_by_the_pad_before_Playnite_reports_it_is_still_swallowed()
        {
            windows.OpenWindow(@"C:\Games\Halo\halo.exe");
            Wait(5);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);

            input.Use();
            clock.Advance(TimeSpan.FromSeconds(1));
            Press(ControllerButton.Guide);
            Wait(0.1);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.False(core.Guide.IsOpen);
        }

        private HomeTile FocusRecentTile()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12)).Snapshot());
            var tile = core.Home.RecentRow[0];
            core.FocusHomeTile(tile);
            return tile;
        }

        /// <summary>A pad button pressed and let go, as the pad reports it.</summary>
        private void Press(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }
    }
}
