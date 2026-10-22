using System;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// When, at one sign-in, the Boot shell takes its black window down and starts explorer and the first apps,
    /// following the Bundle's two signals and the time since the Boot shell started. The black comes down at Start-up's
    /// first frame, or at the 60 s limit. Explorer starts as the Boot video ends; without that signal, 20 s after the
    /// first frame; without that either, at the 60 s limit. Each happens once.
    /// </summary>
    internal sealed class SignInSchedule
    {
        public static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan AfterFirstFrame = TimeSpan.FromSeconds(20);

        private readonly Func<TimeSpan> elapsed;
        private TimeSpan? firstFrameAt;
        private bool bootVideoEnded;
        private bool blackDown;
        private bool explorerStarted;

        /// <param name="elapsed">The time since the Boot shell started.</param>
        public SignInSchedule(Func<TimeSpan> elapsed)
        {
            this.elapsed = elapsed;
        }

        /// <summary>The time until the next limit falls due, or null with none left to wait for.</summary>
        public TimeSpan? UntilNext
        {
            get
            {
                TimeSpan? next = null;
                if (!blackDown && firstFrameAt == null)
                {
                    next = Limit;
                }

                if (!explorerStarted && (next == null || ExplorerAt < next))
                {
                    next = ExplorerAt;
                }

                return next == null ? null : Max(next.Value - elapsed(), TimeSpan.Zero);
            }
        }

        private TimeSpan ExplorerAt => firstFrameAt + AfterFirstFrame ?? Limit;

        /// <summary>Start-up's first frame has shown: the Bundle set FirstFrame.</summary>
        public void FirstFrame() => firstFrameAt ??= elapsed();

        /// <summary>The Boot video has ended, or there was none: the Bundle set BootVideoEnded.</summary>
        public void BootVideoEnded() => bootVideoEnded = true;

        /// <summary>True once, when the black is to come down.</summary>
        public bool TakeBlackDown() => Once(ref blackDown, firstFrameAt != null || elapsed() >= Limit);

        /// <summary>True once, when explorer and the first apps are to start.</summary>
        public bool StartExplorer() => Once(ref explorerStarted, bootVideoEnded || elapsed() >= ExplorerAt);

        private static bool Once(ref bool done, bool due)
        {
            if (done || !due)
            {
                return false;
            }

            done = true;
            return true;
        }

        private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
    }
}
