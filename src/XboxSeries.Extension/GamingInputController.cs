using System;
using System.Linq;
using Playnite.SDK;
using Windows.Gaming.Input;
using Windows.System.Power;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: pad 1's battery. Windows.Gaming.Input finds the pad and says whether it is on a cable; the percentage
    /// of a pad on Bluetooth LE, as Xbox pads on a PC usually are, comes from <see cref="BluetoothBattery"/>. XInput
    /// isn't used: it reports no battery for a pad on Bluetooth.
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

                    // On Bluetooth LE, Windows' own percentage for the pad; through a wireless adapter, the pad's report.
                    var raw = RawGameController.FromGameController(pad);
                    var percent = raw == null ? null : BluetoothBattery.PercentOf(raw.HardwareVendorId, raw.HardwareProductId);
                    if (percent == null)
                    {
                        var report = pad.TryGetBatteryReport();
                        var remaining = report?.RemainingCapacityInMilliwattHours;
                        var full = report?.FullChargeCapacityInMilliwattHours;
                        if (report == null || report.Status == BatteryStatus.NotPresent || remaining == null || full == null || full <= 0)
                        {
                            return ControllerBattery.None;
                        }

                        percent = 100 * remaining.Value / full.Value;
                    }

                    return ControllerBattery.Wireless(percent.Value);
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Warn(e, "Couldn't read the pad's battery");
                    return ControllerBattery.None;
                }
            }
        }
    }
}
