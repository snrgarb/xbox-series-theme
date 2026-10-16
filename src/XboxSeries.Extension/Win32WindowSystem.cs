using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Interop;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the real window system, with the Overlay as the window that takes the foreground.</summary>
    internal sealed class Win32WindowSystem : IWindowSystem
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>Set once the Overlay exists; until then the foreground can't be taken.</summary>
        public OverlayWindow Overlay { get; set; }

        public IntPtr ForegroundWindow => NativeMethods.GetForegroundWindow();

        public bool Exists(IntPtr window) => window != IntPtr.Zero && NativeMethods.IsWindow(window);

        public IntPtr FindWindowUnder(string directory)
        {
            var prefix = directory.TrimEnd('\\') + "\\";
            var window = NativeMethods.FindTopLevelWindow(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            logger.Info(window == IntPtr.Zero
                ? $"No window found for a program under {prefix}"
                : $"Found window {window} for a program under {prefix}");
            return window;
        }

        public void TakeForeground()
        {
            if (Overlay == null)
            {
                return;
            }

            if (!NativeMethods.BringToForeground(new WindowInteropHelper(Overlay).Handle))
            {
                logger.Warn("Windows refused the Overlay the foreground; the Guide shows Topmost and follows the controller callback");
            }

            Overlay.Activate();
            Overlay.Focus();

            // Diagnostic: if the game still reacts to the pad while Playnite is in front, the game reads the pad in the background.
            logger.Info($"Guide opened; the foreground belongs to {ProcessNameOf(NativeMethods.GetForegroundWindow())}");
        }

        private static string ProcessNameOf(IntPtr window)
        {
            try
            {
                using (var process = Process.GetProcessById(NativeMethods.ProcessIdOf(window)))
                {
                    return process.ProcessName;
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return $"window {window}";
            }
        }

        public void SetForeground(IntPtr window)
        {
            if (!NativeMethods.BringToForeground(window))
            {
                logger.Warn($"Couldn't hand the foreground back to window {window}");
            }
        }

        public void CloseGracefully(IntPtr window)
        {
            // What the window's close button sends, so the game can save and exit in its own way.
            if (!NativeMethods.PostMessage(window, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero))
            {
                logger.Warn($"Couldn't ask window {window} to close");
            }
        }

        public void ForceKill(IntPtr window)
        {
            var processId = NativeMethods.ProcessIdOf(window);
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    logger.Info($"Force-killing {process.ProcessName} ({processId})");
                    process.Kill();
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is Win32Exception)
            {
                // Gone already, or elevated beyond our reach.
                logger.Warn(e, $"Couldn't force-kill the program of window {window}");
            }
        }

        public void FreezeGame(string directory, IntPtr window)
        {
        }

        public void ThawGame()
        {
        }
    }
}
