using System;
using System.Threading;
using System.Windows.Threading;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Wakes Playnite's UI thread every 50 ms with an empty operation. Left alone, work queued for when the thread is
    /// idle (ContextIdle and the like, as focus going into My games &amp; apps' grid) waited up to two seconds on the TV
    /// for something else to wake it; with the heartbeat it runs within a frame or two.
    /// </summary>
    internal static class DispatcherHeartbeat
    {
        private const int IntervalMs = 50;
        private static Thread thread;

        public static void Start(Dispatcher dispatcher)
        {
            if (thread != null)
            {
                return;
            }

            var beat = new Action(() => { });
            thread = new Thread(() =>
            {
                while (!dispatcher.HasShutdownStarted)
                {
                    dispatcher.BeginInvoke(DispatcherPriority.Normal, beat);
                    Thread.Sleep(IntervalMs);
                }
            })
            {
                IsBackground = true,
                Name = "XboxSeries dispatcher heartbeat",
                Priority = ThreadPriority.AboveNormal,
            };
            thread.Start();
        }
    }
}
