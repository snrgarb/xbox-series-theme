using System;
using System.Windows;
using System.Windows.Threading;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: the real clock, with timers that fire on Playnite's UI thread, which runs the core, whichever thread
    /// sets them: the core sets its first, the status area's clock, as it is built. Made with a dispatcher, its timers
    /// fire on that dispatcher's thread instead, as the Start-up sequence's do on its own.
    /// </summary>
    internal sealed class DispatcherClock : IClock
    {
        /// <summary>The thread the timers fire on; null for Playnite's UI thread, found as each is set.</summary>
        private readonly Dispatcher dispatcher;

        public DispatcherClock()
        {
        }

        public DispatcherClock(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public DateTime Now => DateTime.Now;

        public IDisposable After(TimeSpan delay, Action callback)
        {
            var dispatcher = this.dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = delay };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                Guarded.Run($"a timer set for {delay.TotalMilliseconds} ms", callback);
            };
            timer.Start();
            return new Cancel(timer);
        }

        private sealed class Cancel : IDisposable
        {
            private readonly DispatcherTimer timer;

            public Cancel(DispatcherTimer timer)
            {
                this.timer = timer;
            }

            public void Dispose() => timer.Stop();
        }
    }
}
