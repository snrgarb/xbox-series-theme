namespace XboxSeries.Core
{
    /// <summary>Which way a Windows audio device carries sound.</summary>
    public enum AudioDeviceKind
    {
        /// <summary>Speakers or headphones.</summary>
        Output,

        /// <summary>A microphone.</summary>
        Input,
    }

    /// <summary>A Windows audio device that is active now, by the name Windows shows for it.</summary>
    public sealed class AudioDevice
    {
        public AudioDevice(string id, string name, AudioDeviceKind kind)
        {
            Id = id;
            Name = name;
            Kind = kind;
        }

        /// <summary>Windows' own id for the device, stable while it stays plugged in.</summary>
        public string Id { get; }

        public string Name { get; }

        public AudioDeviceKind Kind { get; }
    }
}
