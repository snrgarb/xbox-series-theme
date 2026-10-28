using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>One pad's state as XInput reads it: its buttons, its triggers and its sticks.</summary>
    internal struct PadReading
    {
        public PadReading(ushort buttons, byte leftTrigger, byte rightTrigger, short leftX, short leftY, short rightX, short rightY)
        {
            Buttons = buttons;
            LeftTrigger = leftTrigger;
            RightTrigger = rightTrigger;
            LeftX = leftX;
            LeftY = leftY;
            RightX = rightX;
            RightY = rightY;
        }

        public ushort Buttons { get; }
        public byte LeftTrigger { get; }
        public byte RightTrigger { get; }
        public short LeftX { get; }
        public short LeftY { get; }
        public short RightX { get; }
        public short RightY { get; }
    }

    /// <summary>
    /// What a pad's reading says: whether someone is using it, for the Idle screen, and the buttons it holds down, for the
    /// Start-up sequence; apart from XInput so it can be checked on its own.
    /// </summary>
    internal static class PadActivity
    {
        /// <summary>XINPUT_GAMEPAD_TRIGGER_THRESHOLD: a trigger pressed past it is pressed.</summary>
        public const byte TriggerThreshold = 30;

        /// <summary>XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE: the left stick pushed less far than it, any way, is at rest.</summary>
        public const int LeftStickDeadzone = 7849;

        /// <summary>XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE, the right stick's.</summary>
        public const int RightStickDeadzone = 8689;

        /// <summary>
        /// Whether someone holds <paramref name="pad"/> off its rest: a button down, a trigger past its threshold, or a
        /// stick pushed past its deadzone, measured round it as XInput's guidance has, so part way on both axes counts.
        /// </summary>
        public static bool IsOffRest(PadReading pad) =>
            pad.Buttons != 0
            || pad.LeftTrigger > TriggerThreshold
            || pad.RightTrigger > TriggerThreshold
            || Pushed(pad.LeftX, pad.LeftY, LeftStickDeadzone)
            || Pushed(pad.RightX, pad.RightY, RightStickDeadzone);

        /// <summary>How far a trigger, of 255, moves before it counts as moved rather than as noise.</summary>
        public const int TriggerStep = 16;

        /// <summary>How far a stick's axis, of 32767 each way, moves before it counts as moved rather than as drift.</summary>
        public const int StickStep = 2048;

        /// <summary>
        /// Whether someone has moved the pad from <paramref name="before"/> to <paramref name="now"/>: a button pressed or
        /// let go, or a trigger or a stick moved more than a small step, inside its threshold or not, so easing one counts
        /// and a pad lying still, its readings jittering, never does. XInput's packet number isn't used: it counts the
        /// jitter too.
        /// </summary>
        public static bool MovedSince(PadReading before, PadReading now) =>
            before.Buttons != now.Buttons
            || Moved(before.LeftTrigger, now.LeftTrigger, TriggerStep)
            || Moved(before.RightTrigger, now.RightTrigger, TriggerStep)
            || Moved(before.LeftX, now.LeftX, StickStep)
            || Moved(before.LeftY, now.LeftY, StickStep)
            || Moved(before.RightX, now.RightX, StickStep)
            || Moved(before.RightY, now.RightY, StickStep);

        /// <summary>XInput's button bits as the core names them, the Guide button aside, which XInputGetState never reads.</summary>
        private static readonly KeyValuePair<ushort, ControllerButton>[] ButtonBits =
        {
            new KeyValuePair<ushort, ControllerButton>(0x0001, ControllerButton.DPadUp),
            new KeyValuePair<ushort, ControllerButton>(0x0002, ControllerButton.DPadDown),
            new KeyValuePair<ushort, ControllerButton>(0x0004, ControllerButton.DPadLeft),
            new KeyValuePair<ushort, ControllerButton>(0x0008, ControllerButton.DPadRight),
            new KeyValuePair<ushort, ControllerButton>(0x0010, ControllerButton.Start),
            new KeyValuePair<ushort, ControllerButton>(0x0020, ControllerButton.Back),
            new KeyValuePair<ushort, ControllerButton>(0x0040, ControllerButton.LeftStick),
            new KeyValuePair<ushort, ControllerButton>(0x0080, ControllerButton.RightStick),
            new KeyValuePair<ushort, ControllerButton>(0x0100, ControllerButton.LeftShoulder),
            new KeyValuePair<ushort, ControllerButton>(0x0200, ControllerButton.RightShoulder),
            new KeyValuePair<ushort, ControllerButton>(0x1000, ControllerButton.A),
            new KeyValuePair<ushort, ControllerButton>(0x2000, ControllerButton.B),
            new KeyValuePair<ushort, ControllerButton>(0x4000, ControllerButton.X),
            new KeyValuePair<ushort, ControllerButton>(0x8000, ControllerButton.Y),
        };

        /// <summary>How far the left stick's axis, of 32767 each way, is pushed before it counts as the D-pad, as Playnite's own does.</summary>
        public const int StickAsDPad = 16384;

        /// <summary>
        /// The buttons <paramref name="pad"/> holds down as the core names them, View as Back and Menu as Start, with the
        /// left stick pushed past halfway counting as the D-pad, as it does through Playnite.
        /// </summary>
        public static IReadOnlyCollection<ControllerButton> ButtonsOf(PadReading pad)
        {
            var down = new HashSet<ControllerButton>();
            foreach (var bit in ButtonBits)
            {
                if ((pad.Buttons & bit.Key) != 0)
                {
                    down.Add(bit.Value);
                }
            }

            if (pad.LeftX <= -StickAsDPad)
            {
                down.Add(ControllerButton.DPadLeft);
            }
            else if (pad.LeftX >= StickAsDPad)
            {
                down.Add(ControllerButton.DPadRight);
            }

            if (pad.LeftY >= StickAsDPad)
            {
                down.Add(ControllerButton.DPadUp);
            }
            else if (pad.LeftY <= -StickAsDPad)
            {
                down.Add(ControllerButton.DPadDown);
            }

            return down;
        }

        private static bool Moved(int before, int now, int step) => System.Math.Abs(now - before) > step;

        private static bool Pushed(short x, short y, int deadzone) => (long)x * x + (long)y * y > (long)deadzone * deadzone;
    }

    /// <summary>
    /// The pads in XInput's four slots, each reading compared with that pad's last: one used, held off its rest or moved,
    /// is kept until the core next looks, so a tap between two looks still counts. Read on a timer's thread and looked at
    /// on the UI thread.
    /// </summary>
    internal sealed class PadWatch
    {
        public const int Slots = 4;

        private readonly object gate = new object();
        private readonly PadReading?[] last = new PadReading?[Slots];
        private bool used;

        /// <summary><paramref name="reading"/> read from <paramref name="slot"/>, or null with no pad there.</summary>
        public void Saw(int slot, PadReading? reading)
        {
            lock (gate)
            {
                var before = last[slot];
                last[slot] = reading;
                if (reading is PadReading now
                    && (PadActivity.IsOffRest(now) || (before is PadReading was && PadActivity.MovedSince(was, now))))
                {
                    used = true;
                }
            }
        }

        public bool UsedSinceLastLook()
        {
            lock (gate)
            {
                var seen = used;
                used = false;
                return seen;
            }
        }
    }
}
