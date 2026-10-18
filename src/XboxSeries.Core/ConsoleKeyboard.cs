using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// The console's on-screen keyboard: the text typed, the cursor in it, and its keys, laid out on a grid of 5 rows by 12
    /// columns as the console's: the symbols key, the number row and backspace; the cursor's left and right keys either side
    /// of two QWERTY rows; Shift and Enter either side of the last, and the space bar under it. Shift turns the letters to
    /// capitals for one character; the symbols key swaps the letter rows for symbols. Views bind to it and hold no logic.
    /// </summary>
    public sealed class ConsoleKeyboard : INotifyPropertyChanged
    {
        private string text = string.Empty;
        private int cursor;
        private bool isShifted = true;
        private bool showsSymbols;

        internal ConsoleKeyboard()
        {
            Keys = KeyboardKey.Layout();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The text typed so far.</summary>
        public string Text
        {
            get => text;
            internal set
            {
                if (text != value)
                {
                    text = value;
                    Changed(nameof(Text));
                }
            }
        }

        /// <summary>Where the next character goes, as an index into <see cref="Text"/>.</summary>
        public int Cursor
        {
            get => cursor;
            internal set
            {
                if (cursor != value)
                {
                    cursor = value;
                    Changed(nameof(Cursor));
                }
            }
        }

        /// <summary>Whether Shift is on: the letter keys read and type capitals, for one character.</summary>
        public bool IsShifted
        {
            get => isShifted;
            internal set
            {
                if (isShifted != value)
                {
                    isShifted = value;
                    Changed(nameof(IsShifted));
                }
            }
        }

        /// <summary>Whether the letter rows show symbols, the symbols key reading "abc" to go back.</summary>
        public bool ShowsSymbols
        {
            get => showsSymbols;
            internal set
            {
                if (showsSymbols != value)
                {
                    showsSymbols = value;
                    Changed(nameof(ShowsSymbols));
                }
            }
        }

        /// <summary>Every key, in reading order; the same keys for as long as the keyboard lives, their text changing with Shift and symbols.</summary>
        public IReadOnlyList<KeyboardKey> Keys { get; }

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    /// <summary>What a key on the console keyboard does.</summary>
    public enum KeyboardKeyKind
    {
        /// <summary>Types its <see cref="KeyboardKey.Text"/>.</summary>
        Character,

        /// <summary>"#+=": swaps the letter rows for symbols, and back ("abc").</summary>
        Symbols,

        /// <summary>Deletes the character before the cursor; X on the pad.</summary>
        Backspace,

        /// <summary>Moves the cursor one character left; LB on the pad.</summary>
        Left,

        /// <summary>Moves the cursor one character right; RB on the pad.</summary>
        Right,

        /// <summary>Capitals for the next character; the left stick's click on the pad.</summary>
        Shift,

        /// <summary>Submits the text; Start on the pad.</summary>
        Enter,

        /// <summary>Types a space; Y on the pad.</summary>
        Space,
    }

    /// <summary>One key of the <see cref="ConsoleKeyboard"/>, at its place on the keyboard's 5 by 12 grid.</summary>
    public sealed class KeyboardKey : INotifyPropertyChanged
    {
        private string text;

        internal KeyboardKey(KeyboardKeyKind kind, int row, int column, string text = null, int rowSpan = 1, int columnSpan = 1)
        {
            Kind = kind;
            Row = row;
            Column = column;
            RowSpan = rowSpan;
            ColumnSpan = columnSpan;
            this.text = text;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public KeyboardKeyKind Kind { get; }
        public int Row { get; }
        public int Column { get; }
        public int RowSpan { get; }
        public int ColumnSpan { get; }

        /// <summary>
        /// What the key reads: the character a Character key types, as Shift and symbols have it now, "#+=" or "abc" on
        /// the symbols key; null for keys drawn as a glyph (backspace, the arrows, Shift, Enter, space).
        /// </summary>
        public string Text
        {
            get => text;
            internal set
            {
                if (text != value)
                {
                    text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        /// <summary>The console's keys in reading order, letters in capitals, as Shift starts on.</summary>
        internal static IReadOnlyList<KeyboardKey> Layout()
        {
            var keys = new List<KeyboardKey> { new KeyboardKey(KeyboardKeyKind.Symbols, 0, 0, "#+=") };
            void Row(int row, string characters)
            {
                for (var i = 0; i < characters.Length; i++)
                {
                    keys.Add(new KeyboardKey(KeyboardKeyKind.Character, row, i + 1, characters[i].ToString()));
                }
            }

            Row(0, "1234567890");
            keys.Add(new KeyboardKey(KeyboardKeyKind.Backspace, 0, 11));
            keys.Add(new KeyboardKey(KeyboardKeyKind.Left, 1, 0, rowSpan: 2));
            Row(1, "QWERTYUIOP");
            keys.Add(new KeyboardKey(KeyboardKeyKind.Right, 1, 11, rowSpan: 2));
            Row(2, "ASDFGHJKL\"");
            keys.Add(new KeyboardKey(KeyboardKeyKind.Shift, 3, 0, rowSpan: 2));
            Row(3, "ZXCVBNM;:!");
            keys.Add(new KeyboardKey(KeyboardKeyKind.Enter, 3, 11, rowSpan: 2));
            keys.Add(new KeyboardKey(KeyboardKeyKind.Space, 4, 1, columnSpan: 10));
            return keys;
        }
    }
}
