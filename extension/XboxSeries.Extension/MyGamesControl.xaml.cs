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

        /// <summary>Where the Dashboard places its main list, which Full library lifts and leaving it puts back.</summary>
        private Thickness themeListMargin;

        /// <summary>The Dashboard's gap around each tile in its cell, so above a row's tiles.</summary>
        private const double TileGap = 7.5;

        /// <summary>From where the Dashboard clips Full library's list, under its Sort and Filter row, to its first row's tiles.</summary>
        private const double FullLibraryFirstRowPadding = 24;
        private FrameworkElement gameDetails;

        /// <summary>
        /// Full library's tabs, Playnite's filter preset selector the Dashboard places over the page: they come in with the
        /// page's content along the nav, and stay put as tabs switch.
        /// </summary>
        private Control libraryTabSelector;
        private ItemsControl libraryTabs;

        /// <summary>
        /// The Play history game that last had focus, to put focus back on after a rebuild or its Game hub; or the game to
        /// go back to on reopening, on the grid too, as B from Search opened by Y on Games.
        /// </summary>
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

        /// <summary>Whether Full library's name box is open, from Filter or Y, before it holds a name.</summary>
        private bool nameBoxOpen;

        /// <summary>Whether text entry over the page is up for Full library's name, from the name box.</summary>
        private bool typingName;

        /// <summary>The name the grid was narrowed by as text entry opened, to tell a new name kept from one left as it was.</summary>
        private string nameBeforeTyping;

        /// <summary>
        /// Playnite's own letter keys (Y for its name entry, F for its filters, R for its presets), taken off the page
        /// while the name box has focus, so a letter typed on a keyboard there opens text entry with it rather than
        /// running them.
        /// </summary>
        private readonly List<InputBinding> suspendedKeys = new List<InputBinding>();

        /// <summary>What in Full library's Sort and Filter row last had focus, for up from the grid and down from the tabs.</summary>
        private UIElement lastRowFocus;

        internal MyGamesControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.MyGames;
            OpenGamesCommand = new RelayCommand(() => core.OpenMyGames(MyGamesItem.Games));
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Nav.IsKeyboardFocusWithinChanged += OnNavFocusWithinChanged;
            KeyDown += OnPageKeyDown;

            // Handled ones too: a TextBox marks every KeyDown it is sent handled, the pad's buttons too, which Playnite
            // sends as KeyDown with no key, before the box's own handlers hear of them.
            NameBox.AddHandler(KeyDownEvent, new KeyEventHandler(OnNameBoxKeyDown), handledEventsToo: true);
            shownItem = core.MyGames.Item;
            QueueRows.ItemsSource = queueRows;
            ShowHistory();
            ShowQueue();
            ShowItem();
            ShowStorage();
        }

        /// <summary>What the page shows, for the Dashboard to hide Home and place Playnite's main list by.</summary>
        public MyGamesState State => core.MyGames;

        /// <summary>The profile signed in, which the nav's top shows.</summary>
        public StatusState Status => core.Status;

        /// <summary>The top bar's My games &amp; apps: the page, on Games.</summary>
        public ICommand OpenGamesCommand { get; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged += OnStateChanged;
            core.TextEntry.PropertyChanged += OnTextEntryChanged;

            // Put up before the page was loaded: what it shows now is where the next move along the nav is from.
            shownItem = core.MyGames.Item;

            // Playnite's own pad B reaches whatever has focus on the page: the nav, Play history or its main list.
            viewHost = HomeRows.FindPart(this, "PART_ViewHost");
            if (viewHost != null)
            {
                viewHost.KeyDown += OnViewHostKeyDown;
            }

            mainList = HomeRows.FindPart(this, "PART_ListGameItems") as ListBox;
            UseGameGridPanel();
            if (mainList != null)
            {
                themeListMargin = mainList.Margin;
                LiftFullLibrarysFirstRow();
                mainList.SizeChanged += FitFiveToARow;
                mainList.PreviewKeyDown += OnMainListPreviewKeyDown;
                mainList.KeyDown += OnMainListKeyDown;
                mainList.AddHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)OnMainListScrolled);
                ((INotifyCollectionChanged)mainList.Items).CollectionChanged += OnMainListChanged;
            }

            gameDetails = HomeRows.FindPart(this, "PART_ElemGameDetails");
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }

            // Full library's tabs: Playnite lights the preset it applies, from LB/RB or from the page giving the user's back.
            libraryTabSelector = HomeRows.FindPart(this, "PART_FilterPresetSelector") as Control;
            libraryTabSelector?.ApplyTemplate();
            libraryTabs = libraryTabSelector?.Template?.FindName("PART_ItemsFilterPresets", libraryTabSelector) as ItemsControl;
            libraryTabs?.AddHandler(ToggleButton.CheckedEvent, (RoutedEventHandler)OnLibraryTabChecked);

            // Loaded again, as when the Guide brings Playnite back from a Game hub, the page missed what changed while it
            // was unloaded, its item and the page being put up too: shown afresh, and focus put on its content, as
            // Settings and Search do.
            ShowHistory();
            ShowQueue();
            ShowItem();
            ShowStorage();
            ShowName();
            if (core.MyGames.IsShown && !IsKeyboardFocusWithin)
            {
                FocusContentSoon();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged -= OnStateChanged;
            core.TextEntry.PropertyChanged -= OnTextEntryChanged;
            typingName = false;

            // Gone mid-slide, nothing is left hidden or out of place, Playnite's main list and Full library's tabs least of all.
            PaneFlyIn.Stop(ItemHost, mainList, libraryTabSelector);
            libraryTabSelector = null;
            SortPopup.IsOpen = false;
            ResumeLetterKeys();
            if (viewHost != null)
            {
                viewHost.KeyDown -= OnViewHostKeyDown;
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
                mainList.Margin = themeListMargin;
                mainList.SizeChanged -= FitFiveToARow;
                mainList.PreviewKeyDown -= OnMainListPreviewKeyDown;
                mainList.KeyDown -= OnMainListKeyDown;
                mainList.RemoveHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)OnMainListScrolled);
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
                    lastRowFocus = null;
                    if (!core.MyGames.ShowsFullLibrary)
                    {
                        SortPopup.IsOpen = false;
                        nameBoxOpen = false;
                    }

                    ShowItem();
                    LiftFullLibrarysFirstRow();
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
                case nameof(MyGamesState.NameFilter):
                    ShowName();
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
            LibraryBar.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
            TabsFocus.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
            ShowNameBox();
            PlaceLookingForMore();
            var empty = core.MyGames.PlayHistory.Count == 0;
            History.Visibility = history && !empty ? Visibility.Visible : Visibility.Collapsed;
            NoHistory.Visibility = history && empty ? Visibility.Visible : Visibility.Collapsed;
            var noApps = core.MyGames.Apps.Count == 0;
            AppsList.Visibility = apps && !noApps ? Visibility.Visible : Visibility.Collapsed;
            NoApps.Visibility = apps && noApps ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// As focus moves along the nav, the new item's content comes in from below for a move down it, or from above for
        /// a move up, Playnite's main list with it when it shows Games' grid. Full library's tabs, which the Dashboard
        /// places over the page rather than in it, come in with its header and its Sort and Filter row, sliding and fading
        /// in as one, rather than standing there as they come. Changed any other way, as the page opening, it is just there.
        /// </summary>
        private void FlyInItem(bool itemChanged)
        {
            var item = core.MyGames.Item;
            PaneFlyIn.Follow(
                ref shownItem, item, shown => Nav.Children.IndexOf(NavItemOf(shown)),
                itemChanged && core.MyGames.IsShown && Nav.IsKeyboardFocusWithin,
                ItemHost, item == MyGamesItem.Games || item == MyGamesItem.FullLibrary ? mainList : null,
                item == MyGamesItem.FullLibrary ? libraryTabSelector : null);
        }

        /// <summary>
        /// Switching Full library's tabs, its games come in from the right for a tab to the right, or from the left for one
        /// to the left, as the nav's do from below or above; the tabs themselves stay put, as the Guide's do and the
        /// console's. A tab lit any other way, as the page applying the user's
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

            // The tab's preset can bring a sort of its own: the one chosen on Full library's dropdown goes back on at
            // once, before the grid is drawn in the preset's.
            if (core.MyGames.ShowsFullLibrary)
            {
                core.OnMainListChanged();
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
        private void FitFiveToARow(object sender, SizeChangedEventArgs e)
        {
            var list = (FrameworkElement)sender;
            if (e.WidthChanged && list.ActualWidth > 0)
            {
                list.Height = Math.Floor(list.ActualWidth / 5 * 3.5);
                LiftFullLibrarysFirstRow();
            }
        }

        /// <summary>
        /// Full library's first row a padding under its Sort and Filter row, rather than under the quarter cell Playnite's
        /// panel always leaves above it: the list is lifted by that quarter cell, its tile gap and less the padding, and the
        /// Dashboard clips it where it would otherwise start, so scrolled tiles still stop under the row. A quarter of a
        /// cell, a fifth of the list's width, which varies with the screen's shape; elsewhere the list keeps the theme's place.
        /// </summary>
        private void LiftFullLibrarysFirstRow()
        {
            if (mainList == null)
            {
                return;
            }

            var margin = themeListMargin;
            if (core.MyGames.ShowsFullLibrary && mainList.ActualWidth > 0)
            {
                margin.Top -= Math.Floor(mainList.ActualWidth / 5 * 3.5) / 3.5 / 4 + TileGap - FullLibraryFirstRowPadding;
            }

            mainList.Margin = margin;
        }

        private void OnMainListScrolled(object sender, ScrollChangedEventArgs e) => PlaceLookingForMore();

        /// <summary>
        /// "Looking for more?" under Games' last row, as on the console, rather than fixed under the grid: shown only while
        /// that row is all in view, so once the grid is scrolled to its end. Placed from the last tile itself, as Playnite's
        /// panel scrolls by moving its tiles; until it is built, the row isn't in view. Again once the page has laid out,
        /// as Games coming back shows the grid without scrolling it.
        /// </summary>
        private void PlaceLookingForMore()
        {
            LookingForMore.Visibility = Visibility.Collapsed;
            if (core.MyGames.Item != MyGamesItem.Games || mainList == null || mainList.Items.Count == 0)
            {
                return;
            }

            var last = mainList.ItemContainerGenerator.ContainerFromIndex(mainList.Items.Count - 1) as FrameworkElement;
            if (last == null || !last.IsVisible)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PlaceShownLookingForMore));
                return;
            }

            var bottom = last.TranslatePoint(new Point(0, last.ActualHeight), mainList).Y;
            if (bottom > mainList.ActualHeight + 1)
            {
                return;
            }

            var grid = (Grid)LookingForMore.Parent;
            var top = last.TranslatePoint(new Point(0, last.ActualHeight), grid).Y - grid.RowDefinitions[Grid.GetRow(LookingForMore)].Offset;
            LookingForMore.Margin = new Thickness(3, top + TileGap, 0, 0);
            LookingForMore.Visibility = Visibility.Visible;
        }

        /// <summary>Places "Looking for more?" once more, if the grid's last tile is built by then.</summary>
        private void PlaceShownLookingForMore()
        {
            if (mainList?.ItemContainerGenerator.ContainerFromIndex(mainList.Items.Count - 1) is FrameworkElement last && last.IsVisible)
            {
                PlaceLookingForMore();
            }
        }

        /// <summary>
        /// Whether a pad button is one of Playnite's own on its view host, which the page hands no further: Y opens
        /// Playnite's keyboard to filter by name and RS its filters; LB/RB switch the user's filter presets, which only
        /// Full library shows as its tabs, so elsewhere they would swap the grid's games for a preset's. They are bound
        /// on the view host itself, which hears them before any handler of its own, so they are stopped on the way up.
        /// </summary>
        private bool IsPlaynitesOwn(ControllerInput? button) =>
            button == ControllerInput.Y || button == ControllerInput.RightStick
            || (!core.MyGames.ShowsFullLibrary && (button == ControllerInput.LeftShoulder || button == ControllerInput.RightShoulder));

        /// <summary>
        /// The pad button behind a key event, Playnite's keys for its own buttons counted as those buttons; but not in the
        /// name box, whose letters are typed: a key handled there would never become the text.
        /// </summary>
        private ControllerInput? PlayniteButtonOf(KeyEventArgs e) =>
            e.OriginalSource == NameBox ? PlayniteInput.PadButtonOf(e) : PlayniteInput.PlayniteButtonOf(e);

        /// <summary>Playnite's own buttons on the page, those the page's parts don't answer themselves, do nothing.</summary>
        private void OnPageKeyDown(object sender, KeyEventArgs e)
        {
            if (core.MyGames.IsShown && IsPlaynitesOwn(PlayniteButtonOf(e)))
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
                || (e.Key != Key.Left && e.Key != Key.Down && e.Key != Key.Up)
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
            else if (e.Key == Key.Up && core.MyGames.ShowsFullLibrary
                && row.Any(c => mainList.ItemContainerGenerator.IndexFromContainer(c) == 0))
            {
                // Up from Full library's first row: its Sort and Filter row, whose way back down is to this game.
                e.Handled = true;
                lastFocusedGridGame = mainList.ItemContainerGenerator.ItemFromContainer(game);
                FocusRow();
            }
        }

        /// <summary>
        /// Y on Full library's grid opens its name box, and on Games opens Search, rather than Playnite's own keyboard;
        /// on Play history's grid it, and Playnite's other own buttons, do nothing.
        /// </summary>
        private void OnMainListKeyDown(object sender, KeyEventArgs e)
        {
            var button = PlayniteButtonOf(e);
            if (!core.MyGames.IsShown || !IsPlaynitesOwn(button))
            {
                return;
            }

            e.Handled = true;
            if (button != ControllerInput.Y)
            {
                return;
            }

            if (core.MyGames.ShowsFullLibrary)
            {
                OpenNameBox();
            }
            else if (core.MyGames.Item == MyGamesItem.Games)
            {
                var game = ItemsControl.ContainerFromElement(mainList, e.OriginalSource as DependencyObject) is ListBoxItem tile
                    ? GameIdOf(mainList.ItemContainerGenerator.ItemFromContainer(tile))
                    : null;
                core.SearchFromMyGames(game);
            }
        }

        /// <summary>
        /// The id of a game in Playnite's main list; null for anything else. Its entries' type isn't in the SDK, so the
        /// id is read by name.
        /// </summary>
        private static Guid? GameIdOf(object entry) => entry?.GetType().GetProperty("Id")?.GetValue(entry) as Guid?;

        /// <summary>Focuses what in Full library's Sort and Filter row last had focus, else its Sort dropdown.</summary>
        private void FocusRow()
        {
            if (lastRowFocus == null || !lastRowFocus.IsVisible || !lastRowFocus.Focus())
            {
                SortButton.Focus();
            }
        }

        /// <summary>Down from Full library's tabs or row: the grid's game focus last left it from, else its first; with none, focus stays.</summary>
        private void FocusGridFromRow()
        {
            if (mainList == null || mainList.Items.Count == 0)
            {
                return;
            }

            var last = lastFocusedGridGame == null ? -1 : mainList.Items.IndexOf(lastFocusedGridGame);
            FocusGame(Math.Max(last, 0));
        }

        /// <summary>Up from Full library's row: the tabs, through the focus laid under them.</summary>
        private void FocusTabs()
        {
            TabsFocus.Focus();
        }

        /// <summary>
        /// On Full library's tabs, Left and Right switch to the tab beside, as LB and RB do; left from the first goes back
        /// to the nav, as from the grid's first column. Down goes to the Sort and Filter row.
        /// </summary>
        private void OnTabsPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                    e.Handled = true;
                    if (!SwitchTab(-1))
                    {
                        FullLibraryItem.Focus();
                    }

                    break;
                case Key.Right:
                    e.Handled = true;
                    SwitchTab(1);
                    break;
                case Key.Down:
                    e.Handled = true;
                    FocusRow();
                    break;
                case Key.Up:
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>A on the tabs: the tab focused is already the one showing. Y opens the name box.</summary>
        private void OnTabsKeyDown(object sender, KeyEventArgs e)
        {
            var button = PlayniteButtonOf(e);
            if (button == ControllerInput.A)
            {
                e.Handled = true;
            }
            else if (button == ControllerInput.Y)
            {
                e.Handled = true;
                OpenNameBox();
            }
        }

        /// <summary>
        /// Applies the tab <paramref name="step"/> along from the one showing, as Playnite's own tab does when chosen;
        /// false with none there.
        /// </summary>
        private bool SwitchTab(int step)
        {
            var tabs = libraryTabs?.Items.OfType<ButtonBase>().ToList();
            var at = tabs?.FindIndex(t => t is ToggleButton toggle && toggle.IsChecked == true) ?? -1;
            if (at < 0 || at + step < 0 || at + step >= tabs.Count)
            {
                return false;
            }

            var next = tabs[at + step];
            next.Command?.Execute(next.CommandParameter);
            return true;
        }

        private void OnLibraryBarFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            lastRowFocus = e.NewFocus as UIElement;
        }

        /// <summary>
        /// The D-pad along Full library's Sort and Filter row: up to the tabs, down to the grid, left from Sort back to
        /// the nav. The name box handles its own.
        /// </summary>
        private void OnLibraryBarPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource == NameBox)
            {
                return;
            }

            var fromSort = e.OriginalSource == SortButton;
            switch (e.Key)
            {
                case Key.Up:
                    e.Handled = true;
                    FocusTabs();
                    break;
                case Key.Down:
                    e.Handled = true;
                    FocusGridFromRow();
                    break;
                case Key.Left:
                    e.Handled = true;
                    if (fromSort)
                    {
                        FullLibraryItem.Focus();
                    }
                    else if (!NameBox.IsVisible || !NameBox.Focus())
                    {
                        SortButton.Focus();
                    }

                    break;
                case Key.Right:
                    e.Handled = true;
                    if (fromSort && (!NameBox.IsVisible || !NameBox.Focus()))
                    {
                        FilterButton.Focus();
                    }

                    break;
            }
        }

        /// <summary>The pad's A opens the Sort dropdown or, on Filter, the name box; Y opens the name box from anywhere on the row.</summary>
        private void OnLibraryBarKeyDown(object sender, KeyEventArgs e)
        {
            var button = PlayniteButtonOf(e);
            if (button == ControllerInput.A && e.OriginalSource == SortButton)
            {
                e.Handled = true;
                OpenSortList();
            }
            else if ((button == ControllerInput.A && e.OriginalSource == FilterButton) || button == ControllerInput.Y)
            {
                e.Handled = true;
                OpenNameBox();
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnLibraryBarKeyDown.
        private void OnSortClick(object sender, RoutedEventArgs e) => OpenSortList();

        private void OnFilterClick(object sender, RoutedEventArgs e) => OpenNameBox();

        /// <summary>Opens the Sort dropdown's list, its option the grid is in ticked and focused.</summary>
        private void OpenSortList()
        {
            if (!core.MyGames.ShowsFullLibrary)
            {
                return;
            }

            SortPopup.IsOpen = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!SortPopup.IsOpen)
                {
                    return;
                }

                SortOptions.UpdateLayout();
                var options = core.MyGames.SortOptions;
                Button current = null;
                for (var i = 0; i < options.Count; i++)
                {
                    var option = ButtonIn(SortOptions, i);
                    if (option != null)
                    {
                        var chosen = options[i] == core.MyGames.LibrarySort;
                        option.Tag = chosen ? "Selected" : null;
                        current = chosen || current == null ? option : current;
                    }
                }

                current?.Focus();
            }));
        }

        /// <summary>Closes the Sort dropdown's list, focus back on the dropdown.</summary>
        private void CloseSortList()
        {
            SortPopup.IsOpen = false;
            SortButton.Focus();
        }

        /// <summary>Up and down through the Sort dropdown's list, which holds focus while open; Escape closes it.</summary>
        private void OnSortOptionsPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var at = IndexIn(SortOptions, e.OriginalSource as DependencyObject);
            switch (e.Key)
            {
                case Key.Up:
                case Key.Down:
                    e.Handled = true;
                    ButtonIn(SortOptions, at + (e.Key == Key.Up ? -1 : 1))?.Focus();
                    break;
                case Key.Left:
                case Key.Right:
                    e.Handled = true;
                    break;
                case Key.Escape:
                    e.Handled = true;
                    CloseSortList();
                    break;
            }
        }

        /// <summary>
        /// The pad's A sorts the grid by the option focused, B closes the list; the list keeps every other button from
        /// the page behind it, as LB and RB switching its tab.
        /// </summary>
        private void OnSortOptionsKeyDown(object sender, KeyEventArgs e)
        {
            var button = PlayniteInput.PadButtonOf(e);
            if (button == null)
            {
                return;
            }

            e.Handled = true;
            if (button == ControllerInput.A && (e.OriginalSource as FrameworkElement)?.DataContext is LibrarySortOption option)
            {
                ChooseSort(option);
            }
            else if (button == ControllerInput.B)
            {
                CloseSortList();
            }
        }

        // The keyboard's Enter and the mouse.
        private void OnSortOptionClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is LibrarySortOption option)
            {
                ChooseSort(option);
            }
        }

        private void ChooseSort(LibrarySortOption option)
        {
            // The grid in its new order: down from the row starts again at its first game.
            core.SortFullLibrary(option);
            lastFocusedGridGame = null;
            CloseSortList();
        }

        /// <summary>Opens Full library's name box, from Filter or Y, and puts focus on it.</summary>
        private void OpenNameBox()
        {
            if (!core.MyGames.ShowsFullLibrary)
            {
                return;
            }

            SortPopup.IsOpen = false;
            nameBoxOpen = true;
            ShowNameBox();
            LibraryBar.UpdateLayout();
            NameBox.Focus();
        }

        /// <summary>The name box shows while open or while the grid is narrowed by name.</summary>
        private void ShowNameBox()
        {
            NameBox.Visibility = core.MyGames.ShowsFullLibrary && (nameBoxOpen || core.MyGames.HasNameFilter)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// The name box follows the core's name, as it is typed over the page or B clears it. Emptied as it is typed, the
        /// box stays open, for focus to come back to.
        /// </summary>
        private void ShowName()
        {
            if (NameBox.Text != core.MyGames.NameFilter)
            {
                NameBox.Text = core.MyGames.NameFilter;
                NameBox.CaretIndex = NameBox.Text.Length;
            }

            if (!core.MyGames.HasNameFilter && !NameBox.IsKeyboardFocused && !typingName)
            {
                nameBoxOpen = false;
            }

            ShowNameBox();
        }

        private void OnNameBoxFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            SuspendLetterKeys();
        }

        // Left empty, the box closes as focus goes; not for text entry over the page, which gives focus back to it.
        private void OnNameBoxLeft(object sender, KeyboardFocusChangedEventArgs e)
        {
            ResumeLetterKeys();
            if (NameBox.Text.Length == 0 && !typingName)
            {
                nameBoxOpen = false;
                ShowNameBox();
            }
        }

        /// <summary>
        /// The D-pad and keys in the name box: up to the tabs, down to the grid, left and right along the row, Enter
        /// types the name over the page, and Escape clears it.
        /// </summary>
        private void OnNameBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    e.Handled = true;
                    FocusTabs();
                    break;
                case Key.Down:
                    e.Handled = true;
                    FocusGridFromRow();
                    break;
                case Key.Enter:
                    e.Handled = true;
                    TypeName();
                    break;
                case Key.Left:
                    e.Handled = true;
                    SortButton.Focus();
                    break;
                case Key.Right:
                    e.Handled = true;
                    FilterButton.Focus();
                    break;
                case Key.Escape:
                    e.Handled = true;
                    CloseNameBox();
                    break;
            }
        }

        /// <summary>
        /// The pad in the name box: A (or Y) types the name over the page, the grid narrowing as it is typed; B clears the
        /// name and closes the box, the whole tab showing again. Heard though the box marked it handled; not while text
        /// entry is up, whose B cancels it.
        /// </summary>
        private void OnNameBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (core.TextEntry.IsShown)
            {
                return;
            }

            var button = PlayniteInput.PadButtonOf(e);
            if (button == ControllerInput.A || button == ControllerInput.Y)
            {
                e.Handled = true;
                TypeName();
            }
            else if (button == ControllerInput.B)
            {
                e.Handled = true;
                CloseNameBox();
            }
        }

        /// <summary>
        /// A letter typed on a keyboard in the name box, which only shows the name: text entry over the page, the letter
        /// going in after the name there.
        /// </summary>
        private void OnNameBoxTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]) || core.TextEntry.IsShown)
            {
                return;
            }

            TypeName();
            if (core.TextEntry.IsShown)
            {
                core.TypeText(e.Text);
            }
        }

        // The mouse: the box takes focus and the name is typed over the page, rather than a caret placed in it.
        private void OnNameBoxMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            NameBox.Focus();
            TypeName();
        }

        /// <summary>
        /// Text entry over the page for the name, the grid narrowing as it is typed; <see cref="OnTextEntryChanged"/>
        /// moves focus on as it goes.
        /// </summary>
        private void TypeName()
        {
            if (!core.MyGames.ShowsFullLibrary || core.TextEntry.IsShown)
            {
                return;
            }

            nameBeforeTyping = core.MyGames.NameFilter;
            typingName = true;
            core.TypeFullLibraryName();
            typingName = core.TextEntry.IsShown;
        }

        /// <summary>
        /// Text entry for the name gone, the overlay giving focus back to the name box: a new name kept goes on to the
        /// narrowed grid's first game; cancelled, the name kept as it was, or nothing found, focus stays on the box, open.
        /// </summary>
        private void OnTextEntryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TextEntryState.IsShown) || core.TextEntry.IsShown || !typingName)
            {
                return;
            }

            typingName = false;
            var before = nameBeforeTyping;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!core.MyGames.ShowsFullLibrary || !IsVisible)
                {
                    return;
                }

                lastFocusedGridGame = null;
                var kept = core.MyGames.HasNameFilter && core.MyGames.NameFilter != before;
                if (kept && mainList != null && mainList.Items.Count > 0)
                {
                    FocusGame(0);
                }
                else
                {
                    OpenNameBox();
                }
            }));
        }

        /// <summary>Clears the name, the whole tab showing again, and closes the box, focus on Filter, which opens it.</summary>
        private void CloseNameBox()
        {
            core.ClearFullLibraryName();
            nameBoxOpen = false;
            FilterButton.Focus();
            ShowNameBox();
        }

        private void SuspendLetterKeys()
        {
            if (viewHost == null || suspendedKeys.Count > 0)
            {
                return;
            }

            foreach (var key in viewHost.InputBindings.OfType<KeyBinding>().Where(IsTypingKey).ToList())
            {
                viewHost.InputBindings.Remove(key);
                suspendedKeys.Add(key);
            }
        }

        private void ResumeLetterKeys()
        {
            if (viewHost != null)
            {
                foreach (var key in suspendedKeys)
                {
                    viewHost.InputBindings.Add(key);
                }
            }

            suspendedKeys.Clear();
        }

        /// <summary>A key binding of Playnite's that a name typed would run: a letter, digit or space with no modifier.</summary>
        private static bool IsTypingKey(KeyBinding binding) =>
            binding.Modifiers == ModifierKeys.None
            && ((binding.Key >= Key.A && binding.Key <= Key.Z) || (binding.Key >= Key.D0 && binding.Key <= Key.D9) || binding.Key == Key.Space);

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
            // While text entry is up over the page, its B cancels it, and the page stays as it is.
            if (core.MyGames.IsShown && !core.TextEntry.IsShown && PlayniteInput.PadButtonOf(e) == ControllerInput.B)
            {
                // On Manage Queue, B goes back to Manage rather than leaving the page; on Full library narrowed by name,
                // back to its whole tab.
                var item = core.MyGames.Item;
                var narrowed = core.MyGames.HasNameFilter;
                core.LeaveMyGames();
                e.Handled = !core.MyGames.IsShown || core.MyGames.Item != item || narrowed != core.MyGames.HasNameFilter;
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
                    // Gone from the list, as a game uninstalled off Games, it is -1, so the first. Opened again by B from
                    // Search, which Y on Games opened, the game Y was pressed on.
                    var last = lastFocusedGridGame == null || mainList == null ? -1 : mainList.Items.IndexOf(lastFocusedGridGame);
                    if (last < 0 && lastFocusedGame != null && mainList != null)
                    {
                        last = mainList.Items.Cast<object>().ToList().FindIndex(g => GameIdOf(g) == lastFocusedGame);
                    }
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
                ScrollToRowOf(index);
                mainList.UpdateLayout();
            }

            if (mainList.ItemContainerGenerator.ContainerFromIndex(index) is UIElement game)
            {
                game.Focus();
            }
        }

        /// <summary>
        /// Scrolls the grid to the row of the game at <paramref name="index"/>, at the top as the first row rests. Not
        /// ScrollIntoView: Playnite's panel brings only a built tile into view, so it does nothing for a game whose tile
        /// isn't, as one far down the list after a sort. Five to a row, each 1/3.5 of the list's height tall.
        /// </summary>
        private void ScrollToRowOf(int index)
        {
            if (HomeRows.FindChild<VirtualizingPanel>(mainList) is IScrollInfo panel)
            {
                panel.SetVerticalOffset(index / 5 * (panel.ViewportHeight / 3.5));
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
