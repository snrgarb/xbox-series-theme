using System;
using System.Windows;
using System.Windows.Threading;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: the real clock, with timers that fire on Playnite's UI thread, which runs the core, whichever thread
    /// sets them: the core sets its first, the status area's clock, as it is built.
    /// </summary>
    internal sealed class DispatcherClock : IClock
    {
        public DateTime Now => DateTime.Now;

        public IDisposable After(TimeSpan delay, Action callback)
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = delay };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                callback();
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
