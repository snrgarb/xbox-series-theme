using System;

namespace XboxSeries.BootShell
{
    /// <summary>What a pad's buttons have done, as <see cref="PadWatch"/> reads them.</summary>
    [Flags]
    internal enum PadPresses
    {
        None = 0,

        /// <summary>The Guide button went down.</summary>
        Guide = 1,

        /// <summary>View and Menu have been held together for 5 s.</summary>
        Hold = 2,
    }

    /// <summary>
    /// The Boot shell's own reading of each pad's buttons, read through XInput by <see cref="XInputPad"/>: a Guide press
    /// as it goes down, and View+Menu held for 5 s, which fires once until they're let go.
    /// </summary>
    internal sealed class PadWatch
    {
        public const ushort Menu = 0x0010;
        public const ushort View = 0x0020;
        public const ushort GuideButton = 0x0400;
        public const int Pads = 4;
        public static readonly TimeSpan HoldFor = TimeSpan.FromSeconds(5);

        private readonly Func<TimeSpan> elapsed;
        private readonly Action<string> log;
        private readonly ushort[] was = new ushort[Pads];
        private readonly TimeSpan?[] heldSince = new TimeSpan?[Pads];
        private readonly bool[] fired = new bool[Pads];

        /// <param name="elapsed">The time since the Boot shell started.</param>
        /// <param name="log">Writes one line to the Boot shell's log.</param>
        public PadWatch(Func<TimeSpan> elapsed, Action<string> log)
        {
            this.elapsed = elapsed;
            this.log = log;
        }

        /// <summary>Reads pad <paramref name="pad"/>'s <paramref name="buttons"/>, null while it isn't connected.</summary>
        public PadPresses Read(int pad, ushort? buttons)
        {
            var now = buttons ?? 0;
            var then = was[pad];
            was[pad] = now;
            var presses = PadPresses.None;

            if ((now & GuideButton) != 0 && (then & GuideButton) == 0)
            {
                log($"pad {pad}: Guide button down");
                presses |= PadPresses.Guide;
            }

            if ((now & (View | Menu)) != (View | Menu))
            {
                if (heldSince[pad] is TimeSpan since && !fired[pad])
                {
                    log($"pad {pad}: View+Menu let go after {(elapsed() - since).TotalSeconds:0.0} s: nothing");
                }

                heldSince[pad] = null;
                fired[pad] = false;
                return presses;
            }

            if (heldSince[pad] is not TimeSpan start)
            {
                heldSince[pad] = elapsed();
                log($"pad {pad}: View+Menu down");
                return presses;
            }

            if (!fired[pad] && elapsed() - start >= HoldFor)
            {
                fired[pad] = true;
                log($"pad {pad}: View+Menu held {HoldFor.TotalSeconds} s");
                presses |= PadPresses.Hold;
            }

            return presses;
        }
    }
}
