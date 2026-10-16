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

        /// <summary>The arcs <paramref name="elapsed"/> into the spinning, which repeats each <see cref="Period"/>.</summary>
        public static SpinnerArcs At(TimeSpan elapsed)
        {
            throw new NotImplementedException();
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
