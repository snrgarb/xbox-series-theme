using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// Port for Windows' media sessions: the one Windows' media flyout shows, and every other app's, which the Guide's
    /// Audio flyout shows as its Now Playing entry. When a session comes or goes, or what one plays, whether it plays or
    /// where it is in its track changes, the extension calls <see cref="ExtensionCore.OnMediaSessionChanged"/> on the UI
    /// thread, only as a sign to read <see cref="Current"/> and <see cref="Sessions"/> again.
    /// </summary>
    public interface IMediaSessions
    {
        /// <summary>Windows' current media session, as last read; null when there is none.</summary>
        MediaSession Current { get; }

        /// <summary>Every app's media session, as last read, <see cref="Current"/> among them; empty when there is none.</summary>
        IReadOnlyList<MediaSession> Sessions { get; }

        /// <summary>
        /// Asks the app with <paramref name="appId"/> to carry out <paramref name="command"/>. Never throws: an app that
        /// refuses, or has gone, is logged, and what it does shows only as its session changes.
        /// </summary>
        void Send(MediaCommand command, string appId);

        /// <summary>
        /// Reads and decodes the art the app with <paramref name="appId"/> shows now, off the UI thread; the answer
        /// reaches <see cref="ExtensionCore.OnMediaArt"/> on the UI thread with <paramref name="request"/>, null for none.
        /// </summary>
        void FetchArt(string appId, int request);
    }

    /// <summary>What the Now Playing card's controls ask the playing app to do.</summary>
    public enum MediaCommand
    {
        TogglePlayPause,
        Previous,
        Next,
    }

    /// <summary>No media sessions, ever: for a core given no port, and for Windows too old to have them.</summary>
    internal sealed class NoMediaSessions : IMediaSessions
    {
        public static readonly NoMediaSessions Instance = new NoMediaSessions();

        public MediaSession Current => null;

        public IReadOnlyList<MediaSession> Sessions => new MediaSession[0];

        public void Send(MediaCommand command, string appId)
        {
        }

        public void FetchArt(string appId, int request)
        {
        }
    }
}
