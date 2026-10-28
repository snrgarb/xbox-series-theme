using System;
using System.Diagnostics;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Where the second after Start-up shows goes (#78): lines in extensions.log beginning "[Start-up timing]", each with
    /// its milliseconds from the moment the sequence was shown. They cost a stopwatch read and a log line, so they stay.
    /// Read from either thread: the UI thread's work, as Home's build and <c>OnApplicationStarted</c>, and the Start-up
    /// thread's, as its first frame and the pad's presses it answered, line up on the one clock.
    /// </summary>
    internal static class StartupTiming
    {
        /// <summary>How long after the sequence shows each pad press is logged, to see presses answered during the freeze.</summary>
        public static readonly TimeSpan PressesLogged = TimeSpan.FromSeconds(10);

        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly Stopwatch clock = new Stopwatch();

        /// <summary>Milliseconds since the sequence last showed; 0 before it has.</summary>
        public static long Now => clock.ElapsedMilliseconds;

        /// <summary>Whether the sequence showed within <see cref="PressesLogged"/>.</summary>
        public static bool IsEarly => clock.IsRunning && clock.Elapsed < PressesLogged;

        /// <summary>A sequence shows: the clock starts again from zero, for the first start and each wake from sleep.</summary>
        public static void Begin(string what)
        {
            clock.Restart();
            Log(what);
        }

        public static void Log(string what) => logger.Info($"[Start-up timing] {what} at {Now} ms");

        /// <summary>Runs <paramref name="action"/>, logging how long it took and when it began.</summary>
        public static void Time(string what, Action action)
        {
            var began = Now;
            var watch = Stopwatch.StartNew();
            action();
            logger.Info($"[Start-up timing] {what} took {watch.ElapsedMilliseconds} ms, from {began} ms");
        }

        /// <summary>Runs <paramref name="make"/>, adding how long it took, in ticks, to <paramref name="spent"/>.</summary>
        public static T Sum<T>(ref long spent, Func<T> make)
        {
            var watch = Stopwatch.StartNew();
            var made = make();
            spent += watch.ElapsedTicks;
            return made;
        }

        /// <summary>Stopwatch ticks in milliseconds.</summary>
        public static long Ms(long ticks) => ticks * 1000 / Stopwatch.Frequency;
    }
}
