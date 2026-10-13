using System;
using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>A desktop of windows a test opens and closes, tracking which one is in front.</summary>
    public sealed class FakeWindowSystem : IWindowSystem
    {
        /// <summary>The Overlay's window, in front whenever the core has taken the foreground.</summary>
        public static readonly IntPtr Overlay = new IntPtr(1);

        private readonly HashSet<IntPtr> windows = new HashSet<IntPtr> { Overlay };
        private long nextHandle = 100;

        public IntPtr Foreground { get; private set; }

        /// <summary>When true, Windows refuses to hand the Overlay the foreground.</summary>
        public bool RefusesForeground { get; set; }

        /// <summary>Opens a window (a game, an app, Playnite) and brings it to the front.</summary>
        public IntPtr OpenWindow()
        {
            var window = new IntPtr(nextHandle++);
            windows.Add(window);
            Foreground = window;
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

        public void TakeForeground()
        {
            if (!RefusesForeground)
            {
                Foreground = Overlay;
            }
        }

        public void SetForeground(IntPtr window)
        {
            if (Exists(window))
            {
                Foreground = window;
            }
        }
    }
}
