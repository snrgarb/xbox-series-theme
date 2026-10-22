using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// Starts the programs the Boot shell brings up, each from a command line compiled in here: plain CreateProcess,
    /// passing no handles and making no job, so each runs at the Boot shell's own integrity and outlives it.
    /// </summary>
    internal static unsafe class Launcher
    {
        public static readonly string FullscreenApp =
            Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe");

        public const string Explorer = @"C:\Windows\explorer.exe";

        /// <summary>Steam, Discord and Spotify, which the Boot shell starts straight after explorer; their Startup-apps entries are off.</summary>
        public static readonly (string Exe, string Arguments)[] SteamDiscordSpotify =
        {
            (@"C:\Program Files (x86)\Steam\steam.exe", "-silent"),
            (Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Discord\Discord.exe"), "--processStart Discord.exe --process-start-args \"--start-minimized\""),
            (Environment.ExpandEnvironmentVariables(@"%APPDATA%\Spotify\Spotify.exe"), "--autostart --minimized"),
        };

        /// <summary>Starts <paramref name="exe"/> with <paramref name="arguments"/>, in its own folder, and logs it or why it didn't start.</summary>
        public static void Start(SignInLog log, string exe, string arguments = "")
        {
            var commandLine = arguments.Length == 0 ? $"\"{exe}\"" : $"\"{exe}\" {arguments}";
            try
            {
                log.Write($"started pid {StartProcess(exe, commandLine)}: {commandLine}");
            }
            catch (Win32Exception e)
            {
                log.Write($"ERROR: couldn't start {commandLine}: {e.Message}");
            }
        }

        /// <summary>Starts explorer, only while no shell is up, so never a second one.</summary>
        public static void StartExplorer(SignInLog log)
        {
            var shell = GetShellWindow();
            if (shell != 0)
            {
                GetWindowThreadProcessId(shell, out var owner);
                log.Write($"explorer not started: a shell is already up, pid {owner}");
                return;
            }

            Start(log, Explorer);
        }

        /// <summary>Starts one process and returns its pid; throws if Windows won't.</summary>
        private static int StartProcess(string exe, string commandLine)
        {
            // CreateProcess may write to the command line, so it gets a copy.
            var buffer = (commandLine + '\0').ToCharArray();
            var folder = Path.GetDirectoryName(exe);
            var startupInfo = new STARTUPINFOW { cb = sizeof(STARTUPINFOW) };
            fixed (char* line = buffer)
            {
                if (!CreateProcess(exe, line, 0, 0, false, 0, 0, Directory.Exists(folder) ? folder : null, startupInfo, out var process))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                }

                CloseHandle(process.hThread);
                CloseHandle(process.hProcess);
                return process.dwProcessId;
            }
        }
    }
}
