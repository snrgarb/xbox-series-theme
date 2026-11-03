using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace XboxSeries.BootShell
{
    /// <summary>Task Scheduler's tasks in its root folder, as setup reads and writes them: each as its XML; null for none.</summary>
    internal interface ITaskScheduler
    {
        string? Get(string name);

        /// <summary>Registers the task, replacing one of the same name.</summary>
        void Set(string name, string xml);

        void Delete(string name);
    }

    /// <summary>
    /// Through schtasks.exe, at the caller's own rights: a logon task for the user registers without admin on this PC.
    /// </summary>
    internal sealed class UserTasks : ITaskScheduler
    {
        public string? Get(string name)
        {
            var (exitCode, output, error) = Schtasks($"/Query /TN \"{name}\" /XML");
            if (exitCode == 0)
            {
                return output;
            }

            // schtasks fails the same way for a task that isn't there as for one it can't read, and says which only in
            // Windows' own language: its list of every task tells, by name, in any.
            var (listed, list, listError) = Schtasks("/Query /FO CSV /NH");
            if (listed != 0)
            {
                throw Failed($"{error}; listing the tasks: {listError}");
            }

            return Lists(list, name) ? throw Failed(error) : null;
        }

        /// <summary>Whether <paramref name="csv"/>, schtasks' list of every task, holds <paramref name="name"/> in its root folder.</summary>
        internal static bool Lists(string csv, string name)
        {
            var quoted = $"\"\\{name}\",";
            foreach (var line in csv.Split('\n'))
            {
                if (line.TrimStart().StartsWith(quoted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void Set(string name, string xml)
        {
            // schtasks reads the XML from a file, as UTF-16.
            var file = Path.Combine(Path.GetTempPath(), $"XboxSeries.BootShell.{Guid.NewGuid():N}.xml");
            File.WriteAllText(file, xml, Encoding.Unicode);
            try
            {
                Run($"/Create /TN \"{name}\" /XML \"{file}\" /F");
            }
            finally
            {
                File.Delete(file);
            }
        }

        public void Delete(string name)
        {
            if (Get(name) != null)
            {
                Run($"/Delete /TN \"{name}\" /F");
            }
        }

        private static void Run(string arguments)
        {
            var (exitCode, _, error) = Schtasks(arguments);
            if (exitCode != 0)
            {
                throw Failed(error);
            }
        }

        private static (int ExitCode, string Output, string Error) Schtasks(string arguments)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output, error.Result.Trim());
        }

        private static IOException Failed(string error) => new($"schtasks: {error}");
    }
}
