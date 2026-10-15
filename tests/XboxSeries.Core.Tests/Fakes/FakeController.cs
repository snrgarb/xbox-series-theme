using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>The controller, with a battery a test can set.</summary>
    public sealed class FakeController : IController
    {
        public ControllerBattery Battery { get; set; } = ControllerBattery.Wireless(100);
    }
}
