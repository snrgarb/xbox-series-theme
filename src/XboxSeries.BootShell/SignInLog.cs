using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// The Boot shell's plain-text log, one file per sign-in, named for when its process was created. Each line is
    /// stamped with the wall clock, ms since boot and ms since the process was created. Lines written before
    /// <see cref="Open"/> wait in memory, so starting Playnite never waits on the disk. Opening deletes the files past
    /// the newest <see cref="Kept"/>.
    /// </summary>
    internal sealed class SignInLog : IDisposable
    {
        public const int Kept = 30;

        private readonly DateTime created;
        private readonly TimeSpan createdBeforeLog;
        private readonly Stopwatch sinceLog = Stopwatch.StartNew();
        private readonly List<string> pending = new();
        private StreamWriter? writer;

        /// <param name="created">When the Boot shell's process was created, on the local clock.</param>
        public SignInLog(DateTime created)
        {
            this.created = created;

            // Read off the wall clock once, so a clock set later never moves the ms since creation.
            createdBeforeLog = DateTime.Now - created;
        }

        public static string DefaultFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxSeries.BootShell", "logs");

        public void Write(string message)
        {
            var sinceCreated = (long)(createdBeforeLog + sinceLog.Elapsed).TotalMilliseconds;
            var line = $"{DateTime.Now:HH:mm:ss.fff} boot+{Environment.TickCount64} h+{sinceCreated}  {message}";
            if (writer == null)
            {
                pending.Add(line);
                return;
            }

            writer.WriteLine(line);
        }

        /// <summary>Opens this sign-in's file in <paramref name="folder"/>, writes the waiting lines and deletes old files.</summary>
        public void Open(string folder)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, created.ToString("yyyyMMdd-HHmmss") + ".log");
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            foreach (var line in pending)
            {
                writer.WriteLine(line);
            }

            pending.Clear();

            // The names sort by when each sign-in started.
            foreach (var old in Directory.GetFiles(folder, "*.log").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(Kept))
            {
                try
                {
                    File.Delete(old);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Write($"log: couldn't delete {old}: {e.Message}");
                }
            }
        }

        public void Dispose() => writer?.Dispose();
    }
}
