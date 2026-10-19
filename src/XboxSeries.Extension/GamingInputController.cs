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
    /// Adapter: pad 1's battery, and the buttons it holds down for the Start-up sequence. Windows.Gaming.Input finds the
    /// pad and says whether it is on a cable; the percentage of a pad on Bluetooth LE, as Xbox pads on a PC usually are,
    /// comes from <see cref="BluetoothBattery"/>. XInput isn't used: it reports no battery for a pad on Bluetooth.
    /// </summary>
    internal sealed class GamingInputController : IController
    {
        /// <summary>How far the left stick is pushed before it counts as the D-pad, as Playnite's own does.</summary>
        private const double StickAsDPad = 0.5;

        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly ControllerButton[] NoButtons = new ControllerButton[0];

        /// <summary>The pad's buttons as the core names them, the Guide button aside, which Windows.Gaming.Input never reads.</summary>
        private static readonly KeyValuePair<GamepadButtons, ControllerButton>[] Buttons =
        {
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.A, ControllerButton.A),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.B, ControllerButton.B),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.X, ControllerButton.X),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.Y, ControllerButton.Y),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.LeftShoulder, ControllerButton.LeftShoulder),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.RightShoulder, ControllerButton.RightShoulder),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.View, ControllerButton.Back),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.Menu, ControllerButton.Start),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.LeftThumbstick, ControllerButton.LeftStick),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.RightThumbstick, ControllerButton.RightStick),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.DPadUp, ControllerButton.DPadUp),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.DPadDown, ControllerButton.DPadDown),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.DPadLeft, ControllerButton.DPadLeft),
            new KeyValuePair<GamepadButtons, ControllerButton>(GamepadButtons.DPadRight, ControllerButton.DPadRight),
        };

        private bool unavailable;

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
        /// The buttons pad 1 holds down now, read straight from Windows on the calling thread, so the Start-up thread can
        /// read the pad while Playnite's UI thread, through which its own controller callback comes, is busy. The left
        /// stick pushed counts as the D-pad, as it does through Playnite. Empty with no pad, without Windows.Gaming.Input,
        /// or when the reading fails, which is logged the first time. It keeps nothing between calls, so any thread may
        /// call it.
        /// </summary>
        public IReadOnlyCollection<ControllerButton> ButtonsDown()
        {
            if (unavailable)
            {
                return NoButtons;
            }

            try
            {
                var pad = Gamepad.Gamepads.FirstOrDefault();
                if (pad == null)
                {
                    return NoButtons;
                }

                var reading = pad.GetCurrentReading();
                var down = new HashSet<ControllerButton>();
                foreach (var button in Buttons)
                {
                    if ((reading.Buttons & button.Key) != 0)
                    {
                        down.Add(button.Value);
                    }
                }

                if (reading.LeftThumbstickX <= -StickAsDPad)
                {
                    down.Add(ControllerButton.DPadLeft);
                }
                else if (reading.LeftThumbstickX >= StickAsDPad)
                {
                    down.Add(ControllerButton.DPadRight);
                }

                if (reading.LeftThumbstickY >= StickAsDPad)
                {
                    down.Add(ControllerButton.DPadUp);
                }
                else if (reading.LeftThumbstickY <= -StickAsDPad)
                {
                    down.Add(ControllerButton.DPadDown);
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
