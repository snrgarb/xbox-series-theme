using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Text entry, injected as a custom element over everything the Dashboard draws: the dim, the caption, the field and
    /// the Console keyboard. It follows the core's <see cref="TextEntryState"/>, passes the keys pressed, the pad's
    /// shortcuts, B and text typed on a real keyboard to the core, and keeps focus on the keys while it is up; it holds no
    /// text logic. While it is up it has the pad: every button reaching it is handled here, so neither the page under it
    /// nor Playnite's own bindings hear one.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class TextEntryControl : PluginUserControl
    {
        /// <summary>A dot for each character of a secret, as a password box draws it.</summary>
        private const char SecretDot = '●';

        private readonly ExtensionCore core;

        /// <summary>What had focus as text entry came up, for focus to go back to as it goes.</summary>
        private IInputElement focusedBefore;

        internal TextEntryControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.TextEntry;
            ConsoleKeys.Keyboard = core.TextEntry.Keyboard;

            // Watched here rather than in the XAML, which draws no focus of its own: the keyboard's ring is the one.
            Root.IsKeyboardFocusWithinChanged += OnFocusWithinChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ShowText();
        }

        /// <summary>Text entry's state, for the Dashboard to bind to.</summary>
        public TextEntryState State => core.TextEntry;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.TextEntry.PropertyChanged += OnStateChanged;
            core.TextEntry.Keyboard.PropertyChanged += OnKeyboardChanged;
            ShowText();

            // Put up before the element was loaded.
            if (core.TextEntry.IsShown)
            {
                Open();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.TextEntry.PropertyChanged -= OnStateChanged;
            core.TextEntry.Keyboard.PropertyChanged -= OnKeyboardChanged;
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TextEntryState.IsShown):
                    if (core.TextEntry.IsShown)
                    {
                        Open();
                    }
                    else
                    {
                        Close();
                    }

                    break;
                case nameof(TextEntryState.IsSecret):
                    ShowText();
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

        /// <summary>
        /// Up afresh: what has focus is remembered, and focus goes to the keyboard's first letter once it is laid out.
        /// </summary>
        private void Open()
        {
            var focused = Keyboard.FocusedElement;
            if (!(focused is DependencyObject d && IsInside(d)))
            {
                focusedBefore = focused;
            }

            ShowText();
            ConsoleKeys.Reset();
            FocusKeysSoon();
        }

        /// <summary>
        /// Gone: focus goes back to what had it, once the page has settled, if that is still shown; else the page that
        /// opened text entry places it. Not if the page has already moved it somewhere it can be seen.
        /// </summary>
        private void Close()
        {
            var back = focusedBefore;
            focusedBefore = null;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (core.TextEntry.IsShown)
                {
                    return;
                }

                // WPF moves focus off the collapsed keys to what holds them, the window: that is not the page's doing.
                if (Keyboard.FocusedElement is UIElement now && now.IsVisible && !IsInside(now) && !now.IsAncestorOf(this))
                {
                    return;
                }

                if (back is UIElement element && element.IsVisible && PresentationSource.FromVisual(element) != null)
                {
                    element.Focus();
                }
            }));
        }

        /// <summary>Focuses the keyboard's key once it is laid out, as long as text entry is still up.</summary>
        private void FocusKeysSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (core.TextEntry.IsShown && IsVisible && !ConsoleKeys.IsKeyboardFocusWithin)
                {
                    ConsoleKeys.FocusKey();
                }
            }));
        }

        /// <summary>
        /// Focus taken off the keys while text entry is up, as by a page under it refocusing as Full library narrows: it
        /// comes back to the key it was on. Not while Playnite is in the background, where focus goes with the window.
        /// </summary>
        private void OnFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue && core.TextEntry.IsShown && Window.GetWindow(this)?.IsActive == true)
            {
                FocusKeysSoon();
            }
        }

        /// <summary>The field: the text either side of the caret, or a dot for each character while it is secret.</summary>
        private void ShowText()
        {
            var text = core.TextEntry.Keyboard.Text ?? string.Empty;
            if (core.TextEntry.IsSecret)
            {
                text = new string(SecretDot, text.Length);
            }

            var cursor = Math.Max(0, Math.Min(text.Length, core.TextEntry.Keyboard.Cursor));
            BeforeCaret.Text = text.Substring(0, cursor);
            AfterCaret.Text = text.Substring(cursor);
        }

        // A key of the Console keyboard, by the pad's A, the keyboard's Enter, the mouse, or LT for the symbols key.
        private void OnKeyChosen(object sender, KeyboardKey key)
        {
            core.PressKey(key);
        }

        /// <summary>
        /// A real keyboard, for testing on the desktop: Space types a space and Backspace deletes before the cursor, rather
        /// than pressing the key focused; Escape cancels, as B does. Enter presses the key focused, as A does.
        /// </summary>
        private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!core.TextEntry.IsShown)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Space:
                    e.Handled = true;
                    core.TypeText(" ");
                    break;
                case Key.Back:
                    e.Handled = true;
                    core.DeleteText();
                    break;
                case Key.Escape:
                    e.Handled = true;
                    core.CancelTextEntry();
                    break;
            }
        }

        /// <summary>
        /// The pad, through the core: X backspace, Y space, LB and RB the cursor, the left stick's click Shift, Start Enter,
        /// which keeps the text; B cancels. A and LT reach the keys, and the D-pad moves along them, before this. Every
        /// other button is swallowed: the page under text entry, and Playnite's own bindings for Y, LB/RB, RS and Back,
        /// hear none of them.
        /// </summary>
        private void OnRootKeyDown(object sender, KeyEventArgs e)
        {
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == null || !core.TextEntry.IsShown)
            {
                return;
            }

            e.Handled = true;
            switch (pad.Value)
            {
                case ControllerInput.X:
                case ControllerInput.Y:
                case ControllerInput.LeftShoulder:
                case ControllerInput.RightShoulder:
                case ControllerInput.LeftStick:
                case ControllerInput.Start:
                    if (PlayniteInput.ToButton(pad.Value) is ControllerButton button)
                    {
                        core.PressKeyboardShortcut(button);
                    }

                    break;
                case ControllerInput.B:
                    core.CancelTextEntry();
                    break;
            }
        }

        /// <summary>Text typed on a real keyboard goes in at the cursor, as the Console keyboard types it. Space is typed by OnRootPreviewKeyDown.</summary>
        private void OnRootTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!core.TextEntry.IsShown || string.IsNullOrWhiteSpace(e.Text) || e.Text.Any(char.IsControl))
            {
                return;
            }

            e.Handled = true;
            core.TypeText(e.Text);
        }

        private void OnDimClick(object sender, MouseButtonEventArgs e)
        {
            core.CancelTextEntry();
        }

        /// <summary>Whether an element is this control or inside it.</summary>
        private bool IsInside(DependencyObject element) =>
            element == this || (element is Visual visual && IsAncestorOf(visual));
    }
}
