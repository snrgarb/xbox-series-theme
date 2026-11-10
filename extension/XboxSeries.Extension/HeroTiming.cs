using System;
using System.Diagnostics;
using System.Windows.Media;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// How long Home's hero takes to follow the focused Recent-row game (#147): lines in extensions.log beginning
    /// "[Hero timing]", each with its milliseconds from the moment the tile got focus. They cost a stopwatch read and a
    /// log line per step of focus, so they stay; the end-to-end test background-follows-selection reads them.
    /// </summary>
    internal static class HeroTiming
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly Stopwatch sinceFocus = new Stopwatch();

        /// <summary>
        /// A Recent-row tile got focus: the clock starts again from zero. The line says how long after the last tile's
        /// focus, by the same stopwatch, so a held d-pad's pace can be read without the log's own times.
        /// </summary>
        public static void Focused(string title)
        {
            var sinceLast = sinceFocus.IsRunning ? $" ({sinceFocus.ElapsedMilliseconds} ms after the last)" : "";
            sinceFocus.Restart();
            logger.Info($"[Hero timing] focus on {title}{sinceLast}");
        }

        public static void Log(string what) => logger.Info($"[Hero timing] {what} at {sinceFocus.ElapsedMilliseconds} ms");

        /// <summary>Logs <paramref name="what"/> as WPF next draws a frame: the first with what was just changed in it.</summary>
        public static void LogNextFrame(string what)
        {
            EventHandler onFrame = null;
            onFrame = (_, __) =>
            {
                CompositionTarget.Rendering -= onFrame;
                Log(what);
            };
            CompositionTarget.Rendering += onFrame;
        }
    }
}
