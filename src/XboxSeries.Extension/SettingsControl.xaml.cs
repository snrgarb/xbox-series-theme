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
    /// Settings, injected as a custom element over Home: the rail and the category showing beside it. It follows the
    /// core's <see cref="SettingsState"/>, tells the core which category has focus and which of Playnite's entries was
    /// chosen, and takes B back to where the page was opened from. It holds no page logic. The Dashboard hides Home by
    /// <see cref="State"/>, and opens the page from the top bar with <see cref="OpenSettingsCommand"/>.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class SettingsControl : PluginUserControl
    {
        private readonly ExtensionCore core;
        private FrameworkElement viewHost;

        /// <summary>The rail item of each category.</summary>
        private readonly Dictionary<SettingsCategory, Button> categoryItems;

        /// <summary>The category last shown, for which way along the rail focus moved to the next.</summary>
        private SettingsCategory shownCategory;

        /// <summary>The Now Playing source's choices its open list shows, in its order; empty while it is closed.</summary>
        private List<string> shownSources = new List<string>();

        internal SettingsControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.Settings;
            categoryItems = new Dictionary<SettingsCategory, Button>
            {
                [SettingsCategory.Personalisation] = PersonalisationItem,
                [SettingsCategory.Guide] = GuideItem,
                [SettingsCategory.Discord] = DiscordItem,
            };
#if DEBUG
            ShowDebugMenu();
#endif
            PersonalisationPane.Attach(core);
            PersonalisationPane.BackToRail += (s, e) => PersonalisationItem.Focus();
            PersonalisationPane.TitleChanged += (s, e) => ShowCategory();
            DiscordSteps.ItemsSource = core.Settings.Discord.Steps.Select((step, i) => new { Number = $"{i + 1}.", Text = step }).ToList();
            OpenSettingsCommand = new RelayCommand(core.OpenSettings);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            KeyDown += OnPageKeyDown;
            shownCategory = core.Settings.Category;
            ShowCategory();
        }

        /// <summary>What the page shows, for the Dashboard to hide Home by.</summary>
        public SettingsState State => core.Settings;

        /// <summary>The top bar's Settings: the page, on its first category.</summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>The rail, top to bottom, the divider aside.</summary>
        private Button[] RailItems => new[]
        {
            PersonalisationItem, GuideItem, DiscordItem, PlayniteSettingsItem, MorePlayniteOptionsItem,
#if DEBUG
            DebugItem,
#endif
        };

        /// <summary>
        /// Discord's buttons, as the pad moves between them: a row each, Type and Paste side by side for the Client ID and
        /// Secret, as Connect and Disconnect are.
        /// </summary>
        private Button[][] DiscordRows => new[]
        {
            new[] { TypeClientIdButton, PasteClientIdButton },
            new[] { TypeClientSecretButton, PasteClientSecretButton },
            new[] { ConnectButton, DisconnectButton },
        };

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Settings.PropertyChanged += OnStateChanged;

            // Put up before the page was loaded, as when the Guide brings Playnite back from a Game hub.
            shownCategory = core.Settings.Category;
            ShowCategory();
            if (core.Settings.IsShown)
            {
                FocusRailSoon();
            }

            // Playnite's own pad B reaches whatever has focus on the page.
            viewHost = HomeRows.FindPart(this, "PART_ViewHost");
            if (viewHost != null)
            {
                viewHost.KeyDown += OnViewHostKeyDown;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Settings.PropertyChanged -= OnStateChanged;

            // Gone mid-slide, the category is left where it settles.
            PaneFlyIn.Stop(CategoryHost);
            if (viewHost != null)
            {
                viewHost.KeyDown -= OnViewHostKeyDown;
                viewHost = null;
            }
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsState.Category):
                    ShowCategory();
                    FlyInCategory();

                    // Opened again from the Guide while up: focus follows the category back to the rail's first.
                    if (IsKeyboardFocusWithin && !categoryItems[core.Settings.Category].IsKeyboardFocused)
                    {
                        FocusRailSoon();
                    }

                    break;
                case nameof(SettingsState.IsShown):
                    // Opened or closed, the page shows its category at once, with no slide.
                    PaneFlyIn.Stop(CategoryHost);
                    shownCategory = core.Settings.Category;
                    ShowCategory();

                    // Opened from the top bar or the Guide: focus goes to the category showing, the rail's first.
                    if (core.Settings.IsShown)
                    {
                        FocusRailSoon();
                    }

                    break;
            }
        }

        /// <summary>
        /// As focus moves along the rail, the new category comes in from below for a move down it, or from above for a
        /// move up. Changed any other way, as the page opening, it is just there.
        /// </summary>
        private void FlyInCategory()
        {
            var items = RailItems;
            PaneFlyIn.Follow(
                ref shownCategory, core.Settings.Category, category => Array.IndexOf(items, categoryItems[category]),
                core.Settings.IsShown && Rail.IsKeyboardFocusWithin, CategoryHost);
        }

        /// <summary>The header and the lit rail item.</summary>
        private void ShowCategory()
        {
            var category = core.Settings.Category;

            // Personalisation's pages each have their own header; left or closed, it's back on its boxes for next time.
            if (category != SettingsCategory.Personalisation || !core.Settings.IsShown)
            {
                PersonalisationPane.Reset();
            }

            Header.Text = category == SettingsCategory.Personalisation ? PersonalisationPane.Title : category.ToString();

            // Left or closed, the Guide's dropdown is closed for next time.
            if (category != SettingsCategory.Guide || !core.Settings.IsShown)
            {
                CloseNowPlayingSources();
            }

            PersonalisationPane.Visibility = category == SettingsCategory.Personalisation ? Visibility.Visible : Visibility.Collapsed;
            GuidePane.Visibility = category == SettingsCategory.Guide ? Visibility.Visible : Visibility.Collapsed;
            DiscordPane.Visibility = category == SettingsCategory.Discord ? Visibility.Visible : Visibility.Collapsed;
#if DEBUG
            // As Personalisation's, the Debug menu's pages each have their own header and start again on its boxes.
            if (category != SettingsCategory.Debug || !core.Settings.IsShown)
            {
                debugPane.Reset();
            }

            if (category == SettingsCategory.Debug)
            {
                Header.Text = debugPane.Title;
            }

            debugPane.Visibility = category == SettingsCategory.Debug ? Visibility.Visible : Visibility.Collapsed;
#endif
            foreach (var item in categoryItems)
            {
                item.Value.Tag = item.Key == category ? "Selected" : null;
            }
        }

        private void OnCategoryFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            core.FocusSettingsCategory(categoryItems.First(item => item.Value == sender).Key);
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnRailKeyDown.
        private void OnPlayniteEntryClick(object sender, RoutedEventArgs e)
        {
            OpenPlayniteEntry(sender);
        }

        private void OpenPlayniteEntry(object item)
        {
            if (item == PlayniteSettingsItem)
            {
                core.OpenPlayniteSettings();
            }
            else if (item == MorePlayniteOptionsItem)
            {
                core.OpenMorePlayniteOptions();
            }
        }

        /// <summary>
        /// Up and down move through the rail, over the divider, and stay put at its ends: Home is hidden behind the page.
        /// The pad's A opens Playnite's entries. Right or A on Personalisation, Guide or Discord moves into its options;
        /// Left and Right go nowhere else: Playnite's entries have no options to move into.
        /// </summary>
        private void OnRailKeyDown(object sender, KeyEventArgs e)
        {
            var items = RailItems;
            var here = Array.IndexOf(items, e.OriginalSource as Button);
            var pad = PlayniteInput.PadButtonOf(e);
            if (e.OriginalSource == PersonalisationItem && (pad == ControllerInput.A || e.Key == Key.Right))
            {
                e.Handled = true;
                PersonalisationPane.FocusFirst();
            }
            else if (e.OriginalSource == GuideItem && (pad == ControllerInput.A || e.Key == Key.Right))
            {
                e.Handled = true;
                NowPlayingSourceBox.Focus();
            }
            else if (e.OriginalSource == DiscordItem && (pad == ControllerInput.A || e.Key == Key.Right))
            {
                e.Handled = true;
                TypeClientIdButton.Focus();
            }
#if DEBUG
            else if (e.OriginalSource == DebugItem && (pad == ControllerInput.A || e.Key == Key.Right))
            {
                e.Handled = true;
                debugPane.FocusFirst();
            }
#endif
            else if (pad == ControllerInput.A)
            {
                e.Handled = true;
                OpenPlayniteEntry(e.OriginalSource);
            }
            else if ((e.Key == Key.Up || e.Key == Key.Down) && here >= 0)
            {
                e.Handled = true;
                items[Math.Max(0, Math.Min(items.Length - 1, here + (e.Key == Key.Up ? -1 : 1)))].Focus();
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// The pad in the Guide's options. On the Now Playing source's box: A opens its list, Left or B goes back to the
        /// rail's Guide. In the open list: up and down move through the choices, staying put at its ends, A chooses the
        /// one focused and B closes it unchanged, focus going back to the box either way.
        /// </summary>
        private void OnGuidePaneKeyDown(object sender, KeyEventArgs e)
        {
            var pad = PlayniteInput.PadButtonOf(e);
            if (e.OriginalSource == NowPlayingSourceBox)
            {
                if (pad == ControllerInput.A)
                {
                    e.Handled = true;
                    OpenNowPlayingSources();
                }
                else if (pad == ControllerInput.B || e.Key == Key.Left)
                {
                    // Handled here, before the page's B takes it back to where Settings was opened from.
                    e.Handled = true;
                    GuideItem.Focus();
                }
                else if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Right)
                {
                    e.Handled = true;
                }

                return;
            }

            var options = NowPlayingSourceOptions.Children.OfType<Button>().ToList();
            var here = options.IndexOf(e.OriginalSource as Button);
            if (here < 0)
            {
                return;
            }

            if (pad == ControllerInput.A)
            {
                e.Handled = true;
                ChooseNowPlayingSource(here);
            }
            else if (pad == ControllerInput.B)
            {
                // Handled here, before the page's B takes it back to where Settings was opened from.
                e.Handled = true;
                CloseNowPlayingSources();
                NowPlayingSourceBox.Focus();
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                options[Math.Max(0, Math.Min(options.Count - 1, here + (e.Key == Key.Up ? -1 : 1)))].Focus();
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnGuidePaneKeyDown.
        private void OnNowPlayingSourceBoxClick(object sender, RoutedEventArgs e)
        {
            if (NowPlayingSourceList.Visibility == Visibility.Visible)
            {
                CloseNowPlayingSources();
            }
            else
            {
                OpenNowPlayingSources();
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnGuidePaneKeyDown.
        private void OnNowPlayingSourceOptionClick(object sender, RoutedEventArgs e)
        {
            ChooseNowPlayingSource(NowPlayingSourceOptions.Children.IndexOf((UIElement)sender));
        }

        /// <summary>
        /// Opens the Now Playing source's list under its box, a row for each of the core's choices as they are now, the
        /// one chosen ticked and focused.
        /// </summary>
        private void OpenNowPlayingSources()
        {
            var state = core.Settings.Guide;
            shownSources = state.NowPlayingSources.ToList();
            NowPlayingSourceOptions.Children.Clear();
            Button chosen = null;
            foreach (var source in shownSources)
            {
                var option = new Button
                {
                    Style = (Style)FindResource("DropOption"),
                    Content = new TextBlock { Text = source, Style = (Style)FindResource("Text") },
                    Tag = source == state.NowPlayingSource ? "Selected" : null,
                };
                option.Click += OnNowPlayingSourceOptionClick;
                NowPlayingSourceOptions.Children.Add(option);
                if (source == state.NowPlayingSource)
                {
                    chosen = option;
                }
            }

            NowPlayingSourceBox.Tag = "Open";
            NowPlayingSourceList.Visibility = Visibility.Visible;
            (chosen ?? NowPlayingSourceOptions.Children.OfType<Button>().FirstOrDefault())?.Focus();
        }

        /// <summary>The choice at <paramref name="index"/> in the open list: the core takes and saves it, and the list closes onto its box.</summary>
        private void ChooseNowPlayingSource(int index)
        {
            if (index < 0 || index >= shownSources.Count)
            {
                return;
            }

            core.SetNowPlayingSource(shownSources[index]);
            CloseNowPlayingSources();
            NowPlayingSourceBox.Focus();
        }

        /// <summary>Closes the Now Playing source's list, if open, leaving focus to the caller.</summary>
        private void CloseNowPlayingSources()
        {
            NowPlayingSourceBox.Tag = null;
            NowPlayingSourceList.Visibility = Visibility.Collapsed;
            NowPlayingSourceOptions.Children.Clear();
            shownSources = new List<string>();
        }

        /// <summary>
        /// The pad in Discord's options: up and down between its rows, left and right along Type and Paste or Connect and
        /// Disconnect, Left from the first column or B back to the rail's Discord, and A presses the button focused.
        /// </summary>
        private void OnDiscordPaneKeyDown(object sender, KeyEventArgs e)
        {
            // Text entry over the page has the pad: B there cancels it, never goes back to the rail.
            if (core.TextEntry.IsShown)
            {
                return;
            }

            var rows = DiscordRows;
            var row = Array.FindIndex(rows, r => r.Contains(e.OriginalSource));
            if (row < 0)
            {
                return;
            }

            var column = Array.IndexOf(rows[row], e.OriginalSource);
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == ControllerInput.A)
            {
                e.Handled = true;
                PressDiscordButton(e.OriginalSource);
            }
            else if (pad == ControllerInput.B || (e.Key == Key.Left && column == 0))
            {
                // Handled here, before the page's B takes it back to where Settings was opened from.
                e.Handled = true;
                DiscordItem.Focus();
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
                rows[row][Math.Max(0, Math.Min(rows[row].Length - 1, column + (e.Key == Key.Left ? -1 : 1)))].Focus();
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                var next = rows[Math.Max(0, Math.Min(rows.Length - 1, row + (e.Key == Key.Up ? -1 : 1)))];
                next[Math.Min(column, next.Length - 1)].Focus();
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnDiscordPaneKeyDown.
        private void OnDiscordButtonClick(object sender, RoutedEventArgs e)
        {
            PressDiscordButton(sender);
        }

        /// <summary>
        /// Type opens text entry over the page, saving what is kept, the overlay giving focus back to Type as it goes;
        /// Paste saves the clipboard's text at once.
        /// </summary>
        private void PressDiscordButton(object button)
        {
            if (button == TypeClientIdButton)
            {
                core.TypeDiscordClientId();
            }
            else if (button == TypeClientSecretButton)
            {
                core.TypeDiscordClientSecret();
            }
            else if (button == PasteClientIdButton || button == PasteClientSecretButton)
            {
                // A clipboard another program holds open pastes nothing, leaving the field; an empty one forgets it.
                var text = ClipboardText();
                if (text == null)
                {
                    return;
                }

                if (button == PasteClientIdButton)
                {
                    core.SetDiscordClientId(text);
                }
                else
                {
                    core.SetDiscordClientSecret(text);
                }
            }
            else if (button == ConnectButton)
            {
                core.ConnectDiscord();
            }
            else if (button == DisconnectButton)
            {
                core.DisconnectDiscord();
            }
        }

#if DEBUG
        /// <summary>Debug builds only: the Debug menu, beside the rail while it's the category.</summary>
        private DebugPane debugPane;

        /// <summary>Debug builds only: the Steam adapter whose install trials the Debug menu runs.</summary>
        internal SteamDownloads SteamTrials
        {
            set => debugPane.SteamTrials = value;
        }

        /// <summary>
        /// Debug builds only: the second divider and the Debug menu under Playnite's entries, a category like the others,
        /// and its pane in the options' row. In a Release build the divider and item stay collapsed in the XAML.
        /// </summary>
        private void ShowDebugMenu()
        {
            DebugDivider.Visibility = Visibility.Visible;
            DebugItem.Visibility = Visibility.Visible;
            DebugItem.GotKeyboardFocus += OnCategoryFocused;
            categoryItems[SettingsCategory.Debug] = DebugItem;
            debugPane = new DebugPane(core) { Visibility = Visibility.Collapsed };
            Grid.SetRow(debugPane, 1);
            CategoryHost.Children.Add(debugPane);
            debugPane.BackToRail += (s, e) => DebugItem.Focus();
            debugPane.TitleChanged += (s, e) => ShowCategory();
        }
#endif

        /// <summary>The Windows clipboard's text, empty for none, or null when another program holds it open.</summary>
        private static string ClipboardText()
        {
            try
            {
                return Clipboard.GetText();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return null;
            }
        }

        /// <summary>
        /// Playnite's LB/RB switch the user's filter presets, Y opens its keyboard to filter by name and RS its filters,
        /// all on the main list Home hides behind the page; here they do nothing. They are bound on the view host itself,
        /// which hears them before any handler of its own, so they are stopped on the way up to it.
        /// </summary>
        private void OnPageKeyDown(object sender, KeyEventArgs e)
        {
            var button = PlayniteInput.PlayniteButtonOf(e);
            if (core.Settings.IsShown
                && (button == ControllerInput.LeftShoulder || button == ControllerInput.RightShoulder
                    || button == ControllerInput.Y || button == ControllerInput.RightStick))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// B on the page, from Playnite: back to where it was opened from. Opened from the top bar, focus goes back to its
        /// Settings button; from the Guide, Home's rows take it as they come back. From the tile menu's Customise Home, Home's
        /// rows take it too, on the tile they last had focus on, the one whose menu opened the page; from Customise games
        /// &amp; apps, My games &amp; apps comes back in the page's place and puts it on that tile
        /// (<see cref="MyGamesState.ReturnsToGame"/>). Handled, the B goes no further, so My games &amp; apps, back
        /// already, doesn't take it too.
        /// </summary>
        private void OnViewHostKeyDown(object sender, KeyEventArgs e)
        {
            // While text entry is up over the page, its B cancels it, and the page stays.
            if (!core.Settings.IsShown || core.TextEntry.IsShown || PlayniteInput.PadButtonOf(e) != ControllerInput.B)
            {
                return;
            }

            var from = core.Settings.OpenedFrom;
            core.LeaveSettings();
            e.Handled = !core.Settings.IsShown;
            if (e.Handled && from == SettingsOrigin.TopBar)
            {
                FocusTopBarSoon();
            }
        }

        /// <summary>Focuses the category showing once the page is laid out.</summary>
        private void FocusRailSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (core.Settings.IsShown && IsVisible)
                {
                    categoryItems[core.Settings.Category].Focus();
                }
            }));
        }

        /// <summary>
        /// Focuses the top bar's Settings button once Home is back. After the Recent row has taken focus as it came back
        /// (at Loaded), so the button keeps it.
        /// </summary>
        private void FocusTopBarSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!core.Settings.IsShown)
                {
                    HomeRows.FindPart(this, "XboxTopBarSettings")?.Focus();
                }
            }));
        }
    }
}
