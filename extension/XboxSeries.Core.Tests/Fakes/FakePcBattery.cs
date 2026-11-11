using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// The PC's own battery, which a test can set; none to start with, as on a desktop, and null as Windows unable to say.
    /// </summary>
    public sealed class FakePcBattery : IPcBattery
    {
        public PcBattery Battery { get; set; } = PcBattery.None;

        /// <summary>How many times the core has read it.</summary>
        public int Reads { get; private set; }

        public PcBattery Read()
        {
            Reads++;
            return Battery;
        }
    }
}
