using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// Playnite's processes in this session, found by polling: Playnite.FullscreenApp, Playnite.DesktopApp, and the
    /// updater's update.exe, which runs from %TEMP%\Playnite (Discord's Update.exe doesn't). Each is held open from when
    /// it's found, so its exit code can be read once it exits. It never opens Playnite's mutex.
    /// </summary>
    internal sealed unsafe class PlayniteProcesses : IDisposable
    {
        private static readonly List<nint> found = new();

        private readonly Dictionary<int, nint> handles = new();
        private readonly HashSet<int> notPlaynite = new();
        private readonly int session = Process.GetCurrentProcess().SessionId;
        private readonly string updaterFolder = Path.Combine(Path.GetTempPath(), "Playnite") + Path.DirectorySeparatorChar;

        /// <summary>Holds the Fullscreen the Boot shell launched, from its own handle, so even a quick exit is heard.</summary>
        public void Adopt(int pid, nint handle) => handles[pid] = handle;

        /// <summary>
        /// The Playnite processes that have appeared and exited since the last poll, the new ones first, as Playnite
        /// starts what follows it before it exits.
        /// </summary>
        public (List<PlayniteProcess> Appeared, List<(int Pid, int ExitCode)> Exited) Poll()
        {
            // Exits first, then the processes running: Playnite starts what follows it before it exits, so one heard to
            // exit here has its successor already running for the look below. The other way round, a successor started
            // between the look and its parent's exit was missed, and a restart judged a crash or a quit.
            var exited = new List<(int, int)>();
            foreach (var (pid, handle) in handles)
            {
                if (WaitForSingleObject(handle, 0) != WAIT_OBJECT_0)
                {
                    continue;
                }

                exited.Add((pid, GetExitCodeProcess(handle, out var code) ? (int)code : -1));
                CloseHandle(handle);
                handles.Remove(pid);
            }

            var exitedNow = new HashSet<int>(exited.Select(e => e.Item1));
            var appeared = new List<PlayniteProcess>();
            var present = new HashSet<int>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    present.Add(process.Id);
                    // One just heard to exit can still be listed while something else holds it open.
                    if (handles.ContainsKey(process.Id) || notPlaynite.Contains(process.Id) || exitedNow.Contains(process.Id) || process.SessionId != session)
                    {
                        continue;
                    }

                    if (Found(process) is PlayniteProcess found)
                    {
                        appeared.Add(found);
                    }
                }
            }

            notPlaynite.IntersectWith(present);

            return (appeared, exited);
        }

        /// <summary>Asks pid <paramref name="pid"/> to close: WM_CLOSE to each of its visible, unowned windows titled "Playnite".</summary>
        public static void Close(SignInLog log, int pid)
        {
            found.Clear();
            EnumWindows(&FindPlaynite, pid);
            foreach (var hwnd in found)
            {
                var posted = PostMessage(hwnd, WM_CLOSE, 0, 0);
                log.Write(posted ? $"WM_CLOSE to pid {pid}'s window {hwnd:X}" : $"ERROR: couldn't post WM_CLOSE to pid {pid}'s window {hwnd:X}: error {Marshal.GetLastPInvokeError()}");
            }

            if (found.Count == 0)
            {
                log.Write($"pid {pid} has no Playnite window to close");
            }
        }

        /// <summary>Ends pid <paramref name="pid"/>, as it hasn't closed.</summary>
        public static void End(SignInLog log, int pid)
        {
            var handle = OpenProcess(PROCESS_TERMINATE, false, pid);
            var ended = handle != 0 && TerminateProcess(handle, 1);
            var error = Marshal.GetLastPInvokeError();
            if (handle != 0)
            {
                CloseHandle(handle);
            }

            log.Write(ended ? $"ended pid {pid}" : $"ERROR: couldn't end pid {pid}: error {error}");
        }

        public void Dispose()
        {
            foreach (var handle in handles.Values)
            {
                CloseHandle(handle);
            }

            handles.Clear();
        }

        /// <summary>The process as one of Playnite's, held open; or null, remembered so it isn't looked at again.</summary>
        private PlayniteProcess? Found(Process process)
        {
            PlayniteKind? kind = process.ProcessName.ToLowerInvariant() switch
            {
                "playnite.fullscreenapp" => PlayniteKind.Fullscreen,
                "playnite.desktopapp" => PlayniteKind.Desktop,
                "update" => PlayniteKind.Updater,
                _ => null,
            };
            var handle = kind == null ? 0 : OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
            if (handle != 0 && kind == PlayniteKind.Updater && !ImagePath(handle).StartsWith(updaterFolder, StringComparison.OrdinalIgnoreCase))
            {
                CloseHandle(handle);
                handle = 0;
            }

            if (handle == 0)
            {
                notPlaynite.Add(process.Id);
                return null;
            }

            handles[process.Id] = handle;
            var safeStartup = kind == PlayniteKind.Fullscreen && CommandLine(handle).Contains("--safestartup", StringComparison.OrdinalIgnoreCase);
            return new PlayniteProcess(process.Id, kind!.Value, safeStartup);
        }

        [UnmanagedCallersOnly]
        private static int FindPlaynite(nint hwnd, nint pid)
        {
            GetWindowThreadProcessId(hwnd, out var owner);
            if ((nint)owner != pid || !IsWindowVisible(hwnd) || GetWindow(hwnd, GW_OWNER) != 0)
            {
                return 1;
            }

            var title = stackalloc char[16];
            var length = GetWindowText(hwnd, title, 16);
            if (new string(title, 0, length) == "Playnite")
            {
                found.Add(hwnd);
            }

            return 1;
        }

        private static string ImagePath(nint handle)
        {
            var buffer = stackalloc char[1024];
            uint size = 1024;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : "";
        }

        /// <summary>The process's command line, or "" if Windows won't say.</summary>
        private static string CommandLine(nint handle)
        {
            NtQueryInformationProcess(handle, ProcessCommandLineInformation, null, 0, out var length);
            if (length == 0)
            {
                return "";
            }

            var buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (NtQueryInformationProcess(handle, ProcessCommandLineInformation, (void*)buffer, length, out _) != 0)
                {
                    return "";
                }

                var text = (UNICODE_STRING*)buffer;
                return new string(text->Buffer, 0, text->Length / sizeof(char));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
