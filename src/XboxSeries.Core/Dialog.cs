using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// A menu or prompt: a title, an optional message, and options to pick from. The Guide shows them over itself; Home
    /// shows the tile menu centred over it. Both are drawn alike, after the console's context menu.
    /// </summary>
    public sealed class Dialog : INotifyPropertyChanged
    {
        private readonly bool isPrompt;
        private DialogOption focusedOption;

        internal Dialog(string title, string message, bool isPrompt, Action decline, params DialogOption[] options)
        {
            Title = title;
            this.isPrompt = isPrompt;
            Message = message;
            Decline = decline;
            Options = options;
            FocusedOption = options[0];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title { get; }

        /// <summary>A line under the title, or null.</summary>
        public string Message { get; }

        public IReadOnlyList<DialogOption> Options { get; }

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
    }
}
