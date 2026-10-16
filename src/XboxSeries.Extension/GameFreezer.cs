using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Suspends a running game's programs while the Guide is open, and resumes them, so a game that reads the pad in the
    /// background can't take its input. The programs are those under the game's install directory, and the one that
    /// owns its window, never Playnite's own nor one of Windows'. One Windows won't let us open or suspend (elevated, or
    /// guarded by anti-cheat) is skipped.
    /// Suspending counts, so a program is suspended once per freeze at most. A game left frozen would stay so after
    /// Playnite is gone, so the frozen programs' ids, with their start times to tell them from later programs given the
    /// same id, are written to a file before any is suspended and deleted once all are resumed; whatever the file still
    /// lists when Playnite starts again is resumed then. Safe to call from any thread.
    /// </summary>
    internal sealed class GameFreezer
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly object gate = new object();
        private readonly string file;

        /// <summary>The programs frozen, or null when no freeze is in effect (an empty list is one that froze none).</summary>
        private List<FrozenProgram> frozen;

        public GameFreezer(string dataDirectory)
        {
            file = Path.Combine(dataDirectory, "frozen-game.txt");
        }

        public void Freeze(string directory, IntPtr window)
        {
            lock (gate)
            {
                if (frozen != null)
                {
                    return;
                }

                var opened = new List<KeyValuePair<FrozenProgram, IntPtr>>();
                try
                {
                    foreach (var program in GameProgramsOf(directory, window))
                    {
                        var handle = NativeMethods.OpenToSuspend(program.Id);
                        if (handle == IntPtr.Zero)
                        {
                            logger.Warn($"Couldn't open {program} to freeze it; it keeps running while the Guide is open");
                            continue;
                        }

                        var startTime = NativeMethods.StartTimeOf(handle);
                        if (startTime == 0)
                        {
                            NativeMethods.CloseProcess(handle);
                            logger.Warn($"Couldn't read when {program} started, so it isn't frozen");
                            continue;
                        }

                        opened.Add(new KeyValuePair<FrozenProgram, IntPtr>(new FrozenProgram(program.Id, startTime, program.Name), handle));
                    }

                    if (opened.Count == 0)
                    {
                        logger.Info($"Found no program of the game to freeze, under {directory} or owning window {window}");
                        frozen = new List<FrozenProgram>();
                        return;
                    }

                    // Written first, so a crash between here and the thaw still leaves a record of what to resume.
                    var programs = opened.Select(p => p.Key).ToList();
                    if (!Save(programs))
                    {
                        logger.Warn("Not freezing the game: without its record, a crash would leave it frozen");
                        frozen = new List<FrozenProgram>();
                        return;
                    }

                    frozen = new List<FrozenProgram>();
                    foreach (var pair in opened)
                    {
                        var status = NativeMethods.SuspendProcess(pair.Value);
                        if (status == 0)
                        {
                            frozen.Add(pair.Key);
                        }
                        else
                        {
                            logger.Warn($"Windows refused to freeze {pair.Key} (NTSTATUS 0x{status:X8}); it keeps running while the Guide is open");
                        }
                    }

                    if (frozen.Count != programs.Count)
                    {
                        Save(frozen);
                    }

                    logger.Info(frozen.Count == 0
                        ? "Froze none of the game's programs"
                        : $"Froze {string.Join(", ", frozen)}");
                }
                finally
                {
                    foreach (var pair in opened)
                    {
                        NativeMethods.CloseProcess(pair.Value);
                    }
                }
            }
        }

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
                List<FrozenProgram> leftovers;
                try
                {
                    if (!File.Exists(file))
                    {
                        return;
                    }

                    leftovers = File.ReadAllLines(file).Select(FrozenProgram.Parse).Where(p => p != null).ToList();
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

        /// <summary>The programs under <paramref name="directory"/> and the one owning <paramref name="window"/>, Playnite's and Windows' left out.</summary>
        private static List<FrozenProgram> GameProgramsOf(string directory, IntPtr window)
        {
            var prefix = string.IsNullOrEmpty(directory) ? null : directory.TrimEnd('\\') + "\\";
            var windowsPrefix = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            var windowOwner = window == IntPtr.Zero ? 0 : NativeMethods.ProcessIdOf(window);
            int self;
            using (var current = Process.GetCurrentProcess())
            {
                self = current.Id;
            }

            var programs = new List<FrozenProgram>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    if (process.Id == self || process.Id == 0)
                    {
                        continue;
                    }

                    var path = NativeMethods.ProgramPathOf((uint)process.Id);
                    var underGame = prefix != null && path != null && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                    if (!underGame && process.Id != windowOwner)
                    {
                        continue;
                    }

                    // Explorer, or a host such as ApplicationFrameHost owning a store game's window: freezing one would freeze Windows.
                    if (path == null || path.StartsWith(windowsPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Info($"Not freezing {process.ProcessName} ({process.Id}), a program of Windows' or one we can't read");
                        continue;
                    }

                    programs.Add(new FrozenProgram(process.Id, 0, process.ProcessName));
                }
            }

            return programs;
        }

        private static void Resume(IEnumerable<FrozenProgram> programs)
        {
            var thawed = new List<FrozenProgram>();
            foreach (var program in programs)
            {
                var handle = NativeMethods.OpenToSuspend(program.Id);
                if (handle == IntPtr.Zero)
                {
                    logger.Info($"{program} is gone, or can't be opened, so isn't thawed");
                    continue;
                }

                try
                {
                    // An id Windows has since given another program, which we never froze.
                    if (NativeMethods.StartTimeOf(handle) != program.StartTime)
                    {
                        logger.Info($"{program} has ended, and its id is another program's now; not thawing that");
                        continue;
                    }

                    var status = NativeMethods.ResumeProcess(handle);
                    if (status == 0)
                    {
                        thawed.Add(program);
                    }
                    else
                    {
                        logger.Warn($"Windows refused to thaw {program} (NTSTATUS 0x{status:X8})");
                    }
                }
                finally
                {
                    NativeMethods.CloseProcess(handle);
                }
            }

            if (thawed.Count > 0)
            {
                logger.Info($"Thawed {string.Join(", ", thawed)}");
            }
        }

        private bool Save(List<FrozenProgram> programs)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllLines(file, programs.Select(p => p.ToLine()));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                logger.Warn(e, $"Couldn't write {file}, the record of the game's frozen programs");
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

        /// <summary>A program frozen: its id, its start time telling it from a later program given the same id, and its name for the log.</summary>
        private sealed class FrozenProgram
        {
            public FrozenProgram(int id, long startTime, string name)
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
            public static FrozenProgram Parse(string line)
            {
                var parts = line.Split(new[] { ' ' }, 3);
                return parts.Length >= 2
                    && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var startTime)
                    ? new FrozenProgram(id, startTime, parts.Length == 3 ? parts[2] : "?")
                    : null;
            }

            public override string ToString() => $"{Name} ({Id})";
        }
    }
}
