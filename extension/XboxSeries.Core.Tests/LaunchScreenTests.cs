using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Launch screen: a launching game's art over every window, hiding launchers and splash windows until Playnite
    /// says the game is running, as on the console.
    /// </summary>
    public class LaunchScreenTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads steam = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public LaunchScreenTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), steam);
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\Art\halo-bg.jpg", coverImage: @"C:\Art\halo-cover.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), coverImage: @"C:\Art\hades-cover.jpg")
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10));
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void A_launch_shows_the_Launch_screen_with_the_games_background_art()
        {
            Assert.True(core.OnGameStarting(library.Id("Halo Infinite")));

            Assert.True(core.LaunchScreen.IsShown);
            Assert.Equal(library.Id("Halo Infinite"), core.LaunchScreen.Art.GameId);
            Assert.Equal(@"C:\Art\halo-bg.jpg", core.LaunchScreen.Art.Image);
            Assert.False(core.LaunchScreen.Art.IsCover);
        }
        [Fact]
        public void A_game_without_background_art_launches_over_its_cover()
        {
            core.OnGameStarting(library.Id("Hades"));

            Assert.True(core.LaunchScreen.IsShown);
            Assert.Equal(@"C:\Art\hades-cover.jpg", core.LaunchScreen.Art.Image);
            Assert.True(core.LaunchScreen.Art.IsCover);
        }

        [Fact]
        public void A_game_without_any_art_launches_over_no_art()
        {
            core.OnGameStarting(library.Id("Tunic"));

            Assert.True(core.LaunchScreen.IsShown);
            Assert.Null(core.LaunchScreen.Art);
        }

        [Fact]
        public void The_Launch_screen_hides_once_the_game_has_started_keeping_its_art_as_it_fades()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            core.OnGameStarted(library.Id("Halo Infinite"));

            Assert.False(core.LaunchScreen.IsShown);
            Assert.Equal(@"C:\Art\halo-bg.jpg", core.LaunchScreen.Art.Image);
        }
        [Fact]
        public void A_game_without_any_art_still_hides_its_Launch_screen_once_started()
        {
            core.OnGameStarting(library.Id("Tunic"));

            core.OnGameStarted(library.Id("Tunic"));

            Assert.False(core.LaunchScreen.IsShown);
        }
        [Fact]
        public void A_cancelled_launch_hides_the_Launch_screen()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            core.OnGameStartupCancelled(library.Id("Halo Infinite"));

            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void A_game_stopping_before_it_was_reported_started_hides_the_Launch_screen()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void Another_games_events_leave_the_Launch_screen_up()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            core.OnGameStartupCancelled(library.Id("Hades"));
            core.OnGameStopped(library.Id("Hades"));

            Assert.True(core.LaunchScreen.IsShown);
        }
        [Fact]
        public void The_Launch_screen_gives_up_after_90_seconds_so_a_launcher_waiting_for_input_shows()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            clock.Advance(TimeSpan.FromSeconds(89));
            Assert.True(core.LaunchScreen.IsShown);

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void A_fresh_launch_after_a_cancelled_one_gets_the_full_90_seconds()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(60));
            core.OnGameStartupCancelled(library.Id("Halo Infinite"));

            core.OnGameStarting(library.Id("Hades"));
            clock.Advance(TimeSpan.FromSeconds(60));

            Assert.True(core.LaunchScreen.IsShown);
            Assert.Equal(library.Id("Hades"), core.LaunchScreen.Art.GameId);

            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.False(core.LaunchScreen.IsShown);
        }
        [Fact]
        public void A_launch_held_back_while_another_game_runs_shows_no_Launch_screen()
        {
            Play("Halo Infinite");

            Assert.False(core.OnGameStarting(library.Id("Hades")));

            Assert.False(core.LaunchScreen.IsShown);
            Assert.Equal("Quit Halo Infinite and start Hades?", core.Guide.Dialog.Title);
        }

        [Fact]
        public void Playnite_starting_the_game_already_running_shows_no_Launch_screen()
        {
            Play("Halo Infinite");

            Assert.True(core.OnGameStarting(library.Id("Halo Infinite")));

            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void Quit_X_and_start_Y_shows_Ys_Launch_screen_as_Y_launches()
        {
            Play("Halo Infinite");
            core.OnGameStarting(library.Id("Hades"));
            Tap(ControllerButton.A);
            Assert.False(core.LaunchScreen.IsShown);

            core.OnGameStopped(library.Id("Halo Infinite"));
            Assert.True(core.OnGameStarting(library.Id("Hades")));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
            Assert.True(core.LaunchScreen.IsShown);
            Assert.Equal(library.Id("Hades"), core.LaunchScreen.Art.GameId);
        }

        [Fact]
        public void A_launch_the_extension_sends_shows_the_Launch_screen_only_once_Playnite_begins_it()
        {
            // Playnite may never begin it, as for a game that isn't installed, whose install dialog must not be hidden.
            core.PlayGame(library.Id("Hades"));
            Assert.False(core.LaunchScreen.IsShown);

            Assert.True(core.OnGameStarting(library.Id("Hades")));

            Assert.True(core.LaunchScreen.IsShown);
            Assert.Equal(library.Id("Hades"), core.LaunchScreen.Art.GameId);
        }

        [Fact]
        public void The_Guide_button_opens_the_Guide_over_the_Launch_screen()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            Tap(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);
            Assert.True(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void A_launchers_window_holding_the_foreground_for_5_seconds_steps_the_Launch_screen_aside()
        {
            // A EULA, a sign-in or an error the game won't start without: it takes the foreground and waits for an answer.
            core.OnGameStarting(library.Id("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(2));

            windows.OpenWindow(@"C:\Program Files (x86)\Steam\steam.exe");
            clock.Advance(TimeSpan.FromSeconds(4));
            Assert.True(core.LaunchScreen.IsShown);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void A_window_in_front_for_under_5_seconds_leaves_the_Launch_screen_up()
        {
            // A launcher's progress or splash window, gone again before the game starts.
            core.OnGameStarting(library.Id("Halo Infinite"));

            windows.OpenWindow(@"C:\Program Files\Electronic Arts\EA Desktop\EADesktop.exe");
            clock.Advance(TimeSpan.FromSeconds(3));
            playnite.BringToFront();
            clock.Advance(TimeSpan.FromSeconds(3));
            windows.OpenWindow(@"C:\Program Files\Electronic Arts\EA Desktop\EADesktop.exe");
            clock.Advance(TimeSpan.FromSeconds(3));
            playnite.BringToFront();
            clock.Advance(TimeSpan.FromSeconds(30));

            Assert.True(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void Each_window_in_front_counts_its_own_5_seconds()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            windows.OpenWindow(@"C:\Program Files (x86)\Steam\steam.exe");
            clock.Advance(TimeSpan.FromSeconds(4));
            windows.OpenWindow(@"C:\Games\Halo\HaloLauncher.exe");
            clock.Advance(TimeSpan.FromSeconds(4));
            Assert.True(core.LaunchScreen.IsShown);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void Playnites_own_window_in_front_keeps_the_Launch_screen_up_until_the_timeout()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            clock.Advance(TimeSpan.FromSeconds(60));

            Assert.True(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void The_Guide_open_over_the_Launch_screen_doesnt_step_it_aside()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));
            Tap(ControllerButton.Guide);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            clock.Advance(TimeSpan.FromSeconds(20));

            Assert.True(core.Guide.IsOpen);
            Assert.True(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void B_dismisses_the_Launch_screen_as_an_escape_hatch()
        {
            // For a window Windows kept out of the foreground, behind Playnite's.
            core.OnGameStarting(library.Id("Halo Infinite"));

            Tap(ControllerButton.B);

            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void B_with_the_Guide_open_closes_the_Guide_and_leaves_the_Launch_screen_up()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.B);

            Assert.False(core.Guide.IsOpen);
            Assert.True(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void B_with_no_Launch_screen_does_nothing_to_a_later_launch()
        {
            Tap(ControllerButton.B);

            core.OnGameStarting(library.Id("Halo Infinite"));

            Assert.True(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void Once_stepped_aside_the_game_starting_doesnt_bring_it_back_and_the_next_launch_watches_afresh()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));
            windows.OpenWindow(@"C:\Program Files (x86)\Steam\steam.exe");
            clock.Advance(TimeSpan.FromSeconds(6));
            Assert.False(core.LaunchScreen.IsShown);

            core.OnGameStarted(library.Id("Halo Infinite"));
            Assert.False(core.LaunchScreen.IsShown);
            core.OnGameStopped(library.Id("Halo Infinite"));

            playnite.BringToFront();
            core.OnGameStarting(library.Id("Hades"));
            clock.Advance(TimeSpan.FromSeconds(4));
            Assert.True(core.LaunchScreen.IsShown);

            windows.OpenWindow(@"C:\Program Files (x86)\Steam\steam.exe");
            clock.Advance(TimeSpan.FromSeconds(4));
            Assert.True(core.LaunchScreen.IsShown);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void Steams_launch_waiting_on_its_EULA_steps_the_Launch_screen_aside_and_puts_the_EULA_in_front()
        {
            // Steam asks in its main window, which -silent keeps hidden, so no window ever comes to the foreground.
            core.OnGameStarting(library.Id("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.True(core.LaunchScreen.IsShown);

            steam.LaunchesWaitingOnUser.Add(library.Id("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(0.5));

            Assert.False(core.LaunchScreen.IsShown);
            Assert.Equal(new[] { library.Id("Halo Infinite") }, steam.LaunchQuestionsShown);
        }

        [Fact]
        public void A_launch_joining_one_Steam_still_waits_on_steps_aside_at_once()
        {
            // An earlier Play's EULA, never answered: Steam folds the new launch into it and asks nothing new.
            steam.LaunchesWaitingOnUser.Add(library.Id("Halo Infinite"));

            core.OnGameStarting(library.Id("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(0.5));

            Assert.False(core.LaunchScreen.IsShown);
            Assert.Equal(new[] { library.Id("Halo Infinite") }, steam.LaunchQuestionsShown);
        }

        [Fact]
        public void Steams_question_is_put_in_front_once_and_the_game_starting_after_it_is_answered_brings_nothing_back()
        {
            steam.LaunchesWaitingOnUser.Add(library.Id("Halo Infinite"));
            core.OnGameStarting(library.Id("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(30));

            steam.LaunchesWaitingOnUser.Clear();
            core.OnGameStarted(library.Id("Halo Infinite"));

            Assert.False(core.LaunchScreen.IsShown);
            Assert.Single(steam.LaunchQuestionsShown);
        }

        [Fact]
        public void Another_games_launch_waiting_on_Steam_leaves_this_Launch_screen_up()
        {
            steam.LaunchesWaitingOnUser.Add(library.Id("Hades"));

            core.OnGameStarting(library.Id("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(30));

            Assert.True(core.LaunchScreen.IsShown);
            Assert.Empty(steam.LaunchQuestionsShown);
        }

        [Fact]
        public void B_while_Steam_waits_on_the_launch_puts_Steams_question_in_front()
        {
            steam.LaunchesWaitingOnUser.Add(library.Id("Halo Infinite"));
            core.OnGameStarting(library.Id("Halo Infinite"));

            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.False(core.LaunchScreen.IsShown);
            Assert.Equal(new[] { library.Id("Halo Infinite") }, steam.LaunchQuestionsShown);
        }

        [Fact]
        public void B_with_nothing_waiting_on_the_user_only_dismisses_the_Launch_screen()
        {
            core.OnGameStarting(library.Id("Halo Infinite"));

            Tap(ControllerButton.B);

            Assert.False(core.LaunchScreen.IsShown);
            Assert.Empty(steam.LaunchQuestionsShown);
        }

        private void Play(string name)
        {
            var id = library.Id(name);
            Assert.True(core.OnGameStarting(id));
            core.OnGameStarted(id);
        }

        /// <summary>Pressed and let go. A Guide tap opens the Guide 0.3 s after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
