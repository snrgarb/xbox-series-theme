using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using XboxSeries.Extension;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// The Boot shell: at sign-in, Playnite's Fullscreen first, then black over the screen until Start-up's first frame,
    /// then explorer, Steam, Discord and Spotify as the Boot video ends (<see cref="SignInSchedule"/>); then, until
    /// Windows ends the session, it keeps Fullscreen up (<see cref="Watchdog"/>), polling Playnite's processes every
    /// 100 ms, and starts explorer again should Windows not (<see cref="ShellWatch"/>). It reads the pad itself
    /// (<see cref="XInputPad"/>): the Guide button brings Fullscreen back while none runs, and View+Menu held for 5 s
    /// closes Playnite to the desktop. From a terminal, --install and --uninstall set it up and take it off
    /// (<see cref="Setup"/>); explorer's startup run starts it with --sentinel (<see cref="Sentinel"/>).
    /// </summary>
    internal static unsafe class Program
    {
        private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

        private static readonly Stopwatch sinceStart = Stopwatch.StartNew();
        private static readonly SignInLog log = new(Process.GetCurrentProcess().StartTime);
        private static readonly Watchdog watchdog = new(() => sinceStart.Elapsed, log.Write);
        private static readonly PlayniteProcesses playnite = new();
        private static readonly XInputPad pad = new(() => sinceStart.Elapsed, log);
        private static Mutex? running;
        private static nint black;
        private static bool blackUp;
        private static string? line;
        private static EventWaitHandle? firstFrame;
        private static EventWaitHandle? bootVideoEnded;
        private static bool firstFrameSeen;
        private static bool bootVideoEndedSeen;
        private static bool explorerStarted;

        private static int Main(string[] args)
        {
            switch (args)
            {
                case ["--install"]:
                    return SetupCommand.Run(setup => setup.Install());
                case ["--uninstall"]:
                    return SetupCommand.Run(setup => setup.Uninstall());
                case ["--sentinel"]:
                    return Sentinel.Run();

                // Winlogon starts it with none; anything else is a mistyped command, never a sign-in.
                case [_, ..]:
                    return 2;
            }

            // Held until sign-out, so the sentinel explorer's startup run starts finds it.
            running = new Mutex(false, Sentinel.MutexName);
            log.Write("Boot shell started");

            // Fullscreen before anything else: Windows' Welcome screen stays up until the black is there.
            AllowSetForegroundWindow(ASFW_ANY);
            log.Write("Playnite's Fullscreen:");
            var orders = LaunchFullscreen();

            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                black = BlackWindow.Show();
                blackUp = true;
                log.Write("black up");
                SwitchDesktop();

                firstFrame = MakeEvent(BootShellEvents.FirstFrame);
                bootVideoEnded = MakeEvent(BootShellEvents.BootVideoEnded);
                log.Write("events made");

                OpenLog();
                log.Write($"Windows build {WindowsBuild()}");
                DeleteOldExes();

                var schedule = new SignInSchedule(() => sinceStart.Elapsed);
                var shell = new ShellWatch(() => sinceStart.Elapsed);
                var playniteLog = new PlayniteLog(PlayniteLog.DefaultPath);
                Obey(orders);
                pad.Start();
                while (!BlackWindow.SessionEnding)
                {
                    if (!firstFrameSeen && firstFrame.WaitOne(0))
                    {
                        firstFrameSeen = true;
                        log.Write("signal: FirstFrame");
                        schedule.FirstFrame();
                        watchdog.FirstFrame();
                    }

                    if (!bootVideoEndedSeen && bootVideoEnded.WaitOne(0))
                    {
                        bootVideoEndedSeen = true;
                        log.Write("signal: BootVideoEnded");
                        schedule.BootVideoEnded();
                    }

                    if (playniteLog.CrashLogged())
                    {
                        watchdog.CrashWindowShown();
                    }

                    var (appeared, exited) = playnite.Poll();

                    // Windows tells Playnite the session is ending too, and it may be gone first: hear the end before judging.
                    PumpMessages();
                    if (BlackWindow.SessionEnding)
                    {
                        break;
                    }

                    foreach (var process in appeared)
                    {
                        Obey(watchdog.Appeared(process));
                    }

                    foreach (var (pid, exitCode) in exited)
                    {
                        Obey(watchdog.Exited(pid, exitCode));
                    }

                    while (pad.Presses.TryDequeue(out var presses))
                    {
                        if (presses.HasFlag(PadPresses.Hold))
                        {
                            Obey(watchdog.Held());
                        }

                        if (presses.HasFlag(PadPresses.Guide))
                        {
                            Obey(watchdog.GuidePressed());
                        }
                    }

                    Obey(watchdog.Tick());
                    ShowBlack();

                    if (schedule.StartExplorer() && !explorerStarted)
                    {
                        log.Write("explorer, Steam, Discord and Spotify: " + schedule.Why);
                        StartDesktop();
                    }

                    if (shell.StartExplorer(GetShellWindow() != 0))
                    {
                        log.Write($"explorer: no shell for {ShellWatch.Grace.TotalSeconds} s; starting it again");
                        Launcher.StartExplorer(log);
                    }

                    Wait(Poll, firstFrameSeen ? null : firstFrame, bootVideoEndedSeen ? null : bootVideoEnded);
                }

                // The shell stays up until sign-out, as Winlogon expects of the shell it started.
                log.Write("Windows is ending the session: watching stops");
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
                    StartDesktop();
                }

                return 1;
            }
            finally
            {
                playnite.Dispose();
                log.Dispose();
            }
        }

        /// <summary>Carries out what the watchdog ordered.</summary>
        private static void Obey(Orders orders)
        {
            if (orders.HasFlag(Orders.ResetEvents))
            {
                ResetEvents();
            }

            if (orders.HasFlag(Orders.Relaunch))
            {
                ShowBlack();
                DeleteSafeStartFlag();
                ResetEvents();
                Obey(LaunchFullscreen());
            }

            if (orders.HasFlag(Orders.ClosePlaynite))
            {
                foreach (var pid in watchdog.Closing)
                {
                    PlayniteProcesses.Close(log, pid);
                }
            }

            if (orders.HasFlag(Orders.EndPlaynite))
            {
                foreach (var pid in watchdog.Closing)
                {
                    PlayniteProcesses.End(log, pid);
                }
            }

            // After the hold's WM_CLOSE, so Playnite is closing as the desktop starts.
            if (orders.HasFlag(Orders.StartDesktop))
            {
                StartDesktop();
            }
        }

        /// <summary>Launches Fullscreen, and tells the watchdog how it went.</summary>
        private static Orders LaunchFullscreen()
        {
            if (Launcher.StartFullscreen(log) is not (var pid, var handle))
            {
                return watchdog.LaunchFailed();
            }

            playnite.Adopt(pid, handle);
            watchdog.Launched(pid);
            return Orders.None;
        }

        /// <summary>Puts the black and its line up or down as the watchdog has them.</summary>
        private static void ShowBlack()
        {
            if (watchdog.Black && !blackUp)
            {
                BlackWindow.Raise(black);
                log.Write("black up");
            }
            else if (!watchdog.Black && blackUp)
            {
                BlackWindow.TakeDown(black);
                log.Write("black down");
            }

            blackUp = watchdog.Black;
            if (watchdog.Line != line)
            {
                line = watchdog.Line;
                BlackWindow.SetLine(black, line);
                log.Write(line == null ? "line down" : "line up: " + line);
            }
        }

        /// <summary>Explorer, once per sign-in, then Steam, Discord and Spotify, each unless it's up already.</summary>
        private static void StartDesktop()
        {
            if (!explorerStarted)
            {
                explorerStarted = true;
                Launcher.StartExplorer(log);
            }

            foreach (var (exe, arguments) in Launcher.SteamDiscordSpotify)
            {
                Launcher.StartUnlessRunning(log, exe, arguments);
            }
        }

        /// <summary>Unsets both events before a Fullscreen start, so its signals are its own.</summary>
        private static void ResetEvents()
        {
            firstFrame?.Reset();
            bootVideoEnded?.Reset();
            firstFrameSeen = bootVideoEndedSeen = false;
            log.Write("events reset");
        }

        /// <summary>Deletes Playnite's safestart.flag, so a relaunch after a crash doesn't stop on its safe-mode prompt.</summary>
        private static void DeleteSafeStartFlag()
        {
            var flag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "safestart.flag");
            try
            {
                if (File.Exists(flag))
                {
                    File.Delete(flag);
                    log.Write("deleted " + flag);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log.Write($"ERROR: couldn't delete {flag}: {e.Message}");
            }
        }

        /// <summary>Deletes the builds a later --install renamed aside, none of which runs now.</summary>
        private static void DeleteOldExes()
        {
            var folder = Path.GetDirectoryName(Environment.ProcessPath)!;
            foreach (var old in Directory.GetFiles(folder, "XboxSeries.BootShell.*.old"))
            {
                try
                {
                    File.Delete(old);
                    log.Write("deleted " + old);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    log.Write($"ERROR: couldn't delete {old}: {e.Message}");
                }
            }
        }

        /// <summary>Tells Windows the shell's desktop is up, so the Welcome screen gives way to the black.</summary>
        private static void SwitchDesktop()
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

        private static void OpenLog()
        {
            try
            {
                log.Open(SignInLog.DefaultFolder);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
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
        /// event still unseen, keeping the black window's messages going.
        /// </summary>
        private static void Wait(TimeSpan timeout, EventWaitHandle? first, EventWaitHandle? second)
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

            PumpMessages();
        }

        private static void PumpMessages()
        {
            while (PeekMessage(out var message, 0, 0, 0, PM_REMOVE))
            {
                TranslateMessage(message);
                DispatchMessage(message);
            }
        }
    }
}
