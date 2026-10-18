using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Console keyboard's keys, drawn from a <see cref="ConsoleKeyboard"/>. The pad's A, the keyboard's Enter and the
    /// mouse on a key, and LT for the symbols key, raise <see cref="KeyChosen"/> for the host to press it through the core;
    /// the D-pad moves between keys on the grid, spanning keys keeping the row or column focus came in on, and stays on
    /// the board but for Up from the top row, which raises <see cref="LeftTop"/>. It holds no keyboard logic of its own.
    /// </summary>
    public partial class ConsoleKeyboardControl : UserControl
    {
        private const int Rows = 5;
        private const int Columns = 12;

        /// <summary>Where focus is on the grid: inside the focused key's cells, the one focus moved through to reach it.</summary>
        private int row;
        private int column;

        /// <summary>The key focus is on, or was last on, so focus comes back to it; null to start on the first letter.</summary>
        private KeyboardKey focusedKey;

        /// <summary>The key focus is being moved to by the D-pad, which places it on the grid itself.</summary>
        private KeyboardKey movingTo;

        /// <summary>The keys held down by a pad button, A on the key focused or the button a key carries, until it is let go.</summary>
        private readonly Dictionary<ControllerInput, Button> held = new Dictionary<ControllerInput, Button>();

        /// <summary>Whether a key is held down by the pad, drawn lighter as the mouse pressing it draws it.</summary>
        public static readonly DependencyProperty IsHeldProperty =
            DependencyProperty.RegisterAttached("IsHeld", typeof(bool), typeof(ConsoleKeyboardControl), new PropertyMetadata(false));

        public static bool GetIsHeld(DependencyObject element) => (bool)element.GetValue(IsHeldProperty);

        public static void SetIsHeld(DependencyObject element, bool value) => element.SetValue(IsHeldProperty, value);

        public ConsoleKeyboardControl()
        {
            InitializeComponent();
        }

        /// <summary>A key was pressed, by the pad's A, the keyboard's Enter, the mouse, or LT for the symbols key.</summary>
        public event EventHandler<KeyboardKey> KeyChosen;

        /// <summary>Up from the top row: the host can take focus above the keyboard.</summary>
        public event EventHandler LeftTop;

        /// <summary>The keyboard drawn.</summary>
        public ConsoleKeyboard Keyboard
        {
            get => DataContext as ConsoleKeyboard;
            set
            {
                DataContext = value;
                focusedKey = null;
            }
        }

        /// <summary>Focuses the key last focused, which stays the same key as Shift and symbols change what it reads, else the first letter.</summary>
        public void FocusKey()
        {
            var keys = Keyboard?.Keys;
            if (keys == null)
            {
                return;
            }

            var key = focusedKey != null && keys.Contains(focusedKey) ? focusedKey : FirstLetter(keys);
            FocusAt(key, key.Row, key.Column);
        }

        /// <summary>Starts again on the first letter next time, as the page opening afresh does.</summary>
        public void Reset()
        {
            focusedKey = null;
            LetGoAll();
        }

        /// <summary>The first letter: Q, at the start of the first letter row.</summary>
        private static KeyboardKey FirstLetter(IReadOnlyList<KeyboardKey> keys) =>
            keys.FirstOrDefault(k => k.Kind == KeyboardKeyKind.Character && k.Row == 1) ?? keys[0];

        private bool FocusAt(KeyboardKey key, int toRow, int toColumn)
        {
            var button = ButtonOf(key);
            if (button == null)
            {
                return false;
            }

            movingTo = key;
            row = toRow;
            column = toColumn;
            var focused = button.Focus();
            movingTo = null;
            return focused;
        }

        /// <summary>The button drawn for a key; null before the board is laid out.</summary>
        private Button ButtonOf(KeyboardKey key)
        {
            if (!(Board.ItemContainerGenerator.ContainerFromItem(key) is ContentPresenter presenter))
            {
                return null;
            }

            presenter.ApplyTemplate();
            return VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as Button : null;
        }

        /// <summary>The key covering a cell of the grid; null for none.</summary>
        private KeyboardKey KeyAt(int atRow, int atColumn) =>
            Keyboard?.Keys.FirstOrDefault(k => atRow >= k.Row && atRow < k.Row + k.RowSpan
                                                && atColumn >= k.Column && atColumn < k.Column + k.ColumnSpan);

        private void OnKeyFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!(((FrameworkElement)sender).DataContext is KeyboardKey key))
            {
                return;
            }

            // Focused any other way than the D-pad, as by the mouse: from its first cell.
            if (key != movingTo)
            {
                row = key.Row;
                column = key.Column;
            }

            focusedKey = key;
            Grid.SetRow(FocusRing, key.Row);
            Grid.SetColumn(FocusRing, key.Column);
            Grid.SetRowSpan(FocusRing, key.RowSpan);
            Grid.SetColumnSpan(FocusRing, key.ColumnSpan);
            FocusRing.Visibility = Visibility.Visible;
        }

        private void OnFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // A button let go off the board never reaches it, so nothing stays held.
            if (!(bool)e.NewValue)
            {
                FocusRing.Visibility = Visibility.Hidden;
                LetGoAll();
            }
        }

        /// <summary>Draws <paramref name="key"/> held down while <paramref name="pad"/> is.</summary>
        private void Hold(ControllerInput pad, KeyboardKey key)
        {
            var button = ButtonOf(key);
            if (button == null)
            {
                return;
            }

            LetGo(pad);
            held[pad] = button;
            SetIsHeld(button, true);
        }

        private void LetGo(ControllerInput pad)
        {
            if (held.TryGetValue(pad, out var button))
            {
                held.Remove(pad);
                button.ClearValue(IsHeldProperty);
            }
        }

        private void LetGoAll()
        {
            foreach (var pad in held.Keys.ToList())
            {
                LetGo(pad);
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnKeyKeyDown.
        private void OnKeyClick(object sender, RoutedEventArgs e)
        {
            Choose(sender);
        }

        private void OnKeyKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                if (((FrameworkElement)sender).DataContext is KeyboardKey key)
                {
                    Hold(ControllerInput.A, key);
                }

                Choose(sender);
            }
        }

        private void Choose(object button)
        {
            if (((FrameworkElement)button).DataContext is KeyboardKey key)
            {
                KeyChosen?.Invoke(this, key);
            }
        }

        /// <summary>
        /// LT presses the symbols key, as on the console, which the core has no shortcut for. Any pad button a key carries
        /// draws it held down until the button is let go; the host presses it through the core.
        /// </summary>
        private void OnBoardKeyDown(object sender, KeyEventArgs e)
        {
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == null || Keyboard == null)
            {
                return;
            }

            var symbols = Keyboard.Keys.FirstOrDefault(k => k.Kind == KeyboardKeyKind.Symbols);
            if (pad == ControllerInput.TriggerLeft && symbols != null)
            {
                e.Handled = true;
                Hold(pad.Value, symbols);
                KeyChosen?.Invoke(this, symbols);
            }
            else if (PlayniteInput.ToButton(pad.Value) is ControllerButton button && Keyboard.ShortcutKey(button) is KeyboardKey key)
            {
                Hold(pad.Value, key);
            }
        }

        private void OnBoardKeyUp(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) is ControllerInput pad)
            {
                LetGo(pad);
            }
        }

        /// <summary>
        /// The D-pad (arrow keys) moves a cell at a time along the grid from where focus is, so a tall key goes back to the
        /// row it was entered from and the space bar back up the column; at the board's edges focus stays put, but for Up
        /// from the top row. A key's own Space does nothing: typing a space is the host's.
        /// </summary>
        private void OnBoardPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }

            if (focusedKey == null || !(e.OriginalSource is Button))
            {
                return;
            }

            int toRow = row, toColumn = column;
            switch (e.Key)
            {
                case Key.Left: toColumn = focusedKey.Column - 1; break;
                case Key.Right: toColumn = focusedKey.Column + focusedKey.ColumnSpan; break;
                case Key.Up: toRow = focusedKey.Row - 1; break;
                case Key.Down: toRow = focusedKey.Row + focusedKey.RowSpan; break;
                default: return;
            }

            e.Handled = true;
            if (toRow < 0)
            {
                LeftTop?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (toRow >= Rows || toColumn < 0 || toColumn >= Columns)
            {
                return;
            }

            var next = KeyAt(toRow, toColumn);
            if (next != null)
            {
                FocusAt(next, toRow, toColumn);
            }
        }
    }
}
