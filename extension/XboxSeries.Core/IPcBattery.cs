using System;
using System.Globalization;

namespace XboxSeries.Core
{
    /// <summary>Port for the PC's own battery, a laptop's or a handheld's, which the Status area shows before the pad's.</summary>
    public interface IPcBattery
    {
        /// <summary>The battery as Windows reports it now; <see cref="PcBattery.None"/> on a PC without one.</summary>
        PcBattery Read();
    }

    /// <summary>
    /// The PC's battery: whether there is one, the charge left and whether it is charging. The Status area shows it as the
    /// pad's battery is shown, the percent then a cell filled to it, with a bolt while it charges; with none, nothing.
    /// </summary>
    public sealed class PcBattery : IEquatable<PcBattery>
    {
        /// <summary>No system battery, as on a desktop: the Status area shows nothing for it.</summary>
        public static readonly PcBattery None = new PcBattery(false, 0, false);

        private PcBattery(bool isPresent, int percent, bool isCharging)
        {
            IsPresent = isPresent;
            Percent = percent;
            IsCharging = isCharging;
        }

        /// <summary>Whether the PC has a battery, and so whether the Status area shows one.</summary>
        public bool IsPresent { get; }

        /// <summary>The charge left, 0 to 100, to which the cell is filled; 0 with no battery.</summary>
        public int Percent { get; }

        /// <summary>Whether it is on mains and charging, which shows a bolt beside the cell.</summary>
        public bool IsCharging { get; }

        /// <summary>The percent as the Status area writes it, "57%"; empty with no battery.</summary>
        public string Text => IsPresent ? Percent.ToString(CultureInfo.InvariantCulture) + "%" : "";

        /// <summary>A battery with <paramref name="percent"/> left, held to 0 to 100, charging or not.</summary>
        public static PcBattery Of(int percent, bool charging) =>
            new PcBattery(true, Math.Max(0, Math.Min(100, percent)), charging);

        /// <summary>
        /// A battery faked for a Debug build, as its XBOXSERIES_FAKE_BATTERY variable gives it: the percent, then
        /// ",charging" while charging, as "57,charging" or "12". Anything else, no variable included, is no battery.
        /// </summary>
        public static PcBattery FromFake(string variable)
        {
            var parts = (variable ?? "").Split(',');
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
            {
                return None;
            }

            var charging = parts.Length > 1 && string.Equals(parts[1].Trim(), "charging", StringComparison.OrdinalIgnoreCase);
            return Of(percent, charging);
        }

        public bool Equals(PcBattery other) =>
            other != null && IsPresent == other.IsPresent && Percent == other.Percent && IsCharging == other.IsCharging;

        public override bool Equals(object obj) => Equals(obj as PcBattery);

        public override int GetHashCode() => (IsPresent ? 1 : 0) ^ (Percent << 1) ^ (IsCharging ? 1024 : 0);

        /// <summary>As the extension logs it: "none", "57%" or "57%, charging".</summary>
        public override string ToString() => !IsPresent ? "none" : IsCharging ? Text + ", charging" : Text;
    }

    /// <summary>No battery, ever: for a core given no port.</summary>
    internal sealed class NoPcBattery : IPcBattery
    {
        public static readonly NoPcBattery Instance = new NoPcBattery();

        public PcBattery Read() => PcBattery.None;
    }
}
