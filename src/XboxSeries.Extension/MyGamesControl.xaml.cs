using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    /// My games &amp; apps, injected as a custom element over Home: the left nav, the header and Play history. It follows
    /// the core's <see cref="MyGamesState"/>, tells the core which nav item and which Play history game have focus, and
    /// takes B back to Home. It holds no page logic. The Dashboard lays Home and Playnite's main list out around it
    /// from <see cref="State"/>, and opens it from the top bar with <see cref="OpenGamesCommand"/>.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class MyGamesControl : PluginUserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// The console's grid: square tiles, scrolling down, three rows to the screen, which in the Dashboard's place for
        /// it makes five columns. Playnite's own panel, laid out by its own settings (Home's single row) unless replaced.
        /// </summary>
        private const string GameGridPanel =
            "<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "<FullscreenTilePanel UseHorizontalLayout=\"False\" Rows=\"3\" ItemAspectRatio=\"1:1\"" +
            " SmoothScrollEnabled=\"{Settings Fullscreen.SmoothScrolling}\" />" +
            "</ItemsPanelTemplate>";

        private readonly ExtensionCore core;
        private FrameworkElement viewHost;
        private ListBox mainList;
        private FrameworkElement gameDetails;

        /// <summary>The Play history game that last had focus, to put focus back on after a rebuild or its Game hub.</summary>
        private Guid? lastFocusedGame;

        /// <summary>The Games grid game focus left for "See full library", to go back up to.</summary>
        private int gameAboveFooter;

        internal MyGamesControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.MyGames;
            OpenGamesCommand = new RelayCommand(() => core.OpenMyGames(MyGamesItem.Games));
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ShowHistory();
            ShowItem();
        }

        /// <summary>What the page shows, for the Dashboard to hide Home and place Playnite's main list by.</summary>
        public MyGamesState State => core.MyGames;

        /// <summary>The top bar's My games &amp; apps: the page, on Games.</summary>
        public ICommand OpenGamesCommand { get; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged += OnStateChanged;

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
                mainList.PreviewKeyDown += OnMainListPreviewKeyDown;
                LibraryCount.SetBinding(TextBlock.TextProperty, new Binding("Items.Count") { Source = mainList, StringFormat = "{0} games" });
            }

            gameDetails = HomeRows.FindPart(this, "PART_ElemGameDetails");
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged -= OnStateChanged;
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

            if (mainList != null)
            {
                mainList.PreviewKeyDown -= OnMainListPreviewKeyDown;
                BindingOperations.ClearBinding(LibraryCount, TextBlock.TextProperty);
                mainList = null;
            }
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MyGamesState.IsShown):
                case nameof(MyGamesState.Item):
                    ShowItem();

                    // Opened from Home, the top bar or the Guide: focus goes to the first tile. Moving through the nav keeps it there.
                    if (core.MyGames.IsShown && !Nav.IsKeyboardFocusWithin)
                    {
                        lastFocusedGame = null;
                        FocusContentSoon();
                    }

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
            }
        }

        /// <summary>
        /// The header and its count, the lit nav item, Games' way to Full library, and Play history or the note that
        /// it's empty.
        /// </summary>
        private void ShowItem()
        {
            var item = core.MyGames.Item;
            var games = item == MyGamesItem.Games;
            var history = item == MyGamesItem.PlayHistory;
            var library = item == MyGamesItem.FullLibrary;
            Header.Text = history ? "Play history" : library ? "Full library" : "Games";
            GamesItem.Tag = games ? "Selected" : null;
            PlayHistoryItem.Tag = history ? "Selected" : null;
            FullLibraryItem.Tag = library ? "Selected" : null;
            Count.Visibility = games ? Visibility.Visible : Visibility.Collapsed;
            LibraryCount.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
            LookingForMore.Visibility = games ? Visibility.Visible : Visibility.Collapsed;
            var empty = core.MyGames.PlayHistory.Count == 0;
            History.Visibility = history && !empty ? Visibility.Visible : Visibility.Collapsed;
            NoHistory.Visibility = history && empty ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>The nav item of what the page shows.</summary>
        private Button NavItemOf(MyGamesItem item) =>
            item == MyGamesItem.PlayHistory ? PlayHistoryItem : item == MyGamesItem.FullLibrary ? FullLibraryItem : GamesItem;

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
            // The nav item shows Full library as it takes focus; focus then goes on to its first game.
            FullLibraryItem.Focus();
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
                core.LeaveMyGames();
                e.Handled = !core.MyGames.IsShown;
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub opened from Play history. Playnite puts focus back on Games' main list itself.
            if (!(bool)e.NewValue && core.MyGames.IsShown && core.MyGames.Item == MyGamesItem.PlayHistory)
            {
                FocusContentSoon();
            }
        }

        private void OnNavFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            core.OpenMyGames(Enum.GetValues(typeof(MyGamesItem)).Cast<MyGamesItem>().First(item => NavItemOf(item) == sender));
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnNavKeyDown.
        private void OnNavClick(object sender, RoutedEventArgs e)
        {
            FocusContentSoon();
        }

        /// <summary>Right, or the pad's A, goes into the item's games; up and down move through the nav.</summary>
        private void OnNavKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right || PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
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
        /// Focuses the item's first tile once it is laid out: Games' first game in Playnite's main list, or the Play
        /// history game that last had focus, else its first. With none, focus stays on the nav.
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

                if (core.MyGames.ShowsGameGrid)
                {
                    FocusGame(0);
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

        /// <summary>Focuses and selects the grid's game at <paramref name="index"/>; with no games, the nav item.</summary>
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
            mainList.ScrollIntoView(mainList.Items[index]);
            mainList.UpdateLayout();
            if (mainList.ItemContainerGenerator.ContainerFromIndex(index) is UIElement game)
            {
                game.Focus();
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
