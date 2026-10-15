using System;
using System.Linq;
using Playnite.SDK;
using Windows.Gaming.Input;
using Windows.System.Power;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: pad 1's battery, from Windows.Gaming.Input. XInput isn't used: it doesn't report the battery of a pad on
    /// Bluetooth, as Xbox pads on a PC usually are.
    /// </summary>
    internal sealed class GamingInputController : IController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private bool unavailable;

        /// <summary>
        /// Raised, on any thread, as a pad connects or goes. Windows also finds the pads already connected this way, a
        /// moment after the first use, so the battery shows soon after Playnite starts and not on the next minute.
        /// </summary>
        public event Action Changed;

        public GamingInputController()
        {
            try
            {
                Gamepad.GamepadAdded += (_, __) => Changed?.Invoke();
                Gamepad.GamepadRemoved += (_, __) => Changed?.Invoke();
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // Windows.Gaming.Input ships with Windows 10 and later; without it there is no battery to show.
                unavailable = true;
                logger.Warn(e, "Windows.Gaming.Input isn't available; the status area shows no battery");
            }
        }

        public ControllerBattery Battery
        {
            get
            {
                if (unavailable)
                {
                    return ControllerBattery.None;
                }

                try
                {
                    var pad = Gamepad.Gamepads.FirstOrDefault();
                    if (pad == null)
                    {
                        return ControllerBattery.None;
                    }

                    if (!pad.IsWireless)
                    {
                        return ControllerBattery.Wired;
                    }

                    var report = pad.TryGetBatteryReport();
                    var remaining = report?.RemainingCapacityInMilliwattHours;
                    var full = report?.FullChargeCapacityInMilliwattHours;
                    if (report == null || report.Status == BatteryStatus.NotPresent || remaining == null || full == null || full <= 0)
                    {
                        return ControllerBattery.None;
                    }

                    return LevelOf(100 * remaining.Value / full.Value);
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Warn(e, "Couldn't read the pad's battery");
                    return ControllerBattery.None;
                }
            }
        }

        /// <summary>The console's three bars, from the percentage left.</summary>
        private static ControllerBattery LevelOf(int percent)
        {
            if (percent < 5)
            {
                return ControllerBattery.Empty;
            }

            if (percent < 35)
            {
                return ControllerBattery.Low;
            }

            return percent < 70 ? ControllerBattery.Medium : ControllerBattery.Full;
        }
    }
}
