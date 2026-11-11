using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Recent row and the wide tiles under it, injected into Home as a custom element: the top of Home's page. It
    /// follows the core's <see cref="HomeState"/> and tells the core which tile has focus and which one A was pressed on.
    /// It holds no Home logic. It hands the theme the top bar's scroll (<see cref="TopBarScroll"/>).
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class RecentRowControl : PluginUserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly ExtensionCore core;
        private readonly HomePage page;
        private readonly TileGrowth growth;

        /// <summary>Every tile, game tiles then Play history while it shows, found once per rebuild of the row; null when stale.</summary>
        private List<Button> allTiles;
        private Guid? lastFocusedGame;

        /// <summary>Whether focus is in the row. Kept by hand: a rebuild removes the focused tile before this row hears of it.</summary>
        private bool hasFocus;
        private FrameworkElement gameDetails;

        /// <summary>
        /// Whether focus at the top of the page is, or goes back to, the wide tiles under the row rather than the row
        /// itself, and which of them; and the Recent-row tile Down left for them, which Up goes back to.
        /// </summary>
        private bool promosFocused;
        private int lastPromo;
        private Button leftForPromos;

        internal RecentRowControl(ExtensionCore core, HomePage page)
        {
            InitializeComponent();
            this.core = core;
            this.page = page;
            Page.RenderTransform = page.Scroll;
            Promos.Attach(core);
            growth = new TileGrowth(AllTiles);
            DataContext = core.Home;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            GotKeyboardFocus += (_, e) =>
            {
                hasFocus = true;
                promosFocused = e.NewFocus is Button focused && Promos.IsAncestorOf(focused);
                if (promosFocused)
                {
                    lastPromo = Math.Max(0, Promos.AllTiles.ToList().IndexOf((Button)e.NewFocus));
                }

                if (e.NewFocus == PlayHistoryTile)
                {
                    logger.Info("Home focus on the Play history tile");
                }

                page.LastFocusedRow = this;
                page.RestOn(promosFocused ? HomeFocus.WideTiles : HomeFocus.RecentRow);
                growth.Start();
            };
            LostKeyboardFocus += OnLostKeyboardFocus;
            PreviewKeyDown += OnPreviewKeyDown;
            IsVisibleChanged += OnVisibleChanged;

            // New or regenerated tiles: find them again, and size and space them, on the next frame.
            Tiles.ItemContainerGenerator.StatusChanged += (_, __) =>
            {
                allTiles = null;
                growth.Start();
            };
        }

        /// <summary>
        /// The top bar's scroll, which the theme binds the top bar's RenderTransform to (Main.xaml): it rises off the
        /// screen as focus goes into the Groups, and comes back as it leaves them.
        /// </summary>
        public Transform TopBarScroll => page.TopBarScroll;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;
            core.Home.ReturnedFromGame += OnReturnedFromGame;
            core.Home.Revealed += OnRevealed;
            page.RowWanted += OnRowWanted;
            page.Row = this;
            page.RowContent = Page;
            page.TopBar = HomeRows.FindPart(this, "XboxTopBar");
            growth.Start();

            // Playnite focuses its main list on Home, which the Dashboard hides with the extension, and again when the
            // Game hub closes; the row takes focus instead.
            gameDetails = HomeRows.FindGameDetails(this);
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }

            FocusTileSoon();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged -= OnHomeChanged;
            core.Home.ReturnedFromGame -= OnReturnedFromGame;
            core.Home.Revealed -= OnRevealed;
            page.RowWanted -= OnRowWanted;
            if (page.Row == this)
            {
                page.Row = null;
                page.RowContent = null;
                page.TopBar = null;
            }

            growth.Stop();
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged -= OnGameDetailsVisibleChanged;
                gameDetails = null;
            }
        }

        private void OnHomeChanged(object sender, PropertyChangedEventArgs e)
        {
            // The Play history tile shown or gone: the tiles to grow and space are found again, the row's width kept.
            if (e.PropertyName == nameof(HomeState.HasPlayHistoryTile))
            {
                allTiles = null;
                growth.Start();
            }

            // Rebuilding the row replaces its tiles, and with them the focused one: put focus back on the same game.
            if (e.PropertyName == nameof(HomeState.RecentRow) && hasFocus)
            {
                FocusTileSoon();
            }
        }

        /// <summary>
        /// A game launched from the Dashboard has exited: the row takes focus back, on that game's tile, from wherever
        /// Playnite left it. Playnite's "game is running" panel had focus while it ran, and as it goes Playnite focuses
        /// its main list, which Home hides, or the Game hub the game was played from, leaving nothing on screen to take
        /// the pad's keys. Home's page goes back to rest on the row if it was down in the Groups. With the Game hub still up, as under the Guide, the row
        /// takes focus as the hub closes instead, rather than behind it.
        /// </summary>
        private void OnReturnedFromGame(Guid gameId)
        {
            lastFocusedGame = gameId;
            hasFocus = true;
            promosFocused = false;
            page.LastFocusedRow = this;
            page.RestOn(HomeFocus.RecentRow);
            FocusTileSoon();
        }

        /// <summary>
        /// Start-up has ended and Home is revealed from under its swirl: the row flies up into place, the Groups fading
        /// in with it, and the top bar's buttons drop in after it, but only with the page resting above the Groups. Down in the Groups, or under My
        /// games &amp; apps, Settings, Search (which all hide the row) or a Game hub, Home stays as it was left, nothing
        /// hidden. Focus stays where it is.
        /// </summary>
        private void OnRevealed()
        {
            if (IsVisible && !page.RestsOnGroups && gameDetails?.IsVisible != true)
            {
                HomeRows.Reveal(this, page.Groups as UIElement);
            }
        }

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Only a move to another element on screen takes focus out of the row; a tile removed by a rebuild, or
            // Playnite losing the foreground, leaves nothing visible focused.
            if (e.NewFocus is UIElement element && element.IsVisible && !IsAncestorOf(element))
            {
                hasFocus = false;
            }

            growth.Start();
        }

        /// <summary>
        /// Down moves from the row to the wide tiles under it, onto the one below, and from them into the Groups, when
        /// the Dashboard has them; Up from the tiles goes back to the row's tile Down left. Left and Right
        /// move along the tiles, stopping at the ends. Elsewhere in the row, keys move focus as usual.
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(Keyboard.FocusedElement is Button focused))
            {
                return;
            }

            if (Promos.IsAncestorOf(focused))
            {
                var promos = Promos.AllTiles.ToList();
                var place = promos.IndexOf(focused);
                switch (e.Key)
                {
                    case Key.Left when place > 0: promos[place - 1].Focus(); break;
                    case Key.Right when place < promos.Count - 1: promos[place + 1].Focus(); break;
                    case Key.Up: FocusRecentTile(leftForPromos); break;
                    case Key.Down when page.HasGroups: page.EnterGroups(); break;
                    case Key.Left:
                    case Key.Right:
                    case Key.Down:
                        break;
                    default: return;
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                e.Handled = true;
                leftForPromos = focused;
                Promos.NearestTo(focused).Focus();
                return;
            }

            HomeRows.MoveFocus(e);
        }

        /// <summary>
        /// Focus comes back up out of the Groups: onto the wide tiles, the last of them focused, from the first Group's
        /// Up; onto the last focused game's tile from B. The page rests where focus lands.
        /// </summary>
        private void OnRowWanted(bool toWideTiles)
        {
            promosFocused = toWideTiles;
            FocusTileSoon();
        }

        /// <summary>Back on Home from My games &amp; apps, which hides Home's rows: the row takes focus back if focus was in it, not the Groups.</summary>
        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && !page.RestsOnGroups)
            {
                FocusTileSoon();
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub opened here, or from anywhere when no row has had focus yet.
            if (!(bool)e.NewValue && (page.LastFocusedRow == null || page.LastFocusedRow == this))
            {
                FocusTileSoon();
            }
        }

        private void OnTileFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is HomeTile tile)
            {
                lastFocusedGame = tile.GameId;
                HeroTiming.Focused(tile.Title);
                logger.Info($"Home focus on the Recent tile {tile.Title}");
                core.FocusHomeTile(tile);
            }
        }

        private void OnTileLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            // A tile a rebuild has thrown away holds WPF's disconnected item, not its game, by the time it loses focus.
            if (((FrameworkElement)sender).DataContext is HomeTile tile)
            {
                core.LeaveHomeTile(tile);
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnPlayHistoryKeyDown.
        private void OnPlayHistoryClick(object sender, RoutedEventArgs e)
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);
        }

        private void OnPlayHistoryKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                core.OpenMyGames(MyGamesItem.PlayHistory);
            }
        }

        // The keyboard's Enter and the mouse click the tile; the pad's A reaches the core as a button instead.
        private void OnTileClick(object sender, RoutedEventArgs e)
        {
            core.ChooseHomeTile((HomeTile)((FrameworkElement)sender).DataContext);
        }

        /// <summary>
        /// Focuses the wide tile last focused, when focus was on them, or else the last focused game's tile, or
        /// the first, once the tiles are laid out.
        /// </summary>
        private void FocusTileSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                // Not behind a Game hub, as a game exiting from one leaves it up a moment: its closing focuses the row then.
                if (!IsVisible || gameDetails?.IsVisible == true)
                {
                    return;
                }

                if (promosFocused)
                {
                    Promos.AllTiles[Math.Min(lastPromo, Promos.AllTiles.Count - 1)].Focus();
                    return;
                }

                FocusRecentTile(null);
            }));
        }

        /// <summary>Focuses <paramref name="tile"/> if it is still in the row, or else the last focused game's tile, or the first.</summary>
        private void FocusRecentTile(Button tile)
        {
            if (tile == null || !tile.IsVisible || !IsAncestorOf(tile))
            {
                var tiles = GameTiles();
                tile = tiles.FirstOrDefault(b => ((HomeTile)b.DataContext).GameId == lastFocusedGame) ?? tiles.FirstOrDefault() ?? (core.Home.HasPlayHistoryTile ? PlayHistoryTile : null);
                if (tile == null)
                {
                    return;
                }
            }

            tile.Focus();
        }

        private List<Button> GameTiles() =>
            Enumerable.Range(0, Tiles.Items.Count)
                .Select(i => Tiles.ItemContainerGenerator.ContainerFromIndex(i))
                .Select(container => container == null ? null : HomeRows.FindChild<Button>(container))
                .Where(button => button != null)
                .ToList();

        private List<Button> AllTiles()
        {
            if (allTiles != null)
            {
                return allTiles;
            }

            var tiles = GameTiles();
            var complete = tiles.Count == Tiles.Items.Count;
            if (core.Home.HasPlayHistoryTile)
            {
                tiles.Add(PlayHistoryTile);
            }

            growth.Forget(tiles);

            // Kept only once every game tile is built; until then the next frame looks again.
            if (complete)
            {
                allTiles = tiles;
            }

            return tiles;
        }
    }
}
