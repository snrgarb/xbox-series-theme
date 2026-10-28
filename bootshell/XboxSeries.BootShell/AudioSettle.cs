using System;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// When, at sign-in, the Boot shell launches Fullscreen: once Windows' default audio device has stayed the same for
    /// 1.5 s, or at the 6 s limit since the Boot shell started, settled or not. Playnite opens its audio once, as it
    /// starts, and never again; started while Windows is still bringing up the TV's HDMI audio and picking the default
    /// again, it can't open it, and stays silent for the whole run. It happens once; relaunches don't wait.
    /// </summary>
    internal sealed class AudioSettle
    {
        public static readonly TimeSpan Settle = TimeSpan.FromSeconds(1.5);
        public static readonly TimeSpan Limit = TimeSpan.FromSeconds(6);

        private readonly Func<TimeSpan> elapsed;
        private readonly Action<string> log;
        private bool seen;
        private string? device;
        private TimeSpan since;
        private bool launched;

        /// <param name="elapsed">The time since the Boot shell started.</param>
        /// <param name="log">Writes one line to the Boot shell's log.</param>
        public AudioSettle(Func<TimeSpan> elapsed, Action<string> log)
        {
            this.elapsed = elapsed;
            this.log = log;
        }

        /// <summary>Why Fullscreen is due, once <see cref="Launch"/> has said it is.</summary>
        public string Why { get; private set; } = "";

        /// <summary>
        /// Called every poll with the default audio device's id, or null with none; true once, when Fullscreen is to
        /// launch.
        /// </summary>
        public bool Launch(string? defaultDevice)
        {
            if (launched)
            {
                return false;
            }

            var now = elapsed();
            if (!seen || defaultDevice != device)
            {
                log(defaultDevice == null ? "audio: no default audio device"
                    : "audio: default audio device " + (seen && device != null ? "now " : "") + defaultDevice);
                seen = true;
                device = defaultDevice;
                since = now;
            }

            if (device != null && now - since >= Settle)
            {
                Why = $"audio settled after {now.TotalMilliseconds:0} ms";
            }
            else if (now >= Limit)
            {
                Why = device == null ? $"no default audio device after {Limit.TotalSeconds} s; launching anyway"
                    : $"audio not settled after {Limit.TotalSeconds} s; launching anyway";
            }
            else
            {
                return false;
            }

            launched = true;
            return true;
        }
    }
}
