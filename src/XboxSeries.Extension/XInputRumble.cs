using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: buzzes every connected XInput pad, since Playnite doesn't say which one pressed the Guide button.
    /// </summary>
    internal sealed class XInputRumble : IControllerRumble
    {
        private const int MaxPads = 4;
        private const ushort Strength = 40000;
        private static readonly TimeSpan Length = TimeSpan.FromMilliseconds(200);

        public void Pulse()
        {
            SetAll(Strength);
            Task.Delay(Length).ContinueWith(_ => SetAll(0));
        }

        private static void SetAll(ushort speed)
        {
            var vibration = new XInputVibration { LeftMotorSpeed = speed, RightMotorSpeed = speed };
            for (var pad = 0; pad < MaxPads; pad++)
            {
                // Pads not connected answer ERROR_DEVICE_NOT_CONNECTED, which needs nothing.
                XInputSetState(pad, ref vibration);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputVibration
        {
            public ushort LeftMotorSpeed;
            public ushort RightMotorSpeed;
        }

        [DllImport("xinput1_4.dll")]
        private static extern int XInputSetState(int userIndex, ref XInputVibration vibration);
    }
}
