using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// My games &amp; apps, injected as a custom element over Home: the left nav, the header, Apps and Play history. It
    /// follows the core's <see cref="MyGamesState"/>, tells the core which nav item and which Play history game have focus,
    /// starts the App chosen, and takes B back to Home. It holds no page logic. The Dashboard lays Home and Playnite's main list out around it
    /// from <see cref="State"/>, and opens it from the top bar with <see cref="OpenGamesCommand"/>.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class MyGamesControl : PluginUserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// The console's grid: square tiles, scrolling down. Playnite's own panel, laid out by its own settings (Home's single
        /// row) unless replaced. It sizes its cells from its height, 3.5 of them to it, and fits as many to a row as its
        /// width allows, so <see cref="FitFiveToARow"/> keeps the list 3.5 fifths of its width tall.
        /// </summary>
        private const string GameGridPanel =
            "<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "<FullscreenTilePanel UseHorizontalLayout=\"False\" Rows=\"3\" ItemAspectRatio=\"1:1\"" +
            " SmoothScrollEnabled=\"{Settings Fullscreen.SmoothScrolling}\" />" +
            "</ItemsPanelTemplate>";

        /// <summary>Apps to a row, as Games' grid: the Apps list's UniformGrid takes its columns from this.</summary>
        public const int AppsPerRow = 5;

        private readonly ExtensionCore core;
        private FrameworkElement viewHost;
        private ListBox mainList;
        private FrameworkElement gameDetails;
        private ItemsControl libraryTabs;

        /// <summary>The Play history game that last had focus, to put focus back on after a rebuild or its Game hub.</summary>
        private Guid? lastFocusedGame;

        /// <summary>The App that last had focus, to put focus back on after Apps change or coming back from the nav.</summary>
        private Guid? lastFocusedApp;

        /// <summary>
        /// The grid's game focus last left for the nav, Playnite's own entry in its main list, for right from the nav to
        /// go back to rather than to the first game: normally still the main list's selected game, so going back changes
        /// nothing Playnite has to build again. Only for the nav item and Full library tab it was in, so null once either
        /// changes.
        /// </summary>
        private object lastFocusedGridGame;

        /// <summary>The Games grid game focus left for "See full library", to go back up to.</summary>
        private int gameAboveFooter;

        /// <summary>Whether the core is yet to hear of the main list's latest changes.</summary>
        private bool mainListChangePending;

        /// <summary>The nav item last shown, for which way along the nav focus moved to the next.</summary>
        private MyGamesItem shownItem;

        /// <summary>Full library's tab last lit, for which way along the tabs the user switched to the next; -1 for none.</summary>
        private int shownTab = -1;

        /// <summary>The game whose cover or queue row on Manage Queue last had focus, to put focus back on after its Game hub.</summary>
        private Guid? lastFocusedQueueGame;

        /// <summary>Recently installed's cover that last had focus, for up from the queue's first row to go back to.</summary>
        private int lastFocusedCover;

        /// <summary>
        /// Manage Queue's rows, one for each game in the queue, kept as the queue updates rather than built again: its
        /// figures change every moment, and a row built again would lose focus and read its cover again each time.
        /// </summary>
        private readonly ObservableCollection<QueueRow> queueRows = new ObservableCollection<QueueRow>();

        /// <summary>The queue row with focus, whose game's tile the core was last told of.</summary>
        private QueueRow focusedQueueRow;

        internal MyGamesControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.MyGames;
            OpenGamesCommand = new RelayCommand(() => core.OpenMyGames(MyGamesItem.Games));
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Nav.IsKeyboardFocusWithinChanged += OnNavFocusWithinChanged;
            shownItem = core.MyGames.Item;
            QueueRows.ItemsSource = queueRows;
            ShowHistory();
            ShowQueue();
            ShowItem();
            ShowStorage();
        }

        /// <summary>What the page shows, for the Dashboard to hide Home and place Playnite's main list by.</summary>
        public MyGamesState State => core.MyGames;

        /// <summary>The top bar's My games &amp; apps: the page, on Games.</summary>
        public ICommand OpenGamesCommand { get; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged += OnStateChanged;

            // Put up before the page was loaded: what it shows now is where the next move along the nav is from.
            shownItem = core.MyGames.Item;

            // Playnite's own pad B reaches whatever has focus on the page: the nav, Play history or its main list.
            viewHost = HomeRows.FindPart(this, "PART_ViewHost");
            if (viewHost != null)
            {
                viewHost.KeyDown += OnViewHostKeyDown;
                viewHost.PreviewKeyDown += OnViewHostPreviewKeyDown;
            }

            mainList = HomeRows.FindPart(this, "PART_ListGameItems") as ListBox;
            UseGameGridPanel();
            if (mainList != null)
            {
                mainList.SizeChanged += FitFiveToARow;
                mainList.PreviewKeyDown += OnMainListPreviewKeyDown;
                ((INotifyCollectionChanged)mainList.Items).CollectionChanged += OnMainListChanged;
            }

            gameDetails = HomeRows.FindPart(this, "PART_ElemGameDetails");
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }

            // Full library's tabs: Playnite lights the preset it applies, from LB/RB or from the page giving the user's back.
            var tabSelector = HomeRows.FindPart(this, "PART_FilterPresetSelector") as Control;
            tabSelector?.ApplyTemplate();
            libraryTabs = tabSelector?.Template?.FindName("PART_ItemsFilterPresets", tabSelector) as ItemsControl;
            libraryTabs?.AddHandler(ToggleButton.CheckedEvent, (RoutedEventHandler)OnLibraryTabChecked);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged -= OnStateChanged;

            // Gone mid-slide, nothing is left hidden or out of place, Playnite's main list least of all.
            PaneFlyIn.Stop(ItemHost, mainList);
            if (viewHost != null)
            {
                viewHost.KeyDown -= OnViewHostKeyDown;
                viewHost.PreviewKeyDown -= OnViewHostPreviewKeyDown;
                viewHost = null;
            }

            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged -= OnGameDetailsVisibleChanged;
                gameDetails = null;
            }

            if (libraryTabs != null)
            {
                libraryTabs.RemoveHandler(ToggleButton.CheckedEvent, (RoutedEventHandler)OnLibraryTabChecked);
                libraryTabs = null;
                shownTab = -1;
            }

            if (mainList != null)
            {
                mainList.SizeChanged -= FitFiveToARow;
                mainList.PreviewKeyDown -= OnMainListPreviewKeyDown;
                ((INotifyCollectionChanged)mainList.Items).CollectionChanged -= OnMainListChanged;
                mainList = null;
            }
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MyGamesState.IsShown):
                case nameof(MyGamesState.Item):
                    // Another item's games, or the page opened afresh: right from the nav starts again at the first.
                    lastFocusedGridGame = null;
                    ShowItem();
                    FlyInItem(e.PropertyName == nameof(MyGamesState.Item));

                    // Opened from Home, the top bar or the Guide: focus goes to the first tile. Moving through the nav keeps it there.
                    // Opened again by B from Settings, which Customise games & apps opened, it goes back to that menu's tile.
                    if (core.MyGames.IsShown && !Nav.IsKeyboardFocusWithin)
                    {
                        var returnsTo = e.PropertyName == nameof(MyGamesState.IsShown) ? core.MyGames.ReturnsToGame : null;
                        lastFocusedGame = returnsTo;
                        lastFocusedApp = null;
                        lastFocusedQueueGame = returnsTo;
                        lastFocusedCover = 0;
                        FocusContentSoon();
                    }

                    break;
                case nameof(MyGamesState.Storage):
                    ShowStorage();
                    break;
                case nameof(MyGamesState.PlayHistory):
                    // Rebuilding Play history replaces its rows, and with them the focused one: put focus back on the same game.
                    ShowHistory();
                    ShowItem();
                    if (History.IsKeyboardFocusWithin)
                    {
                        FocusContentSoon();
                    }

                    break;
                case nameof(MyGamesState.Apps):
                    // New Apps replace their tiles, and with them the focused one: put focus back on the same App.
                    ShowItem();
                    if (AppsList.IsKeyboardFocusWithin)
                    {
                        FocusContentSoon();
                    }

                    break;
                case nameof(MyGamesState.Queue):
                    // A row that went, as a download finishing, takes focus with it: put it back on the same game, or the first.
                    var hadFocus = QueuePage.IsKeyboardFocusWithin;
                    ShowQueue();
                    KeepQueueFocusSoon(hadFocus);
                    break;
                case nameof(MyGamesState.RecentlyInstalled):
                    // New covers replace the old, and with them the focused one: put focus back on the same game.
                    KeepQueueFocusSoon(Covers.IsKeyboardFocusWithin);
                    break;
            }
        }

        /// <summary>
        /// The header and its count, the lit nav item, Games' way to Full library, and Apps or Play history or the note
        /// that they're empty.
        /// </summary>
        private void ShowItem()
        {
            var item = core.MyGames.Item;
            var games = item == MyGamesItem.Games;
            var apps = item == MyGamesItem.Apps;
            var history = item == MyGamesItem.PlayHistory;
            var library = item == MyGamesItem.FullLibrary;
            var manage = item == MyGamesItem.Manage;
            var queue = item == MyGamesItem.ManageQueue;
            Header.Text = apps ? "Apps" : history ? "Play history" : library ? "Full library" : manage ? "Manage" : "Games";
            GamesItem.Tag = games ? "Selected" : null;
            AppsItem.Tag = apps ? "Selected" : null;
            PlayHistoryItem.Tag = history ? "Selected" : null;
            FullLibraryItem.Tag = library ? "Selected" : null;
            ManageItem.Tag = manage || queue ? "Selected" : null;
            QueueBox.Visibility = manage ? Visibility.Visible : Visibility.Collapsed;

            // Manage Queue is a page of its own over the whole of My games & apps, as on the console: the nav goes with it.
            var navShown = queue ? Visibility.Collapsed : Visibility.Visible;
            NavPane.Visibility = navShown;
            NavFoot.Visibility = navShown;
            NavDivider.Visibility = navShown;
            ItemHost.Visibility = navShown;
            QueuePage.Visibility = queue ? Visibility.Visible : Visibility.Collapsed;
            Count.Visibility = games ? Visibility.Visible : Visibility.Collapsed;
            LibraryCount.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
            LookingForMore.Visibility = games ? Visibility.Visible : Visibility.Collapsed;
            var empty = core.MyGames.PlayHistory.Count == 0;
            History.Visibility = history && !empty ? Visibility.Visible : Visibility.Collapsed;
            NoHistory.Visibility = history && empty ? Visibility.Visible : Visibility.Collapsed;
            var noApps = core.MyGames.Apps.Count == 0;
            AppsList.Visibility = apps && !noApps ? Visibility.Visible : Visibility.Collapsed;
            NoApps.Visibility = apps && noApps ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// As focus moves along the nav, the new item's content comes in from below for a move down it, or from above for
        /// a move up, Playnite's main list with it when it shows Games' grid. Changed any other way, as the page opening,
        /// it is just there.
        /// </summary>
        private void FlyInItem(bool itemChanged)
        {
            var item = core.MyGames.Item;
            PaneFlyIn.Follow(
                ref shownItem, item, shown => Nav.Children.IndexOf(NavItemOf(shown)),
                itemChanged && core.MyGames.IsShown && Nav.IsKeyboardFocusWithin,
                ItemHost, item == MyGamesItem.Games || item == MyGamesItem.FullLibrary ? mainList : null);
        }

        /// <summary>
        /// Switching Full library's tabs, its games come in from the right for a tab to the right, or from the left for one
        /// to the left, as the nav's do from below or above. A tab lit any other way, as the page applying the user's
        /// preset on its way to Full library (before it shows it), just lights.
        /// </summary>
        private void OnLibraryTabChecked(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is FrameworkElement tab) || mainList == null)
            {
                return;
            }

            var index = libraryTabs.Items.IndexOf(tab);
            if (index < 0)
            {
                index = libraryTabs.Items.IndexOf(tab.DataContext);
            }

            // Another tab's games: right from the nav starts again at the first.
            if (index != shownTab)
            {
                lastFocusedGridGame = null;
            }

            // Off Full library the list isn't the tabs' to move, so a slide it may be in along the nav is left alone.
            if (!core.MyGames.IsShown || core.MyGames.Item != MyGamesItem.FullLibrary || shownItem != MyGamesItem.FullLibrary)
            {
                shownTab = index;
                return;
            }

            PaneFlyIn.FollowAcross(ref shownTab, index, i => i, shownTab >= 0 && index >= 0, mainList);
        }

        /// <summary>The storage ring, filled round as far as the drive is used; hidden without a drive to show.</summary>
        private void ShowStorage()
        {
            var storage = core.MyGames.Storage;
            StorageRing.Visibility = storage == null ? Visibility.Collapsed : Visibility.Visible;
            if (storage != null)
            {
                // Dashes are in stroke widths: the ring's centre line is π × (its width − the stroke) long.
                var circumference = Math.PI * (StorageUsed.Width - StorageUsed.StrokeThickness) / StorageUsed.StrokeThickness;
                StorageUsed.StrokeDashArray = new DoubleCollection { storage.UsedFraction * circumference, circumference };
            }
        }

        /// <summary>
        /// Under Full library, the user switching tabs: its count follows. Once a burst of changes is over, as a library
        /// update adds its games one by one.
        /// </summary>
        private void OnMainListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (mainListChangePending)
            {
                return;
            }

            mainListChangePending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                mainListChangePending = false;
                core.OnMainListChanged();
            }));
        }

        /// <summary>The nav item of what the page shows.</summary>
        private Button NavItemOf(MyGamesItem item) =>
            item == MyGamesItem.Apps ? AppsItem
            : item == MyGamesItem.PlayHistory ? PlayHistoryItem
            : item == MyGamesItem.FullLibrary ? FullLibraryItem
            : item == MyGamesItem.Manage || item == MyGamesItem.ManageQueue ? ManageItem
            : GamesItem;

        /// <summary>Play history's games, top to bottom, in their sections.</summary>
        private void ShowHistory()
        {
            var sections = core.MyGames.PlayHistory;
            var view = new ListCollectionView(sections.SelectMany(s => s.Entries).ToList());
            view.GroupDescriptions.Add(new SectionOf(sections));
            History.ItemsSource = view;
        }

        /// <summary>
        /// Lays Playnite's main list out as the console's grid. With the extension the main list shows only as Games and
        /// Full library, so it keeps this layout; Home's tiles are the extension's own rows.
        /// </summary>
        private void UseGameGridPanel()
        {
            if (mainList == null)
            {
                return;
            }

            try
            {
                mainList.ItemsPanel = (ItemsPanelTemplate)XamlReader.Parse(GameGridPanel);
            }
            catch (Exception e)
            {
                logger.Error(e, "Can't lay Games out as a grid: this Playnite build has no FullscreenTilePanel as expected");
            }
        }

        /// <summary>
        /// Five tiles to a row, as on the console, whatever the screen's shape: Playnite makes the Dashboard 1080 tall and
        /// as wide as the screen's aspect ratio, so the list's width varies, and a fixed height would fit fewer cells to a
        /// row on a narrower screen. Floored, so rounding never makes a cell wider than a fifth.
        /// </summary>
        private static void FitFiveToARow(object sender, SizeChangedEventArgs e)
        {
            var list = (FrameworkElement)sender;
            if (e.WidthChanged && list.ActualWidth > 0)
            {
                list.Height = Math.Floor(list.ActualWidth / 5 * 3.5);
            }
        }

        /// <summary>
        /// Playnite's LB/RB switch the user's filter presets, which only Full library shows; elsewhere on the page they
        /// would swap the grid's games for a preset's, so they do nothing.
        /// </summary>
        private void OnViewHostPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var button = PlayniteInput.PadButtonOf(e);
            if (core.MyGames.IsShown && !core.MyGames.ShowsFullLibrary
                && (button == ControllerInput.LeftShoulder || button == ControllerInput.RightShoulder))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// In the grid, left from its first column goes back to the nav item it belongs to, and down from Games' last row
        /// to "See full library", under it.
        /// </summary>
        private void OnMainListPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!core.MyGames.ShowsGameGrid
                || (e.Key != Key.Left && e.Key != Key.Down)
                || !(ItemsControl.ContainerFromElement(mainList, e.OriginalSource as DependencyObject) is ListBoxItem game)
                || !(VisualTreeHelper.GetParent(game) is Panel panel))
            {
                return;
            }

            // The grid lays out as many columns as fit, so its rows are read from where the tiles are: the focused one's
            // row is on screen, so built.
            var top = Math.Round(game.TranslatePoint(new Point(), panel).Y);
            var row = panel.Children.OfType<ListBoxItem>()
                .Where(c => Math.Round(c.TranslatePoint(new Point(), panel).Y) == top)
                .ToList();
            var index = mainList.ItemContainerGenerator.IndexFromContainer(game);
            if (e.Key == Key.Left && row.All(c => c.TranslatePoint(new Point(), panel).X >= game.TranslatePoint(new Point(), panel).X))
            {
                e.Handled = true;
                lastFocusedGridGame = mainList.ItemContainerGenerator.ItemFromContainer(game);
                NavItemOf(core.MyGames.Item).Focus();
            }
            else if (e.Key == Key.Down && core.MyGames.Item == MyGamesItem.Games
                && row.Max(c => mainList.ItemContainerGenerator.IndexFromContainer(c)) == mainList.Items.Count - 1)
            {
                e.Handled = true;
                gameAboveFooter = index;
                SeeFullLibrary.Focus();
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnSeeFullLibraryKeyDown.
        private void OnSeeFullLibrary(object sender, RoutedEventArgs e)
        {
            // Focus goes by the nav item on to Full library's first game, which opens at once rather than as focus settles there.
            FullLibraryItem.Focus();
            core.OpenMyGames(MyGamesItem.FullLibrary);
            FocusContentSoon();
        }

        /// <summary>The pad's A opens Full library; up goes back into the grid, left to the nav.</summary>
        private void OnSeeFullLibraryKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                OnSeeFullLibrary(sender, e);
            }
            else if (e.Key == Key.Up)
            {
                e.Handled = true;
                FocusGame(gameAboveFooter);
            }
            else if (e.Key == Key.Left)
            {
                e.Handled = true;
                if (mainList != null && gameAboveFooter < mainList.Items.Count)
                {
                    lastFocusedGridGame = mainList.Items[gameAboveFooter];
                }

                GamesItem.Focus();
            }
            else if (e.Key == Key.Right || e.Key == Key.Down)
            {
                e.Handled = true;
            }
        }

        /// <summary>B on the page, from Playnite: back to Home. B on the Game hub or a menu never gets here.</summary>
        private void OnViewHostKeyDown(object sender, KeyEventArgs e)
        {
            if (core.MyGames.IsShown && PlayniteInput.PadButtonOf(e) == ControllerInput.B)
            {
                // On Manage Queue, B goes back to Manage rather than leaving the page.
                var item = core.MyGames.Item;
                core.LeaveMyGames();
                e.Handled = !core.MyGames.IsShown || core.MyGames.Item != item;
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub opened from Play history or Manage Queue. Playnite puts focus back on Games' main list itself.
            if (!(bool)e.NewValue && core.MyGames.IsShown
                && (core.MyGames.Item == MyGamesItem.PlayHistory || core.MyGames.Item == MyGamesItem.ManageQueue))
            {
                FocusContentSoon();
            }
        }

        // The highlight and its sound follow focus at once; the item's content waits for focus to settle on it.
        private void OnNavFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            core.FocusMyGamesNav(ItemOfNav(sender).Value);
        }

        // Out of the nav other than into the content, as with Playnite leaving the foreground: an item waiting to open doesn't.
        private void OnNavFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                core.LeaveMyGamesNav();
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnNavKeyDown.
        private void OnNavClick(object sender, RoutedEventArgs e)
        {
            OpenNavItemNow(sender);
            FocusContentSoon();
        }

        /// <summary>Right, or the pad's A, goes into the item's games; up and down move through the nav.</summary>
        private void OnNavKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right || PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                OpenNavItemNow(e.OriginalSource);
                FocusContentSoon();
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                HomeRows.MoveFocus(e);
            }
            else if (e.Key == Key.Left)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Going into a nav item's content opens the item first if it is still waiting for focus to settle, so focus
        /// never lands in the content of the item before.
        /// </summary>
        private void OpenNavItemNow(object navItem)
        {
            if (ItemOfNav(navItem) is MyGamesItem item)
            {
                core.OpenMyGames(item);
            }
        }

        /// <summary>What the nav item opens; null for anything else.</summary>
        private MyGamesItem? ItemOfNav(object navItem) =>
            Enum.GetValues(typeof(MyGamesItem)).Cast<MyGamesItem?>().FirstOrDefault(item => NavItemOf(item.Value) == navItem);

        /// <summary>Left goes back to the nav. The list itself moves up and down, building rows as they come on screen.</summary>
        private void OnHistoryKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                e.Handled = true;
                PlayHistoryItem.Focus();
            }
            else if (e.Key == Key.Right)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Left from an App in the first column goes back to the nav; at the grid's other edges focus stays put. The list
        /// itself moves between Apps.
        /// </summary>
        private void OnAppsKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left
                && ItemsControl.ContainerFromElement(AppsList, e.OriginalSource as DependencyObject) is ListBoxItem tile
                && AppsList.ItemContainerGenerator.IndexFromContainer(tile) % AppsPerRow == 0)
            {
                e.Handled = true;
                AppsItem.Focus();
            }
        }

        private void OnAppFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is App app)
            {
                lastFocusedApp = app.Id;
            }
        }

        // An App is no game, so the core's own A, for the game with focus, doesn't reach it: the pad's A starts it here,
        // as do the keyboard's Enter and the mouse.
        private void OnAppKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                StartApp(sender);
            }
        }

        private void OnAppClick(object sender, MouseButtonEventArgs e)
        {
            StartApp(sender);
        }

        private void StartApp(object tile)
        {
            if (((FrameworkElement)tile).DataContext is App app)
            {
                core.StartApp(app.Id);
            }
        }

        // Menu and the pad's A on a Play history game work as on a Home tile, through the core.
        private void OnEntryFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is PlayHistoryEntry entry)
            {
                lastFocusedGame = entry.Tile.GameId;
                core.FocusHomeTile(entry.Tile);
            }
        }

        private void OnEntryLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            // A row a rebuild has thrown away holds WPF's disconnected item, not its game, by the time it loses focus.
            if (((FrameworkElement)sender).DataContext is PlayHistoryEntry entry)
            {
                core.LeaveHomeTile(entry.Tile);
            }
        }

        // The keyboard's Enter and the mouse choose the row; the pad's A reaches the core as a button instead.
        private void OnEntryKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && PlayniteInput.PadButtonOf(e) == null)
            {
                e.Handled = true;
                ChooseEntry(sender);
            }
        }

        private void OnEntryClick(object sender, MouseButtonEventArgs e)
        {
            ChooseEntry(sender);
        }

        private void ChooseEntry(object row)
        {
            if (((FrameworkElement)row).DataContext is PlayHistoryEntry entry)
            {
                core.ChooseHomeTile(entry.Tile);
            }
        }

        /// <summary>
        /// Focuses the item's tile once it is laid out: the grid's game, App or Play history game that last had focus,
        /// else its first. With none, focus stays on the nav.
        /// </summary>
        private void FocusContentSoon()
        {
            // After Playnite has refilled its main list from the filter the core just applied.
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!core.MyGames.IsShown || !IsVisible)
                {
                    return;
                }

                if (core.MyGames.Item == MyGamesItem.Apps)
                {
                    FocusApp();
                    return;
                }

                if (core.MyGames.ShowsGameGrid)
                {
                    // Gone from the list, as a game uninstalled off Games, it is -1, so the first.
                    var last = lastFocusedGridGame == null || mainList == null ? -1 : mainList.Items.IndexOf(lastFocusedGridGame);
                    FocusGame(Math.Max(last, 0));
                    return;
                }

                if (core.MyGames.Item == MyGamesItem.Manage)
                {
                    QueueBox.Focus();
                    return;
                }

                if (core.MyGames.Item == MyGamesItem.ManageQueue)
                {
                    FocusQueuePage();
                    return;
                }

                var entries = History.Items.Cast<PlayHistoryEntry>().ToList();
                var entry = entries.FirstOrDefault(g => g.Tile.GameId == lastFocusedGame) ?? entries.FirstOrDefault();
                if (entry == null)
                {
                    PlayHistoryItem.Focus();
                    return;
                }

                History.ScrollIntoView(entry);
                History.UpdateLayout();
                ((UIElement)RowOf(History, entry) ?? PlayHistoryItem).Focus();
            }));
        }

        /// <summary>Focuses the App that last had focus, else the first; with none, the nav item.</summary>
        private void FocusApp()
        {
            var apps = core.MyGames.Apps;
            var app = apps.FirstOrDefault(a => a.Id == lastFocusedApp) ?? apps.FirstOrDefault();
            if (app == null)
            {
                AppsItem.Focus();
                return;
            }

            AppsList.UpdateLayout();
            ((UIElement)AppsList.ItemContainerGenerator.ContainerFromItem(app) ?? AppsItem).Focus();
        }

        /// <summary>
        /// Focuses and selects the grid's game at <paramref name="index"/>; with no games, the nav item. A game whose tile
        /// is already built, as the one focus just left for the nav, is focused as it is: scrolling to it and laying the
        /// whole list out again is only for one that isn't.
        /// </summary>
        private void FocusGame(int index)
        {
            if (mainList == null || mainList.Items.Count == 0)
            {
                NavItemOf(core.MyGames.Item).Focus();
                return;
            }

            // Selected too: the Game hub, opened by A, shows the main list's selected game.
            index = Math.Min(index, mainList.Items.Count - 1);
            mainList.SelectedIndex = index;
            if (mainList.ItemContainerGenerator.ContainerFromIndex(index) == null)
            {
                mainList.ScrollIntoView(mainList.Items[index]);
                mainList.UpdateLayout();
            }

            if (mainList.ItemContainerGenerator.ContainerFromIndex(index) is UIElement game)
            {
                game.Focus();
            }
        }

        /// <summary>
        /// Manage Queue's rows follow the queue: a game's row stays as its figures change, taking the new ones, and rows
        /// come, go and move as games do. The row with focus tells the core of its game's new tile.
        /// </summary>
        private void ShowQueue()
        {
            var queue = core.MyGames.Queue;
            for (var i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                var at = -1;
                for (var j = i; j < queueRows.Count; j++)
                {
                    if (queueRows[j].GameId == entry.Tile.GameId)
                    {
                        at = j;
                        break;
                    }
                }

                if (at < 0)
                {
                    queueRows.Insert(i, new QueueRow(entry));
                    continue;
                }

                if (at != i)
                {
                    queueRows.Move(at, i);
                }

                var row = queueRows[i];
                var oldTile = row.Entry.Tile;
                row.Entry = entry;
                if (row == focusedQueueRow && entry.Tile != oldTile)
                {
                    core.FocusHomeTile(entry.Tile);
                }
            }

            while (queueRows.Count > queue.Count)
            {
                queueRows.RemoveAt(queueRows.Count - 1);
            }
        }

        /// <summary>
        /// Once Manage Queue's covers or rows have changed, focus back on the same game, or the first, if the page had it
        /// and lost it.
        /// </summary>
        private void KeepQueueFocusSoon(bool hadFocus)
        {
            if (!hadFocus)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (core.MyGames.IsShown && core.MyGames.Item == MyGamesItem.ManageQueue && IsVisible
                    && (!QueuePage.IsKeyboardFocusWithin || QueuePage.IsKeyboardFocused))
                {
                    FocusQueuePage();
                }
            }));
        }

        /// <summary>
        /// Focuses Manage Queue's cover or row of the game that last had focus, else the first cover, else the first row;
        /// with neither, the page itself, so B still reaches it.
        /// </summary>
        private void FocusQueuePage()
        {
            QueuePage.UpdateLayout();
            var rowIndex = queueRows.ToList().FindIndex(r => r.GameId == lastFocusedQueueGame);
            var coverIndex = core.MyGames.RecentlyInstalled.ToList().FindIndex(t => t.GameId == lastFocusedQueueGame);
            var target = rowIndex >= 0 ? ButtonIn(QueueRows, rowIndex)
                : coverIndex >= 0 ? ButtonIn(Covers, coverIndex)
                : ButtonIn(Covers, 0) ?? ButtonIn(QueueRows, 0);
            if (target == null || !target.Focus())
            {
                QueuePage.Focus();
            }
        }

        /// <summary>
        /// On Manage Queue the D-pad goes along Recently installed's covers, down from them to the queue's rows and down
        /// those, and back up; up from the first row to the cover last focused. The nav is hidden, so nothing leaves the
        /// page sideways.
        /// </summary>
        private void OnQueuePageKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down)
            {
                return;
            }

            e.Handled = true;
            var source = e.OriginalSource as DependencyObject;
            var cover = IndexIn(Covers, source);
            var row = IndexIn(QueueRows, source);
            Button next;
            if (cover >= 0)
            {
                next = e.Key == Key.Left ? ButtonIn(Covers, cover - 1)
                    : e.Key == Key.Right ? ButtonIn(Covers, cover + 1)
                    : e.Key == Key.Down ? ButtonIn(QueueRows, 0)
                    : null;
            }
            else if (row >= 0)
            {
                next = e.Key == Key.Down ? ButtonIn(QueueRows, row + 1)
                    : e.Key != Key.Up ? null
                    : row > 0 ? ButtonIn(QueueRows, row - 1)
                    : ButtonIn(Covers, lastFocusedCover) ?? ButtonIn(Covers, 0);
            }
            else
            {
                // On the page itself, having had nothing to focus when it was put up: whatever has come since.
                next = ButtonIn(Covers, 0) ?? ButtonIn(QueueRows, 0);
            }

            next?.Focus();
        }

        /// <summary>The index in <paramref name="list"/> of the item holding <paramref name="element"/>; -1 for none.</summary>
        private static int IndexIn(ItemsControl list, DependencyObject element)
        {
            var container = element == null ? null : ItemsControl.ContainerFromElement(list, element);
            return container == null ? -1 : list.ItemContainerGenerator.IndexFromContainer(container);
        }

        /// <summary>The button built for <paramref name="list"/>'s item at <paramref name="index"/>; null for none.</summary>
        private static Button ButtonIn(ItemsControl list, int index)
        {
            if (index < 0 || index >= list.Items.Count
                || !(list.ItemContainerGenerator.ContainerFromIndex(index) is ContentPresenter container))
            {
                return null;
            }

            container.ApplyTemplate();
            return VisualTreeHelper.GetChildrenCount(container) > 0 ? VisualTreeHelper.GetChild(container, 0) as Button : null;
        }

        // Manage's Queue box: the keyboard's Enter and the mouse; the pad's A arrives as OnQueueBoxKeyDown.
        private void OnQueueBoxClick(object sender, RoutedEventArgs e)
        {
            core.OpenMyGames(MyGamesItem.ManageQueue);
        }

        /// <summary>The pad's A opens Manage Queue; left goes back to the nav. It is Manage's only box, so nothing else moves.</summary>
        private void OnQueueBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                core.OpenMyGames(MyGamesItem.ManageQueue);
            }
            else if (e.Key == Key.Left)
            {
                e.Handled = true;
                ManageItem.Focus();
            }
            else if (e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
            }
        }

        // Recently installed's covers and the queue's rows: focus, Menu, View and the pad's A work as on a Home tile, through
        // the core. The keyboard's Enter and the mouse choose the game here; the pad's A, which has no key, never clicks.
        private void OnCoverFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is HomeTile tile)
            {
                lastFocusedQueueGame = tile.GameId;
                lastFocusedCover = Math.Max(0, IndexIn(Covers, (DependencyObject)sender));

                // The covers are the page's top: coming up from the rows, the page scrolls back to them.
                QueueScroll.ScrollToTop();
                core.FocusHomeTile(tile);
            }
        }

        private void OnCoverLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is HomeTile tile)
            {
                core.LeaveHomeTile(tile);
            }
        }

        private void OnCoverClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is HomeTile tile)
            {
                core.ChooseHomeTile(tile);
            }
        }

        private void OnQueueRowFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is QueueRow row)
            {
                focusedQueueRow = row;
                lastFocusedQueueGame = row.GameId;
                core.FocusHomeTile(row.Entry.Tile);
            }
        }

        private void OnQueueRowLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is QueueRow row)
            {
                if (focusedQueueRow == row)
                {
                    focusedQueueRow = null;
                }

                core.LeaveHomeTile(row.Entry.Tile);
            }
        }

        private void OnQueueRowClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is QueueRow row)
            {
                core.ChooseHomeTile(row.Entry.Tile);
            }
        }

        /// <summary>
        /// The row built for <paramref name="entry"/>; null if it isn't on screen. Grouped, the list's own lookup only
        /// knows its sections, so this looks through what's built.
        /// </summary>
        private static ListBoxItem RowOf(DependencyObject parent, PlayHistoryEntry entry)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var row = child is ListBoxItem item && item.DataContext == entry ? item : RowOf(child, entry);
                if (row != null)
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// One of Manage Queue's rows: a game in the queue, kept while it stays there. Its cover changes only with the
        /// game's, so the row reads it once, not with every new figure.
        /// </summary>
        public sealed class QueueRow : INotifyPropertyChanged
        {
            private QueueEntry entry;
            private string cover;

            internal QueueRow(QueueEntry entry)
            {
                GameId = entry.Tile.GameId;
                Entry = entry;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public Guid GameId { get; }

            /// <summary>The game's place in the queue now: its state, speed, amount, share and bar.</summary>
            public QueueEntry Entry
            {
                get => entry;
                internal set
                {
                    entry = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Entry)));
                    if (cover != value.Tile.CoverImage)
                    {
                        cover = value.Tile.CoverImage;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cover)));
                    }
                }
            }

            /// <summary>The full path or URL of the game's cover; null when it has none.</summary>
            public string Cover => cover;
        }

        /// <summary>Puts each Play history game in its section, keeping the sections' order.</summary>
        private sealed class SectionOf : GroupDescription
        {
            private readonly Dictionary<PlayHistoryEntry, string> sections;

            public SectionOf(IEnumerable<PlayHistorySection> sections)
            {
                this.sections = sections.SelectMany(s => s.Entries.Select(e => new { s.Title, Entry = e }))
                    .ToDictionary(p => p.Entry, p => p.Title);
            }

            public override object GroupNameFromItem(object item, int level, CultureInfo culture) => sections[(PlayHistoryEntry)item];
        }
    }
}
