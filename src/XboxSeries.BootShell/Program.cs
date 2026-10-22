using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using XboxSeries.Extension;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// The Boot shell at sign-in: Playnite's Fullscreen first, then black over the screen until Start-up's first frame,
    /// then explorer, Steam, Discord and Spotify as the Boot video ends (<see cref="SignInSchedule"/>).
    /// </summary>
    internal static unsafe class Program
    {
        private static int Main()
        {
            var sinceStart = Stopwatch.StartNew();
            var log = new SignInLog(Process.GetCurrentProcess().StartTime);
            log.Write("Boot shell started");

            // Fullscreen before anything else: Windows' Welcome screen stays up until the black is there.
            AllowSetForegroundWindow(ASFW_ANY);
            log.Write("Playnite's Fullscreen:");
            Launcher.Start(log, Launcher.FullscreenApp, "--hidesplashscreen");

            nint black = 0;
            var explorerStarted = false;
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                black = BlackWindow.Show();
                log.Write("black up");
                SwitchDesktop(log);

                using var firstFrame = MakeEvent(BootShellEvents.FirstFrame);
                using var bootVideoEnded = MakeEvent(BootShellEvents.BootVideoEnded);
                log.Write("events made");

                OpenLog(log);
                log.Write($"Windows build {WindowsBuild()}");

                var schedule = new SignInSchedule(() => sinceStart.Elapsed);
                var firstFrameSeen = false;
                var bootVideoEndedSeen = false;
                while (true)
                {
                    var limit = $"no first frame by the {SignInSchedule.Limit.TotalSeconds} s limit";
                    if (schedule.TakeBlackDown())
                    {
                        BlackWindow.TakeDown(black);
                        log.Write("black down: " + (firstFrameSeen ? "Start-up's first frame" : limit));
                    }

                    if (schedule.StartExplorer())
                    {
                        log.Write("explorer, Steam, Discord and Spotify: " + (bootVideoEndedSeen ? "the Boot video ended"
                            : firstFrameSeen ? $"no Boot video end {SignInSchedule.AfterFirstFrame.TotalSeconds} s after the first frame" : limit));
                        explorerStarted = true;
                        StartExplorerAndApps(log);
                    }

                    if (schedule.UntilNext is not TimeSpan until)
                    {
                        break;
                    }

                    var signal = Wait(until, firstFrameSeen ? null : firstFrame, bootVideoEndedSeen ? null : bootVideoEnded);
                    if (signal == firstFrame)
                    {
                        firstFrameSeen = true;
                        log.Write("signal: FirstFrame");
                        schedule.FirstFrame();
                    }
                    else if (signal == bootVideoEnded)
                    {
                        bootVideoEndedSeen = true;
                        log.Write("signal: BootVideoEnded");
                        schedule.BootVideoEnded();
                    }
                }

                // The shell stays up until sign-out, as Winlogon expects of the shell it started.
                log.Write("sign-in done; up until sign-out");
                while (true)
                {
                    Wait(Timeout.InfiniteTimeSpan, null, null);
                }
            }
            catch (Exception e)
            {
                // Never leave the TV on black with no shell.
                log.Write($"ERROR: {e}");
                if (black != 0)
                {
                    BlackWindow.TakeDown(black);
                    log.Write("black down: error");
                }

                if (!explorerStarted)
                {
                    StartExplorerAndApps(log);
                }

                return 1;
            }
            finally
            {
                log.Dispose();
            }
        }

        private static void StartExplorerAndApps(SignInLog log)
        {
            Launcher.StartExplorer(log);
            foreach (var (exe, arguments) in Launcher.SteamDiscordSpotify)
            {
                Launcher.Start(log, exe, arguments);
            }
        }

        /// <summary>Tells Windows the shell's desktop is up, so the Welcome screen gives way to the black.</summary>
        private static void SwitchDesktop(SignInLog log)
        {
            var handle = OpenEvent(EVENT_MODIFY_STATE, false, "ShellDesktopSwitchEvent");
            if (handle == 0)
            {
                log.Write($"ERROR: couldn't open ShellDesktopSwitchEvent: error {Marshal.GetLastPInvokeError()}");
                return;
            }

            var set = SetEvent(handle);
            var error = Marshal.GetLastPInvokeError();
            CloseHandle(handle);
            log.Write(set ? "ShellDesktopSwitchEvent set" : $"ERROR: couldn't set ShellDesktopSwitchEvent: error {error}");
        }

        /// <summary>One of the Boot shell's two events, which Start-up sets, made unset; one left over is reset.</summary>
        private static EventWaitHandle MakeEvent(string name)
        {
            var handle = new EventWaitHandle(false, EventResetMode.ManualReset, name);
            handle.Reset();
            return handle;
        }

        private static void OpenLog(SignInLog log)
        {
            try
            {
                log.Open(SignInLog.DefaultFolder);
            }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
            {
                log.Write($"ERROR: couldn't open the log: {e.Message}");
            }
        }

        private static string WindowsBuild()
        {
            const string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            return $"{Registry.GetValue(key, "CurrentBuild", "?")}.{Registry.GetValue(key, "UBR", "?")} {Registry.GetValue(key, "DisplayVersion", "")}";
        }

        /// <summary>
        /// Waits up to <paramref name="timeout"/>, or for ever with <see cref="Timeout.InfiniteTimeSpan"/>, for either
        /// event still unseen, keeping the black window's messages going; returns the event set, or null.
        /// </summary>
        private static EventWaitHandle? Wait(TimeSpan timeout, EventWaitHandle? first, EventWaitHandle? second)
        {
            var events = new[] { first, second }.Where(e => e != null).ToArray();
            var handles = stackalloc nint[2];
            for (var i = 0; i < events.Length; i++)
            {
                handles[i] = events[i]!.SafeWaitHandle.DangerousGetHandle();
            }

            var milliseconds = timeout == Timeout.InfiniteTimeSpan ? INFINITE : (uint)Math.Ceiling(timeout.TotalMilliseconds);
            var result = MsgWaitForMultipleObjectsEx((uint)events.Length, handles, milliseconds, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
            if (result == WAIT_FAILED)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            if (result < events.Length)
            {
                return events[result];
            }

            while (PeekMessage(out var message, 0, 0, 0, PM_REMOVE))
            {
                TranslateMessage(message);
                DispatchMessage(message);
            }

            return null;
        }
    }
}
