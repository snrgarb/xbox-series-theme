using System;
using System.Windows.Forms;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: the PC's own battery, as Windows reports it in <see cref="SystemInformation.PowerStatus"/>, the same
    /// reading Playnite's own top bar shows with the theme alone. No battery when its charge status says
    /// <see cref="BatteryChargeStatus.NoSystemBattery"/>, as on a desktop, or Unknown, which includes it. Charging while on
    /// mains with the Charging flag. The core reads it every 30 s, and again as the plugin hears Windows'
    /// PowerModeChanged.
    /// A Debug build fakes one when XBOXSERIES_FAKE_BATTERY is set, as "57,charging" (<see cref="PcBattery.FromFake"/>),
    /// for the e2e VM, which has none; a Release build never reads the variable.
    /// </summary>
    internal sealed class WindowsPcBattery : IPcBattery
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>The Debug build's fake, read once at start; null in Release, or with the variable unset.</summary>
        private readonly PcBattery fake;

        /// <summary>Whether a reading has failed already, so the failure is logged once.</summary>
        private bool readingFailed;

        public WindowsPcBattery()
        {
            fake = Fake();
        }

        public PcBattery Read()
        {
            if (fake != null)
            {
                return fake;
            }

            try
            {
                var power = SystemInformation.PowerStatus;
                var status = power.BatteryChargeStatus;
                if ((status & BatteryChargeStatus.NoSystemBattery) != 0)
                {
                    return PcBattery.None;
                }

                // BatteryLifePercent is 0 to 1, or 2.55 when Windows doesn't know it, which PcBattery holds to 100.
                var percent = (int)Math.Round(power.BatteryLifePercent * 100);
                var charging = power.PowerLineStatus == PowerLineStatus.Online && (status & BatteryChargeStatus.Charging) != 0;
                return PcBattery.Of(percent, charging);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                if (!readingFailed)
                {
                    readingFailed = true;
                    logger.Warn(e, "Couldn't read the PC's battery; the Status area shows none");
                }

                return PcBattery.None;
            }
        }

        /// <summary>The battery XBOXSERIES_FAKE_BATTERY fakes, in a Debug build with it set; null otherwise.</summary>
        private static PcBattery Fake()
        {
#if DEBUG
            var variable = Environment.GetEnvironmentVariable("XBOXSERIES_FAKE_BATTERY");
            if (!string.IsNullOrEmpty(variable))
            {
                var fake = PcBattery.FromFake(variable);
                logger.Info($"[Status] PC battery faked by XBOXSERIES_FAKE_BATTERY={variable}: {fake}");
                return fake;
            }
#endif
            return null;
        }
    }
}
