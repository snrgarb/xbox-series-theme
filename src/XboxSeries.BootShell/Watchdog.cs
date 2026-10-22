using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.BootShell
{
    internal enum PlayniteKind { Fullscreen, Desktop, Updater }

    /// <summary>One of Playnite's processes: Fullscreen, Desktop mode, or the updater's update.exe.</summary>
    /// <param name="SafeStartup">A Fullscreen started with --safestartup, where no Bundle loads to signal its first frame.</param>
    internal readonly record struct PlayniteProcess(int Pid, PlayniteKind Kind, bool SafeStartup = false);

    /// <summary>What the Boot shell is to do after the <see cref="Watchdog"/> has heard something.</summary>
    [Flags]
    internal enum Orders
    {
        None = 0,

        /// <summary>A Fullscreen the Boot shell didn't launch has begun a start: reset the two events for its signals.</summary>
        ResetEvents = 1,

        /// <summary>Delete safestart.flag, reset the events and launch Fullscreen, then tell the watchdog how it went.</summary>
        Relaunch = 2,

        /// <summary>The fallback's line is done: explorer, then Steam, Discord and Spotify, if they aren't up yet.</summary>
        StartDesktop = 4,
    }

    /// <summary>
    /// Keeps Fullscreen up for the whole sign-in, judging Playnite's processes as they come and go, never its own child:
    /// when the last one exits, what's left and its exit code say what it was. With another Playnite or the updater
    /// left, it waits for them; with nothing left, exit code 0 was a quit the user meant, and anything else, or any exit
    /// before that start's first frame, a crash, which relaunches Fullscreen at once. The third crash within 5 minutes
    /// falls back: the line on the black for 5 s, then the desktop, and nothing relaunched on its own after that.
    /// It also decides the black for each Fullscreen start: up until Start-up's first frame, or 60 s at most, and lifted
    /// at once for Playnite's crash window.
    /// </summary>
    internal sealed class Watchdog
    {
        public static readonly TimeSpan BlackLimit = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan CrashSpan = TimeSpan.FromMinutes(5);
        public const int CrashLimit = 3;
        public static readonly TimeSpan LineFor = TimeSpan.FromSeconds(5);
        public static readonly string FallbackLine =
            $"Playnite closed {CrashLimit} times in {CrashSpan.TotalMinutes} minutes. Press the Guide button to try again.";

        private readonly Func<TimeSpan> elapsed;
        private readonly Action<string> log;
        private readonly Dictionary<int, Tracked> alive = new();
        private readonly List<TimeSpan> crashes = new();
        private int seen;
        private Tracked? start;
        private TimeSpan startedAt;
        private bool startBlack;
        private bool fallenBack;
        private TimeSpan? lineUntil;

        /// <param name="elapsed">The time since the Boot shell started.</param>
        /// <param name="log">Writes one line to the Boot shell's log.</param>
        public Watchdog(Func<TimeSpan> elapsed, Action<string> log)
        {
            this.elapsed = elapsed;
            this.log = log;
        }

        /// <summary>Whether the black window is to be up: for a start before its first frame, or for the fallback's line.</summary>
        public bool Black => startBlack || Line != null;

        /// <summary>The line to show on the black, or null.</summary>
        public string? Line => lineUntil == null ? null : FallbackLine;

        /// <summary>The Boot shell launched Fullscreen, as pid <paramref name="pid"/>: a start, behind the black.</summary>
        public void Launched(int pid)
        {
            var process = new Tracked(new PlayniteProcess(pid, PlayniteKind.Fullscreen), ++seen);
            alive[pid] = process;
            Begin(process, "launched by the Boot shell");
        }

        /// <summary>Start-up's first frame has shown: the Bundle set FirstFrame.</summary>
        public void FirstFrame()
        {
            if (start == null || start.PastFirstFrame)
            {
                return;
            }

            start.PastFirstFrame = true;
            log($"watchdog: Start-up's first frame for pid {start.Process.Pid}, {Seconds(elapsed() - startedAt)} after its start");
            LiftBlack("Start-up's first frame");
        }

        /// <summary>Playnite logged an unhandled exception, so its crash window or error box is up: the pad must reach it.</summary>
        public void CrashWindowShown()
        {
            log("watchdog: Playnite's crash window");
            LiftBlack("Playnite's crash window");
        }

        /// <summary>
        /// A Playnite process the Boot shell didn't launch has appeared. A Fullscreen begins a start of its own once the
        /// start before it has exited, as Playnite starts what follows it before it exits.
        /// </summary>
        public Orders Appeared(PlayniteProcess process)
        {
            alive[process.Pid] = new Tracked(process, ++seen);
            log($"watchdog: {process.Kind} pid {process.Pid} appeared" + (process.SafeStartup ? " with --safestartup" : ""));
            return BeginNextStart();
        }

        /// <summary>A Playnite process has exited with <paramref name="exitCode"/>.</summary>
        public Orders Exited(int pid, int exitCode)
        {
            if (!alive.Remove(pid, out var process))
            {
                return Orders.None;
            }

            var left = alive.Values.Select(p => p.Process).ToList();
            var beforeFirstFrame = process.Process.Kind == PlayniteKind.Fullscreen && !process.PastFirstFrame;
            var crash = exitCode != 0 || (beforeFirstFrame && left.Count == 0);
            var judgement = exitCode != 0 ? "a crash"
                : !beforeFirstFrame ? (left.Count == 0 ? "a quit the user meant" : "a hand-off")
                : left.Count == 0 ? "before its first frame, so a crash" : "before its first frame, but a hand-off";
            log($"watchdog: {process.Process.Kind} pid {pid} exited with code {exitCode}: {judgement}; left: {Names(left)}");
            if (crash)
            {
                CountCrash();
            }

            if (left.Count > 0)
            {
                log("watchdog: waiting for " + Names(left) + (left.Any(p => p.Kind == PlayniteKind.Updater) ? ", never relaunching while update.exe runs" : ""));
                if (process == start && !left.Any(p => p.Kind == PlayniteKind.Fullscreen))
                {
                    LiftBlack("its Fullscreen exited, leaving " + Names(left));
                }

                return BeginNextStart();
            }

            if (crash && !fallenBack)
            {
                return Relaunch();
            }

            LiftBlack("Playnite is down");
            log("watchdog: Playnite stays down" + (crash ? ", as the Boot shell has fallen back" : ""));
            return Orders.None;
        }

        /// <summary>The Boot shell couldn't launch Fullscreen: a crash, as Playnite is down all the same.</summary>
        public Orders LaunchFailed()
        {
            log("watchdog: Fullscreen didn't launch: a crash");
            CountCrash();
            if (fallenBack)
            {
                LiftBlack("the Boot shell has fallen back");
                return Orders.None;
            }

            return Relaunch();
        }

        /// <summary>Called every poll: the black's 60 s limit, and the end of the fallback's line.</summary>
        public Orders Tick()
        {
            if (startBlack && elapsed() - startedAt >= BlackLimit)
            {
                // Its Start-up may never signal, so from here its exits are judged as after a first frame.
                start!.PastFirstFrame = true;
                LiftBlack($"no first frame by the {BlackLimit.TotalSeconds} s limit");
            }

            if (lineUntil is TimeSpan until && elapsed() >= until)
            {
                lineUntil = null;
                log("watchdog: fallback line down; starting the desktop");
                return Orders.StartDesktop;
            }

            return Orders.None;
        }

        private Orders Relaunch()
        {
            log("watchdog: deleting safestart.flag and relaunching Fullscreen at once");
            startBlack = true;
            return Orders.Relaunch;
        }

        /// <summary>Counts a crash; the third within 5 minutes falls back, once.</summary>
        private void CountCrash()
        {
            var now = elapsed();
            crashes.Add(now);
            crashes.RemoveAll(at => now - at > CrashSpan);
            log($"watchdog: {crashes.Count} crash{(crashes.Count == 1 ? "" : "es")} in the last {CrashSpan.TotalMinutes} min");
            if (fallenBack || crashes.Count < CrashLimit)
            {
                return;
            }

            fallenBack = true;
            lineUntil = now + LineFor;
            log($"watchdog: fallback: Playnite closed {crashes.Count} times in {CrashSpan.TotalMinutes} minutes; " +
                $"the line on the black for {LineFor.TotalSeconds} s, then the desktop, and nothing more relaunched on its own");
        }

        /// <summary>Begins a start for the newest Fullscreen that hasn't had one, once the last start's Fullscreen has exited.</summary>
        private Orders BeginNextStart()
        {
            if (start != null && alive.ContainsKey(start.Process.Pid))
            {
                return Orders.None;
            }

            var next = alive.Values.Where(p => p.Process.Kind == PlayniteKind.Fullscreen && !p.Started).MaxBy(p => p.Seen);
            if (next == null)
            {
                return Orders.None;
            }

            if (next.Process.SafeStartup)
            {
                // No Bundle loads to signal its first frame, so it has none to wait for.
                start = next;
                next.Started = true;
                next.PastFirstFrame = true;
                log($"watchdog: Fullscreen start, pid {next.Process.Pid}, with --safestartup: no black");
                return Orders.None;
            }

            Begin(next, "which followed the last");
            return Orders.ResetEvents;
        }

        private void Begin(Tracked process, string how)
        {
            start = process;
            process.Started = true;
            startedAt = elapsed();
            startBlack = true;
            log($"watchdog: Fullscreen start, pid {process.Process.Pid}, {how}: black until its first frame, {BlackLimit.TotalSeconds} s at most");
        }

        private static string Names(IReadOnlyCollection<PlayniteProcess> processes) =>
            processes.Count == 0 ? "nothing" : string.Join(", ", processes.Select(p => $"{p.Kind} pid {p.Pid}"));

        private void LiftBlack(string why)
        {
            if (!startBlack)
            {
                return;
            }

            startBlack = false;
            log("watchdog: black down: " + why);
        }

        private static string Seconds(TimeSpan time) => $"{time.TotalSeconds:0.0} s";

        private sealed class Tracked
        {
            public Tracked(PlayniteProcess process, int seen)
            {
                Process = process;
                Seen = seen;
            }

            public PlayniteProcess Process { get; }

            /// <summary>The order the watchdog heard of it in, so the newest Fullscreen begins the next start.</summary>
            public int Seen { get; }

            /// <summary>The Fullscreen has begun a start of its own, behind the black.</summary>
            public bool Started { get; set; }

            public bool PastFirstFrame { get; set; }
        }
    }
}
