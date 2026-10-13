using System;
using System.Windows.Threading;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the real clock, with timers that fire on the UI thread that created it.</summary>
    internal sealed class DispatcherClock : IClock
    {
        public DateTime Now => DateTime.Now;

        public IDisposable After(TimeSpan delay, Action callback)
        {
            var timer = new DispatcherTimer { Interval = delay };
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
