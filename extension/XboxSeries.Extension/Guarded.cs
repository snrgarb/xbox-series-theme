using System;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The extension's own work on Playnite's UI thread, run so what it throws is logged rather than taking Playnite
    /// down: a dispatcher callback or timer tick that throws reaches WPF's unhandled exception, which ends Playnite.
    /// Playnite catches what the plugin's own callbacks throw (OnGameStarting, OnControllerButtonStateChanged and the
    /// rest), but not what they post to the dispatcher, nor the timers the core sets.
    /// </summary>
    internal static class Guarded
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static void Run(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Error(e, $"Xbox Series extension: {what} failed");
            }
        }
    }
}
