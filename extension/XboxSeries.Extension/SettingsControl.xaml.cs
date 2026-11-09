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
    /// core's <see cref="SettingsState"/>, tells the core which category has focus and when More Playnite options
    /// was chosen, and takes B back to where the page was opened from. It holds no page logic. The Dashboard hides Home by
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

        /// <summary>Theme settings' page last shown, for the box focus goes back to as it's left.</summary>
        private ThemeSettingsPage shownPage;

        /// <summary>The gap from a dropdown's box down to its list, and the least room its list leaves above the page's foot.</summary>
        private const double DropListGap = 8, DropListFoot = 32;

        /// <summary>How far past a list's row is kept in view as it is focused: its focus ring's 8, and a little more.</summary>
        private const double DropRingReach = 10;

        /// <summary>The shortest a dropdown's list is cut to, three rows, however little room is left under its box.</summary>
        private const double DropListMinHeight = 3 * 62 + 16;

        /// <summary>The Guide page's dropdown, the Now Playing source, and the Idle page's, Dim after then Show things after.</summary>
        private readonly Dropdown nowPlayingSource, dimAfter, showThingsAfter;

        /// <summary>The three dropdowns, in that order.</summary>
        private readonly Dropdown[] dropdowns;

        internal SettingsControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.Settings;
            SettingsLog.FollowFocus(this);
            var guide = core.Settings.Guide;
            nowPlayingSource = new Dropdown(NowPlayingSourceBox, NowPlayingSourceList, NowPlayingSourceOptions,
                () => guide.NowPlayingSources.Select(source =>
                    new DropChoice(source, source == guide.NowPlayingSource, () => core.SetNowPlayingSource(source))));
            dimAfter = new Dropdown(DimAfterBox, DimAfterList, DimAfterOptions,
                () => StepChoices(IdleState.Steps, core.Idle.DimAfter, core.SetDimAfter));
            showThingsAfter = new Dropdown(ShowThingsAfterBox, ShowThingsAfterList, ShowThingsAfterOptions,
                () => StepChoices(core.Idle.ShowThingsAfterSteps, core.Idle.ShowThingsAfter, core.SetShowThingsAfter));
            dropdowns = new[] { nowPlayingSource, dimAfter, showThingsAfter };
            foreach (var dropdown in dropdowns)
            {
                dropdown.Options.KeyDown += (s, e) => OnDropListKeyDown(dropdown, e);
            }

            ShowIdle();
            categoryItems = new Dictionary<SettingsCategory, Button>
            {
                [SettingsCategory.ThemeSettings] = ThemeSettingsItem,
                [SettingsCategory.PlayniteSettings] = PlayniteSettingsItem,
            };
#if DEBUG
            ShowDebugMenu();
#endif
            ThemeSettingsPane.Attach(core);
            ThemeSettingsPane.BackToRail += (s, e) => ThemeSettingsItem.Focus();
            PersonalisationPane.Attach(core);
            PersonalisationPane.BackToThemeSettings += (s, e) => core.LeaveThemePage();
            PersonalisationPane.TitleChanged += (s, e) => ShowCategory();
            PlaynitePane.Attach(core);
            PlaynitePane.BackToRail += (s, e) => PlayniteSettingsItem.Focus();
            DiscordSteps.ItemsSource = core.Settings.Discord.Steps.Select((step, i) => new { Number = $"{i + 1}.", Text = step }).ToList();
            OpenSettingsCommand = new RelayCommand(core.OpenSettings);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            KeyDown += OnPageKeyDown;
            shownCategory = core.Settings.Category;
            shownPage = core.Settings.ThemePage;
            ShowCategory();
        }

        /// <summary>What the page shows, for the Dashboard to hide Home by.</summary>
        public SettingsState State => core.Settings;

        /// <summary>The top bar's Settings: the page, on its first category.</summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>The rail, top to bottom, the dividers aside.</summary>
        private Button[] RailItems => new[]
        {
            ThemeSettingsItem, PlayniteSettingsItem, MorePlayniteOptionsItem,
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
            core.Settings.Opened += OnOpened;
            core.Idle.PropertyChanged += OnIdleChanged;
            ShowIdle();

            // Put up before the page was loaded, as when the Guide brings Playnite back from a Game hub.
            shownCategory = core.Settings.Category;
            shownPage = core.Settings.ThemePage;
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
            core.Settings.Opened -= OnOpened;
            core.Idle.PropertyChanged -= OnIdleChanged;

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
                    break;
                case nameof(SettingsState.ThemePage):
                    ShowThemePage();
                    break;
                case nameof(SettingsState.IsShown):
                    // Opened or closed, the page shows its category at once, with no slide.
                    PaneFlyIn.Stop(CategoryHost);
                    shownCategory = core.Settings.Category;
                    shownPage = core.Settings.ThemePage;
                    ShowCategory();
                    break;
            }
        }

        /// <summary>
        /// Opened, also again from the Guide while up: from the top bar or the Guide, focus goes to the category showing,
        /// the rail's first; from a tile under Home's Recent row or the Tile menu's Customise, into the page it came for.
        /// </summary>
        private void OnOpened(object sender, EventArgs e) => FocusRailSoon();

        /// <summary>
        /// Theme settings' page changed: shown in the boxes' place at once, as Personalisation's own pages are, with no
        /// slide. Opened from a box, focus goes to the page's first option; left from the page, back to the box that
        /// opened it. Changed with focus elsewhere, as the rail's Theme settings taking it back to its boxes, focus stays.
        /// </summary>
        private void ShowThemePage()
        {
            var left = shownPage;
            shownPage = core.Settings.ThemePage;
            var fromBoxes = ThemeSettingsPane.IsKeyboardFocusWithin;
            var fromPage = PaneOf(left)?.IsKeyboardFocusWithin == true;
            ShowCategory();
            if (!core.Settings.IsShown)
            {
                return;
            }

            if (shownPage != ThemeSettingsPage.None && fromBoxes)
            {
                FocusSoon(() => FocusThemePage(shownPage));
            }
            else if (shownPage == ThemeSettingsPage.None && fromPage)
            {
                FocusSoon(() => ThemeSettingsPane.FocusBox(left));
            }
        }

        /// <summary>The pane showing a page of Theme settings; none for its boxes.</summary>
        private FrameworkElement PaneOf(ThemeSettingsPage page)
        {
            switch (page)
            {
                case ThemeSettingsPage.Personalisation: return PersonalisationPane;
                case ThemeSettingsPage.Idle: return IdlePane;
                case ThemeSettingsPage.Guide: return GuidePane;
                case ThemeSettingsPage.Discord: return DiscordPane;
                default: return null;
            }
        }

        /// <summary>
        /// A page of Theme settings' first option: Personalisation's first box, Dim after's box, the Now Playing source's
        /// box, or Discord's Type for the Client ID; for none, the first of Theme settings' own boxes.
        /// </summary>
        private void FocusThemePage(ThemeSettingsPage page)
        {
            switch (page)
            {
                case ThemeSettingsPage.Personalisation:
                    PersonalisationPane.FocusFirst();
                    break;
                case ThemeSettingsPage.Idle:
                    DimAfterBox.Focus();
                    break;
                case ThemeSettingsPage.Guide:
                    NowPlayingSourceBox.Focus();
                    break;
                case ThemeSettingsPage.Discord:
                    TypeClientIdButton.Focus();
                    break;
                default:
                    ThemeSettingsPane.FocusFirst();
                    break;
            }
        }

        /// <summary>Focuses once the pane just shown is laid out, while the page is still up.</summary>
        private void FocusSoon(Action focus)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (core.Settings.IsShown && IsVisible)
                {
                    UpdateLayout();
                    focus();
                }
            }));
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

        /// <summary>The header, the pane showing, and the lit rail item.</summary>
        private void ShowCategory()
        {
            var category = core.Settings.Category;
            var themeSettings = category == SettingsCategory.ThemeSettings;
            var page = themeSettings ? core.Settings.ThemePage : ThemeSettingsPage.None;

            // Personalisation's pages each have their own header; left or closed, it's back on its boxes for next time.
            if (page != ThemeSettingsPage.Personalisation || !core.Settings.IsShown)
            {
                PersonalisationPane.Reset();
            }

            Header.Text = page == ThemeSettingsPage.Personalisation ? PersonalisationPane.Title
                : page == ThemeSettingsPage.Idle ? "Idle"
                : page == ThemeSettingsPage.Guide ? "Guide"
                : page == ThemeSettingsPage.Discord ? "Discord"
                : themeSettings ? "Theme settings"
                : category == SettingsCategory.PlayniteSettings ? "Playnite settings"
                : category.ToString();

            // Left or closed, a page's dropdowns are closed for next time.
            if (page != ThemeSettingsPage.Guide || !core.Settings.IsShown)
            {
                CloseDropdown(nowPlayingSource);
            }

            if (page != ThemeSettingsPage.Idle || !core.Settings.IsShown)
            {
                CloseDropdown(dimAfter);
                CloseDropdown(showThingsAfter);
            }

            ThemeSettingsPane.Visibility = themeSettings && page == ThemeSettingsPage.None ? Visibility.Visible : Visibility.Collapsed;
            PersonalisationPane.Visibility = page == ThemeSettingsPage.Personalisation ? Visibility.Visible : Visibility.Collapsed;
            IdlePane.Visibility = page == ThemeSettingsPage.Idle ? Visibility.Visible : Visibility.Collapsed;
            GuidePane.Visibility = page == ThemeSettingsPage.Guide ? Visibility.Visible : Visibility.Collapsed;
            DiscordPane.Visibility = page == ThemeSettingsPage.Discord ? Visibility.Visible : Visibility.Collapsed;
            PlaynitePane.Visibility = category == SettingsCategory.PlayniteSettings ? Visibility.Visible : Visibility.Collapsed;
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
            if (item == MorePlayniteOptionsItem)
            {
                core.OpenMorePlayniteOptions();
            }
        }

        /// <summary>
        /// Up and down move through the rail, over its dividers, and stay put at its ends: Home is hidden behind the page.
        /// The pad's A opens More Playnite options. Right or A on Theme settings or Playnite settings moves into its boxes;
        /// Left and Right go nowhere else: More Playnite options has no options to move into.
        /// </summary>
        private void OnRailKeyDown(object sender, KeyEventArgs e)
        {
            var items = RailItems;
            var here = Array.IndexOf(items, e.OriginalSource as Button);
            var pad = PlayniteInput.PadButtonOf(e);
            if (e.OriginalSource == ThemeSettingsItem && (pad == ControllerInput.A || e.Key == Key.Right))
            {
                e.Handled = true;
                ThemeSettingsPane.FocusFirst();
            }
            else if (e.OriginalSource == PlayniteSettingsItem && (pad == ControllerInput.A || e.Key == Key.Right))
            {
                e.Handled = true;
                PlaynitePane.FocusFirst();
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

        // The Guide's dropdown and the Idle page's two, top to bottom.
        private void OnGuidePaneKeyDown(object sender, KeyEventArgs e) =>
            OnDropdownsKeyDown(e, nowPlayingSource);

        private void OnIdlePaneKeyDown(object sender, KeyEventArgs e) =>
            OnDropdownsKeyDown(e, dimAfter, showThingsAfter);

        /// <summary>
        /// The pad on a page of dropdowns' boxes, <paramref name="dropdowns"/> top to bottom: A opens the box's list, up and
        /// down move between the boxes, staying put at the ends, and Left or B goes back to Theme settings' boxes, focus on
        /// the page's. An open list takes its own keys (<see cref="OnDropListKeyDown"/>).
        /// </summary>
        private void OnDropdownsKeyDown(KeyEventArgs e, params Dropdown[] dropdowns)
        {
            var box = Array.FindIndex(dropdowns, d => d.Box == e.OriginalSource);
            if (box < 0)
            {
                return;
            }

            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == ControllerInput.A)
            {
                e.Handled = true;
                OpenDropdown(dropdowns[box]);
            }
            else if (pad == ControllerInput.B || e.Key == Key.Left)
            {
                // Handled here, before the page's own B steps back to Theme settings' boxes.
                e.Handled = true;
                core.LeaveThemePage();
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                dropdowns[Math.Max(0, Math.Min(dropdowns.Length - 1, box + (e.Key == Key.Up ? -1 : 1)))].Box.Focus();
            }
            else if (e.Key == Key.Right)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// The pad in a dropdown's open list, taken on its rows before the scroller round them, which would otherwise move
        /// focus itself, out of the list at its ends: up and down move through the choices, staying put at its ends, A
        /// chooses the one focused and B closes it unchanged, focus going back to the box either way.
        /// </summary>
        private void OnDropListKeyDown(Dropdown dropdown, KeyEventArgs e)
        {
            var options = dropdown.Options.Children.OfType<Button>().ToList();
            var here = options.IndexOf(e.OriginalSource as Button);
            if (here < 0)
            {
                return;
            }

            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == ControllerInput.A)
            {
                e.Handled = true;
                ChooseFromDropdown(dropdown, here);
            }
            else if (pad == ControllerInput.B)
            {
                // Handled here, before the page's own B steps back to Theme settings' boxes.
                e.Handled = true;
                CloseDropdown(dropdown);
                dropdown.Box.Focus();
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                FocusDropRow(options[Math.Max(0, Math.Min(options.Count - 1, here + (e.Key == Key.Up ? -1 : 1)))]);
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnDropdownsKeyDown.
        private void OnDropBoxClick(object sender, RoutedEventArgs e)
        {
            var dropdown = dropdowns.Single(d => d.Box == sender);
            if (dropdown.IsOpen)
            {
                CloseDropdown(dropdown);
            }
            else
            {
                OpenDropdown(dropdown);
            }
        }

        /// <summary>
        /// Opens a dropdown's list under its box, a row for each of its choices as the core has them now, the one chosen
        /// ticked and focused. Cut to the room left under the box above the page's foot, the list scrolls, focus keeping
        /// its row in view.
        /// </summary>
        private void OpenDropdown(Dropdown dropdown)
        {
            dropdown.Choices = dropdown.ReadChoices().ToList();
            dropdown.Options.Children.Clear();
            Button chosen = null;
            for (var i = 0; i < dropdown.Choices.Count; i++)
            {
                var index = i;
                var choice = dropdown.Choices[i];
                var option = new Button
                {
                    Style = (Style)FindResource("DropOption"),
                    Content = new TextBlock { Text = choice.Label, Style = (Style)FindResource("Text") },
                    Tag = choice.IsChosen ? "Selected" : null,
                };
                option.Click += (s, e) => ChooseFromDropdown(dropdown, index);
                dropdown.Options.Children.Add(option);
                if (choice.IsChosen)
                {
                    chosen = option;
                }
            }

            var top = dropdown.Box.TranslatePoint(new Point(0, dropdown.Box.ActualHeight + DropListGap), this).Y;
            dropdown.List.MaxHeight = Math.Max(DropListMinHeight, ActualHeight - top - DropListFoot);
            dropdown.Box.Tag = "Open";
            dropdown.List.Visibility = Visibility.Visible;
            dropdown.List.UpdateLayout();
            FocusDropRow(chosen ?? dropdown.Options.Children.OfType<Button>().FirstOrDefault());
        }

        /// <summary>
        /// Focuses a row of an open list, scrolled into view with its focus ring, which stands 8 off it: the row alone,
        /// as focus brings it, would leave the ring cut at the list's top or foot.
        /// </summary>
        private static void FocusDropRow(Button row)
        {
            if (row != null && row.Focus())
            {
                row.BringIntoView(new Rect(0, -DropRingReach, row.ActualWidth, row.ActualHeight + 2 * DropRingReach));
            }
        }

        /// <summary>
        /// The choice at <paramref name="index"/> in a dropdown's open list: the core takes and saves it, and the list
        /// closes onto its box. The box shows what the core then holds: one it refuses, or one already set, changes nothing.
        /// </summary>
        private void ChooseFromDropdown(Dropdown dropdown, int index)
        {
            if (index < 0 || index >= dropdown.Choices.Count)
            {
                return;
            }

            dropdown.Choices[index].Choose();
            CloseDropdown(dropdown);
            dropdown.Box.Focus();
        }

        /// <summary>Closes a dropdown's list, if open, leaving focus to the caller.</summary>
        private static void CloseDropdown(Dropdown dropdown)
        {
            dropdown.Box.Tag = null;
            dropdown.List.Visibility = Visibility.Collapsed;
            dropdown.Options.Children.Clear();
            dropdown.Choices = new List<DropChoice>();
        }

        /// <summary>The Idle page's boxes: the core's Dim after and Show things after, in its words.</summary>
        private void ShowIdle()
        {
            DimAfterText.Text = IdleState.Describe(core.Idle.DimAfter);
            ShowThingsAfterText.Text = IdleState.Describe(core.Idle.ShowThingsAfter);
        }

        // Dim after raised past Show things after raises it too: both boxes follow.
        private void OnIdleChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IdleState.DimAfter) || e.PropertyName == nameof(IdleState.ShowThingsAfter))
            {
                ShowIdle();
            }
        }

        /// <summary>The Idle page's choices: its steps in the core's words, the one set now chosen, each setting its own.</summary>
        private static IEnumerable<DropChoice> StepChoices(IEnumerable<TimeSpan?> steps, TimeSpan? now, Action<TimeSpan?> set) =>
            steps.Select(step => new DropChoice(IdleState.Describe(step), step == now, () => set(step)));

        /// <summary>
        /// A dropdown on a page of Theme settings, as the Guide's Audio Output and Input: its box, the list that opens
        /// under it, and the core's choices it reads as it opens.
        /// </summary>
        private sealed class Dropdown
        {
            public Dropdown(Button box, FrameworkElement list, Panel options, Func<IEnumerable<DropChoice>> readChoices)
            {
                Box = box;
                List = list;
                Options = options;
                ReadChoices = readChoices;
            }

            /// <summary>The box showing the one set now, which A opens the list from.</summary>
            public Button Box { get; }

            /// <summary>The list under the box, shown while it is open.</summary>
            public FrameworkElement List { get; }

            /// <summary>The open list's rows.</summary>
            public Panel Options { get; }

            /// <summary>The core's choices now, read as the list opens.</summary>
            public Func<IEnumerable<DropChoice>> ReadChoices { get; }

            /// <summary>The choices the open list shows, in its order; empty while it is closed.</summary>
            public List<DropChoice> Choices { get; set; } = new List<DropChoice>();

            /// <summary>Whether the list shows.</summary>
            public bool IsOpen => List.Visibility == Visibility.Visible;
        }

        /// <summary>A row of a dropdown's list: its words, whether it is the one set now, and what choosing it does.</summary>
        private sealed class DropChoice
        {
            public DropChoice(string label, bool isChosen, Action choose)
            {
                Label = label;
                IsChosen = isChosen;
                Choose = choose;
            }

            /// <summary>The row's words.</summary>
            public string Label { get; }

            /// <summary>Whether it is the one set now, ticked and focused as the list opens.</summary>
            public bool IsChosen { get; }

            /// <summary>Hands the choice to the core.</summary>
            public Action Choose { get; }
        }

        /// <summary>
        /// The pad in Discord's options: up and down between its rows, left and right along Type and Paste or Connect and
        /// Disconnect, Left from the first column or B back to Theme settings' boxes, focus on Discord, and A presses the
        /// button focused.
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
                // Handled here, before the page's own B steps back to Theme settings' boxes.
                e.Handled = true;
                core.LeaveThemePage();
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
        /// B on the page, from Playnite: on a page of Theme settings, back to its boxes; otherwise back to where it was
        /// opened from. Opened from the top bar, focus goes back to its Settings button; from the Guide, Home's rows take it
        /// as they come back. From the tile menu's Customise Home, Home's rows take it too, on the tile they last had focus on, the one whose menu opened the page; from Customise games
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
            var page = core.Settings.ThemePage;
            core.LeaveSettings();

            // Stepped back from a page of Theme settings to its boxes, the B is spent too.
            e.Handled = !core.Settings.IsShown || core.Settings.ThemePage != page;
            if (!core.Settings.IsShown && from == SettingsOrigin.TopBar)
            {
                FocusTopBarSoon();
            }
        }

        /// <summary>
        /// Focuses the category showing once the page is laid out, or, opened by a tile under Home's Recent row or the Tile
        /// menu's Customise, the page or option it came for: Discord's Client ID, Personalisation's first box, or the
        /// Playnite settings box.
        /// </summary>
        private void FocusRailSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!core.Settings.IsShown || !IsVisible)
                {
                    return;
                }

                if (core.Settings.OpensInPane && core.Settings.Category == SettingsCategory.ThemeSettings
                    && core.Settings.ThemePage != ThemeSettingsPage.None)
                {
                    FocusThemePage(core.Settings.ThemePage);
                }
                else if (core.Settings.OpensInPane && core.Settings.Category == SettingsCategory.PlayniteSettings)
                {
                    PlaynitePane.FocusPlayniteSettings();
                }
                else
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
