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

            var tiles = AllTiles();
            foreach (var gone in growth.Keys.Except(tiles).ToList())
            {
                growth.Remove(gone);
            }

            var targets = tiles.ToDictionary(t => t, t => t.IsKeyboardFocused ? 1.0 : 0.0);
            var shares = tiles.ToDictionary(t => t, t =>
            {
                growth.TryGetValue(t, out var share);
                return share + (targets[t] - share) * step;
            });

            // All tiles settle in the same frame, so snapping to the targets keeps the shares' total exact.
            var settled = tiles.All(t => Math.Abs(targets[t] - shares[t]) < 0.002);
            foreach (var tile in tiles)
            {
                var share = settled ? targets[tile] : shares[tile];
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

        private List<Button> AllTiles()
        {
            var tiles = GameTiles();
            tiles.Add(PlayHistoryTile);
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
