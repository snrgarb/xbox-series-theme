using System;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// When, at one sign-in, the Boot shell starts explorer and the first apps, following the Bundle's two signals and
    /// the time since the Boot shell started: as the Boot video ends; without that signal, 20 s after the first frame;
    /// without that either, at the 60 s limit. It happens once. The black is the <see cref="Watchdog"/>'s.
    /// </summary>
    internal sealed class SignInSchedule
    {
        public static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan AfterFirstFrame = TimeSpan.FromSeconds(20);

        private readonly Func<TimeSpan> elapsed;
        private TimeSpan? firstFrameAt;
        private bool bootVideoEnded;
        private bool explorerStarted;

        /// <param name="elapsed">The time since the Boot shell started.</param>
        public SignInSchedule(Func<TimeSpan> elapsed)
        {
            this.elapsed = elapsed;
        }

        private TimeSpan ExplorerAt => firstFrameAt + AfterFirstFrame ?? Limit;

        /// <summary>Why explorer is due, once <see cref="StartExplorer"/> has said it is.</summary>
        public string Why => bootVideoEnded ? "the Boot video ended"
            : firstFrameAt != null ? $"no Boot video end {AfterFirstFrame.TotalSeconds} s after the first frame"
            : $"no first frame by the {Limit.TotalSeconds} s limit";

        /// <summary>Start-up's first frame has shown: the Bundle set FirstFrame.</summary>
        public void FirstFrame() => firstFrameAt ??= elapsed();

        /// <summary>The Boot video has ended, or there was none: the Bundle set BootVideoEnded.</summary>
        public void BootVideoEnded() => bootVideoEnded = true;

        /// <summary>True once, when explorer and the first apps are to start.</summary>
        public bool StartExplorer()
        {
            if (explorerStarted || !(bootVideoEnded || elapsed() >= ExplorerAt))
            {
                return false;
            }

            explorerStarted = true;
            return true;
        }
    }
}
