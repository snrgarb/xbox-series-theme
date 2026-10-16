using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// The console's loading spinner, as Signing you in shows it round the profile's picture: an arc on a ring, white at
    /// its head and green at its tail, that grows clockwise from a dot at the top, its tail chasing its head round, and
    /// rests as the dot again before the next turn. No track ring is drawn under it. Angles are degrees clockwise from
    /// the top.
    /// </summary>
    public static class Spinner
    {
        /// <summary>One turn and its rest.</summary>
        public static readonly TimeSpan Period = TimeSpan.FromSeconds(1.2);

        /// <summary>How long the dot is that the spinner starts and rests as, in degrees.</summary>
        public const double Dot = 6;

        /// <summary>
        /// One turn after another with no rest between them, the Guide's flyouts' spinner: each turn's head sets off as
        /// the last's reaches the top, while that turn's tail is still catching it up.
        /// </summary>
        public static readonly TimeSpan UnbrokenPeriod = TimeSpan.FromSeconds(HeadEnd);

        /// <summary>When in a turn the head is back at the top.</summary>
        private const double HeadEnd = 0.80;

        /// <summary>The arcs <paramref name="elapsed"/> into the spinning, which repeats each <see cref="Period"/>.</summary>
        public static SpinnerArcs At(TimeSpan elapsed) => Turn(Within(elapsed, Period));

        /// <summary>
        /// The arcs <paramref name="elapsed"/> into the unbroken spinning, which repeats each <see cref="UnbrokenPeriod"/>:
        /// while a turn's tail is still where it starts, the last turn's, catching up to the top, is the tail.
        /// </summary>
        public static SpinnerArcs Unbroken(TimeSpan elapsed)
        {
            var seconds = Within(elapsed, UnbrokenPeriod);
            var turn = Turn(seconds);
            var last = Turn(seconds + HeadEnd);
            return turn.WhiteTail > 0
                ? turn
                : new SpinnerArcs(turn.Head, last.WhiteTail - 360, last.GreenTail - 360);
        }

        /// <summary>Seconds into the turn <paramref name="elapsed"/> is in, turns repeating each <paramref name="period"/>.</summary>
        private static double Within(TimeSpan elapsed, TimeSpan period)
        {
            // Time before the start counts back from the end of a turn, so the spinning runs the same either side of it.
            var ticks = elapsed.Ticks % period.Ticks;
            if (ticks < 0)
                ticks += period.Ticks;
            return ticks / (double)TimeSpan.TicksPerSecond;
        }

        /// <summary>The arcs <paramref name="seconds"/> into a turn, resting as the dot once it is done.</summary>
        private static SpinnerArcs Turn(double seconds)
        {
            // Each end eases round once over its own stretch of the turn: the head first, slow to leave the top, fast
            // through the middle and slowing hard into the top again; the point where white turns green and the very end
            // of the green set off later and steeper, so the arc grows long down the right and shrinks back to the dot
            // as they catch the head up. They are kept in order so the green never passes the white, nor the white come
            // within a dot of the head.
            var head = Dot + 360 * Ease(Progress(seconds, 0.00, HeadEnd), 0.70, 0.20, 0.25, 0.95);
            var whiteTail = Math.Min(360 * Ease(Progress(seconds, 0.15, 0.85), 0.75, 0.20, 0.15, 0.60), head - Dot);
            var greenTail = Math.Min(360 * Ease(Progress(seconds, 0.25, 0.90), 0.70, 0.10, 0.15, 0.75), whiteTail);
            return new SpinnerArcs(head, whiteTail, greenTail);
        }

        private static double Progress(double seconds, double start, double end)
        {
            if (seconds <= start)
                return 0;
            if (seconds >= end)
                return 1;
            return (seconds - start) / (end - start);
        }

        /// <summary>
        /// How far along a cubic-bezier ease is at <paramref name="x"/> of the way through, as a key spline or CSS's
        /// cubic-bezier would have it, with the curve's two inner points at (x1, y1) and (x2, y2).
        /// </summary>
        private static double Ease(double x, double x1, double y1, double x2, double y2)
        {
            if (x <= 0)
                return 0;
            if (x >= 1)
                return 1;

            // The curve's x only ever grows along it, so halving the range finds the point on it with this x.
            double low = 0, high = 1;
            for (var i = 0; i < 40; i++)
            {
                var middle = (low + high) / 2;
                if (Bezier(middle, x1, x2) < x)
                    low = middle;
                else
                    high = middle;
            }
            return Bezier((low + high) / 2, y1, y2);
        }

        private static double Bezier(double u, double p1, double p2)
        {
            var v = 1 - u;
            return 3 * p1 * u * v * v + 3 * p2 * u * u * v + u * u * u;
        }
    }

    /// <summary>
    /// Where the spinner's arcs are at one moment: the white arc from <see cref="WhiteTail"/> to <see cref="Head"/>, the
    /// green from <see cref="GreenTail"/> to <see cref="WhiteTail"/>, in degrees clockwise from the top, the head as far
    /// as 360 past the start of a turn.
    /// </summary>
    public struct SpinnerArcs
    {
        public SpinnerArcs(double head, double whiteTail, double greenTail)
        {
            Head = head;
            WhiteTail = whiteTail;
            GreenTail = greenTail;
        }

        public double Head { get; }
        public double WhiteTail { get; }
        public double GreenTail { get; }
    }
}
