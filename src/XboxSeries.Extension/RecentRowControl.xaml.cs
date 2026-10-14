using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK.Controls;
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
        /// <summary>A tile's size at rest, and how much it grows when focused, at 1080p (the console captures, #7).</summary>
        private const double TileSize = 153;
        private const double FocusGrowth = 92;

        /// <summary>How quickly a tile eases toward its size: about 98% of the way in 0.2 s, as on the console.</summary>
        private static readonly TimeSpan GrowthTimeConstant = TimeSpan.FromMilliseconds(50);

        private readonly ExtensionCore core;

        // Each tile's share of the focus growth. Every frame each share eases toward 1 for the focused tile and 0 for the
        // rest by the same fraction, so the shares always add up to the same total and the row's width never changes:
        // only the tiles growing or shrinking move, however fast focus moves. Per-tile animations can't promise that
        // once one is interrupted mid-way.
        private readonly Dictionary<Button, double> growth = new Dictionary<Button, double>();
        private readonly Stopwatch frameClock = new Stopwatch();
        private bool isAnimating;

        /// <summary>Every tile, game tiles then Play history, found once per rebuild of the row; null when stale.</summary>
        private List<Button> allTiles;
        private Guid? lastFocusedGame;

        /// <summary>Whether focus is in the row. Kept by hand: a rebuild removes the focused tile before this row hears of it.</summary>
        private bool hasFocus;
        private FrameworkElement gameDetails;

        public RecentRowControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.Home;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            GotKeyboardFocus += (_, __) =>
            {
                hasFocus = true;
                StartGrowing();
            };
            LostKeyboardFocus += OnLostKeyboardFocus;

            // New or regenerated tiles: find them again on the next frame that needs them.
            Tiles.ItemContainerGenerator.StatusChanged += (_, __) => allTiles = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;

            // Playnite focuses its main list on Home, which the Dashboard hides with the extension, and again when the
            // Game hub closes; the row takes focus instead.
            gameDetails = FindGameDetails();
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }

            FocusTileSoon();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged -= OnHomeChanged;
            StopGrowing();
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

            StartGrowing();
        }

        private void StartGrowing()
        {
            if (!isAnimating)
            {
                isAnimating = true;
                frameClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
        }

        private void StopGrowing()
        {
            if (isAnimating)
            {
                isAnimating = false;
                CompositionTarget.Rendering -= OnFrame;
            }
        }

        /// <summary>Eases every tile's share of the growth toward its target, and sizes the tiles from their shares.</summary>
        private void OnFrame(object sender, EventArgs e)
        {
            var elapsed = frameClock.Elapsed;
            frameClock.Restart();
            var step = 1 - Math.Exp(-elapsed.TotalMilliseconds / GrowthTimeConstant.TotalMilliseconds);

            // No allocations here: this runs every frame while tiles change size.
            var tiles = AllTiles();
            var settled = true;
            foreach (var tile in tiles)
            {
                if (Math.Abs(GrowthTarget(tile) - NextGrowth(tile, step)) >= 0.002)
                {
                    settled = false;
                }
            }

            // All tiles settle in the same frame, so snapping to the targets keeps the shares' total exact.
            foreach (var tile in tiles)
            {
                var share = settled ? GrowthTarget(tile) : NextGrowth(tile, step);
                growth[tile] = share;
                tile.Width = tile.Height = TileSize + FocusGrowth * share;
            }

            if (settled)
            {
                StopGrowing();
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                FocusTileSoon();
            }
        }

        private void OnTileFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            var tile = (HomeTile)((FrameworkElement)sender).DataContext;
            lastFocusedGame = tile.GameId;
            core.FocusHomeTile(tile);
        }

        // Playnite sends the pad's A as Enter, which clicks the focused tile.
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
                .Select(container => container == null ? null : FindChild<Button>(container))
                .Where(button => button != null)
                .ToList();

        private static double GrowthTarget(Button tile) => tile.IsKeyboardFocused ? 1 : 0;

        private double NextGrowth(Button tile, double step)
        {
            growth.TryGetValue(tile, out var share);
            return share + (GrowthTarget(tile) - share) * step;
        }

        private List<Button> AllTiles()
        {
            if (allTiles != null)
            {
                return allTiles;
            }

            var tiles = GameTiles();
            var complete = tiles.Count == Tiles.Items.Count;
            tiles.Add(PlayHistoryTile);
            foreach (var gone in growth.Keys.Except(tiles).ToList())
            {
                growth.Remove(gone);
            }

            // Kept only once every game tile is built; until then the next frame looks again.
            if (complete)
            {
                allTiles = tiles;
            }

            return tiles;
        }

        /// <summary>Playnite's Game details view in the Main template this row sits in; null if the theme has none.</summary>
        private FrameworkElement FindGameDetails()
        {
            for (DependencyObject node = this; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is Control control && control.Template?.FindName("PART_ElemGameDetails", control) is FrameworkElement details)
                {
                    return details;
                }
            }

            return null;
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                var nested = FindChild<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
