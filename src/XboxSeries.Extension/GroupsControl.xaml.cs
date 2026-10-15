using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Playnite.SDK.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Home's Groups, injected as a custom element: the screen below the Recent row. It follows the core's
    /// <see cref="HomeState"/> and tells the core which tile has focus and which one A was pressed on. It holds no Home logic. A
    /// <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class GroupsControl : PluginUserControl
    {
        private static readonly Duration ScrollDuration = TimeSpan.FromMilliseconds(300);

        private readonly ExtensionCore core;
        private readonly HomeScreen homeScreen;
        private FrameworkElement gameDetails;

        /// <summary>Whether focus is in the Groups. Kept by hand: a rebuild removes the focused tile before this hears of it.</summary>
        private bool hasFocus;

        // Where focus last was, to put it back after a rebuild or a Game hub: the Group's place, and the game (null for See all).
        private int lastGroup;
        private Guid? lastGame;

        internal GroupsControl(ExtensionCore core, HomeScreen homeScreen)
        {
            InitializeComponent();
            this.core = core;
            this.homeScreen = homeScreen;
            DataContext = core.Home;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            GotKeyboardFocus += OnGotKeyboardFocus;
            LostKeyboardFocus += OnLostKeyboardFocus;
            PreviewKeyDown += OnPreviewKeyDown;
            IsVisibleChanged += OnVisibleChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;
            homeScreen.Changed += OnScreenChanged;
            homeScreen.HasGroups = true;
            HomeRows.Fly(this, homeScreen.ShowsGroups, HomeRows.FlyDistance, animate: false);
            gameDetails = HomeRows.FindGameDetails(this);
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged -= OnHomeChanged;
            homeScreen.Changed -= OnScreenChanged;
            homeScreen.HasGroups = false;
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

        /// <summary>Flies the Groups in from below as the Recent row goes, onto the first Group, and back out as it returns.</summary>
        private void OnScreenChanged(object sender, EventArgs e)
        {
            HomeRows.Fly(this, homeScreen.ShowsGroups, HomeRows.FlyDistance);
            if (homeScreen.ShowsGroups)
            {
                lastGroup = 0;
                lastGame = null;
                FocusTileSoon();
            }
        }

        /// <summary>Back on Home from My games &amp; apps, which hides Home's rows: the Groups take focus if they are the screen showing.</summary>
        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && homeScreen.ShowsGroups)
            {
                FocusTileSoon();
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub opened from a Group.
            if (!(bool)e.NewValue && homeScreen.LastFocusedRow == this)
            {
                FocusTileSoon();
            }
        }

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            hasFocus = true;
            homeScreen.LastFocusedRow = this;
            homeScreen.Show(groups: true);
            if (e.NewFocus is Button tile && Rows.ContainerFromElement(tile) is FrameworkElement row)
            {
                lastGroup = Rows.ItemContainerGenerator.IndexFromContainer(row);
                lastGame = (tile.DataContext as HomeTile)?.GameId;
                ScrollTo(row);
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

        /// <summary>
        /// The D-pad moves along a Group and between Groups, keeping to the same place in the row as far as the next row
        /// goes. Up from the first Group goes back to the Recent row.
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(Keyboard.FocusedElement is Button tile) || !(Rows.ContainerFromElement(tile) is DependencyObject row))
            {
                return;
            }

            var group = Rows.ItemContainerGenerator.IndexFromContainer(row);
            var place = Tiles(row).IndexOf(tile);
            switch (e.Key)
            {
                case Key.Left: Focus(group, place - 1); break;
                case Key.Right: Focus(group, place + 1); break;
                case Key.Down: Focus(group + 1, place); break;
                case Key.Up when group == 0: homeScreen.Show(groups: false); break;
                case Key.Up: Focus(group - 1, place); break;
                default: return;
            }

            e.Handled = true;
        }

        /// <summary>Focuses the tile at <paramref name="place"/> in a Group, or its last tile if the Group is shorter.</summary>
        private void Focus(int group, int place)
        {
            if (group < 0 || group >= Rows.Items.Count || place < 0)
            {
                return;
            }

            var row = Rows.ItemContainerGenerator.ContainerFromIndex(group);
            var tiles = row == null ? new List<Button>() : Tiles(row);
            if (tiles.Count > 0)
            {
                tiles[Math.Min(place, tiles.Count - 1)].Focus();
            }
        }

        /// <summary>Slides the Groups so that <paramref name="row"/> sits at the top of the screen.</summary>
        private void ScrollTo(FrameworkElement row)
        {
            var scroll = -row.TranslatePoint(new Point(0, 0), Rows).Y;
            Scroll.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(scroll, ScrollDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            ShowWholeGroupsOnly(scroll);
        }

        /// <summary>
        /// Fades out every Group that won't fit wholly on screen once the Groups have slid by <paramref name="scroll"/>,
        /// and fades in the rest, so that no Group is ever shown cut off at the screen's edge.
        /// </summary>
        private void ShowWholeGroupsOnly(double scroll)
        {
            for (var i = 0; i < Rows.Items.Count; i++)
            {
                if (!(Rows.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement row) || VisualTreeHelper.GetChildrenCount(row) == 0)
                {
                    continue;
                }

                // The Group itself, without the space after it.
                var group = (FrameworkElement)VisualTreeHelper.GetChild(row, 0);
                var top = group.TranslatePoint(new Point(0, 0), Rows).Y + scroll;
                var fits = top >= 0 && top + group.ActualHeight <= View.ActualHeight;
                row.BeginAnimation(OpacityProperty, new DoubleAnimation(fits ? 1 : 0, ScrollDuration));
            }
        }

        // Menu on a game tile opens its tile menu.
        private void OnTileFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is HomeTile tile)
            {
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

        // The keyboard's Enter and the mouse click the tile; the pad's A reaches the core as a button instead.
        private void OnTileClick(object sender, RoutedEventArgs e)
        {
            core.ChooseHomeTile((HomeTile)((FrameworkElement)sender).DataContext);
        }

        /// <summary>Focuses the last focused game's tile, or failing that its Group's first tile, once the tiles are laid out.</summary>
        private void FocusTileSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                // Not behind a Game hub, as a game exiting from one leaves it up a moment: its closing focuses the row then.
                if (!IsVisible || gameDetails?.IsVisible == true || Rows.Items.Count == 0)
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
