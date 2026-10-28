using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// Windows' media sessions, which a test sets as an app starting, pausing or closing would. Setting them here
    /// doesn't tell the core; the test does, as the extension would. Commands and art fetches are recorded; a test
    /// answers a fetch through <see cref="ExtensionCore.OnMediaArt"/>.
    /// </summary>
    public sealed class FakeMediaSessions : IMediaSessions
    {
        private IReadOnlyList<MediaSession> sessions;

        public MediaSession Current { get; set; }

        /// <summary>Every app's session: as set, else just <see cref="Current"/>, if any.</summary>
        public IReadOnlyList<MediaSession> Sessions
        {
            get => sessions ?? (Current == null ? new MediaSession[0] : new[] { Current });
            set => sessions = value;
        }

        /// <summary>Every command sent, in order.</summary>
        public List<(MediaCommand Command, string AppId)> Sent { get; } = new List<(MediaCommand, string)>();

        /// <summary>Every art fetch asked for, in order.</summary>
        public List<(string AppId, int Request)> ArtFetches { get; } = new List<(string, int)>();

        public void Send(MediaCommand command, string appId) => Sent.Add((command, appId));

        public void FetchArt(string appId, int request) => ArtFetches.Add((appId, request));
    }
}
