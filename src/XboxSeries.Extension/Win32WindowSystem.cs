using System;
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
        }

        public void SetForeground(IntPtr window)
        {
            if (!NativeMethods.BringToForeground(window))
            {
                logger.Warn($"Couldn't hand the foreground back to window {window}");
            }
        }
    }
}
