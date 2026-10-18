using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Dashboard's hint bar (<see cref="HomeState.Hints"/>): More options while what has focus has a menu. A game tile
    /// with a Tile menu tells the core it has focus already, for Menu; Playnite's own grid and the Game hub, where Menu
    /// opens Playnite's Manage game, say so here. The bar only binds to the result.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        private static readonly IReadOnlyList<GuideHint> NoHints = new GuideHint[0];

        /// <summary>One list for as long as it shows, so the bar isn't handed a new one at every tile focus moves to.</summary>
        private static readonly IReadOnlyList<GuideHint> MoreOptionsHints = new[] { new GuideHint(ControllerButton.Start, "More options") };

        private bool gameGridFocused;
        private bool gameHubFocused;

        /// <summary>
        /// The tile with focus as the hints last saw it, and whether it was on the Recent row then: told as focus lands,
        /// so the Recent row rebuilt under it, with tiles of its own, doesn't make it a Group's.
        /// </summary>
        private HomeTile hintedTile;
        private bool hintedTileOnRecentRow;

        /// <summary>A game in Playnite's own grid, Games' or Full library's, got focus: Menu there opens Playnite's Manage game.</summary>
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
        /// The Game hub has the pad: focus is on it, where Menu opens Playnite's Manage game. Manage game's own window, or
        /// a dialog's, takes focus from the hub as it opens, and <see cref="LeaveGameHub"/> says so.
        /// </summary>
        public void FocusGameHub()
        {
            gameHubFocused = true;
            RefreshHints();
        }

        /// <summary>Focus left the Game hub: it closed, or Manage game or a dialog took the pad.</summary>
        public void LeaveGameHub()
        {
            gameHubFocused = false;
            RefreshHints();
        }

        /// <summary>
        /// More options while a tile with a Tile menu has focus, other than on Home's Recent row, or a game in My games
        /// &amp; apps' grid, or the Game hub; nothing while a Tile menu, the Guide, a prompt or Start-up is up.
        /// </summary>
        private void RefreshHints()
        {
            var covered = Home.TileMenu != null || ActiveDialog != null || Guide.IsOverlayShown || Startup.IsShown;
            if (focusedTile != hintedTile)
            {
                hintedTile = focusedTile;
                hintedTileOnRecentRow = focusedTile != null && Home.RecentRow.Contains(focusedTile);
            }

            var tileHasMenu = focusedTile != null && (MyGames.IsShown || !hintedTileOnRecentRow);
            var hasMenu = tileHasMenu || (gameGridFocused && MyGames.ShowsGameGrid) || gameHubFocused;
            Home.Hints = !covered && hasMenu ? MoreOptionsHints : NoHints;
        }

        /// <summary>The hints follow whatever covers the Dashboard or changes its page, however it came about.</summary>
        private void WatchHints()
        {
            PropertyChangedEventHandler refresh = (_, __) => RefreshHints();
            Home.PropertyChanged += refresh;
            Guide.PropertyChanged += refresh;
            MyGames.PropertyChanged += refresh;
            Startup.PropertyChanged += refresh;
        }
    }
}
