using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

        /// <summary>As the keyboard comes up on a page just opened: nothing typed, Shift on, the letters showing.</summary>
        internal void Reset()
        {
            Text = string.Empty;
            Cursor = 0;
            IsShifted = true;
            ShowsSymbols = false;
            Relabel();
        }

        /// <summary>
        /// As the keyboard comes up over text already typed, as text entry's does: <paramref name="startingText"/>, the
        /// cursor at its end, Shift on only with nothing typed, the letters showing.
        /// </summary>
        internal void Reset(string startingText)
        {
            Reset();
            Text = startingText ?? string.Empty;
            Cursor = Text.Length;
            SetShifted(Text.Length == 0);
        }

        /// <summary>
        /// A on <paramref name="key"/>: a character or a space goes in at the cursor, Shift going off after it; Shift and
        /// the symbols key turn; backspace and the arrows edit and move. Enter is the keyboard's owner's to answer, so
        /// it only says so: returns whether the key was Enter.
        /// </summary>
        internal bool Press(KeyboardKey key)
        {
            switch (key.Kind)
            {
                case KeyboardKeyKind.Character:
                    Type(key);
                    break;
                case KeyboardKeyKind.Space:
                    Insert(" ");
                    SetShifted(false);
                    break;
                case KeyboardKeyKind.Shift:
                    SetShifted(!IsShifted);
                    break;
                case KeyboardKeyKind.Symbols:
                    ToggleSymbols();
                    break;
                case KeyboardKeyKind.Backspace:
                    Backspace();
                    break;
                case KeyboardKeyKind.Left:
                    MoveCursor(-1);
                    break;
                case KeyboardKeyKind.Right:
                    MoveCursor(1);
                    break;
                case KeyboardKeyKind.Enter:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Text typed on a physical keyboard: in at the cursor as typed, whatever Shift says, which goes off as after any
        /// character. Backspace and Enter, say, reach a view as text too; only what prints goes in. Returns whether anything did.
        /// </summary>
        internal bool InsertTyped(string typed)
        {
            var printable = new string((typed ?? string.Empty).Where(c => !char.IsControl(c)).ToArray());
            if (printable.Length == 0)
            {
                return false;
            }

            Insert(printable);
            SetShifted(false);
            return true;
        }

        /// <summary>The key a pad button stands for: X backspace, Y space, LB and RB the cursor, the left stick's click Shift, Start Enter; null for a button that isn't the keyboard's.</summary>
        internal KeyboardKey ShortcutKey(ControllerButton button)
        {
            KeyboardKeyKind kind;
            switch (button)
            {
                case ControllerButton.X: kind = KeyboardKeyKind.Backspace; break;
                case ControllerButton.Y: kind = KeyboardKeyKind.Space; break;
                case ControllerButton.LeftShoulder: kind = KeyboardKeyKind.Left; break;
                case ControllerButton.RightShoulder: kind = KeyboardKeyKind.Right; break;
                case ControllerButton.LeftStick: kind = KeyboardKeyKind.Shift; break;
                case ControllerButton.Start: kind = KeyboardKeyKind.Enter; break;
                default: return null;
            }

            return Keys.First(k => k.Kind == kind);
        }

        /// <summary>Puts <paramref name="characters"/> in at the cursor, which moves past them.</summary>
        internal void Insert(string characters)
        {
            Text = Text.Insert(Cursor, characters);
            Cursor += characters.Length;
        }

        /// <summary>A Character key: its text at the cursor, Shift going off after the one character.</summary>
        internal void Type(KeyboardKey key)
        {
            Insert(key.Text);
            SetShifted(false);
        }

        /// <summary>Deletes the character before the cursor, if any.</summary>
        internal void Backspace()
        {
            if (Cursor > 0)
            {
                Text = Text.Remove(Cursor - 1, 1);
                Cursor--;
            }
        }

        /// <summary>Moves the cursor <paramref name="by"/> characters, held between the start and the end of the text.</summary>
        internal void MoveCursor(int by) => Cursor = Math.Max(0, Math.Min(Text.Length, Cursor + by));

        internal void SetShifted(bool shifted)
        {
            IsShifted = shifted;
            Relabel();
        }

        internal void ToggleSymbols()
        {
            ShowsSymbols = !ShowsSymbols;
            Relabel();
        }

        /// <summary>The keys' text as Shift and symbols have it now: the letter rows in capitals or not, or their symbols; the symbols key "#+=" or "abc".</summary>
        private void Relabel()
        {
            foreach (var key in Keys)
            {
                if (key.Kind == KeyboardKeyKind.Symbols)
                {
                    key.Text = ShowsSymbols ? "abc" : "#+=";
                }
                else if (key.Kind == KeyboardKeyKind.Character && key.Row >= 1 && key.Row <= 3)
                {
                    var character = (ShowsSymbols ? KeyboardKey.SymbolRows : KeyboardKey.LetterRows)[key.Row - 1][key.Column - 1].ToString();
                    key.Text = ShowsSymbols || IsShifted ? character : character.ToLowerInvariant();
                }
            }
        }

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

        /// <summary>The three letter rows, under the number row, in capitals; the last two end in punctuation, as the console's do.</summary>
        internal static readonly string[] LetterRows = { "QWERTYUIOP", "ASDFGHJKL\"", "ZXCVBNM;:!" };

        /// <summary>What the letter rows read in place of letters with symbols showing; the number row stays.</summary>
        internal static readonly string[] SymbolRows = { "!@#$%^&*()", "-_=+[]{}\\|", "~`<>/?,.'\"" };

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
            Row(1, LetterRows[0]);
            keys.Add(new KeyboardKey(KeyboardKeyKind.Right, 1, 11, rowSpan: 2));
            Row(2, LetterRows[1]);
            keys.Add(new KeyboardKey(KeyboardKeyKind.Shift, 3, 0, rowSpan: 2));
            Row(3, LetterRows[2]);
            keys.Add(new KeyboardKey(KeyboardKeyKind.Enter, 3, 11, rowSpan: 2));
            keys.Add(new KeyboardKey(KeyboardKeyKind.Space, 4, 1, columnSpan: 10));
            return keys;
        }
    }
}
