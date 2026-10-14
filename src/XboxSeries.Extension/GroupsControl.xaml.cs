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
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Home's Groups, injected below the Recent row as a custom element. It follows the core's <see cref="HomeState"/>
    /// and tells the core which tile A was pressed on. It holds no Home logic. A <see cref="PluginUserControl"/>,
    /// because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class GroupsControl : PluginUserControl
    {
        private readonly ExtensionCore core;
        private readonly HomeFocus homeFocus;
        private FrameworkElement gameDetails;

        /// <summary>Whether focus is in the Groups. Kept by hand: a rebuild removes the focused tile before this hears of it.</summary>
        private bool hasFocus;

        // Where focus last was, to put it back after a rebuild or a Game hub: the Group's place, and the game (null for See all).
        private int lastGroup;
        private Guid? lastGame;

        internal GroupsControl(ExtensionCore core, HomeFocus homeFocus)
        {
            InitializeComponent();
            this.core = core;
            this.homeFocus = homeFocus;
            DataContext = core.Home;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            GotKeyboardFocus += OnGotKeyboardFocus;
            LostKeyboardFocus += OnLostKeyboardFocus;
            PreviewKeyDown += (_, e) => HomeRows.MoveFocus(e);

            // Also when already handled: a Group's own sideways scroller handles the request before it gets here.
            AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(OnRequestBringIntoView), true);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;
            gameDetails = HomeRows.FindGameDetails(this);
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }
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
            // Rebuilding the Groups replaces their tiles, and with them the focused one: put focus back in the same place.
            if (e.PropertyName == nameof(HomeState.Groups) && hasFocus)
            {
                FocusTileSoon();
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub opened from a Group.
            if (!(bool)e.NewValue && homeFocus.LastFocusedRow == this)
            {
                FocusTileSoon();
            }
        }

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            hasFocus = true;
            homeFocus.LastFocusedRow = this;
            if (e.NewFocus is Button tile && Rows.ContainerFromElement(tile) is DependencyObject row)
            {
                lastGroup = Rows.ItemContainerGenerator.IndexFromContainer(row);
                lastGame = (tile.DataContext as HomeTile)?.GameId;
            }
        }

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Only a move to another element on screen takes focus out of the Groups; a tile removed by a rebuild, or
            // Playnite losing the foreground, leaves nothing visible focused.
            if (e.NewFocus is UIElement element && element.IsVisible && !IsAncestorOf(element))
            {
                hasFocus = false;
            }
        }

        /// <summary>A tile got focus: the Dashboard scrolls Home to centre its Group on screen, as far as the page goes.</summary>
        private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            var scroller = HomeRows.PageScroller(this);
            if (scroller == null || !(e.TargetObject is DependencyObject target) || !(Rows.ContainerFromElement(target) is FrameworkElement row))
            {
                return;
            }

            e.Handled = true;
            var bounds = row.TransformToAncestor(scroller).TransformBounds(new Rect(row.RenderSize));
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset + bounds.Top + bounds.Height / 2 - scroller.ViewportHeight / 2);
        }

        // Playnite sends the pad's A as Enter, which clicks the focused tile.
        private void OnTileClick(object sender, RoutedEventArgs e)
        {
            core.ChooseHomeTile((HomeTile)((FrameworkElement)sender).DataContext);
        }

        /// <summary>Focuses the last focused game's tile, or failing that its Group's first tile, once the tiles are laid out.</summary>
        private void FocusTileSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!IsVisible || Rows.Items.Count == 0)
                {
                    return;
                }

                var row = Rows.ItemContainerGenerator.ContainerFromIndex(Math.Min(lastGroup, Rows.Items.Count - 1));
                var tiles = row == null ? new List<Button>() : Tiles(row);
                var tile = tiles.FirstOrDefault(b => (b.DataContext as HomeTile)?.GameId == lastGame) ?? tiles.FirstOrDefault();
                tile?.Focus();
            }));
        }

        /// <summary>A Group's tiles, left to right, ending with See all.</summary>
        private static List<Button> Tiles(DependencyObject row)
        {
            var tiles = new List<Button>();
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(row); i++)
            {
                var child = VisualTreeHelper.GetChild(row, i);
                if (child is Button tile)
                {
                    tiles.Add(tile);
                }
                else
                {
                    tiles.AddRange(Tiles(child));
                }
            }

            return tiles;
        }
    }
}
