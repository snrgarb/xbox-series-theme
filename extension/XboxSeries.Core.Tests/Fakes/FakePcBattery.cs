using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>The PC's own battery, which a test can set; none to start with, as on a desktop.</summary>
    public sealed class FakePcBattery : IPcBattery
    {
        public PcBattery Battery { get; set; } = PcBattery.None;

        public PcBattery Read() => Battery;
    }
}
