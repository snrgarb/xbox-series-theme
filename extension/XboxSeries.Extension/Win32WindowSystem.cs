using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the real window system, with the Overlay as the window that takes the foreground.</summary>
    internal sealed class Win32WindowSystem : IWindowSystem
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>Playnite's process, which the Bundle's windows share.</summary>
        private static readonly int ThisProcessId = Process.GetCurrentProcess().Id;

        /// <summary>How long after handing a window the foreground to look whether it's in front: another process's window only comes once its thread handles the activation.</summary>
        private static readonly TimeSpan ForegroundSettle = TimeSpan.FromMilliseconds(250);

        private readonly GameFreezer freezer;

        /// <param name="dataDirectory">The extension's data folder, where the record of a frozen game is kept.</param>
        public Win32WindowSystem(string dataDirectory)
        {
            freezer = new GameFreezer(dataDirectory);
        }

        /// <summary>Set once the Overlay exists; until then the foreground can't be taken.</summary>
        public OverlayWindow Overlay { get; set; }

        public IntPtr ForegroundWindow => NativeMethods.GetForegroundWindow();

        public bool Exists(IntPtr window) => window != IntPtr.Zero && NativeMethods.IsWindow(window);

        /// <summary>Playnite's own: a window of this process, which the Bundle's windows share with Playnite's.</summary>
        public bool IsPlaynites(IntPtr window) =>
            window != IntPtr.Zero && NativeMethods.ProcessIdOf(window) == ThisProcessId;

        /// <summary>
        /// Playnite opens every dialog of its own with ShowDialog, on the UI thread, which the core runs on too, and sets
        /// it an owner only sometimes; the Bundle's windows are never modal. Asked on the UI thread.
        /// </summary>
        public bool IsPlayniteAsking => ComponentDispatcher.IsThreadModal;

        public IntPtr FindWindowUnder(string directory)
        {
            // Made whole and followed past junctions and subst drives, as the programs' own paths may be either.
            var prefixes = NativeMethods.PathPrefixesOf(directory);
            var window = prefixes.Count == 0
                ? IntPtr.Zero
                : NativeMethods.FindTopLevelWindow(path => prefixes.Exists(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
            var under = string.Join(" or ", prefixes);
            logger.Info(window == IntPtr.Zero
                ? $"No window found for a program under {(under.Length == 0 ? directory : under)}"
                : $"Found window {window} for a program under {under}");
            return window;
        }

        public void TakeForeground()
        {
            if (Overlay == null)
            {
                return;
            }

            // Diagnostic: the window the Guide opened over, a game's when one is running.
            var before = NativeMethods.GetForegroundWindow();
            var overlay = new WindowInteropHelper(Overlay).Handle;
            if (!NativeMethods.BringToForeground(overlay, altTap: true))
            {
                logger.Warn("Windows refused the Overlay the foreground; the Guide shows Topmost and follows the controller callback");
            }

            Overlay.Activate();
            Overlay.Focus();

            // Diagnostic: if the game still reacts to the pad while Playnite is in front, the game reads the pad in the background.
            // Written from another thread: naming a window's process reads every process there is, which the Guide
            // needn't wait for before it shows.
            var after = NativeMethods.GetForegroundWindow();
            Task.Run(() => logger.Info($"Guide opened over {ProcessNameOf(before)}; the foreground belongs to {ProcessNameOf(after)}"));
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
            // No Alt tap: Playnite holds the foreground as the Guide closes, so Windows lets it give it away, and when it
            // doesn't, the tap would land in whichever window is in front.
            if (!NativeMethods.BringToForeground(window, altTap: false))
            {
                logger.Warn($"Windows refused to hand the foreground back to window {window}; {ProcessNameOf(NativeMethods.GetForegroundWindow())} is in front");
                return;
            }

            // Accepted; look again once the game's thread has had time to handle the activation.
            var check = new DispatcherTimer { Interval = ForegroundSettle };
            check.Tick += (_, __) =>
            {
                check.Stop();
                var front = NativeMethods.GetForegroundWindow();
                if (front != window && NativeMethods.IsWindow(window) && NativeMethods.ProcessIdOf(front) != NativeMethods.ProcessIdOf(window))
                {
                    logger.Warn($"Handed the foreground back to window {window} ({ProcessNameOf(window)}), but {ProcessNameOf(front)} is in front");
                }
            };
            check.Start();
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

        public void FreezeGame(string directory, IntPtr window) => freezer.Freeze(directory, window);

        public void ThawGame() => freezer.Thaw();

        /// <summary>Resumes the processes of a game a Playnite that stopped without thawing it left frozen.</summary>
        public void ThawLeftoverGame() => freezer.ThawLeftovers();
    }
}
