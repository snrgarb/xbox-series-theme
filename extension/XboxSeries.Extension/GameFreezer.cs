using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Suspends a running game's processes while the Guide is open, and resumes them, so a game that reads the pad in the
    /// background can't take its input. The processes are those under the game's install directory, and the one that
    /// owns its window, never Playnite's own nor one of Windows'. One Windows won't let us open or suspend (elevated, or
    /// guarded by anti-cheat), or one that ends meanwhile, is skipped; a game installed at a drive's root isn't frozen.
    /// Suspending counts, so a process is suspended once per freeze at most. A game left frozen would stay so after
    /// Playnite is gone, so the frozen processes' ids, with their start times to tell them from later processes given the
    /// same id, are written to a file before any is suspended and deleted once all are resumed; whatever the file still
    /// lists when Playnite starts again is resumed then. Safe to call from any thread.
    /// </summary>
    internal sealed class GameFreezer
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly object gate = new object();
        private readonly string file;

        /// <summary>The processes frozen, or null when no freeze is in effect (an empty list is one that froze none).</summary>
        private List<FrozenProcess> frozen;

        public GameFreezer(string dataDirectory)
        {
            file = Path.Combine(dataDirectory, "frozen-game.txt");
        }

        /// <summary>
        /// Suspends the processes under <paramref name="directory"/>, the game's install directory, and the one owning
        /// <paramref name="window"/>, until <see cref="Thaw"/>; nothing while a freeze is already in effect. Never throws
        /// for a process Windows won't let us suspend or one that ends meanwhile, which is skipped.
        /// </summary>
        public void Freeze(string directory, IntPtr window)
        {
            lock (gate)
            {
                if (frozen != null)
                {
                    return;
                }

                frozen = new List<FrozenProcess>();
                if (IsDriveRoot(directory))
                {
                    logger.Warn($"Not freezing the game: its install directory, {directory}, is a drive's root, under which everything would freeze");
                    return;
                }

                // Each one's start time read first, so the record tells it from a later process given the same id.
                var found = new List<FrozenProcess>();
                foreach (var candidate in GameProcessesOf(directory, window))
                {
                    var startTime = StartTimeOf(candidate.Key);
                    if (startTime == 0)
                    {
                        logger.Warn($"Couldn't open {candidate.Value} ({candidate.Key}) to freeze it, or it has ended; it isn't frozen");
                        continue;
                    }

                    found.Add(new FrozenProcess(candidate.Key, startTime, candidate.Value));
                }

                if (found.Count == 0)
                {
                    logger.Info($"Found no process of the game to freeze, under {directory} or owning window {window}");
                    return;
                }

                // Written first, so a crash between here and the thaw still leaves a record of what to resume.
                if (!Save(found))
                {
                    logger.Warn("Not freezing the game: without its record, a crash would leave it frozen");
                    return;
                }

                foreach (var process in found)
                {
                    var status = Apply(process, NativeMethods.SuspendProcess);
                    if (status == 0)
                    {
                        frozen.Add(process);
                    }
                    else if (status == null)
                    {
                        logger.Info($"{process} ended before it could be frozen");
                    }
                    else
                    {
                        logger.Warn($"Windows refused to freeze {process} (NTSTATUS 0x{status:X8}); it keeps running while the Guide is open");
                    }
                }

                if (frozen.Count != found.Count)
                {
                    Save(frozen);
                }

                logger.Info(frozen.Count == 0
                    ? "Froze none of the game's processes"
                    : $"Froze {string.Join(", ", frozen)}");
            }
        }

        /// <summary>Resumes every process <see cref="Freeze"/> suspended, and deletes the record; nothing when no freeze is in effect.</summary>
        public void Thaw()
        {
            lock (gate)
            {
                if (frozen == null)
                {
                    return;
                }

                Resume(frozen);
                frozen = null;
                Delete();
            }
        }

        /// <summary>Resumes whatever the record from a Playnite that stopped with a game frozen still lists, and deletes it.</summary>
        public void ThawLeftovers()
        {
            lock (gate)
            {
                List<FrozenProcess> leftovers;
                try
                {
                    if (!File.Exists(file))
                    {
                        return;
                    }

                    leftovers = File.ReadAllLines(file).Select(FrozenProcess.Parse).Where(p => p != null).ToList();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    logger.Warn(e, $"Couldn't read {file}, the record of a game left frozen");
                    return;
                }

                logger.Warn($"Playnite stopped last time with a game frozen; thawing {string.Join(", ", leftovers)}");
                Resume(leftovers);
                Delete();
            }
        }

        /// <summary>Whether <paramref name="directory"/> is a drive's or a share's root, under which every process of it would match.</summary>
        private static bool IsDriveRoot(string directory)
        {
            try
            {
                var root = Path.GetPathRoot(directory);
                return !string.IsNullOrEmpty(root)
                    && string.Equals(root.TrimEnd('\\'), directory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                // Not a path at all, so nothing is under it.
                return false;
            }
        }

        /// <summary>
        /// The ids and names of the processes under <paramref name="directory"/> and the one owning <paramref name="window"/>,
        /// Playnite's and Windows' left out, as are any that end while they're looked at.
        /// </summary>
        private static Dictionary<int, string> GameProcessesOf(string directory, IntPtr window)
        {
            // Made whole and followed past junctions and subst drives, as the processes' own paths may be either.
            var prefixes = string.IsNullOrEmpty(directory) ? new List<string>() : NativeMethods.PathPrefixesOf(directory);
            var windowsPrefix = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            var windowOwner = window == IntPtr.Zero ? 0 : NativeMethods.ProcessIdOf(window);
            int self;
            using (var current = Process.GetCurrentProcess())
            {
                self = current.Id;
            }

            var candidates = new Dictionary<int, string>();
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                logger.Warn(e, "Couldn't list the running processes, so none of the game's is frozen");
                return candidates;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    // The id is the snapshot's own; anything else read of a process that has since ended throws.
                    var id = process.Id;
                    if (id == self || id == 0)
                    {
                        continue;
                    }

                    var path = NativeMethods.ProgramPathOf((uint)id);
                    var underGame = path != null && prefixes.Exists(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (!underGame && id != windowOwner)
                    {
                        continue;
                    }

                    string name;
                    try
                    {
                        name = process.ProcessName;
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                    {
                        // Ended since the snapshot, so there's nothing to freeze.
                        continue;
                    }

                    // Explorer, or a host such as ApplicationFrameHost owning a store game's window: freezing one would freeze Windows.
                    if (path == null || path.StartsWith(windowsPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Info($"Not freezing {name} ({id}), a process of Windows' or one we can't read");
                        continue;
                    }

                    candidates[id] = name;
                }
            }

            return candidates;
        }

        /// <summary>When process <paramref name="id"/> started, as a FILETIME; 0 when it can't be opened or has ended.</summary>
        private static long StartTimeOf(int id)
        {
            var handle = NativeMethods.OpenToSuspend(id);
            if (handle == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                return NativeMethods.StartTimeOf(handle);
            }
            finally
            {
                NativeMethods.CloseProcess(handle);
            }
        }

        /// <summary>
        /// Suspends or resumes <paramref name="process"/> with <paramref name="action"/>, returning its NTSTATUS, 0 on
        /// success; null when the process can't be opened or has ended, its id perhaps another's now, which isn't touched.
        /// </summary>
        private static int? Apply(FrozenProcess process, Func<IntPtr, int> action)
        {
            var handle = NativeMethods.OpenToSuspend(process.Id);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return NativeMethods.StartTimeOf(handle) == process.StartTime ? action(handle) : (int?)null;
            }
            finally
            {
                NativeMethods.CloseProcess(handle);
            }
        }

        private static void Resume(IEnumerable<FrozenProcess> processes)
        {
            var thawed = new List<FrozenProcess>();
            foreach (var process in processes)
            {
                var status = Apply(process, NativeMethods.ResumeProcess);
                if (status == 0)
                {
                    thawed.Add(process);
                }
                else if (status == null)
                {
                    logger.Info($"{process} has ended, or can't be opened, so isn't thawed");
                }
                else
                {
                    logger.Warn($"Windows refused to thaw {process} (NTSTATUS 0x{status:X8})");
                }
            }

            if (thawed.Count > 0)
            {
                logger.Info($"Thawed {string.Join(", ", thawed)}");
            }
        }

        private bool Save(List<FrozenProcess> processes)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllLines(file, processes.Select(p => p.ToLine()));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                logger.Warn(e, $"Couldn't write {file}, the record of the game's frozen processes");
                return false;
            }
        }

        private void Delete()
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                logger.Warn(e, $"Couldn't delete {file}; the next start-up will try to thaw what it lists again");
            }
        }

        /// <summary>A process frozen: its id, its start time telling it from a later process given the same id, and its name for the log.</summary>
        private sealed class FrozenProcess
        {
            public FrozenProcess(int id, long startTime, string name)
            {
                Id = id;
                StartTime = startTime;
                Name = name;
            }

            public int Id { get; }

            public long StartTime { get; }

            public string Name { get; }

            public string ToLine() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", Id, StartTime, Name);

            /// <summary>A line of the record, or null when it isn't one.</summary>
            public static FrozenProcess Parse(string line)
            {
                var parts = line.Split(new[] { ' ' }, 3);
                return parts.Length >= 2
                    && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var startTime)
                    ? new FrozenProcess(id, startTime, parts.Length == 3 ? parts[2] : "?")
                    : null;
            }

            public override string ToString() => $"{Name} ({Id})";
        }
    }
}
