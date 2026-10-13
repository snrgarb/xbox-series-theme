using System;
using System.ComponentModel;
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
        private readonly ExtensionCore core;
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
            GotKeyboardFocus += (_, __) => hasFocus = true;
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

                var tiles = Enumerable.Range(0, Tiles.Items.Count)
                    .Select(i => Tiles.ItemContainerGenerator.ContainerFromIndex(i))
                    .Select(container => container == null ? null : FindChild<Button>(container))
                    .Where(button => button != null)
                    .ToList();
                var tile = tiles.FirstOrDefault(b => ((HomeTile)b.DataContext).GameId == lastFocusedGame) ?? tiles.FirstOrDefault();
                (tile ?? PlayHistoryTile).Focus();
            }));
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
