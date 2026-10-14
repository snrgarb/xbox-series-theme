using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        private readonly ExtensionCore core;
        private readonly HomeScreen homeScreen;
        private readonly TileGrowth growth;

        /// <summary>Every tile, game tiles then Play history, found once per rebuild of the row; null when stale.</summary>
        private List<Button> allTiles;
        private Guid? lastFocusedGame;

        /// <summary>Whether focus is in the row. Kept by hand: a rebuild removes the focused tile before this row hears of it.</summary>
        private bool hasFocus;
        private FrameworkElement gameDetails;

        internal RecentRowControl(ExtensionCore core, HomeScreen homeScreen)
        {
            InitializeComponent();
            this.core = core;
            this.homeScreen = homeScreen;
            growth = new TileGrowth(AllTiles);
            DataContext = core.Home;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            GotKeyboardFocus += (_, __) =>
            {
                hasFocus = true;
                homeScreen.LastFocusedRow = this;
                homeScreen.Show(groups: false);
                growth.Start();
            };
            LostKeyboardFocus += OnLostKeyboardFocus;
            PreviewKeyDown += OnPreviewKeyDown;
            IsVisibleChanged += OnVisibleChanged;

            // New or regenerated tiles: find them again on the next frame that needs them.
            Tiles.ItemContainerGenerator.StatusChanged += (_, __) => allTiles = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;
            homeScreen.Changed += OnScreenChanged;
            HomeRows.Fly(this, !homeScreen.ShowsGroups, -HomeRows.FlyDistance, animate: false);

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

        /// <summary>Down moves to the Groups' screen, when the Dashboard has them; other directions move focus as usual.</summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && homeScreen.HasGroups)
            {
                e.Handled = true;
                homeScreen.Show(groups: true);
                return;
            }

            HomeRows.MoveFocus(e);
        }

        /// <summary>Flies the row out as the Groups come in, and back in, taking focus, as they go.</summary>
        private void OnScreenChanged(object sender, EventArgs e)
        {
            HomeRows.Fly(this, !homeScreen.ShowsGroups, -HomeRows.FlyDistance);
            if (!homeScreen.ShowsGroups)
            {
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

        /// <summary>Focuses the last focused game's tile, or the first, once the tiles are laid out.</summary>
        private void FocusTileSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!IsVisible)
                {
                    return;
                }

                var tiles = GameTiles();
                var tile = tiles.FirstOrDefault(b => ((HomeTile)b.DataContext).GameId == lastFocusedGame) ?? tiles.FirstOrDefault();
                (tile ?? PlayHistoryTile).Focus();
            }));
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
