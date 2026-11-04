using System;

namespace XboxSeries.Core
{
    /// <summary>Port for the Windows window system: the foreground window, taking it for the Overlay, and closing games.</summary>
    public interface IWindowSystem
    {
        /// <summary>The window currently in front, or <see cref="IntPtr.Zero"/> when there is none.</summary>
        IntPtr ForegroundWindow { get; }

        /// <summary>Whether <paramref name="window"/> still exists (a game may quit while the Guide is open).</summary>
        bool Exists(IntPtr window);

        /// <summary>
        /// A top-level window of a program that lives under <paramref name="directory"/>, such as a running game's
        /// window found from its install directory; <see cref="IntPtr.Zero"/> when there is none.
        /// </summary>
        IntPtr FindWindowUnder(string directory);

        /// <summary>
        /// Whether <paramref name="window"/> is one of Playnite's own: Fullscreen's, its dialogs', or the Bundle's, the
        /// Overlay and the Launch screen among them; not a launcher's or a game's.
        /// </summary>
        bool IsPlaynites(IntPtr window);

        /// <summary>
        /// Whether one of Playnite's dialogs is open, waiting on the user, as its error on a launch that failed. Not the
        /// Bundle's windows, which never wait so.
        /// </summary>
        bool IsPlayniteAsking { get; }

        /// <summary>Tries to make the Overlay the foreground window with keyboard focus. Windows may refuse.</summary>
        void TakeForeground();

        /// <summary>Hands the foreground to <paramref name="window"/>.</summary>
        void SetForeground(IntPtr window);

        /// <summary>Asks <paramref name="window"/> to close, as its close button would, so a game can save and exit.</summary>
        void CloseGracefully(IntPtr window);

        /// <summary>Kills the program that owns <paramref name="window"/>, losing anything unsaved.</summary>
        void ForceKill(IntPtr window);

        /// <summary>
        /// Suspends a running game's processes while the Guide is open, so a game that reads the pad in the background
        /// can't take its input: every process under <paramref name="directory"/>, its install directory, and the one
        /// that owns <paramref name="window"/>, when there is one. A process Windows won't let us suspend, or one that
        /// ends meanwhile, is skipped, and nothing is frozen when <paramref name="directory"/> is a drive's root. Never
        /// throws for a reason of Windows'.
        /// </summary>
        void FreezeGame(string directory, IntPtr window);

        /// <summary>Resumes every process <see cref="FreezeGame"/> suspended; nothing when none is.</summary>
        void ThawGame();
    }
}
