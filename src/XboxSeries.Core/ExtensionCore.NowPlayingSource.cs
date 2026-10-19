namespace XboxSeries.Core
{
    /// <summary>Settings' Now Playing source: which app the Now Playing card follows (#75).</summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The session the card shows. Stub: Windows' current.</summary>
        private MediaSession ShownSession() => mediaSessions.Current;

        /// <summary>
        /// Sets Settings' Now Playing source to <paramref name="source"/>, one of
        /// <see cref="GuideSettingsState.NowPlayingSources"/>. Stub: nothing.
        /// </summary>
        public void SetNowPlayingSource(string source)
        {
        }
    }
}
