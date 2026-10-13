using System;
using System.Runtime.InteropServices;
using System.Text;

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
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
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

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// The first visible, unowned, non-tool top-level window whose program's path satisfies <paramref name="matches"/>,
        /// or <see cref="IntPtr.Zero"/>. Programs whose path can't be read (elevated ones) are skipped.
        /// </summary>
        public static IntPtr FindTopLevelWindow(Func<string, bool> matches)
        {
            const uint GW_OWNER = 4;
            const int GWL_EXSTYLE = -20;
            const int WS_EX_TOOLWINDOW = 0x80;
            var found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)
                    || GetWindow(hWnd, GW_OWNER) != IntPtr.Zero
                    || (GetWindowLong(hWnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0)
                {
                    return true;
                }

                GetWindowThreadProcessId(hWnd, out var processId);
                var path = ProgramPathOf(processId);
                if (path != null && matches(path))
                {
                    found = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static string ProgramPathOf(uint processId)
        {
            const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var size = 1024;
                var name = new StringBuilder(size);
                return QueryFullProcessImageName(process, 0, name, ref size) ? name.ToString() : null;
            }
            finally
            {
                CloseHandle(process);
            }
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
