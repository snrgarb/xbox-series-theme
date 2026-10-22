using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            var appeared = new List<PlayniteProcess>();
            var present = new HashSet<int>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    present.Add(process.Id);
                    if (handles.ContainsKey(process.Id) || notPlaynite.Contains(process.Id) || process.SessionId != session)
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

            return (appeared, exited);
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
