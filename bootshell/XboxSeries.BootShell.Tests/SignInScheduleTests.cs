using System;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // When the Boot shell starts explorer, against a clock the test moves: as the Boot video ends, and at a limit when a
    // signal never comes.
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

            At(23.2);
            Assert.False(schedule.StartExplorer());

            At(23.3);
            Assert.True(schedule.StartExplorer());
        }

        [Fact]
        public void With_no_first_frame_explorer_starts_at_the_60_s_limit()
        {
            At(59.9);
            Assert.False(schedule.StartExplorer());

            At(60);
            Assert.True(schedule.StartExplorer());
        }

        [Fact]
        public void Explorer_starts_once_per_sign_in_however_late_the_signals_come()
        {
            At(60);
            Assert.True(schedule.StartExplorer());

            At(62);
            schedule.FirstFrame();
            Assert.False(schedule.StartExplorer());

            At(73);
            schedule.BootVideoEnded();
            Assert.False(schedule.StartExplorer());

            At(90);
            Assert.False(schedule.StartExplorer());
        }
    }
}
