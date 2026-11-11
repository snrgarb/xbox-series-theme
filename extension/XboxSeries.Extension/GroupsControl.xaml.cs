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
    /// Home's Groups, injected as a custom element: the rest of Home's page, under the Recent row. It follows the core's
    /// <see cref="HomeState"/> and tells the core which tile has focus and which one A was pressed on. It holds no Home logic. A
    /// <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class GroupsControl : PluginUserControl, IHomeGroupsView
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly HomePage page;
        private FrameworkElement gameDetails;

        private bool layoutChangePending;

        /// <summary>Whether focus is in the Groups. Kept by hand: a rebuild removes the focused tile before this hears of it.</summary>
        private bool hasFocus;

        // Where focus last was, to put it back after a rebuild, a Game hub or My games & apps: the Group's place, and the
        // game, or its See all tile.
        private int lastGroup;
        private Guid? lastGame;
        private bool lastSeeAll;

        internal GroupsControl(ExtensionCore core, HomePage page)
        {
            InitializeComponent();
            this.core = core;
            this.page = page;
            RenderTransform = page.Scroll;
            DataContext = core.Home;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            GotKeyboardFocus += OnGotKeyboardFocus;
            LostKeyboardFocus += OnLostKeyboardFocus;
            PreviewKeyDown += OnPreviewKeyDown;
            KeyDown += OnKeyDown;
            IsVisibleChanged += OnVisibleChanged;

            // The Groups laid out anew: rebuilt, or grown or shrunk with the screen.
            SizeChanged += (_, __) => LayoutChangedSoon();
            Rows.SizeChanged += (_, __) => LayoutChangedSoon();
            Rows.ItemContainerGenerator.ItemsChanged += (_, __) => LayoutChangedSoon();
        }

        /// <summary>
        /// Tells the page the Groups' layout has changed, once their layout is done, however many changes came in the
        /// meantime. The page's slide moves this control by a render transform, which changes no layout, so it never
        /// comes back here.
        /// </summary>
        private void LayoutChangedSoon()
        {
            if (layoutChangePending)
            {
                return;
            }

            layoutChangePending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                layoutChangePending = false;
                if (page.Groups == this)
                {
                    page.LayoutChanged();
                }
            }));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;
            page.GroupsWanted += OnGroupsWanted;
            page.Groups = this;
            LayoutChangedSoon();
            gameDetails = HomeRows.FindGameDetails(this);
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged -= OnHomeChanged;
            page.GroupsWanted -= OnGroupsWanted;
            if (page.Groups == this)
            {
                page.Groups = null;
            }

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

        /// <summary>Down from the wide tiles: focus comes into the Groups, on the first Group's first game.</summary>
        private void OnGroupsWanted(object sender, EventArgs e)
        {
            lastGroup = 0;
            lastGame = null;
            lastSeeAll = false;
            FocusTileSoon();
        }

        /// <summary>
        /// Back on Home from My games &amp; apps, which hides Home's rows: the Groups take focus back if focus was in them,
        /// where the page still rests.
        /// </summary>
        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && page.RestsOnGroups)
            {
                FocusTileSoon();
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub opened from a Group.
            if (!(bool)e.NewValue && page.LastFocusedRow == this)
            {
                FocusTileSoon();
            }
        }

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            hasFocus = true;
            page.LastFocusedRow = this;
            if (e.NewFocus is Button tile && Rows.ContainerFromElement(tile) is FrameworkElement row)
            {
                // A row not yet known to the generator, as one being built, leaves the page where it rests.
                var index = Rows.ItemContainerGenerator.IndexFromContainer(row);
                if (index >= 0)
                {
                    lastGroup = index;
                    page.RestOn(HomeFocus.Group(lastGroup));
                }

                lastGame = (tile.DataContext as HomeTile)?.GameId;
                lastSeeAll = tile.DataContext is HomeGroup;
                if (tile.DataContext is HomeGroup group)
                {
                    logger.Info($"Home focus on See all of the Group {group.Name}");
                }
                else if (tile.DataContext is HomeTile game)
                {
                    logger.Info($"Home focus on the Group tile {game.Title}");
                }
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
        /// goes. Up from the first Group goes back up to the wide tiles.
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
                case Key.Up when group == 0: page.LeaveGroups(toWideTiles: true); break;
                case Key.Up: Focus(group - 1, place); break;
                default: return;
            }

            e.Handled = true;
        }

        /// <summary>
        /// B anywhere on the Groups goes back to the Recent row, on the game it last had focus. Playnite raises the pad's
        /// B on what has focus as a KeyDown alone, with no PreviewKeyDown before it, as My games &amp; apps and Settings
        /// find it on the view host; here it comes up from the focused tile. Handled, so nothing above takes it too.
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled && PlayniteInput.PadButtonOf(e) == ControllerInput.B && Keyboard.FocusedElement is Button tile && IsAncestorOf(tile))
            {
                e.Handled = true;
                logger.Info("Home: B on the Groups, back to the Recent row");
                page.LeaveGroups(toWideTiles: false);
            }
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

        /// <summary>
        /// Where the Groups are laid out with the page resting on the Recent row, in the Dashboard's pixels: each Group's
        /// top, and its cards' top under it and height, from the first Group's first card. Taken from where they are now,
        /// less the page's scroll so far (on this control, so not in what is measured inside it).
        /// </summary>
        public HomePageLayout Layout()
        {
            var host = HomeRows.FindPart(this, "PART_ViewHost");
            var screenHeight = host?.ActualHeight > 0 ? host.ActualHeight : 1080;
            var origin = host == null ? 0 : TranslatePoint(new Point(0, 0), host).Y - page.Scroll.Y;

            var tops = new List<double>();
            double cardsTop = 0, cardHeight = 0;
            for (var i = 0; i < Rows.Items.Count; i++)
            {
                if (!(Rows.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement row))
                {
                    break;
                }

                var top = origin + row.TranslatePoint(new Point(0, 0), this).Y;
                tops.Add(top);
                if (i == 0 && Tiles(row).FirstOrDefault() is Button card)
                {
                    var cardTop = origin + card.TranslatePoint(new Point(0, 0), this).Y;
                    cardsTop = cardTop - top;
                    cardHeight = origin + card.TranslatePoint(new Point(0, card.ActualHeight), this).Y - cardTop;
                }
            }

            return new HomePageLayout(screenHeight, tops, cardsTop, cardHeight);
        }

        /// <summary>The name of the Group at <paramref name="group"/>, for the log.</summary>
        public string NameOf(int group) =>
            group >= 0 && group < core.Home.Groups.Count ? core.Home.Groups[group].Name : null;

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

        // The keyboard's Enter and the mouse; the pad's A arrives as OnSeeAllKeyDown. The tile's DataContext is its Group.
        private void OnSeeAllClick(object sender, RoutedEventArgs e)
        {
            core.ChooseSeeAll((HomeGroup)((FrameworkElement)sender).DataContext);
        }

        private void OnSeeAllKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                core.ChooseSeeAll((HomeGroup)((FrameworkElement)sender).DataContext);
            }
        }

        /// <summary>
        /// Focuses the last focused game's tile, or its Group's See all tile if that was last focused, or failing either
        /// its Group's first tile, once the tiles are laid out.
        /// </summary>
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
                var tile = (lastSeeAll ? tiles.LastOrDefault() : tiles.FirstOrDefault(b => b.DataContext is HomeTile t && t.GameId == lastGame))
                    ?? tiles.FirstOrDefault();
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
