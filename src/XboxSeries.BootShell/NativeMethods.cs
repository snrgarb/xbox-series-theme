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
        public static readonly nint HWND_TOPMOST = -1;
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
        public const int SM_SHUTTINGDOWN = 0x2000;
        public const int BLACK_BRUSH = 4;
        public const uint WM_PAINT = 0x000F;
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_QUERYENDSESSION = 0x0011;
        public const uint WM_ENDSESSION = 0x0016;
        public const uint WM_SETCURSOR = 0x0020;

        public const uint INFINITE = 0xFFFFFFFF;
        public const uint WAIT_FAILED = 0xFFFFFFFF;
        public const uint QS_ALLINPUT = 0x04FF;
        public const uint MWMO_INPUTAVAILABLE = 0x0004;
        public const uint PM_REMOVE = 0x0001;

        public const uint SYNCHRONIZE = 0x00100000;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint PROCESS_TERMINATE = 0x0001;
        public const uint GW_OWNER = 4;
        public const uint WAIT_OBJECT_0 = 0;
        public const int ProcessCommandLineInformation = 60;

        public const int TRANSPARENT = 1;
        public const uint DT_CENTER = 0x0001, DT_VCENTER = 0x0004, DT_SINGLELINE = 0x0020, DT_NOPREFIX = 0x0800;
        public const int FW_NORMAL = 400;
        public const uint CLEARTYPE_QUALITY = 5;

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
        public struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PAINTSTRUCT
        {
            public nint hdc;
            public int fErase;
            public RECT rcPaint;
            public int fRestore, fIncUpdate;
            public fixed byte rgbReserved[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING
        {
            public ushort Length, MaximumLength;
            public char* Buffer;
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

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial uint WaitForSingleObject(nint handle, uint milliseconds);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetExitCodeProcess(nint process, out uint exitCode);

        [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryFullProcessImageName(nint process, uint flags, char* name, ref uint size);

        [LibraryImport("ntdll.dll")]
        public static partial int NtQueryInformationProcess(nint process, int informationClass, void* information, uint length, out uint returnLength);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TerminateProcess(nint process, uint exitCode);

        /// <summary>An export by its ordinal, as <paramref name="ordinal"/> below 0x10000 is taken for one.</summary>
        [LibraryImport("kernel32.dll")]
        public static partial nint GetProcAddress(nint module, nint ordinal);

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

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool InvalidateRect(nint hwnd, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

        [LibraryImport("user32.dll")]
        public static partial nint BeginPaint(nint hwnd, PAINTSTRUCT* paint);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool EndPaint(nint hwnd, PAINTSTRUCT* paint);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetClientRect(nint hwnd, out RECT rect);

        [LibraryImport("user32.dll", EntryPoint = "DrawTextW")]
        public static partial int DrawText(nint hdc, char* text, int length, RECT* rect, uint format);

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

        // user32: Playnite's windows

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint param);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(nint hwnd);

        [LibraryImport("user32.dll")]
        public static partial nint GetWindow(nint hwnd, uint command);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW")]
        public static partial int GetWindowText(nint hwnd, char* text, int maxCount);

        [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);

        // gdi32, dwmapi

        [LibraryImport("gdi32.dll")]
        public static partial nint GetStockObject(int index);

        [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", StringMarshalling = StringMarshalling.Utf16)]
        public static partial nint CreateFont(int height, int width, int escapement, int orientation, int weight, uint italic,
            uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);

        [LibraryImport("gdi32.dll")]
        public static partial nint SelectObject(nint hdc, nint obj);

        [LibraryImport("gdi32.dll")]
        public static partial uint SetTextColor(nint hdc, uint color);

        [LibraryImport("gdi32.dll")]
        public static partial int SetBkMode(nint hdc, int mode);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmFlush();
    }
}
