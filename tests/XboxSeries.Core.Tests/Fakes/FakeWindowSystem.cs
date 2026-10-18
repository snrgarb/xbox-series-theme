using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>A desktop of windows a test opens and closes, tracking which one is in front.</summary>
    public sealed class FakeWindowSystem : IWindowSystem
    {
        /// <summary>The Overlay's window, in front whenever the core has taken the foreground.</summary>
        public static readonly IntPtr Overlay = new IntPtr(1);

        private readonly HashSet<IntPtr> windows = new HashSet<IntPtr> { Overlay };
        private readonly Dictionary<IntPtr, string> programs = new Dictionary<IntPtr, string>();

        /// <summary>Playnite's own windows: the Overlay's, and Fullscreen's.</summary>
        private readonly HashSet<IntPtr> playnites = new HashSet<IntPtr> { Overlay };
        private long nextHandle = 100;

        public IntPtr Foreground { get; private set; }

        /// <summary>Every window asked to close gracefully, in order. The fake never closes them itself: a test decides whether the game exits.</summary>
        public List<IntPtr> CloseRequests { get; } = new List<IntPtr>();

        /// <summary>Every window whose program was force-killed, in order.</summary>
        public List<IntPtr> Killed { get; } = new List<IntPtr>();

        /// <summary>Every freeze, as its install directory and game window, in order.</summary>
        public List<(string Directory, IntPtr Window)> Freezes { get; } = new List<(string, IntPtr)>();

        /// <summary>How many times the core thawed the game.</summary>
        public int Thaws { get; private set; }

        /// <summary>Whether the game is frozen now: frozen since the last thaw.</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>Every window handed the foreground while the game was frozen, in order. A frozen game can't take it.</summary>
        public List<IntPtr> HandedForegroundWhileFrozen { get; } = new List<IntPtr>();

        /// <summary>Every window asked to close or killed while the game was frozen, in order. A frozen game can't answer a close.</summary>
        public List<IntPtr> QuitWhileFrozen { get; } = new List<IntPtr>();

        /// <summary>When true, Windows refuses to hand the Overlay the foreground.</summary>
        public bool RefusesForeground { get; set; }

        /// <summary>Opens a window (a game, an app, Playnite), owned by the program at <paramref name="programPath"/>, and brings it to the front.</summary>
        public IntPtr OpenWindow(string programPath = null)
        {
            var window = new IntPtr(nextHandle++);
            windows.Add(window);
            programs[window] = programPath;
            Foreground = window;
            return window;
        }

        /// <summary>Opens one of Playnite's own windows, as Fullscreen's, and brings it to the front.</summary>
        public IntPtr OpenPlayniteWindow()
        {
            var window = OpenWindow();
            playnites.Add(window);
            return window;
        }

        /// <summary>Closes a window, as when a game quits or crashes.</summary>
        public void CloseWindow(IntPtr window)
        {
            windows.Remove(window);
            if (Foreground == window)
            {
                Foreground = IntPtr.Zero;
            }
        }

        IntPtr IWindowSystem.ForegroundWindow => Foreground;

        public bool Exists(IntPtr window) => windows.Contains(window);

        public bool IsPlaynites(IntPtr window) => playnites.Contains(window);

        public IntPtr FindWindowUnder(string directory) =>
            windows.FirstOrDefault(w => programs.TryGetValue(w, out var path)
                && path != null
                && path.StartsWith(directory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));

        public void TakeForeground()
        {
            if (!RefusesForeground)
            {
                Foreground = Overlay;
            }
        }

        public void SetForeground(IntPtr window)
        {
            if (IsFrozen)
            {
                HandedForegroundWhileFrozen.Add(window);
            }

            if (Exists(window))
            {
                Foreground = window;
            }
        }

        public void CloseGracefully(IntPtr window)
        {
            if (IsFrozen)
            {
                QuitWhileFrozen.Add(window);
            }

            CloseRequests.Add(window);
        }

        /// <summary>Records the kill. Like the real thing, Playnite's game stopped event is left to the test.</summary>
        public void ForceKill(IntPtr window)
        {
            if (IsFrozen)
            {
                QuitWhileFrozen.Add(window);
            }

            Killed.Add(window);
        }

        public void FreezeGame(string directory, IntPtr window)
        {
            Freezes.Add((directory, window));
            IsFrozen = true;
        }

        public void ThawGame()
        {
            Thaws++;
            IsFrozen = false;
        }
    }
}
