using System;
using System.Collections.Generic;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // How the Boot shell keeps Fullscreen up, against Playnite processes and a clock the test makes up: what each exit
    // was, when it relaunches, when the black is up, and the fallback to the desktop after three crashes in 5 minutes.
    public class WatchdogTests
    {
        private static readonly PlayniteProcess Fullscreen1 = new(101, PlayniteKind.Fullscreen);
        private static readonly PlayniteProcess Fullscreen2 = new(102, PlayniteKind.Fullscreen);
        private static readonly PlayniteProcess Updater = new(201, PlayniteKind.Updater);

        private TimeSpan now = TimeSpan.FromSeconds(0.3);
        private readonly List<string> log = new();
        private readonly Watchdog watchdog;

        public WatchdogTests()
        {
            watchdog = new Watchdog(() => now, log.Add);
        }

        private void At(double seconds) => now = TimeSpan.FromSeconds(seconds);

        /// <summary>The Boot shell launches a Fullscreen, and Start-up shows its first frame.</summary>
        private void Launched(int pid, double firstFrameAt)
        {
            watchdog.Launched(pid);
            At(firstFrameAt);
            watchdog.FirstFrame();
        }

        [Fact]
        public void A_crash_relaunches_Fullscreen_at_once_behind_the_black()
        {
            Launched(101, firstFrameAt: 4);
            Assert.False(watchdog.Black);

            At(600);
            Assert.Equal(Orders.Relaunch, watchdog.Exited(101, -1));
            Assert.True(watchdog.Black);
        }

        [Fact]
        public void A_restart_waits_for_the_Fullscreen_that_follows_and_covers_its_start()
        {
            Launched(101, firstFrameAt: 4);

            // Playnite starts what follows it before it exits.
            At(600);
            Assert.Equal(Orders.None, watchdog.Appeared(Fullscreen2));
            Assert.False(watchdog.Black);

            At(600.1);
            Assert.Equal(Orders.ResetEvents, watchdog.Exited(101, 0));
            Assert.True(watchdog.Black);

            At(604);
            watchdog.FirstFrame();
            Assert.False(watchdog.Black);
        }

        [Fact]
        public void An_update_is_never_relaunched_over_while_update_exe_runs()
        {
            Launched(101, firstFrameAt: 4);

            At(600);
            watchdog.Appeared(Updater);
            Assert.Equal(Orders.None, watchdog.Exited(101, 0));
            Assert.False(watchdog.Black);
            At(640);
            Assert.Equal(Orders.None, watchdog.Tick());

            // The updater starts the new Fullscreen, then exits.
            At(650);
            Assert.Equal(Orders.ResetEvents, watchdog.Appeared(Fullscreen2));
            Assert.True(watchdog.Black);
            Assert.Equal(Orders.None, watchdog.Exited(201, 0));
            Assert.True(watchdog.Black);
        }

        [Fact]
        public void A_crash_while_update_exe_runs_waits_for_it()
        {
            Launched(101, firstFrameAt: 4);

            At(600);
            watchdog.Appeared(Updater);
            Assert.Equal(Orders.None, watchdog.Exited(101, -1));
        }

        /// <summary>The Fullscreen <paramref name="pid"/> crashes at <paramref name="seconds"/>, and the Boot shell relaunches it.</summary>
        private Orders CrashAt(double seconds, int pid, int relaunchedAs)
        {
            At(seconds);
            var orders = watchdog.Exited(pid, -1);
            if (orders.HasFlag(Orders.Relaunch))
            {
                Launched(relaunchedAs, firstFrameAt: seconds + 4);
            }

            return orders;
        }

        [Fact]
        public void The_third_crash_in_5_minutes_shows_the_line_for_5_s_then_starts_the_desktop()
        {
            Launched(101, firstFrameAt: 4);
            Assert.Equal(Orders.Relaunch, CrashAt(100, 101, relaunchedAs: 102));
            Assert.Equal(Orders.Relaunch, CrashAt(200, 102, relaunchedAs: 103));

            Assert.Equal(Orders.None, CrashAt(399, 103, relaunchedAs: 104));
            Assert.True(watchdog.Black);
            Assert.Equal("Playnite closed 3 times in 5 minutes. Press the Guide button to try again.", watchdog.Line);
            Assert.Contains(log, line => line.Contains("fallback"));

            At(403.9);
            Assert.Equal(Orders.None, watchdog.Tick());
            Assert.True(watchdog.Black);

            At(404);
            Assert.Equal(Orders.StartDesktop, watchdog.Tick());
            Assert.Null(watchdog.Line);
            Assert.False(watchdog.Black);

            At(405);
            Assert.Equal(Orders.None, watchdog.Tick());
        }

        [Fact]
        public void After_the_fallback_nothing_is_relaunched_on_its_own()
        {
            Launched(101, firstFrameAt: 4);
            CrashAt(100, 101, relaunchedAs: 102);
            CrashAt(110, 102, relaunchedAs: 103);
            CrashAt(120, 103, relaunchedAs: 104);
            At(125);
            watchdog.Tick();

            // The user starts Fullscreen from the desktop, and it crashes too.
            At(200);
            watchdog.Appeared(new PlayniteProcess(105, PlayniteKind.Fullscreen));
            At(204);
            watchdog.FirstFrame();
            At(300);
            Assert.Equal(Orders.None, watchdog.Exited(105, -1));
            Assert.False(watchdog.Black);
        }

        [Fact]
        public void Two_crashes_6_minutes_apart_dont_fall_back()
        {
            Launched(101, firstFrameAt: 4);
            Assert.Equal(Orders.Relaunch, CrashAt(100, 101, relaunchedAs: 102));
            Assert.Equal(Orders.Relaunch, CrashAt(460, 102, relaunchedAs: 103));
            Assert.Equal(Orders.Relaunch, CrashAt(470, 103, relaunchedAs: 104));
            Assert.Null(watchdog.Line);
        }

        [Fact]
        public void The_crash_windows_Restart_counts_toward_the_limit_though_its_Playnite_is_waited_for()
        {
            Launched(101, firstFrameAt: 4);
            CrashAt(100, 101, relaunchedAs: 102);

            // The crash window's Restart starts a Fullscreen, then kills the one that crashed.
            At(200);
            watchdog.Appeared(new PlayniteProcess(103, PlayniteKind.Fullscreen));
            Assert.Equal(Orders.ResetEvents, watchdog.Exited(102, -1));
            At(204);
            watchdog.FirstFrame();

            Assert.Equal(Orders.None, CrashAt(300, 103, relaunchedAs: 104));
            Assert.NotNull(watchdog.Line);
        }

        [Fact]
        public void Exit_Playnite_leaves_it_down()
        {
            Launched(101, firstFrameAt: 4);

            At(600);
            Assert.Equal(Orders.None, watchdog.Exited(101, 0));
            Assert.False(watchdog.Black);

            At(700);
            Assert.Equal(Orders.None, watchdog.Tick());
        }

        [Fact]
        public void An_exit_before_the_first_frame_is_a_crash_whatever_its_code()
        {
            watchdog.Launched(101);

            At(3);
            Assert.Equal(Orders.Relaunch, watchdog.Exited(101, 0));
            Assert.True(watchdog.Black);
        }

        [Fact]
        public void The_black_for_a_start_lifts_at_the_60_s_limit()
        {
            Launched(101, firstFrameAt: 4);
            At(600);
            watchdog.Exited(101, -1);
            watchdog.Launched(102);

            At(659.9);
            watchdog.Tick();
            Assert.True(watchdog.Black);

            At(660);
            watchdog.Tick();
            Assert.False(watchdog.Black);
        }

        [Fact]
        public void A_start_with_no_first_frame_by_the_60_s_limit_can_still_be_exited()
        {
            watchdog.Launched(101);
            At(60.3);
            watchdog.Tick();

            At(600);
            Assert.Equal(Orders.None, watchdog.Exited(101, 0));
        }

        [Fact]
        public void Playnites_crash_window_lifts_the_black_at_once()
        {
            watchdog.Launched(101);

            At(5);
            watchdog.CrashWindowShown();
            Assert.False(watchdog.Black);
        }

        [Fact]
        public void A_safestartup_start_gets_no_black()
        {
            Launched(101, firstFrameAt: 4);

            At(600);
            watchdog.Appeared(new PlayniteProcess(102, PlayniteKind.Fullscreen, SafeStartup: true));
            Assert.Equal(Orders.None, watchdog.Exited(101, 0));
            Assert.False(watchdog.Black);

            // Its Exit is a quit the user meant, with no first frame to wait for.
            At(700);
            Assert.Equal(Orders.None, watchdog.Exited(102, 0));
        }

        [Fact]
        public void A_switch_to_Desktop_mode_gets_no_black_and_switching_back_does()
        {
            Launched(101, firstFrameAt: 4);

            At(600);
            watchdog.Appeared(new PlayniteProcess(301, PlayniteKind.Desktop));
            Assert.Equal(Orders.None, watchdog.Exited(101, 0));
            Assert.False(watchdog.Black);

            At(700);
            Assert.Equal(Orders.ResetEvents, watchdog.Appeared(Fullscreen2));
            Assert.Equal(Orders.None, watchdog.Exited(301, 0));
            Assert.True(watchdog.Black);
        }

        [Fact]
        public void After_the_fallback_a_Guide_press_starts_Fullscreen_and_resets_the_crash_count()
        {
            Launched(101, firstFrameAt: 4);
            CrashAt(100, 101, relaunchedAs: 102);
            CrashAt(110, 102, relaunchedAs: 103);
            CrashAt(120, 103, relaunchedAs: 104);
            At(125);
            watchdog.Tick();

            At(130);
            Assert.Equal(Orders.Relaunch, watchdog.GuidePressed());
            Assert.True(watchdog.Black);
            Launched(105, firstFrameAt: 134);

            // Two crashes more within the same 5 minutes are relaunched, as the count started again.
            Assert.Equal(Orders.Relaunch, CrashAt(140, 105, relaunchedAs: 106));
            Assert.Equal(Orders.Relaunch, CrashAt(150, 106, relaunchedAs: 107));
            Assert.Null(watchdog.Line);
        }

        [Fact]
        public void A_Guide_press_during_the_fallbacks_line_takes_it_down_and_starts_no_desktop()
        {
            Launched(101, firstFrameAt: 4);
            CrashAt(100, 101, relaunchedAs: 102);
            CrashAt(110, 102, relaunchedAs: 103);
            CrashAt(120, 103, relaunchedAs: 104);

            At(121);
            Assert.Equal(Orders.Relaunch, watchdog.GuidePressed());
            Assert.Null(watchdog.Line);
            watchdog.Launched(105);
            At(125);
            Assert.Equal(Orders.None, watchdog.Tick());
            Assert.True(watchdog.Black);
        }

        [Fact]
        public void After_Exit_Playnite_a_Guide_press_starts_Fullscreen()
        {
            Launched(101, firstFrameAt: 4);
            At(600);
            watchdog.Exited(101, 0);

            At(700);
            Assert.Equal(Orders.Relaunch, watchdog.GuidePressed());
        }

        [Fact]
        public void In_Desktop_mode_a_Guide_press_starts_Fullscreen_for_Playnite_to_switch_back()
        {
            Launched(101, firstFrameAt: 4);
            At(600);
            watchdog.Appeared(new PlayniteProcess(301, PlayniteKind.Desktop));
            watchdog.Exited(101, 0);

            At(700);
            Assert.Equal(Orders.Relaunch, watchdog.GuidePressed());
        }

        [Fact]
        public void While_Fullscreen_runs_the_Guide_button_is_Playnites()
        {
            Launched(101, firstFrameAt: 4);

            At(600);
            Assert.Equal(Orders.None, watchdog.GuidePressed());
            Assert.False(watchdog.Black);
        }

        [Fact]
        public void While_update_exe_runs_a_Guide_press_starts_nothing()
        {
            Launched(101, firstFrameAt: 4);
            At(600);
            watchdog.Appeared(Updater);
            watchdog.Exited(101, 0);

            Assert.Equal(Orders.None, watchdog.GuidePressed());
        }

        [Fact]
        public void The_hold_closes_Playnite_to_the_desktop_and_it_stays_down()
        {
            Launched(101, firstFrameAt: 4);
            At(600);
            watchdog.Appeared(Updater);

            Assert.Equal(Orders.ClosePlaynite | Orders.StartDesktop, watchdog.Held());
            Assert.Equal(new[] { 101 }, watchdog.Closing);
            Assert.False(watchdog.Black);

            At(602);
            Assert.Equal(Orders.None, watchdog.Exited(101, 0));
            Assert.Empty(watchdog.Closing);
            At(610);
            Assert.Equal(Orders.None, watchdog.Tick());
        }

        [Fact]
        public void Under_the_black_at_sign_in_the_hold_lifts_it_and_the_exit_before_the_first_frame_isnt_a_crash()
        {
            watchdog.Launched(101);

            At(3);
            Assert.Equal(Orders.ClosePlaynite | Orders.StartDesktop, watchdog.Held());
            Assert.False(watchdog.Black);

            At(4);
            Assert.Equal(Orders.None, watchdog.Exited(101, 0));
            Assert.False(watchdog.Black);
        }

        [Fact]
        public void A_Playnite_still_up_10_s_after_the_hold_is_ended_and_that_isnt_a_crash()
        {
            Launched(101, firstFrameAt: 4);
            CrashAt(100, 101, relaunchedAs: 102);
            CrashAt(110, 102, relaunchedAs: 103);

            At(120);
            watchdog.Held();
            At(129.9);
            Assert.Equal(Orders.None, watchdog.Tick());
            At(130);
            Assert.Equal(Orders.EndPlaynite, watchdog.Tick());
            At(130.1);
            Assert.Equal(Orders.None, watchdog.Tick());

            Assert.Equal(Orders.None, watchdog.Exited(103, 1));
            Assert.Null(watchdog.Line);
            Assert.False(watchdog.Black);
        }

        [Fact]
        public void With_no_Playnite_up_the_hold_only_starts_the_desktop()
        {
            Launched(101, firstFrameAt: 4);
            At(600);
            watchdog.Exited(101, 0);

            Assert.Equal(Orders.StartDesktop, watchdog.Held());
        }

        [Fact]
        public void The_hold_during_the_fallbacks_line_takes_it_down_and_starts_the_desktop_at_once()
        {
            Launched(101, firstFrameAt: 4);
            CrashAt(100, 101, relaunchedAs: 102);
            CrashAt(110, 102, relaunchedAs: 103);
            CrashAt(120, 103, relaunchedAs: 104);

            At(121);
            Assert.Equal(Orders.StartDesktop, watchdog.Held());
            Assert.Null(watchdog.Line);
            Assert.False(watchdog.Black);
            At(125);
            Assert.Equal(Orders.None, watchdog.Tick());
        }
    }
}