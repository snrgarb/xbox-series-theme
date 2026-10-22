using System.Runtime.InteropServices;

namespace XboxSeries.BootShell
{
    /// <summary>The Win32 calls the Boot shell makes, written by hand for NativeAOT.</summary>
    internal static unsafe partial class NativeMethods
    {
        public const uint EVENT_MODIFY_STATE = 0x0002;
        public const int ASFW_ANY = -1;
        public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

        public const uint WS_POPUP = 0x80000000;
        public const uint WS_EX_TOPMOST = 0x00000008;
        public const uint WS_EX_TOOLWINDOW = 0x00000080;
        public const uint WS_EX_NOACTIVATE = 0x08000000;
        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
        public const int BLACK_BRUSH = 4;
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_SETCURSOR = 0x0020;

        public const uint INFINITE = 0xFFFFFFFF;
        public const uint WAIT_FAILED = 0xFFFFFFFF;
        public const uint QS_ALLINPUT = 0x04FF;
        public const uint MWMO_INPUTAVAILABLE = 0x0004;
        public const uint PM_REMOVE = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        public struct WNDCLASSEXW
        {
            public uint cbSize;
            public uint style;
            public delegate* unmanaged<nint, uint, nint, nint, nint> lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public char* lpszMenuName;
            public char* lpszClassName;
            public nint hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public nint hwnd;
            public uint message;
            public nint wParam;
            public nint lParam;
            public uint time;
            public int ptX;
            public int ptY;
            public uint lPrivate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFOW
        {
            public int cb;
            public nint lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public nint lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public nint hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        // kernel32: processes and events

        [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CreateProcess(string applicationName, char* commandLine, nint processAttributes, nint threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, nint environment, string? currentDirectory,
            in STARTUPINFOW startupInfo, out PROCESS_INFORMATION processInformation);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(nint handle);

        [LibraryImport("kernel32.dll", EntryPoint = "OpenEventW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        public static partial nint OpenEvent(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, string name);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetEvent(nint handle);

        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")]
        public static partial nint GetModuleHandle(nint moduleName);

        // user32: the black window and its messages

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessDpiAwarenessContext(nint value);

        [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
        public static partial ushort RegisterClassEx(WNDCLASSEXW* windowClass);

        [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
        public static partial nint CreateWindowEx(uint exStyle, char* className, char* windowName, uint style,
            int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

        [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
        public static partial nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(nint hwnd, int command);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UpdateWindow(nint hwnd);

        [LibraryImport("user32.dll")]
        public static partial int GetSystemMetrics(int index);

        [LibraryImport("user32.dll")]
        public static partial nint SetCursor(nint cursor);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial uint MsgWaitForMultipleObjectsEx(uint count, nint* handles, uint milliseconds, uint wakeMask, uint flags);

        [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PeekMessage(out MSG message, nint hwnd, uint filterMin, uint filterMax, uint remove);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TranslateMessage(in MSG message);

        [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
        public static partial nint DispatchMessage(in MSG message);

        // user32: the shell and the foreground

        [LibraryImport("user32.dll")]
        public static partial nint GetShellWindow();

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AllowSetForegroundWindow(int processId);

        // gdi32, dwmapi

        [LibraryImport("gdi32.dll")]
        public static partial nint GetStockObject(int index);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmFlush();
    }
}
