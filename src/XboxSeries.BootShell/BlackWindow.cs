using System.ComponentModel;
using System.Runtime.InteropServices;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// The black that covers the screen from Windows' Welcome screen to Start-up's first frame, and over each later
    /// Fullscreen start: a plain Win32 window, topmost over the whole virtual screen, painted by its class's black brush,
    /// with no cursor, and a line of white text when it has one. It never takes the foreground, so Playnite's windows
    /// can, and it can't be closed, only taken down. Being the shell's top-level window, it also hears the session end.
    /// </summary>
    internal static unsafe class BlackWindow
    {
        private const string ClassName = "XboxSeries.BootShell.Black";
        private const uint White = 0x00FFFFFF;

        private static string? line;
        private static nint font;
        private static volatile bool sessionEnding;

        /// <summary>Windows is ending the session: signing out, restarting or shutting down.</summary>
        public static bool SessionEnding => sessionEnding || GetSystemMetrics(SM_SHUTTINGDOWN) != 0;

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

        /// <summary>Puts the black back up, above every topmost window, without taking the foreground.</summary>
        public static void Raise(nint hwnd)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            UpdateWindow(hwnd);
            DwmFlush();
        }

        public static void TakeDown(nint hwnd) => ShowWindow(hwnd, SW_HIDE);

        /// <summary>Shows <paramref name="text"/> in white across the black's middle, or nothing with null.</summary>
        public static void SetLine(nint hwnd, string? text)
        {
            line = text;
            InvalidateRect(hwnd, 0, true);
            UpdateWindow(hwnd);
        }

        private static void Paint(nint hwnd)
        {
            PAINTSTRUCT paint;
            var hdc = BeginPaint(hwnd, &paint);
            if (line != null)
            {
                GetClientRect(hwnd, out var rect);
                if (font == 0)
                {
                    font = CreateFont(-(rect.bottom - rect.top) / 30, 0, 0, 0, FW_NORMAL, 0, 0, 0, 0, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
                }

                var old = SelectObject(hdc, font);
                SetTextColor(hdc, White);
                SetBkMode(hdc, TRANSPARENT);
                fixed (char* text = line)
                {
                    DrawText(hdc, text, line.Length, &rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                }

                SelectObject(hdc, old);
            }

            EndPaint(hwnd, &paint);
        }

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
                case WM_PAINT:
                    Paint(hwnd);
                    return 0;
                case WM_QUERYENDSESSION:
                    sessionEnding = true;
                    return 1;
                case WM_ENDSESSION:
                    sessionEnding |= wParam != 0;
                    return 0;
                default:
                    return DefWindowProc(hwnd, message, wParam, lParam);
            }
        }
    }
}
