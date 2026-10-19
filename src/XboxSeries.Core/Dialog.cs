using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// A menu or prompt: a title, an optional message, and options to pick from. The Guide shows them over itself; Home
    /// shows the tile menu centred over it. Both are drawn alike, after the console's context menu. A wide dialog is
    /// drawn instead after the console's "What do you want to do?": a band across the screen, its options as tiles in
    /// rows of <see cref="Columns"/>, with a Close button below them.
    /// </summary>
    public sealed class Dialog : INotifyPropertyChanged
    {
        /// <summary>How many tiles a row of a wide dialog holds, as on the console.</summary>
        public const int Columns = 3;

        private readonly bool isPrompt;
        private DialogOption focusedOption;

        internal Dialog(string title, string message, bool isPrompt, Action decline, params DialogOption[] options)
        {
            Title = title;
            this.isPrompt = isPrompt;
            Message = message;
            Decline = decline;
            Options = options;
            Tiles = new DialogOption[0];
            FocusedOption = options[0];
        }

        private Dialog(string title, Action decline, DialogOption close, DialogOption[] tiles)
        {
            Title = title;
            Decline = decline;
            Tiles = tiles;
            CloseOption = close;
            Options = tiles.Concat(new[] { close }).ToList();
            FocusedOption = close;
        }

        /// <summary>A wide dialog: <paramref name="tiles"/> in rows, then <paramref name="close"/>, which has focus as it opens.</summary>
        internal static Dialog Wide(string title, Action decline, DialogOption close, params DialogOption[] tiles) =>
            new Dialog(title, decline, close, tiles);

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title { get; }

        /// <summary>A line under the title, or null.</summary>
        public string Message { get; }

        /// <summary>Every option, in focus order: a wide dialog's tiles, then its Close.</summary>
        public IReadOnlyList<DialogOption> Options { get; }

        /// <summary>Whether this is drawn wide, with <see cref="Tiles"/> and a <see cref="CloseOption"/>.</summary>
        public bool IsWide => CloseOption != null;

        /// <summary>A question to answer ("Quit X and start Y?") rather than a menu of what can be done with something.</summary>
        public bool IsPrompt => isPrompt;

        /// <summary>A wide dialog's tiles, row by row; none for a menu or prompt.</summary>
        public IReadOnlyList<DialogOption> Tiles { get; }

        /// <summary>A wide dialog's Close button, under its tiles; null for a menu or prompt.</summary>
        public DialogOption CloseOption { get; }

        /// <summary>
        /// The sound as an option is chosen. Menus and prompts open and close alike, but a prompt asks about what just
        /// happened, so any answer closes it, while a menu offers what can be done, so a choice goes on with it.
        /// </summary>
        internal UiSound ChooseSound => isPrompt ? UiSound.PromptClose : UiSound.Enter;

        /// <summary>The option with focus. Each option's <see cref="DialogOption.IsFocused"/> follows it.</summary>
        public DialogOption FocusedOption
        {
            get => focusedOption;
            internal set
            {
                if (focusedOption == value)
                {
                    return;
                }

                if (focusedOption != null)
                {
                    focusedOption.IsFocused = false;
                }

                focusedOption = value;
                focusedOption.IsFocused = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FocusedOption)));
            }
        }

        /// <summary>What B, or closing whatever shows it, does: the answer "no".</summary>
        internal Action Decline { get; }

        /// <summary>
        /// The option the D-pad moves focus to from the focused one, or the focused one at an edge. A menu or prompt is
        /// a column. A wide dialog is a grid: down from a tile with none below goes to the next row's last tile, down
        /// from the last row to Close, and up from Close to the last row's first tile.
        /// </summary>
        internal DialogOption Towards(ControllerButton direction)
        {
            var index = Options.ToList().IndexOf(FocusedOption);
            if (!IsWide)
            {
                var by = direction == ControllerButton.DPadUp ? -1 : direction == ControllerButton.DPadDown ? 1 : 0;
                return Options[Math.Max(0, Math.Min(Options.Count - 1, index + by))];
            }

            var count = Tiles.Count;
            var lastRow = (count - 1) / Columns;
            if (FocusedOption == CloseOption)
            {
                return direction == ControllerButton.DPadUp && count > 0 ? Tiles[lastRow * Columns] : CloseOption;
            }

            var row = index / Columns;
            var column = index % Columns;
            switch (direction)
            {
                case ControllerButton.DPadLeft:
                    return column > 0 ? Tiles[index - 1] : FocusedOption;
                case ControllerButton.DPadRight:
                    return column < Columns - 1 && index + 1 < count ? Tiles[index + 1] : FocusedOption;
                case ControllerButton.DPadUp:
                    return row > 0 ? Tiles[index - Columns] : FocusedOption;
                case ControllerButton.DPadDown:
                    return index + Columns < count ? Tiles[index + Columns]
                        : row == lastRow ? CloseOption
                        : Tiles[count - 1];
                default:
                    return FocusedOption;
            }
        }
    }

    /// <summary>One choice in a <see cref="Dialog"/>.</summary>
    public sealed class DialogOption : INotifyPropertyChanged
    {
        private bool isFocused;

        internal DialogOption(string label, DialogIcon icon, Action choose)
        {
            Label = label;
            Icon = icon;
            Choose = choose;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Label { get; }

        /// <summary>The line icon left of the label.</summary>
        public DialogIcon Icon { get; }

        public bool IsFocused
        {
            get => isFocused;
            internal set
            {
                if (isFocused == value)
                {
                    return;
                }

                isFocused = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFocused)));
            }
        }

        internal Action Choose { get; }
    }

    /// <summary>What an option's icon shows. Views pick the glyph.</summary>
    public enum DialogIcon
    {
        Play,
        Quit,
        Cancel,
        Favourite,
        Unfavourite,
        Manage,
        TurnOff,
        Restart,
        Sleep,
        Exit,

        /// <summary>Taking something away, as a Discord shortcut's Remove shortcut.</summary>
        Remove,

        /// <summary>Customise Home and Customise games &amp; apps, on the tile menu: Settings, on Personalisation.</summary>
        Customise,
    }
}
