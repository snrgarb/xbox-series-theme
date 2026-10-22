using System;
using System.Collections.Generic;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // The Boot shell's own reading of the pad, against buttons and a clock the test makes up: a Guide press, and
    // View+Menu held for 5 s.
    public class PadWatchTests
    {
        private const ushort ViewMenu = PadWatch.View | PadWatch.Menu;

        private TimeSpan now;
        private readonly List<string> log = new();
        private readonly PadWatch pad;

        public PadWatchTests()
        {
            pad = new PadWatch(() => now, log.Add);
        }

        private void At(double seconds) => now = TimeSpan.FromSeconds(seconds);

        [Fact]
        public void View_and_Menu_held_for_5_s_fires_once()
        {
            At(10);
            Assert.Equal(PadPresses.None, pad.Read(0, ViewMenu));
            At(14.9);
            Assert.Equal(PadPresses.None, pad.Read(0, ViewMenu));
            At(15);
            Assert.Equal(PadPresses.Hold, pad.Read(0, ViewMenu));
            At(25);
            Assert.Equal(PadPresses.None, pad.Read(0, ViewMenu));
        }

        [Fact]
        public void A_hold_let_go_sooner_does_nothing_and_the_next_counts_from_its_own_start()
        {
            At(10);
            pad.Read(0, ViewMenu);
            At(14);
            Assert.Equal(PadPresses.None, pad.Read(0, PadWatch.View));
            At(15);
            Assert.Equal(PadPresses.None, pad.Read(0, ViewMenu));
            At(19.9);
            Assert.Equal(PadPresses.None, pad.Read(0, ViewMenu));
            At(20);
            Assert.Equal(PadPresses.Hold, pad.Read(0, ViewMenu));
        }

        [Fact]
        public void A_hold_on_a_pad_that_disconnects_does_nothing()
        {
            At(10);
            pad.Read(1, ViewMenu);
            At(12);
            pad.Read(1, null);
            At(16);
            Assert.Equal(PadPresses.None, pad.Read(1, ViewMenu));
        }

        [Fact]
        public void The_Guide_button_is_pressed_once_as_it_goes_down()
        {
            Assert.Equal(PadPresses.Guide, pad.Read(0, PadWatch.GuideButton));
            Assert.Equal(PadPresses.None, pad.Read(0, PadWatch.GuideButton));
            Assert.Equal(PadPresses.None, pad.Read(0, 0));
            Assert.Equal(PadPresses.Guide, pad.Read(2, PadWatch.GuideButton | PadWatch.View));
        }
    }
}
