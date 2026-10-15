using System;
using System.Runtime.InteropServices;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the controller's battery, from XInput, for pad 1.</summary>
    internal sealed class XInputController : IController
    {
        private const byte DeviceTypeGamepad = 0;
        private const byte TypeWired = 1;
        private const byte TypeAlkaline = 2;
        private const byte TypeNiMh = 3;

        /// <summary>Pad 1's battery: XInput's first pad, as the console shows its own pad's.</summary>
        public ControllerBattery Battery
        {
            get
            {
                XINPUT_BATTERY_INFORMATION info;
                try
                {
                    if (XInputGetBatteryInformation(0, DeviceTypeGamepad, out info) != 0)
                    {
                        return ControllerBattery.None;
                    }
                }
                catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
                {
                    // XInput 1.4 ships with Windows 8 and later; without it there is no battery to show.
                    return ControllerBattery.None;
                }

                switch (info.BatteryType)
                {
                    case TypeWired:
                        return ControllerBattery.Wired;
                    case TypeAlkaline:
                    case TypeNiMh:
                        return LevelOf(info.BatteryLevel);
                    default:
                        // Not connected, or connected with a battery it doesn't report, as some pads over Bluetooth.
                        return ControllerBattery.None;
                }
            }
        }

        private static ControllerBattery LevelOf(byte level)
        {
            switch (level)
            {
                case 0: return ControllerBattery.Empty;
                case 1: return ControllerBattery.Low;
                case 2: return ControllerBattery.Medium;
                default: return ControllerBattery.Full;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_BATTERY_INFORMATION
        {
            public byte BatteryType;
            public byte BatteryLevel;
        }

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputGetBatteryInformation(uint userIndex, byte devType, out XINPUT_BATTERY_INFORMATION batteryInformation);
    }
}
