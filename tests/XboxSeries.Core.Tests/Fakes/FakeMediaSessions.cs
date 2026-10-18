using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// Windows' current media session, which a test sets as an app starting, pausing or closing would. Setting it here
    /// doesn't tell the core; the test does, as the extension would.
    /// </summary>
    public sealed class FakeMediaSessions : IMediaSessions
    {
        public MediaSession Current { get; set; }
    }
}
