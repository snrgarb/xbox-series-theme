using System;
using System.Globalization;

namespace XboxSeries.Core
{
    /// <summary>Port for the PC's own battery, a laptop's or a handheld's, which the Status area shows before the pad's.</summary>
    public interface IPcBattery
    {
        /// <summary>
        /// The battery as Windows reports it now: <see cref="PcBattery.None"/> on a PC without one, one with no percent
        /// (<see cref="PcBattery.WithoutPercent"/>) when Windows doesn't know the charge, and null when it can't say
        /// anything of the battery at all, so the last reading stands (<see cref="PcBattery.Update"/>).
        /// </summary>
        PcBattery Read();
    }

    /// <summary>
    /// The PC's battery: whether there is one, the charge left and whether it is charging. The Status area shows its percent
    /// then Windows' upright battery (<see cref="Glyph"/>), so it never reads as the pad's, which lies on its side as the
    /// console draws it; with none, nothing.
    /// </summary>
    public sealed class PcBattery : IEquatable<PcBattery>
    {
        /// <summary>No system battery, as on a desktop: the Status area shows nothing for it.</summary>
        public static readonly PcBattery None = new PcBattery(false, false, 0, false);

        private PcBattery(bool isPresent, bool hasPercent, int percent, bool isCharging)
        {
            IsPresent = isPresent;
            HasPercent = hasPercent;
            Percent = percent;
            IsCharging = isCharging;
        }

        /// <summary>Whether the charge is known, so the percent shows; false with no battery, or one Windows can't measure.</summary>
        public bool HasPercent { get; }

        /// <summary>Whether the PC has a battery, and so whether the Status area shows one.</summary>
        public bool IsPresent { get; }

        /// <summary>The charge left, 0 to 100, to which the battery is filled; 0 with no battery or no known charge.</summary>
        public int Percent { get; }

        /// <summary>Whether it is on mains and charging, which shows the charging battery, with its bolt.</summary>
        public bool IsCharging { get; }

        /// <summary>
        /// Segoe Fluent Icons' (and MDL2 Assets') upright battery, as Windows shows a handheld's: VerticalBattery0 to 10
        /// (U+F5F2 to U+F5FC) filled to the nearest tenth, VerticalBatteryCharging0 to 10 (U+F5FD to U+F607), which
        /// carry their own bolt, while charging, and VerticalBatteryUnknown (U+F608) when the charge isn't known; empty
        /// with no battery.
        /// </summary>
        public string Glyph
        {
            get
            {
                if (!IsPresent)
                {
                    return "";
                }

                if (!HasPercent)
                {
                    return "\uF608";
                }

                var tenths = (Percent + 5) / 10;
                return ((char)((IsCharging ? 0xF5FD : 0xF5F2) + tenths)).ToString();
            }
        }

        /// <summary>The percent as the Status area writes it, "57%"; empty with no battery, or no known charge.</summary>
        public string Text => HasPercent ? Percent.ToString(CultureInfo.InvariantCulture) + "%" : "";

        /// <summary>A battery with <paramref name="percent"/> left, held to 0 to 100, charging or not.</summary>
        public static PcBattery Of(int percent, bool charging) =>
            new PcBattery(true, true, Math.Max(0, Math.Min(100, percent)), charging);

        /// <summary>A battery whose charge Windows doesn't know (it reports 255%): the unknown battery shows, the percent doesn't.</summary>
        public static PcBattery WithoutPercent(bool charging) => new PcBattery(true, false, 0, charging);

        /// <summary>
        /// What shows after <paramref name="reading"/>, given <paramref name="shown"/>: a null reading, Windows unable to
        /// say, keeps what shows; a battery read without a percent keeps the last percent known, charging as now read;
        /// any other reading shows as read.
        /// </summary>
        public static PcBattery Update(PcBattery shown, PcBattery reading)
        {
            if (reading == null)
            {
                return shown ?? None;
            }

            if (reading.IsPresent && !reading.HasPercent && shown != null && shown.HasPercent)
            {
                return Of(shown.Percent, reading.IsCharging);
            }

            return reading;
        }

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
            other != null && IsPresent == other.IsPresent && HasPercent == other.HasPercent && Percent == other.Percent
            && IsCharging == other.IsCharging;

        public override bool Equals(object obj) => Equals(obj as PcBattery);

        public override int GetHashCode() => (IsPresent ? 1 : 0) ^ (HasPercent ? 2 : 0) ^ (Percent << 2) ^ (IsCharging ? 1024 : 0);

        /// <summary>As the extension logs it: "none", "57%", "57%, charging", or "unknown %" without a percent.</summary>
        public override string ToString()
        {
            if (!IsPresent)
            {
                return "none";
            }

            var charge = HasPercent ? Text : "unknown %";
            return IsCharging ? charge + ", charging" : charge;
        }
    }

    /// <summary>No battery, ever: for a core given no port.</summary>
    internal sealed class NoPcBattery : IPcBattery
    {
        public static readonly NoPcBattery Instance = new NoPcBattery();

        public PcBattery Read() => PcBattery.None;
    }
}
