using System;
using XboxSeries.Extension;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The press that wakes the Idle screen, swallowed whole on the extension's side: its repeats, its release, and its
    // twin, the same pad press Playnite also sends as a key, whichever of the two comes first. The core says whether a
    // press only woke the screen; the swallowing is plain C# (WakeSwallow.cs, compiled in here).
    public class WakeSwallowTests
    {
        private static readonly DateTime At = new DateTime(2026, 10, 20, 12, 0, 0);
        private readonly WakeSwallow<string> swallow = new WakeSwallow<string>();
        private int asked;

        private bool Press(string input, bool woke, double ms = 0, params string[] twins) =>
            swallow.Press(input, At.AddMilliseconds(ms), () => { asked++; return woke; }, twins);

        [Fact]
        public void A_press_that_only_woke_the_screen_is_swallowed_and_one_that_acts_is_not()
        {
            Assert.True(Press("A", woke: true));
            Assert.True(swallow.Release("A"));

            Assert.False(Press("A", woke: false));
            Assert.False(swallow.Release("A"));
        }

        [Fact]
        public void The_wake_press_held_down_stays_swallowed_until_it_is_released_without_asking_the_core_again()
        {
            Press("Down", woke: true);
            Assert.True(Press("Down", woke: false, ms: 700));
            Assert.True(Press("Down", woke: false, ms: 800));
            Assert.Equal(1, asked);

            Assert.True(swallow.Release("Down"));
            Assert.False(Press("Down", woke: false, ms: 900));
        }

        [Fact]
        public void The_twin_of_the_wake_press_is_swallowed_with_its_release_when_it_comes_soon_after()
        {
            Press("pad DPadDown", woke: true, 0, "key Down");

            Assert.True(Press("key Down", woke: false, ms: 20));
            Assert.Equal(1, asked);
            Assert.True(swallow.Release("pad DPadDown"));
            Assert.True(swallow.Release("key Down"));
        }

        [Fact]
        public void A_twin_that_comes_too_late_is_a_press_of_its_own()
        {
            Press("pad DPadDown", woke: true, 0, "key Down");
            swallow.Release("pad DPadDown");

            Assert.False(Press("key Down", woke: false, ms: 1000));
            Assert.Equal(2, asked);
        }

        [Fact]
        public void A_press_that_acts_ends_the_wake_press_so_a_release_lost_swallows_nothing_later()
        {
            Press("Enter", woke: true, 0, "pad A");

            Assert.False(Press("Down", woke: false, ms: 500));
            Assert.False(Press("Enter", woke: false, ms: 600));
            Assert.False(Press("pad A", woke: false, ms: 610));
        }

        [Fact]
        public void Holds_tells_whether_a_wake_press_is_still_down()
        {
            Assert.False(swallow.Holds(i => i.StartsWith("key")));
            Press("key A", woke: true);
            Assert.True(swallow.Holds(i => i.StartsWith("key")));
            Assert.False(swallow.Holds(i => i.StartsWith("pad")));
            swallow.Release("key A");
            Assert.False(swallow.Holds(i => i.StartsWith("key")));
        }
    }
}
