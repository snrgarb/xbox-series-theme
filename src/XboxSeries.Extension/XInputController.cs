using System;
using System.Runtime.InteropServices;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the controller's battery, from XInput, as the console reports it for the pad in use.</summary>
    internal sealed class XInputController : IController
    {
        private const byte DeviceTypeGamepad = 0;
        private const byte TypeDisconnected = 0;
        private const byte TypeWired = 1;
        private const byte TypeAlkaline = 2;
        private const byte TypeNiMh = 3;

        /// <summary>
        /// The first of XInput's four pads that is connected and reports its battery. Playnite doesn't say which pad
        /// pressed a button, so a second pad's battery shows only while the first is off.
        /// </summary>
        public ControllerBattery Battery
        {
            get
            {
                for (uint pad = 0; pad < 4; pad++)
                {
                    XINPUT_BATTERY_INFORMATION info;
                    try
                    {
                        if (XInputGetBatteryInformation(pad, DeviceTypeGamepad, out info) != 0)
                        {
                            continue;
                        }
                    }
                    catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
                    {
                        // XInput 1.4 ships with Windows 8 and later; without it there is no battery to show.
                        return ControllerBattery.None;
                    }

                    switch (info.BatteryType)
                    {
                        case TypeDisconnected:
                            continue;
                        case TypeWired:
                            return ControllerBattery.Wired;
                        case TypeAlkaline:
                        case TypeNiMh:
                            return LevelOf(info.BatteryLevel);
                        default:
                            // Connected, but its battery is unknown, as some pads over Bluetooth report: try the next.
                            continue;
                    }
                }

                return ControllerBattery.None;
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
