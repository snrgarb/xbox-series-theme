using System.Threading;

namespace XboxSeries.Shared
{
    /// <summary>
    /// The two named events through which Start-up tells the Boot shell, this TV PC's own sign-in shell, when its first
    /// frame shows and when the boot video ends. The Boot shell makes them, resetting them before each launch of
    /// Playnite, and decides which it waits on; the Bundle only sets them. With no Boot shell running there are none, as
    /// on every other PC, and nothing happens. Plain C# with no Playnite types, so the Boot shell and the extension both
    /// link this file for the names.
    /// </summary>
    internal static class BootShellEvents
    {
        /// <summary>Start-up's first frame that shows something: the Boot shell takes its black window down.</summary>
        public const string FirstFrame = @"Local\XboxSeries.BootShell.FirstFrame";

        /// <summary>The boot video has ended, or there was none: the Boot shell starts explorer and the first startup apps.</summary>
        public const string BootVideoEnded = @"Local\XboxSeries.BootShell.BootVideoEnded";

        /// <summary>
        /// Sets the event <paramref name="name"/> if the Boot shell has made it: true once set, false with none to set.
        /// Throws if one exists that Windows won't let this process set.
        /// </summary>
        public static bool Set(string name)
        {
            if (!EventWaitHandle.TryOpenExisting(name, out var handle))
            {
                return false;
            }

            using (handle)
            {
                handle.Set();
            }

            return true;
        }
    }
}
