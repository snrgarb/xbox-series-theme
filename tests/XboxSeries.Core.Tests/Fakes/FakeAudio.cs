using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>Windows' master volume, which a test can also change as another app would, or make refuse changes.</summary>
    public sealed class FakeAudio : IAudio
    {
        public int Volume { get; set; } = 50;

        public bool IsMuted { get; set; }

        /// <summary>Whether Windows refuses to change the volume, as with no output device.</summary>
        public bool Refuses { get; set; }

        public void SetVolume(int volume)
        {
            if (!Refuses)
            {
                Volume = volume;
            }
        }

        public void SetMuted(bool muted) => IsMuted = muted;
    }
}
