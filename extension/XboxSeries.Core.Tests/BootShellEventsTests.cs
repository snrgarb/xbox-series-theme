using System;
using System.Threading;
using XboxSeries.Shared;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Boot shell's two named events, as the Bundle sets them: this test program makes an event as the Boot shell
    // would, under a name of its own so a Boot shell running on this PC never hears it. Plain C# (BootShellEvents.cs,
    // compiled in here), with no Playnite types, so the Boot shell's project can link it too.
    public class BootShellEventsTests
    {
        private readonly string name = @"Local\XboxSeries.BootShell.Tests." + Guid.NewGuid().ToString("N");

        [Fact]
        public void An_event_the_Boot_shell_made_is_set()
        {
            using (var made = new EventWaitHandle(false, EventResetMode.ManualReset, name))
            {
                Assert.True(BootShellEvents.Set(name));

                Assert.True(made.WaitOne(0));
            }
        }

        [Fact]
        public void With_no_Boot_shell_there_is_nothing_to_set()
        {
            Assert.False(BootShellEvents.Set(name));
        }
    }
}
