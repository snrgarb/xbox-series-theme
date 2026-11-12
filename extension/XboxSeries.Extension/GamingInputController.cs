using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Windows.Gaming.Input;
using Windows.System.Power;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: pad 1's battery, and the buttons the pads hold down for the Start-up sequence. Windows.Gaming.Input finds the
    /// pad and says whether it is on a cable; the percentage of a pad on Bluetooth LE, as Xbox pads on a PC usually are,
    /// comes from <see cref="BluetoothBattery"/>, as XInput reports no battery for a pad on Bluetooth. The buttons come
    /// from <see cref="XInput"/>, as Windows.Gaming.Input reads a pad at rest while another program has the foreground.
    /// </summary>
    internal sealed class GamingInputController : IController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly ControllerButton[] NoButtons = new ControllerButton[0];

        private bool unavailable;

        /// <summary>The battery XBOXSERIES_FAKE_PAD_BATTERY fakes in a Debug build, read once; null to read the real pad.</summary>
        private readonly ControllerBattery fake = Fake();

        /// <summary>Whether a reading has failed already, so the failure is logged once, whichever thread reads.</summary>
        private volatile bool readingFailed;

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

        /// <summary>
        /// The buttons any pad holds down now, read straight from Windows on the calling thread, so the Start-up thread can
        /// read the pads while Playnite's UI thread, through which its own controller callback comes, is busy, and while
        /// another program has the foreground, as a startup app may take it while Who's playing today? shows, and
        /// explorer and the startup apps do under the Boot shell. Every slot is read, as Playnite reads them: the first
        /// may hold a pad nobody holds, as the virtual pad a streaming host such as Sunshine leaves connected. The left
        /// stick pushed counts as the D-pad, as it does through Playnite (<see cref="PadActivity.ButtonsOf"/>). Empty with
        /// no pad, without XInput, or when the reading fails, which is logged the first time. Any thread may call it.
        /// </summary>
        public IReadOnlyCollection<ControllerButton> ButtonsDown()
        {
            try
            {
                var down = new HashSet<ControllerButton>();
                for (var slot = 0; slot < PadWatch.Slots; slot++)
                {
                    if (XInput.Read(slot) is PadReading pad)
                    {
                        down.UnionWith(PadActivity.ButtonsOf(pad));
                    }
                }

                return down;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                if (!readingFailed)
                {
                    readingFailed = true;
                    logger.Warn(e, "Couldn't read the pad's buttons");
                }

                return NoButtons;
            }
        }

        public ControllerBattery Battery
        {
            get
            {
                if (fake != null)
                {
                    return fake;
                }

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

        /// <summary>
        /// The pad battery XBOXSERIES_FAKE_PAD_BATTERY fakes, in a Debug build with it set, as "88" or "wired"
        /// (<see cref="ControllerBattery.FromFake"/>), for the e2e VM, whose pad is a virtual one on a cable; null otherwise.
        /// </summary>
        private static ControllerBattery Fake()
        {
#if DEBUG
            var variable = Environment.GetEnvironmentVariable("XBOXSERIES_FAKE_PAD_BATTERY");
            var fake = ControllerBattery.FromFake(variable);
            if (fake != null)
            {
                logger.Info($"[Status] pad battery faked by XBOXSERIES_FAKE_PAD_BATTERY={variable}: {fake}");
                return fake;
            }
#endif
            return null;
        }
    }
}
