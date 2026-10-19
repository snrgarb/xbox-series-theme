using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Search, injected as a custom element over Home: the search box, the rows of what was found and the Console
    /// keyboard. It follows the core's <see cref="SearchState"/>, passes the keys pressed, the pad's shortcuts and text
    /// typed on a real keyboard to the core, and moves focus between its parts; it holds no search logic. The Dashboard
    /// hides Home by <see cref="State"/>, and opens the page from the top bar with <see cref="OpenSearchCommand"/>.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class SearchControl : PluginUserControl
    {
        private readonly ExtensionCore core;
        private FrameworkElement viewHost;
        private FrameworkElement gameDetails;

        /// <summary>Playnite's letter, digit and space shortcuts, lifted while the page is up so typing reaches the page.</summary>
        private readonly List<KeyBinding> suspendedKeys = new List<KeyBinding>();

        /// <summary>The tile each row last had focus on, so moving up or down into a row goes back to it.</summary>
        private readonly Dictionary<ListBox, int> lastIndex = new Dictionary<ListBox, int>();

        /// <summary>The game whose Game hub the page last opened, for focus to come back to.</summary>
        private Guid? chosenGame;

        internal SearchControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.Search;
            ConsoleKeys.Keyboard = core.Search.Keyboard;
            OpenSearchCommand = new RelayCommand(core.OpenSearch);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ShowText();
        }

        /// <summary>What the page shows, for the Dashboard to hide Home by.</summary>
        public SearchState State => core.Search;

        /// <summary>The top bar's Search: the page, empty, the keyboard up.</summary>
        public ICommand OpenSearchCommand { get; }

        /// <summary>The rows showing, top to bottom, with tiles in them.</summary>
        private List<ListBox> ShownRows =>
            new[] { RecentRow, GamesRow, AppsRow }
                .Where(row => ((FrameworkElement)row.Parent).Visibility == Visibility.Visible && row.Items.Count > 0)
                .ToList();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Search.PropertyChanged += OnStateChanged;
            core.Search.Keyboard.PropertyChanged += OnKeyboardChanged;
            ShowText();

            // Playnite's own pad B reaches whatever has focus on the page. Its Y, on Home, opens this page.
            viewHost = HomeRows.FindPart(this, "PART_ViewHost");
            if (viewHost != null)
            {
                PlayniteViewHost.TakeOver(viewHost, core);
                viewHost.KeyDown += OnViewHostKeyDown;
            }

            gameDetails = HomeRows.FindGameDetails(this);
            if (gameDetails != null)
            {
                gameDetails.IsVisibleChanged += OnGameDetailsVisibleChanged;
            }

            // Put up before the page was loaded, as when the Guide brings Playnite back from a Game hub.
            if (core.Search.IsShown)
            {
                SuspendLetterKeys();
                FocusSoon(chosenGame);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Search.PropertyChanged -= OnStateChanged;
            core.Search.Keyboard.PropertyChanged -= OnKeyboardChanged;
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
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SearchState.IsShown):
                    if (core.Search.IsShown)
                    {
                        // Opened afresh: the keyboard starts on its first letter, the rows on their first tiles.
                        SuspendLetterKeys();
                        ConsoleKeys.Reset();
                        lastIndex.Clear();
                        chosenGame = null;
                        FocusSoon();
                    }
                    else
                    {
                        ResumeLetterKeys();
                    }

                    break;
                case nameof(SearchState.IsKeyboardShown):
                    // Up: focus goes to its keys. Put away by Enter, Start or B: to the first tile found, else the search box.
                    FocusSoon();
                    break;
                case nameof(SearchState.RecentlyAdded):
                case nameof(SearchState.Games):
                case nameof(SearchState.Apps):
                    // New tiles replace the old, and with them the focused one: back to the first tile, or the next place.
                    // Not while a Game hub is open over the page, or focus would leave it.
                    lastIndex.Clear();
                    if (core.Search.IsShown && gameDetails?.IsVisible != true
                        && !ConsoleKeys.IsKeyboardFocusWithin && !SearchBox.IsKeyboardFocused)
                    {
                        FocusSoon();
                    }

                    break;
            }
        }

        private void OnKeyboardChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConsoleKeyboard.Text) || e.PropertyName == nameof(ConsoleKeyboard.Cursor))
            {
                ShowText();
            }
        }

        /// <summary>The search box: the text either side of the caret, or the prompt with nothing typed.</summary>
        private void ShowText()
        {
            var text = core.Search.Keyboard.Text ?? string.Empty;
            var cursor = Math.Max(0, Math.Min(text.Length, core.Search.Keyboard.Cursor));
            BeforeCaret.Text = text.Substring(0, cursor);
            AfterCaret.Text = text.Substring(cursor);
            Prompt.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // A key of the Console keyboard, by the pad's A, the keyboard's Enter, the mouse, or LT for the symbols key.
        private void OnKeyChosen(object sender, KeyboardKey key)
        {
            core.PressKey(key);
        }

        /// <summary>Up from the keyboard's top row: the last row found, else the search box.</summary>
        private void OnKeyboardLeftTop(object sender, EventArgs e)
        {
            var rows = ShownRows;
            if (rows.Count == 0 || !FocusTile(rows[rows.Count - 1]))
            {
                SearchBox.Focus();
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnSearchBoxKeyDown.
        private void OnSearchBoxClick(object sender, RoutedEventArgs e)
        {
            core.ShowSearchKeyboard();
        }

        private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                core.ShowSearchKeyboard();
            }
        }

        private void OnTileFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            var tile = (ListBoxItem)sender;
            if (ItemsControl.ItemsControlFromItemContainer(tile) is ListBox row)
            {
                lastIndex[row] = row.ItemContainerGenerator.IndexFromContainer(tile);
            }
        }

        // A game or an App is chosen by the pad's A, the keyboard's Enter or the mouse.
        private void OnTileKeyDown(object sender, KeyEventArgs e)
        {
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == ControllerInput.A || (pad == null && e.Key == Key.Enter))
            {
                e.Handled = true;
                Choose(sender);
            }
        }

        private void OnTileClick(object sender, MouseButtonEventArgs e)
        {
            Choose(sender);
        }

        /// <summary>A game opens its Game hub; an App starts.</summary>
        private void Choose(object tile)
        {
            switch (((FrameworkElement)tile).DataContext)
            {
                case HomeTile game:
                    chosenGame = game.GameId;
                    core.ChooseSearchGame(game.GameId);
                    break;
                case App app:
                    core.StartSearchApp(app.Id);
                    break;
            }
        }

        /// <summary>
        /// A real keyboard, for testing on the desktop: Space types a space and Backspace deletes before the cursor, the
        /// keyboard up or away, rather than pressing what has focus. The arrows are the D-pad, which Playnite sends as
        /// them, and move between the page's parts: the search box, each row and the keyboard, which moves along its own
        /// keys itself. Along a row the list moves itself, focus staying put at its ends.
        /// </summary>
        private void OnPagePreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!core.Search.IsShown)
            {
                return;
            }

            if (e.Key == Key.Space)
            {
                e.Handled = true;
                core.TypeText(" ");
                return;
            }

            if (e.Key == Key.Back)
            {
                e.Handled = true;
                core.DeleteText();
                return;
            }

            if ((e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down)
                || (e.OriginalSource is System.Windows.Media.Visual source && ConsoleKeys.IsAncestorOf(source)))
            {
                return;
            }

            var rows = ShownRows;
            if (e.OriginalSource == SearchBox)
            {
                e.Handled = true;
                if (e.Key == Key.Down && !(rows.Count > 0 && FocusTile(rows[0])) && core.Search.IsKeyboardShown)
                {
                    ConsoleKeys.FocusKey();
                }

                return;
            }

            if (!(e.OriginalSource is ListBoxItem tile) || !(ItemsControl.ItemsControlFromItemContainer(tile) is ListBox row))
            {
                return;
            }

            var index = row.ItemContainerGenerator.IndexFromContainer(tile);
            var at = rows.IndexOf(row);
            switch (e.Key)
            {
                case Key.Left:
                    e.Handled = index <= 0;
                    break;
                case Key.Right:
                    e.Handled = index >= row.Items.Count - 1;
                    break;
                case Key.Up:
                    e.Handled = true;
                    if (at <= 0 || !FocusTile(rows[at - 1]))
                    {
                        SearchBox.Focus();
                    }

                    break;
                case Key.Down:
                    e.Handled = true;
                    if (at >= 0 && at < rows.Count - 1)
                    {
                        FocusTile(rows[at + 1]);
                    }
                    else if (core.Search.IsKeyboardShown)
                    {
                        ConsoleKeys.FocusKey();
                    }

                    break;
            }
        }

        /// <summary>
        /// The pad's keyboard shortcuts, through the core: X backspace, Y space, LB and RB the cursor, the left stick's
        /// click Shift, Start Enter. Playnite's own LB/RB (its filter presets), X, Y (filter by name) and RS (its filters)
        /// act on the main list hidden behind the page, so on the page they do nothing.
        /// </summary>
        private void OnPageKeyDown(object sender, KeyEventArgs e)
        {
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == null || !core.Search.IsShown)
            {
                return;
            }

            switch (pad.Value)
            {
                case ControllerInput.X:
                case ControllerInput.Y:
                case ControllerInput.LeftShoulder:
                case ControllerInput.RightShoulder:
                case ControllerInput.LeftStick:
                case ControllerInput.Start:
                    if (PlayniteInput.ToButton(pad.Value) is ControllerButton button && core.PressKeyboardShortcut(button))
                    {
                        e.Handled = true;
                        return;
                    }

                    break;
            }

            if (pad == ControllerInput.X || pad == ControllerInput.Y || pad == ControllerInput.LeftShoulder
                || pad == ControllerInput.RightShoulder || pad == ControllerInput.RightStick)
            {
                e.Handled = true;
            }
        }

        /// <summary>Text typed on a real keyboard goes in at the cursor, as the Console keyboard types it. Space is typed by OnPagePreviewKeyDown.</summary>
        private void OnPageTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!core.Search.IsShown || string.IsNullOrWhiteSpace(e.Text) || e.Text.Any(char.IsControl))
            {
                return;
            }

            e.Handled = true;
            core.TypeText(e.Text);
        }

        /// <summary>
        /// B on the page, from Playnite: the keyboard goes away first, then the page, back to where it was opened from.
        /// Opened from the top bar, focus goes back to its Search button; from the Guide, Home's rows take it as they come
        /// back. Handled, the B goes no further.
        /// </summary>
        private void OnViewHostKeyDown(object sender, KeyEventArgs e)
        {
            if (!core.Search.IsShown || PlayniteInput.PadButtonOf(e) != ControllerInput.B)
            {
                return;
            }

            var from = core.Search.OpenedFrom;
            core.LeaveSearch();
            e.Handled = true;
            if (!core.Search.IsShown && from == SearchOrigin.TopBar)
            {
                FocusTopBarSoon();
            }
        }

        private void OnGameDetailsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Back from a Game hub the page opened: Playnite puts focus on its main list, so it goes back to the game's tile.
            if (!(bool)e.NewValue && core.Search.IsShown)
            {
                FocusSoon(chosenGame);
            }
        }

        /// <summary>
        /// Once the page is laid out: the tile of <paramref name="game"/> if it's still found; else the keyboard's key while
        /// the keyboard is up; else the first tile found, or the search box with none.
        /// </summary>
        private void FocusSoon(Guid? game = null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!core.Search.IsShown || !IsVisible)
                {
                    return;
                }

                if (game is Guid id && FocusGame(id))
                {
                    return;
                }

                if (core.Search.IsKeyboardShown)
                {
                    ConsoleKeys.FocusKey();
                    return;
                }

                var rows = ShownRows;
                if (rows.Count == 0 || !FocusTile(rows[0], 0))
                {
                    SearchBox.Focus();
                }
            }));
        }

        /// <summary>Focuses a game's tile in the first row that has it.</summary>
        private bool FocusGame(Guid game)
        {
            foreach (var row in ShownRows.Where(r => r != AppsRow))
            {
                var index = row.Items.Cast<object>().ToList().FindIndex(item => (item as HomeTile)?.GameId == game);
                if (index >= 0 && FocusTile(row, index))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Focuses a tile of a row, by default the one it last had focus on, scrolling to it first so it is built.</summary>
        private bool FocusTile(ListBox row, int? index = null)
        {
            if (row.Items.Count == 0)
            {
                return false;
            }

            var at = Math.Max(0, Math.Min(row.Items.Count - 1, index ?? (lastIndex.TryGetValue(row, out var last) ? last : 0)));
            row.ScrollIntoView(row.Items[at]);
            row.UpdateLayout();
            return row.ItemContainerGenerator.ContainerFromIndex(at) is ListBoxItem tile && tile.Focus();
        }

        /// <summary>
        /// Focuses the top bar's Search button once Home is back. After the Recent row has taken focus as it came back
        /// (at Loaded), so the button keeps it.
        /// </summary>
        private void FocusTopBarSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!core.Search.IsShown)
                {
                    HomeRows.FindPart(this, "XboxTopBarSearch")?.Focus();
                }
            }));
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

        /// <summary>A key binding of Playnite's that typing would run: a letter, digit or space with no modifier.</summary>
        private static bool IsTypingKey(KeyBinding binding) =>
            binding.Modifiers == ModifierKeys.None
            && ((binding.Key >= Key.A && binding.Key <= Key.Z) || (binding.Key >= Key.D0 && binding.Key <= Key.D9) || binding.Key == Key.Space);
    }
}
