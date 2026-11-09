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
    /// The Recent row, injected into Home as a custom element. It follows the core's <see cref="HomeState"/> and
    /// tells the core which tile has focus and which one A was pressed on. It holds no Home logic.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class RecentRowControl : PluginUserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly ExtensionCore core;
        private readonly HomeScreen homeScreen;
        private readonly TileGrowth growth;

        /// <summary>Every tile, game tiles then Play history, found once per rebuild of the row; null when stale.</summary>
        private List<Button> allTiles;
        private Guid? lastFocusedGame;

        /// <summary>Whether focus is in the row. Kept by hand: a rebuild removes the focused tile before this row hears of it.</summary>
        private bool hasFocus;
        private FrameworkElement gameDetails;

        /// <summary>
        /// Whether focus on the first screen is, or goes back to, the placeholder tiles under the row rather than the row
        /// itself, and which of them; and the Recent-row tile Down left for them, which Up goes back to.
        /// </summary>

        private bool promosFocused;
        private int lastPromo;
        private Button leftForPromos;

        internal RecentRowControl(ExtensionCore core, HomeScreen homeScreen)
        {
            InitializeComponent();
            this.core = core;
            this.homeScreen = homeScreen;
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

                homeScreen.LastFocusedRow = this;
                homeScreen.Show(groups: false);
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;
            core.Home.ReturnedFromGame += OnReturnedFromGame;
            core.Home.Revealed += OnRevealed;
            homeScreen.Changed += OnScreenChanged;
            HomeRows.Fly(this, !homeScreen.ShowsGroups, -HomeRows.FlyDistance, animate: false);
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
            homeScreen.Changed -= OnScreenChanged;
            growth.Stop();
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged -= OnGameDetailsVisibleChanged;
                gameDetails = null;
            }
        }

        private void OnHomeChanged(object sender, PropertyChangedEventArgs e)
        {
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
        /// the pad's keys. The Groups fly out if they were up. With the Game hub still up, as under the Guide, the row
        /// takes focus as the hub closes instead, rather than behind it.
        /// </summary>
        private void OnReturnedFromGame(Guid gameId)
        {
            lastFocusedGame = gameId;
            hasFocus = true;
            promosFocused = false;
            homeScreen.LastFocusedRow = this;
            homeScreen.Show(groups: false);
            FocusTileSoon();
        }

        /// <summary>
        /// Start-up has ended and Home is revealed from under its swirl: the row flies up into place and the top bar's buttons drop
        /// in after it, but only with the row the screen showing. Under the Groups, My games &amp; apps, Settings, Search
        /// (which all hide the row) or a Game hub, Home stays as it was left, nothing hidden. Focus stays where it is.
        /// </summary>
        private void OnRevealed()
        {
            if (IsVisible && !homeScreen.ShowsGroups && gameDetails?.IsVisible != true)
            {
                HomeRows.Reveal(this);
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
        /// Down moves from the row to the placeholder tiles under it, onto the one below, and from them to the Groups'
        /// screen, when the Dashboard has them; Up from the tiles goes back to the row's tile Down left. Left and Right
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
                    case Key.Down when homeScreen.HasGroups: homeScreen.Show(groups: true); break;
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
        /// Flies the row out as the Groups come in, and back in, taking focus, as they go: up from the Groups onto the
        /// placeholder tiles, the screen they were left from, or back to where it was.
        /// </summary>
        private void OnScreenChanged(object sender, EventArgs e)
        {
            HomeRows.Fly(this, !homeScreen.ShowsGroups, -HomeRows.FlyDistance);
            if (!homeScreen.ShowsGroups)
            {
                // The row itself setting the screen, taking focus or with a game exiting, has already said where it goes.
                if (homeScreen.LastFocusedRow != this)
                {
                    promosFocused = true;
                }

                FocusTileSoon();
            }
        }

        /// <summary>Back on Home from My games &amp; apps, which hides Home's rows: the row takes focus if it is the screen showing.</summary>
        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && !homeScreen.ShowsGroups)
            {
                FocusTileSoon();
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub opened here, or from anywhere when no row has had focus yet.
            if (!(bool)e.NewValue && (homeScreen.LastFocusedRow == null || homeScreen.LastFocusedRow == this))
            {
                FocusTileSoon();
            }
        }

        private void OnTileFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is HomeTile tile)
            {
                lastFocusedGame = tile.GameId;
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
        /// Focuses the placeholder tile last focused, when focus was on them, or else the last focused game's tile, or
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
                tile = tiles.FirstOrDefault(b => ((HomeTile)b.DataContext).GameId == lastFocusedGame) ?? tiles.FirstOrDefault() ?? PlayHistoryTile;
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
            tiles.Add(PlayHistoryTile);
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
