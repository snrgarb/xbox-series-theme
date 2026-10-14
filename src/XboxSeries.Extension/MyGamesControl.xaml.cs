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
    /// My games &amp; apps, injected as a custom element over Home: the left nav, the header and Play history. It follows
    /// the core's <see cref="MyGamesState"/>, tells the core which nav item and which Play history game have focus, and
    /// takes B back to Home. It holds no page logic. The Dashboard lays Home and Playnite's main list out around it
    /// from <see cref="State"/>, and opens it from the top bar with <see cref="OpenGamesCommand"/>.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class MyGamesControl : PluginUserControl
    {
        private readonly ExtensionCore core;
        private FrameworkElement viewHost;
        private ListBox mainList;
        private FrameworkElement gameDetails;

        /// <summary>The Play history game that last had focus, to put focus back on after a rebuild or its Game hub.</summary>
        private Guid? lastFocusedGame;

        internal MyGamesControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.MyGames;
            OpenGamesCommand = new RelayCommand(() => core.OpenMyGames(MyGamesItem.Games));
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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
            }

            mainList = HomeRows.FindPart(this, "PART_ListGameItems") as ListBox;
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
                viewHost = null;
            }

            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged -= OnGameDetailsVisibleChanged;
                gameDetails = null;
            }

            mainList = null;
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
                    ShowItem();
                    if (History.IsKeyboardFocusWithin)
                    {
                        FocusContentSoon();
                    }

                    break;
            }
        }

        /// <summary>The header, the lit nav item, and Play history or the note that it's empty.</summary>
        private void ShowItem()
        {
            var history = core.MyGames.Item == MyGamesItem.PlayHistory;
            Header.Text = history ? "Play history" : "Games";
            GamesItem.Tag = history ? null : "Selected";
            PlayHistoryItem.Tag = history ? "Selected" : null;
            var empty = core.MyGames.PlayHistory.Count == 0;
            History.Visibility = history && !empty ? Visibility.Visible : Visibility.Collapsed;
            NoHistory.Visibility = history && empty ? Visibility.Visible : Visibility.Collapsed;
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
            core.OpenMyGames(sender == GamesItem ? MyGamesItem.Games : MyGamesItem.PlayHistory);
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

        /// <summary>Left goes back to the nav; up and down move through Play history.</summary>
        private void OnHistoryKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                e.Handled = true;
                PlayHistoryItem.Focus();
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                HomeRows.MoveFocus(e);
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

        // The keyboard's Enter and the mouse click the row; the pad's A reaches the core as a button instead.
        private void OnEntryClick(object sender, RoutedEventArgs e)
        {
            core.ChooseHomeTile(((PlayHistoryEntry)((FrameworkElement)sender).DataContext).Tile);
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

                if (core.MyGames.Item == MyGamesItem.Games)
                {
                    FocusFirstGame();
                    return;
                }

                var rows = EntryRows(Sections);
                var row = rows.FirstOrDefault(b => (b.DataContext as PlayHistoryEntry)?.Tile.GameId == lastFocusedGame) ?? rows.FirstOrDefault();
                (row ?? PlayHistoryItem).Focus();
            }));
        }

        private void FocusFirstGame()
        {
            if (mainList == null || mainList.Items.Count == 0)
            {
                GamesItem.Focus();
                return;
            }

            // Selected too: the Game hub, opened by A, shows the main list's selected game.
            mainList.SelectedIndex = 0;
            mainList.ScrollIntoView(mainList.Items[0]);
            mainList.UpdateLayout();
            if (mainList.ItemContainerGenerator.ContainerFromIndex(0) is UIElement first)
            {
                first.Focus();
            }
        }

        /// <summary>Play history's rows, top to bottom.</summary>
        private static List<Button> EntryRows(DependencyObject parent)
        {
            var rows = new List<Button>();
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button row)
                {
                    rows.Add(row);
                }
                else
                {
                    rows.AddRange(EntryRows(child));
                }
            }

            return rows;
        }
    }
}
