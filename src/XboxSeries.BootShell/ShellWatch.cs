using System;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// Explorer once it's the shell: Windows usually brings a dead explorer back by itself, so the Boot shell starts it
    /// only when there has been no shell for 10 s, and then waits 10 s more before trying again. The startup apps don't
    /// run a second time; Windows runs them once per sign-in.
    /// </summary>
    internal sealed class ShellWatch
    {
        public static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

        private readonly Func<TimeSpan> elapsed;
        private bool wasShell;
        private TimeSpan? goneSince;

        /// <param name="elapsed">The time since the Boot shell started.</param>
        public ShellWatch(Func<TimeSpan> elapsed)
        {
            this.elapsed = elapsed;
        }

        /// <summary>Called every poll with whether a shell window is up; true when the Boot shell is to start explorer.</summary>
        public bool StartExplorer(bool shellUp)
        {
            if (shellUp)
            {
                wasShell = true;
                goneSince = null;
                return false;
            }

            if (!wasShell)
            {
                return false;
            }

            var now = elapsed();
            goneSince ??= now;
            if (now - goneSince < Grace)
            {
                return false;
            }

            goneSince = now;
            return true;
        }
    }
}
