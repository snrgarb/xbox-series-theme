using System.ComponentModel;
using System.Runtime.InteropServices;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// The black that covers the screen from Windows' Welcome screen to Start-up's first frame: a plain Win32 window,
    /// topmost over the whole virtual screen, painted by its class's black brush, with no cursor. It never takes the
    /// foreground, so Playnite's windows can, and it can't be closed, only taken down.
    /// </summary>
    internal static unsafe class BlackWindow
    {
        private const string ClassName = "XboxSeries.BootShell.Black";

        /// <summary>Shows the black and waits until the desktop has composed it; returns its window.</summary>
        public static nint Show()
        {
            var instance = GetModuleHandle(0);
            fixed (char* className = ClassName)
            {
                var windowClass = new WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = &WindowProc,
                    hInstance = instance,
                    hbrBackground = GetStockObject(BLACK_BRUSH),
                    lpszClassName = className,
                };
                if (RegisterClassEx(&windowClass) == 0)
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                }

                var hwnd = CreateWindowEx(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, className, className, WS_POPUP,
                    GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
                    GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN), 0, 0, instance, 0);
                if (hwnd == 0)
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                }

                ShowWindow(hwnd, SW_SHOWNOACTIVATE);
                UpdateWindow(hwnd);
                DwmFlush();
                return hwnd;
            }
        }

        public static void TakeDown(nint hwnd) => ShowWindow(hwnd, SW_HIDE);

        [UnmanagedCallersOnly]
        private static nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
        {
            switch (message)
            {
                case WM_SETCURSOR:
                    SetCursor(0);
                    return 1;
                case WM_CLOSE:
                    return 0;
                default:
                    return DefWindowProc(hwnd, message, wParam, lParam);
            }
        }
    }
}
