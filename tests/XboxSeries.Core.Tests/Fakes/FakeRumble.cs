using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>Counts the controller's buzzes.</summary>
    public sealed class FakeRumble : IControllerRumble
    {
        public int Pulses { get; private set; }

        public void Pulse() => Pulses++;
    }
}
