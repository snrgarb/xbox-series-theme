using System;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // Explorer once it's the shell: if Windows doesn't bring it back within 10 s of its going, the Boot shell starts it.
    public class ShellWatchTests
    {
        private TimeSpan now;
        private readonly ShellWatch watch;

        public ShellWatchTests()
        {
            watch = new ShellWatch(() => now);
        }

        private void At(double seconds) => now = TimeSpan.FromSeconds(seconds);

        [Fact]
        public void Before_explorer_is_the_shell_no_shell_is_left_alone()
        {
            At(100);
            Assert.False(watch.StartExplorer(shellUp: false));
        }

        [Fact]
        public void A_shell_gone_for_10_s_starts_explorer_once_then_waits_10_s_more()
        {
            At(20);
            watch.StartExplorer(shellUp: true);

            At(300);
            Assert.False(watch.StartExplorer(shellUp: false));
            At(309.9);
            Assert.False(watch.StartExplorer(shellUp: false));
            At(310);
            Assert.True(watch.StartExplorer(shellUp: false));
            At(310.1);
            Assert.False(watch.StartExplorer(shellUp: false));
            At(320);
            Assert.True(watch.StartExplorer(shellUp: false));
        }

        [Fact]
        public void Windows_bringing_explorer_back_in_time_starts_nothing()
        {
            At(20);
            watch.StartExplorer(shellUp: true);
            At(300);
            watch.StartExplorer(shellUp: false);
            At(303);
            watch.StartExplorer(shellUp: true);

            At(315);
            Assert.False(watch.StartExplorer(shellUp: false));
        }
    }
}
