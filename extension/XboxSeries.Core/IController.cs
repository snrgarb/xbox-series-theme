using System;
using System.Globalization;

namespace XboxSeries.Core
{
    /// <summary>Port for the controller, for the battery the status area shows.</summary>
    public interface IController
    {
        /// <summary>The battery of pad 1.</summary>
        ControllerBattery Battery { get; }
    }

    /// <summary>How pad 1 is powered.</summary>
    public enum ControllerConnection
    {
        /// <summary>No controller, or one that doesn't report its battery: the status area shows none.</summary>
        None,

        /// <summary>Plugged in by cable, so it has no battery to show.</summary>
        Wired,

        /// <summary>On its batteries, with how much charge is left.</summary>
        Wireless,
    }

    /// <summary>A controller's battery: how it is connected and, without a cable, the percentage left.</summary>
    public sealed class ControllerBattery : IEquatable<ControllerBattery>
    {
        public static readonly ControllerBattery None = new ControllerBattery(ControllerConnection.None, 0);
        public static readonly ControllerBattery Wired = new ControllerBattery(ControllerConnection.Wired, 0);

        private ControllerBattery(ControllerConnection connection, int percent)
        {
            Connection = connection;
            Percent = percent;
        }

        public ControllerConnection Connection { get; }

        /// <summary>The charge left, 0 to 100; 0 unless <see cref="Connection"/> is Wireless.</summary>
        public int Percent { get; }

        public static ControllerBattery Wireless(int percent) =>
            new ControllerBattery(ControllerConnection.Wireless, Math.Max(0, Math.Min(100, percent)));

        /// <summary>
        /// A pad battery faked for a Debug build, as its XBOXSERIES_FAKE_PAD_BATTERY variable gives it: a percent, as "88",
        /// for a wireless pad, or "wired". Anything else, no variable included, is null, so the real pad shows.
        /// </summary>
        public static ControllerBattery FromFake(string variable)
        {
            var value = (variable ?? "").Trim();
            if (string.Equals(value, "wired", StringComparison.OrdinalIgnoreCase))
            {
                return Wired;
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent) ? Wireless(percent) : null;
        }

        public bool Equals(ControllerBattery other) =>
            other != null && Connection == other.Connection && Percent == other.Percent;

        public override bool Equals(object obj) => Equals(obj as ControllerBattery);

        public override int GetHashCode() => ((int)Connection * 397) ^ Percent;

        public override string ToString() => Connection == ControllerConnection.Wireless ? $"{Percent}%" : Connection.ToString();
    }
}
