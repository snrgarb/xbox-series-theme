using System;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // When the Boot shell takes its black down and starts explorer, against a clock the test moves: the black at
    // Start-up's first frame, explorer as the Boot video ends, and each at a limit when a signal never comes.
    public class SignInScheduleTests
    {
        private TimeSpan now = TimeSpan.FromSeconds(0.3);
        private readonly SignInSchedule schedule;

        public SignInScheduleTests()
        {
            schedule = new SignInSchedule(() => now);
        }

        private void At(double seconds) => now = TimeSpan.FromSeconds(seconds);

        [Fact]
        public void Explorer_starts_as_the_Boot_video_ends()
        {
            At(3.3);
            schedule.FirstFrame();
            Assert.False(schedule.StartExplorer());

            At(14.1);
            schedule.BootVideoEnded();
            Assert.True(schedule.StartExplorer());
        }

        [Fact]
        public void Without_the_Boot_videos_end_explorer_starts_20_s_after_the_first_frame()
        {
            At(3.3);
            schedule.FirstFrame();
            Assert.Equal(TimeSpan.FromSeconds(20), schedule.UntilNext);

            At(23.2);
            Assert.False(schedule.StartExplorer());

            At(23.3);
            Assert.True(schedule.StartExplorer());
        }

        [Fact]
        public void With_no_first_frame_the_black_comes_down_and_explorer_starts_at_the_60_s_limit()
        {
            Assert.Equal(TimeSpan.FromSeconds(59.7), schedule.UntilNext);

            At(59.9);
            Assert.False(schedule.TakeBlackDown());
            Assert.False(schedule.StartExplorer());

            At(60);
            Assert.True(schedule.TakeBlackDown());
            Assert.True(schedule.StartExplorer());
            Assert.Null(schedule.UntilNext);
        }

        [Fact]
        public void Explorer_starts_once_per_sign_in_however_late_the_signals_come()
        {
            At(60);
            Assert.True(schedule.TakeBlackDown());
            Assert.True(schedule.StartExplorer());

            At(62);
            schedule.FirstFrame();
            Assert.False(schedule.TakeBlackDown());
            Assert.False(schedule.StartExplorer());

            At(73);
            schedule.BootVideoEnded();
            Assert.False(schedule.StartExplorer());

            At(90);
            Assert.False(schedule.StartExplorer());
            Assert.Null(schedule.UntilNext);
        }

        [Fact]
        public void The_black_comes_down_at_the_first_frame()
        {
            Assert.False(schedule.TakeBlackDown());

            At(3.3);
            schedule.FirstFrame();
            Assert.True(schedule.TakeBlackDown());
        }
    }
}
