using System;
using System.Runtime.InteropServices;

namespace XboxSeries.Extension
{
    /// <summary>The user32 calls the extension needs: the foreground window, taking it, placing the Overlay over a monitor, and the debug hotkey.</summary>
    internal static class NativeMethods
    {
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const int SW_RESTORE = 9;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_NOREPEAT = 0x4000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFO mi);

        /// <summary>
        /// Hands <paramref name="window"/> the foreground. Returns whether it is now in front.
        /// When Windows refuses (we're in the background), a synthetic Alt tap lets us win <c>SetForegroundWindow</c>
        /// (validated by the #8 spike). The tap is a last resort: it lands on whichever window ends up focused, and a
        /// lone Alt puts a window such as Playnite's into menu mode, where it swallows the next keys.
        /// </summary>
        public static bool BringToForeground(IntPtr window)
        {
            if (SetForegroundWindow(window) && GetForegroundWindow() == window)
            {
                return true;
            }

            const byte VK_MENU = 0x12;
            const uint KEYEVENTF_KEYUP = 0x0002;
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            SetForegroundWindow(window);
            return GetForegroundWindow() == window;
        }

        /// <summary>The bounds, in physical pixels, of the monitor <paramref name="window"/> is mostly on.</summary>
        public static RECT MonitorBoundsOf(IntPtr window)
        {
            const uint MONITOR_DEFAULTTONEAREST = 2;
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), ref info);
            return info.rcMonitor;
        }
    }
}
