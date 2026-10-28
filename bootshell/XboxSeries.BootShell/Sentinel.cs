using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// --sentinel, which explorer's startup run starts from the HKCU Run entry setup writes. A full Windows upgrade may
    /// drop the HKCU Shell value, and the Boot shell can't notice a sign-in it never ran: if no Boot shell holds its
    /// mutex in this session, explorer was the shell from the start, so the sentinel writes Shell back, naming its own
    /// exe, and starts Fullscreen. The next sign-in is the Boot shell's again. Its lines go to logs\sentinel.log.
    /// </summary>
    internal static class Sentinel
    {
        /// <summary>The Boot shell's mutex, which it holds for its session.</summary>
        public const string MutexName = @"Local\XboxSeries.BootShell";

        public static int Run()
        {
            using var log = new SignInLog(Process.GetCurrentProcess().StartTime);
            try
            {
                log.OpenFile(Path.Combine(SignInLog.DefaultFolder, "sentinel.log"));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Its lines then go nowhere, but it still does its job.
            }

            try
            {
                var bootShellRuns = Mutex.TryOpenExisting(MutexName, out var mutex);
                mutex?.Dispose();
                Check(new UserRegistry(), Environment.ProcessPath!, bootShellRuns, () => Launcher.StartUnlessRunning(log, Launcher.FullscreenApp, "--hidesplashscreen"), log.Write);
                return 0;
            }
            catch (Exception e)
            {
                log.Write($"ERROR: {e}");
                return 1;
            }
        }

        /// <summary>With no Boot shell running, writes Shell back as <paramref name="exe"/> unless it names it, and starts Fullscreen.</summary>
        public static void Check(IRegistry registry, string exe, bool bootShellRuns, Action startFullscreen, Action<string> log)
        {
            if (bootShellRuns)
            {
                log("sentinel: the Boot shell runs; nothing to do");
                return;
            }

            log("sentinel: no Boot shell in this session, so explorer was the shell from the start");
            var shell = registry.Get(Setup.Winlogon, "Shell") as string;
            if (string.Equals(shell, exe, StringComparison.OrdinalIgnoreCase))
            {
                log($"Shell holds, {exe}");
            }
            else
            {
                registry.Set(Setup.Winlogon, "Shell", exe);
                log($"Shell: {shell ?? "none"} -> {exe}");
            }

            startFullscreen();
        }
    }
}
