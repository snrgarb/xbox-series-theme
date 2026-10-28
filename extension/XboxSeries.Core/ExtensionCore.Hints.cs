using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Dashboard's hint bar (<see cref="HomeState.Hints"/>): More options while what has focus has a menu, and Y Search
    /// to its right wherever Y opens Search, alone on Home's top bar, which has no menu. A game tile
    /// with a Tile menu tells the core it has focus already, for Menu; Playnite's own grid and the Game hub, where Menu
    /// opens the Tile menu through Playnite's own binding (<see cref="OpenTileMenu"/>), say so here. The bar only binds to
    /// the result.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        private static readonly IReadOnlyList<GuideHint> NoHints = new GuideHint[0];

        /// <summary>One list for as long as it shows, so the bar isn't handed a new one at every tile focus moves to.</summary>
        private static readonly IReadOnlyList<GuideHint> MoreOptionsHints = new[] { new GuideHint(ControllerButton.Start, "More options") };

        /// <summary>More options, then Y Search to its right: a Group's tile on Home, a game in Games' grid.</summary>
        private static readonly IReadOnlyList<GuideHint> MoreOptionsAndSearchHints = MoreOptionsHints.Concat(new[] { new GuideHint(ControllerButton.Y, "Search") }).ToArray();

        private bool gameGridFocused;
        private bool gameHubFocused;

        /// <summary>
        /// The tile with focus as the hints last saw it, and whether it was on the Recent row then: told as focus lands,
        /// so the Recent row rebuilt under it, with tiles of its own, doesn't make it a Group's.
        /// </summary>
        private HomeTile hintedTile;
        private bool hintedTileOnRecentRow;

        /// <summary>A game in Playnite's own grid, Games' or Full library's, got focus: Menu there opens the Tile menu.</summary>
        public void FocusGameGrid()
        {
            gameGridFocused = true;
            RefreshHints();
        }

        /// <summary>Focus left Playnite's own grid, to elsewhere on the page or with Playnite leaving the foreground.</summary>
        public void LeaveGameGrid()
        {
            gameGridFocused = false;
            RefreshHints();
        }

        /// <summary>
        /// The Game hub has the pad: focus is on it, where Menu opens the Tile menu. A window of Playnite's, or a dialog's,
        /// takes focus from the hub as it opens, and <see cref="LeaveGameHub"/> says so; the Tile menu leaves focus there.
        /// </summary>
        public void FocusGameHub()
        {
            gameHubFocused = true;
            RefreshHints();
        }

        /// <summary>Focus left the Game hub: it closed, or a window of Playnite's or a dialog took the pad.</summary>
        public void LeaveGameHub()
        {
            gameHubFocused = false;
            RefreshHints();
        }

        /// <summary>
        /// More options while a tile with a Tile menu has focus, other than on Home's Recent row, or a game in My games
        /// &amp; apps' grid, or the Game hub; Y Search after it where Y opens Search (<see cref="SearchFromHome"/>,
        /// <see cref="SearchFromMyGames"/>) on a tile with a menu: a Group's tile on Home with no page in its place, and a
        /// game in Games' grid, not Full library's, where Y opens its name box. Never Y Search alone: elsewhere on Home,
        /// the top bar and the Recent row, the bar stays away until focus is down on the Groups. Nothing while a Tile menu,
        /// the Guide, a prompt, Start-up or the Idle screen's dim is up (where Y opens the card's game), nor Y Search with
        /// text entry up.
        /// </summary>
        private void RefreshHints()
        {
            var covered = Home.TileMenu != null || ActiveDialog != null || Guide.IsOverlayShown || IsStartupShown || Idle.IsDimmed;
            if (focusedTile != hintedTile)
            {
                hintedTile = focusedTile;
                hintedTileOnRecentRow = focusedTile != null && Home.RecentRow.Contains(focusedTile);
            }

            var tileHasMenu = focusedTile != null && (MyGames.IsShown || !hintedTileOnRecentRow);
            var hasMenu = tileHasMenu || (gameGridFocused && MyGames.ShowsGameGrid) || gameHubFocused;
            var homeOpensSearch = HomeAnswersYWithSearch && focusedTile != null && !hintedTileOnRecentRow;
            var gamesOpensSearch = GamesAnswersYWithSearch && gameGridFocused;
            var opensSearch = !gameHubFocused && (homeOpensSearch || gamesOpensSearch);
            Home.Hints = covered || !hasMenu ? NoHints
                : opensSearch ? MoreOptionsAndSearchHints
                : MoreOptionsHints;
        }

        /// <summary>The hints follow whatever covers the Dashboard or changes its page, however it came about.</summary>
        private void WatchHints()
        {
            PropertyChangedEventHandler refresh = (_, __) => RefreshHints();
            Home.PropertyChanged += refresh;
            Guide.PropertyChanged += refresh;
            MyGames.PropertyChanged += refresh;
            Settings.PropertyChanged += refresh;
            Search.PropertyChanged += refresh;
            TextEntry.PropertyChanged += refresh;
            Idle.PropertyChanged += refresh;
        }
    }
}
