using System;

namespace XboxSeries.Core
{
    /// <summary>Port for the Windows window system: the foreground window, and taking it for the Overlay.</summary>
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

        /// <summary>Tries to make the Overlay the foreground window with keyboard focus. Windows may refuse.</summary>
        void TakeForeground();

        /// <summary>Hands the foreground to <paramref name="window"/>.</summary>
        void SetForeground(IntPtr window);
    }
}
