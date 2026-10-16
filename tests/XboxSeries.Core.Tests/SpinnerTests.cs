using System;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class SpinnerTests
    {
        private const double Tolerance = 25;

        [Fact]
        public void A_turn_begins_as_a_short_white_dot_at_the_top()
        {
            var arcs = Spinner.At(TimeSpan.Zero);

            Assert.Equal(Spinner.Dot, arcs.Head, 6);
            Assert.Equal(0, arcs.WhiteTail, 6);
            Assert.Equal(0, arcs.GreenTail, 6);
            Assert.InRange(Spinner.Dot, 3, 10);
        }

        [Theory]
        [InlineData(0.90)]
        [InlineData(1.00)]
        [InlineData(1.10)]
        [InlineData(1.19)]
        public void After_the_turn_it_rests_as_the_dot_at_the_top(double seconds)
        {
            var arcs = Spinner.At(TimeSpan.FromSeconds(seconds));

            Assert.Equal(360 + Spinner.Dot, arcs.Head, 6);
            Assert.Equal(360, arcs.WhiteTail, 6);
            Assert.Equal(360, arcs.GreenTail, 6);
        }

        [Fact]
        public void Each_turn_repeats_the_last()
        {
            for (var ms = 0; ms < 1200; ms += 7)
            {
                var first = Spinner.At(TimeSpan.FromMilliseconds(ms));
                var later = Spinner.At(TimeSpan.FromMilliseconds(ms) + Spinner.Period + Spinner.Period + Spinner.Period);

                AssertSameAngle(first.Head, later.Head);
                AssertSameAngle(first.WhiteTail, later.WhiteTail);
                AssertSameAngle(first.GreenTail, later.GreenTail);
            }
        }

        [Fact]
        public void The_green_stays_behind_the_white_and_the_white_behind_the_head()
        {
            for (var ms = 0; ms <= 1200; ms++)
            {
                var arcs = Spinner.At(TimeSpan.FromMilliseconds(ms));

                Assert.True(arcs.GreenTail <= arcs.WhiteTail, $"green passed white at {ms} ms");
                Assert.True(arcs.WhiteTail <= arcs.Head - Spinner.Dot + 1e-9, $"white came within a dot of the head at {ms} ms");
                Assert.InRange(arcs.GreenTail, 0, 360);
                Assert.InRange(arcs.Head, Spinner.Dot, 360 + Spinner.Dot);
            }
        }

        [Fact]
        public void Nothing_goes_backwards_within_a_turn()
        {
            var last = Spinner.At(TimeSpan.Zero);
            for (var ms = 1; ms < 1200; ms++)
            {
                var arcs = Spinner.At(TimeSpan.FromMilliseconds(ms));

                Assert.True(arcs.Head >= last.Head, $"head went back at {ms} ms");
                Assert.True(arcs.WhiteTail >= last.WhiteTail, $"white tail went back at {ms} ms");
                Assert.True(arcs.GreenTail >= last.GreenTail, $"green tail went back at {ms} ms");
                last = arcs;
            }
        }

        [Theory]
        [InlineData(0.167, 35, 5, 0)]
        [InlineData(0.233, 60, 15, 0)]
        [InlineData(0.300, 105, 30, 10)]
        [InlineData(0.367, 175, 50, 25)]
        [InlineData(0.433, 270, 90, 30)]
        [InlineData(0.467, 290, 145, 65)]
        [InlineData(0.500, 305, 195, 90)]
        [InlineData(0.533, 325, 230, 145)]
        [InlineData(0.850, 360, 360, 360)]
        public void It_follows_the_console_through_a_turn(double seconds, double head, double whiteTail, double greenTail)
        {
            var arcs = Spinner.At(TimeSpan.FromSeconds(seconds));

            Assert.InRange(arcs.Head, head - Tolerance, head + Tolerance);
            Assert.InRange(arcs.WhiteTail, whiteTail - Tolerance, whiteTail + Tolerance);
            Assert.InRange(arcs.GreenTail, greenTail - Tolerance, greenTail + Tolerance);
        }

        [Fact]
        public void Time_before_the_start_and_long_after_it_still_lands_in_a_turn()
        {
            var before = Spinner.At(TimeSpan.FromSeconds(-0.7));
            var matching = Spinner.At(TimeSpan.FromSeconds(0.5));
            AssertSameAngle(matching.Head, before.Head);
            AssertSameAngle(matching.WhiteTail, before.WhiteTail);
            AssertSameAngle(matching.GreenTail, before.GreenTail);

            foreach (var elapsed in new[] { TimeSpan.MaxValue, TimeSpan.MinValue, TimeSpan.FromDays(10000) })
            {
                var arcs = Spinner.At(elapsed);
                Assert.False(double.IsNaN(arcs.Head) || double.IsNaN(arcs.WhiteTail) || double.IsNaN(arcs.GreenTail));
                Assert.True(arcs.GreenTail <= arcs.WhiteTail && arcs.WhiteTail <= arcs.Head - Spinner.Dot + 1e-9);
            }
        }

        private static void AssertSameAngle(double expected, double actual)
        {
            var difference = ((expected - actual) % 360 + 360) % 360;
            Assert.True(difference < 1e-6 || difference > 360 - 1e-6, $"expected {expected} but was {actual}");
        }
    }
}
