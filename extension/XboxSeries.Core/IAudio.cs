using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// Port for Windows' audio: the master volume and mute, which the Guide's Audio utility controls, and the output and
    /// input devices it switches between. When the devices or the defaults change, the extension calls
    /// <see cref="ExtensionCore.OnAudioDevicesChanged"/> on the UI thread.
    /// </summary>
    public interface IAudio
    {
        /// <summary>The master volume, 0 to 100.</summary>
        int Volume { get; }

        bool IsMuted { get; }

        /// <summary>Sets the master volume, 0 to 100.</summary>
        void SetVolume(int volume);

        void SetMuted(bool muted);

        /// <summary>The devices of <paramref name="kind"/> active now, in the order Windows lists them; empty when there are none.</summary>
        IReadOnlyList<AudioDevice> Devices(AudioDeviceKind kind);

        /// <summary>The id of Windows' default device of <paramref name="kind"/>, or null when there is none.</summary>
        string DefaultDeviceId(AudioDeviceKind kind);

        /// <summary>Makes <paramref name="device"/> Windows' default of its kind, for the console and multimedia roles.</summary>
        void SetDefaultDevice(AudioDevice device);
    }
}
