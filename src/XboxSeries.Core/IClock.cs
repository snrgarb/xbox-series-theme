using System;

namespace XboxSeries.Core
{
    /// <summary>Port for time: the current time and one-shot timers (hold, dismiss-release fallback, quit timeout, hold-repeat, ...).</summary>
    public interface IClock
    {
        DateTime Now { get; }

        /// <summary>Runs <paramref name="callback"/> once after <paramref name="delay"/>. Dispose the result to cancel it.</summary>
        IDisposable After(TimeSpan delay, Action callback);
    }
}
