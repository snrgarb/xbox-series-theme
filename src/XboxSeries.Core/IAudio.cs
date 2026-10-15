namespace XboxSeries.Core
{
    /// <summary>Port for Windows' master volume, which the Guide's Audio utility controls.</summary>
    public interface IAudio
    {
        /// <summary>The master volume, 0 to 100.</summary>
        int Volume { get; }

        bool IsMuted { get; }

        /// <summary>Sets the master volume, 0 to 100.</summary>
        void SetVolume(int volume);

        void SetMuted(bool muted);
    }
}
