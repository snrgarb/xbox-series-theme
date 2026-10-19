namespace XboxSeries.Core
{
    /// <summary>
    /// The Audio flyout's Now Playing entry, last, under the mute switches after a divider: what Windows' media flyout
    /// shows, from Windows' current media session, the track, its art, the app playing it and whether it plays, or "No
    /// music app is running" with none. It follows the session live while the flyout shows.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// Windows' current media session changed, or what it plays or whether it plays: once the changes have settled
        /// (<see cref="AfterMediaSettles"/>), an open Audio flyout shows the last of them (<see cref="ShowNowPlaying"/>).
        /// Otherwise nothing, the flyout reading the session afresh as it opens. Only a sign to read the port again.
        /// Called on the UI thread.
        /// </summary>
        public void OnMediaSessionChanged()
        {
            NoteMediaApps();
            if (!Guide.IsOpen || Guide.Flyout?.Kind != GuideItemKind.Audio)
            {
                return;
            }

            AfterMediaSettles(ShowNowPlaying);
        }

        /// <summary>
        /// The open Audio flyout shows the media session as it is now, the card taking a new track, app or play state in
        /// place, so the rest of the flyout, an open device list and focus are left as they were; a music app starting or
        /// the last one going swaps the card and "No music app is running" as a device change rebuilds the flyout. Nothing
        /// with the Guide closed or another flyout showing.
        /// </summary>
        private void ShowNowPlaying()
        {
            if (!Guide.IsOpen || Guide.Flyout?.Kind != GuideItemKind.Audio)
            {
                return;
            }

            var shown = Guide.Flyout.Items[Guide.Flyout.Items.Count - 1];
            var wanted = NowPlayingItem();
            if (shown.IsSameEntry(wanted))
            {
                shown.TakeStateFrom(wanted);
                return;
            }

            RefreshAudioFlyout();
        }

        /// <summary>The Now Playing entry, behind a divider: the current session's card, or "No music app is running" with none.</summary>
        private GuideItem NowPlayingItem()
        {
            var session = ShownSession();
            if (session == null)
            {
                return new GuideItem(GuideItemKind.NothingPlaying, "No music app is running", hasDividerAbove: true);
            }

            var card = new GuideItem(GuideItemKind.NowPlaying, session.Title, hasDividerAbove: true, detail: session.Artist)
            {
                App = session.App,
                IsPlaying = session.IsPlaying,
                Controls = MediaControls(session),
            };
            ShowArt(card, session);
            ShowTimeline(card, session);
            return card;
        }
    }
}
