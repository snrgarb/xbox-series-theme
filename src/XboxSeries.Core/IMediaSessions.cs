namespace XboxSeries.Core
{
    /// <summary>
    /// Port for Windows' media sessions: the one Windows' media flyout shows, which the Guide's Audio flyout shows as its Now Playing entry.
    /// When the current session changes, or what it is playing or whether it plays, the extension calls
    /// <see cref="ExtensionCore.OnMediaSessionChanged"/> on the UI thread, only as a sign to read <see cref="Current"/> again.
    /// </summary>
    public interface IMediaSessions
    {
        /// <summary>Windows' current media session, as last read; null when there is none.</summary>
        MediaSession Current { get; }
    }

    /// <summary>No media sessions, ever: for a core given no port, and for Windows too old to have them.</summary>
    internal sealed class NoMediaSessions : IMediaSessions
    {
        public static readonly NoMediaSessions Instance = new NoMediaSessions();

        public MediaSession Current => null;
    }
}
