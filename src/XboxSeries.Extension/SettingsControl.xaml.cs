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
#if DEBUG

        /// <summary>Debug builds' Expire token now, after Disconnect: the saved tokens count as expiring, to try the refresh.</summary>
        private readonly Button expireTokenButton;
#endif

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
            DiscordSteps.ItemsSource = core.Settings.Discord.Steps.Select((step, i) => new { Number = $"{i + 1}.", Text = step }).ToList();
            OpenSettingsCommand = new RelayCommand(core.OpenSettings);
#if DEBUG
            expireTokenButton = new Button
            {
                Style = (Style)FindResource("PaneButton"),
                Margin = new Thickness(20, 0, 0, 0),
                Content = new TextBlock { Text = "Expire token now", Style = (Style)FindResource("Text") },
            };
            expireTokenButton.SetBinding(TagProperty, nameof(DiscordSettingsState.CanDisconnect));
            expireTokenButton.Click += OnDiscordButtonClick;
            DiscordButtons.Children.Add(expireTokenButton);
#endif
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ShowCategory();
        }

        /// <summary>What the page shows, for the Dashboard to hide Home by.</summary>
        public SettingsState State => core.Settings;

        /// <summary>The top bar's Settings: the page, on its first category.</summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>The rail, top to bottom, the divider aside.</summary>
        private Button[] RailItems => new[] { PersonalisationItem, GuideItem, DiscordItem, PlayniteSettingsItem, MorePlayniteOptionsItem };

        /// <summary>Discord's buttons, as the pad moves between them: a row each, Connect and Disconnect side by side.</summary>
        private Button[][] DiscordRows => new[]
        {
            new[] { PasteClientIdButton },
            new[] { PasteClientSecretButton },
#if DEBUG
            new[] { ConnectButton, DisconnectButton, expireTokenButton },
#else
            new[] { ConnectButton, DisconnectButton },
#endif
        };

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Settings.PropertyChanged += OnStateChanged;

            // Put up before the page was loaded, as when the Guide brings Playnite back from a Game hub.
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
                viewHost.PreviewKeyDown += OnViewHostPreviewKeyDown;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Settings.PropertyChanged -= OnStateChanged;
            if (viewHost != null)
            {
                viewHost.KeyDown -= OnViewHostKeyDown;
                viewHost.PreviewKeyDown -= OnViewHostPreviewKeyDown;
                viewHost = null;
            }
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsState.Category):
                    ShowCategory();

                    // Opened again from the Guide while up: focus follows the category back to the rail's first.
                    if (IsKeyboardFocusWithin && !categoryItems[core.Settings.Category].IsKeyboardFocused)
                    {
                        FocusRailSoon();
                    }

                    break;
                case nameof(SettingsState.IsShown):
                    ShowCategory();

                    // Opened from the top bar or the Guide: focus goes to the category showing, the rail's first.
                    if (core.Settings.IsShown)
                    {
                        FocusRailSoon();
                    }

                    break;
            }
        }

        /// <summary>The header and the lit rail item.</summary>
        private void ShowCategory()
        {
            var category = core.Settings.Category;
            Header.Text = category.ToString();
            DiscordPane.Visibility = category == SettingsCategory.Discord ? Visibility.Visible : Visibility.Collapsed;
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
        /// The pad's A opens Playnite's entries. Right or A on Discord moves into its options; Left and Right go nowhere
        /// else yet: the other categories have no options to move into.
        /// </summary>
        private void OnRailKeyDown(object sender, KeyEventArgs e)
        {
            var items = RailItems;
            var here = Array.IndexOf(items, e.OriginalSource as Button);
            var pad = PlayniteInput.PadButtonOf(e);
            if (e.OriginalSource == DiscordItem && (pad == ControllerInput.A || e.Key == Key.Right))
            {
                e.Handled = true;
                PasteClientIdButton.Focus();
            }
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
        /// The pad in Discord's options: up and down between its rows, left and right along Connect and Disconnect, Left
        /// from the first column or B back to the rail's Discord, and A presses the button focused.
        /// </summary>
        private void OnDiscordPaneKeyDown(object sender, KeyEventArgs e)
        {
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

        private void PressDiscordButton(object button)
        {
            if (button == PasteClientIdButton || button == PasteClientSecretButton)
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
#if DEBUG
            else if (button == expireTokenButton)
            {
                core.ExpireDiscordTokens();
            }
#endif
        }

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
        /// Playnite's LB/RB switch the user's filter presets, Y filters by name and RS opens the filters, all on the main
        /// list Home hides behind the page; here they do nothing.
        /// </summary>
        private void OnViewHostPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var button = PlayniteInput.PadButtonOf(e);
            if (core.Settings.IsShown
                && (button == ControllerInput.LeftShoulder || button == ControllerInput.RightShoulder
                    || button == ControllerInput.Y || button == ControllerInput.RightStick))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// B on the page, from Playnite: back to where it was opened from. Opened from the top bar, focus goes back to its
        /// Settings button; from the Guide, Home's rows take it as they come back.
        /// </summary>
        private void OnViewHostKeyDown(object sender, KeyEventArgs e)
        {
            if (!core.Settings.IsShown || PlayniteInput.PadButtonOf(e) != ControllerInput.B)
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
