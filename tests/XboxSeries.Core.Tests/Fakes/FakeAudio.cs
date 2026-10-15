using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// Windows' master volume and audio devices, which a test can also change as another app or a plug would, or make
    /// refuse volume changes. Changing devices or a default here doesn't tell the core; the test does, as Windows would.
    /// </summary>
    public sealed class FakeAudio : IAudio
    {
        private readonly List<AudioDevice> devices = new List<AudioDevice>();
        private readonly Dictionary<AudioDeviceKind, string> defaults = new Dictionary<AudioDeviceKind, string>();

        public int Volume { get; set; } = 50;

        public bool IsMuted { get; set; }

        /// <summary>Whether Windows refuses to change the volume, as with no output device.</summary>
        public bool Refuses { get; set; }

        /// <summary>The devices asked to be made the default, in order.</summary>
        public List<AudioDevice> DefaultsSet { get; } = new List<AudioDevice>();

        public void SetVolume(int volume)
        {
            if (!Refuses)
            {
                Volume = volume;
            }
        }

        public void SetMuted(bool muted) => IsMuted = muted;

        /// <summary>Plugs in a device, after those already there; the first of its kind becomes the default.</summary>
        public AudioDevice Add(string id, string name, AudioDeviceKind kind)
        {
            var device = new AudioDevice(id, name, kind);
            devices.Add(device);
            if (DefaultDeviceId(kind) == null)
            {
                defaults[kind] = id;
            }

            return device;
        }

        /// <summary>Unplugs a device; if it was the default, the first left of its kind becomes the default, as Windows picks one.</summary>
        public void Remove(string id)
        {
            var device = devices.Single(d => d.Id == id);
            devices.Remove(device);
            if (DefaultDeviceId(device.Kind) == id)
            {
                defaults[device.Kind] = Devices(device.Kind).FirstOrDefault()?.Id;
            }
        }

        /// <summary>Makes the device with <paramref name="id"/> the default of <paramref name="kind"/>, as the Windows sound settings would.</summary>
        public void SetDefault(AudioDeviceKind kind, string id) => defaults[kind] = id;

        public IReadOnlyList<AudioDevice> Devices(AudioDeviceKind kind) => devices.Where(d => d.Kind == kind).ToList();

        public string DefaultDeviceId(AudioDeviceKind kind) => defaults.TryGetValue(kind, out var id) ? id : null;

        public void SetDefaultDevice(AudioDevice device)
        {
            DefaultsSet.Add(device);
            defaults[device.Kind] = device.Id;
        }
    }
}
