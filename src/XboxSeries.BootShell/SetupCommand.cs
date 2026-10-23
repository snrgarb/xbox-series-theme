using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// --install and --uninstall on this PC, from a terminal: <see cref="Setup"/> on the user's registry and folders,
    /// each line written to the terminal and to logs\setup-&lt;yyyyMMdd-HHmmss&gt;.log. The exe is a Windows app, so the
    /// terminal shows its lines, and waits for it, only through a pipe (<c>| Out-Host</c>).
    /// </summary>
    internal static class SetupCommand
    {
        private static readonly TimeSpan CloseLimit = TimeSpan.FromSeconds(60);

        private static readonly string[] PlayniteApps = { "Playnite.FullscreenApp", "Playnite.DesktopApp" };

        public static int Run(Func<Setup, bool> command)
        {
            var paths = SetupPaths.ThisPc();
            var logs = Path.Combine(paths.InstallFolder, "logs");
            Directory.CreateDirectory(logs);
            using var file = new StreamWriter(Path.Combine(logs, $"setup-{DateTime.Now:yyyyMMdd-HHmmss}.log"), true, new UTF8Encoding(false)) { AutoFlush = true };
            void Say(string line)
            {
                Console.WriteLine(line);
                file.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {line}");
            }

            return command(new Setup(new UserRegistry(), paths, () => ClosePlaynite(Say), Say, () => DateTime.Now)) ? 0 : 1;
        }

        /// <summary>
        /// Sends a running Playnite --shutdown, as a second start of its own exe, and waits for it to exit. A running
        /// Boot shell sees it exit 0 with nothing following, and leaves it down.
        /// </summary>
        private static bool ClosePlaynite(Action<string> say)
        {
            var session = Process.GetCurrentProcess().SessionId;
            var running = PlayniteApps.SelectMany(Process.GetProcessesByName).Where(p => p.SessionId == session).ToList();
            if (running.Count == 0)
            {
                return true;
            }

            var exe = Path.Combine(Path.GetDirectoryName(Launcher.FullscreenApp)!, running[0].ProcessName + ".exe");
            say($"Playnite runs, pid {string.Join(", ", running.Select(p => p.Id))}: {exe} --shutdown");
            // The second start hands --shutdown to the first and exits; it's waited for too, so none is left running.
            running.Add(Process.Start(new ProcessStartInfo(exe, "--shutdown") { UseShellExecute = false })!);

            var closed = running.All(p => p.WaitForExit(CloseLimit));
            foreach (var process in running)
            {
                process.Dispose();
            }

            // From an elevated terminal, --shutdown never reaches the user's Playnite.
            say(closed ? "Playnite closed" : $"Playnite didn't close within {CloseLimit.TotalSeconds} s: is this terminal elevated? Close Playnite, then run it again.");
            return closed;
        }
    }
}
