using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Guide's Now Playing tab: what Windows' media flyout shows, from Windows' current media session, the track,
    /// its art, the app playing it and whether it plays, or "Nothing's playing" with none. It follows the session live.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// Windows' current media session changed, or what it plays or whether it plays: the Now Playing tab, if
        /// showing, shows it now, the card taking a new play state in place, focus staying on it. Only a sign to read
        /// the port again. Called on the UI thread.
        /// </summary>
        public void OnMediaSessionChanged()
        {
            if (!Guide.IsOpen || Guide.Tab != GuideTabKind.NowPlaying || UpdateInPlace(Guide.Items, NowPlayingItems()))
            {
                return;
            }

            ShowNowPlayingTab();
        }

        /// <summary>The Now Playing tab, focus on its one entry.</summary>
        private void ShowNowPlayingTab()
        {
            Guide.Items = NowPlayingItems();
            Guide.FocusedItem = Guide.Items[0];
        }

        /// <summary>The current session's card, or "Nothing's playing" with none.</summary>
        private IReadOnlyList<GuideItem> NowPlayingItems()
        {
            var session = mediaSessions.Current;
            if (session == null)
            {
                return new[] { new GuideItem(GuideItemKind.NothingPlaying, "Nothing's playing") };
            }

            return new[]
            {
                new GuideItem(GuideItemKind.NowPlaying, session.Title, detail: session.Artist)
                {
                    App = session.App,
                    Art = session.Art,
                    IsPlaying = session.IsPlaying,
                },
            };
        }
    }
}
