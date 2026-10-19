using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>A clock that only moves when a test calls <see cref="Advance"/>, firing any timers that fall due.</summary>
    public sealed class FakeClock : IClock
    {
        private readonly List<Timer> timers = new List<Timer>();

        public FakeClock(DateTime start)
        {
            Now = start;
        }

        public DateTime Now { get; private set; }

        /// <summary>How many timers are waiting to fall due.</summary>
        public int Pending => timers.Count;

        public IDisposable After(TimeSpan delay, Action callback)
        {
            var timer = new Timer(Now + delay, callback, t => timers.Remove(t));
            timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            var target = Now + by;
            while (true)
            {
                var next = timers.Where(t => t.DueAt <= target).OrderBy(t => t.DueAt).FirstOrDefault();
                if (next == null)
                {
                    break;
                }

                Now = next.DueAt;
                timers.Remove(next);
                next.Callback();
            }

            Now = target;
        }

        private sealed class Timer : IDisposable
        {
            private readonly Action<Timer> cancel;

            public Timer(DateTime dueAt, Action callback, Action<Timer> cancel)
            {
                DueAt = dueAt;
                Callback = callback;
                this.cancel = cancel;
            }

            public DateTime DueAt { get; }
            public Action Callback { get; }

            public void Dispose() => cancel(this);
        }
    }
}
