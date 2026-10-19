namespace XboxSeries.Core
{
    /// <summary>The Now Playing card's art, kept until a new track's has loaded (#74).</summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The art <paramref name="card"/> shows for <paramref name="session"/>. Stub: the session's own.</summary>
        private void ShowArt(GuideItem card, MediaSession session) => card.Art = session.Art;

        /// <summary>
        /// The art asked for by <see cref="IMediaSessions.FetchArt"/> as <paramref name="request"/>; null for none. Called
        /// on the UI thread. Stub: nothing.
        /// </summary>
        public void OnMediaArt(int request, object art)
        {
        }
    }
}
