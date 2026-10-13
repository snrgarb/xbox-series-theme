using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What the Guide shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class GuideState : INotifyPropertyChanged
    {
        private bool isOpen;
        private bool isOverlayShown;
        private IReadOnlyList<GuideItem> items = new GuideItem[0];
        private GuideItem focusedItem;
        private GuideDialog dialog;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Whether the Guide panel is in: it slides in when this turns true and out when it turns false.</summary>
        public bool IsOpen
        {
            get => isOpen;
            internal set => Set(ref isOpen, value, nameof(IsOpen));
        }

        /// <summary>
        /// Whether the Overlay window is up. It outlives <see cref="IsOpen"/> until focus has been handed back,
        /// because hiding the focused Overlay early would hand the game focus while the dismiss button is still held.
        /// </summary>
        public bool IsOverlayShown
        {
            get => isOverlayShown;
            internal set => Set(ref isOverlayShown, value, nameof(IsOverlayShown));
        }

        /// <summary>The landing tab's entries, top to bottom.</summary>
        public IReadOnlyList<GuideItem> Items
        {
            get => items;
            internal set => Set(ref items, value, nameof(Items));
        }

        /// <summary>The entry with focus. Each item's <see cref="GuideItem.IsFocused"/> follows it.</summary>
        public GuideItem FocusedItem
        {
            get => focusedItem;
            internal set
            {
                if (focusedItem == value)
                {
                    return;
                }

                if (focusedItem != null)
                {
                    focusedItem.IsFocused = false;
                }

                focusedItem = value;
                if (focusedItem != null)
                {
                    focusedItem.IsFocused = true;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FocusedItem)));
            }
        }

        /// <summary>
        /// The menu or prompt shown over the Guide, which has the pad until it is answered; null when there is none.
        /// </summary>
        public GuideDialog Dialog
        {
            get => dialog;
            internal set => Set(ref dialog, value, nameof(Dialog));
        }

        private void Set<T>(ref T field, T value, string name)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>What a landing-tab entry is, which decides what A does on it.</summary>
    public enum GuideItemKind
    {
        Home,
        MyGamesAndApps,
        RunningGame,
        RecentGame,
    }

    /// <summary>One entry on the Guide's landing tab.</summary>
    public sealed class GuideItem : INotifyPropertyChanged
    {
        private bool isFocused;

        internal GuideItem(GuideItemKind kind, string title, Guid? gameId = null, bool hasDividerAbove = false)
        {
            Kind = kind;
            Title = title;
            GameId = gameId;
            HasDividerAbove = hasDividerAbove;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public GuideItemKind Kind { get; }
        public string Title { get; }

        /// <summary>The game this entry stands for, or null for Home and My games &amp; apps.</summary>
        public Guid? GameId { get; }

        /// <summary>True for the first game, which a divider separates from Home and My games &amp; apps.</summary>
        public bool HasDividerAbove { get; }

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
    }

    /// <summary>A menu or prompt over the Guide: a title, an optional message, and options to pick from.</summary>
    public sealed class GuideDialog : INotifyPropertyChanged
    {
        private GuideDialogOption focusedOption;

        internal GuideDialog(string title, string message, Action decline, params GuideDialogOption[] options)
        {
            Title = title;
            Message = message;
            Decline = decline;
            Options = options;
            FocusedOption = options[0];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title { get; }

        /// <summary>A line under the title, or null.</summary>
        public string Message { get; }

        public IReadOnlyList<GuideDialogOption> Options { get; }

        /// <summary>The option with focus. Each option's <see cref="GuideDialogOption.IsFocused"/> follows it.</summary>
        public GuideDialogOption FocusedOption
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

        /// <summary>What B, or closing the Guide, does: the answer "no".</summary>
        internal Action Decline { get; }
    }

    /// <summary>One choice in a <see cref="GuideDialog"/>.</summary>
    public sealed class GuideDialogOption : INotifyPropertyChanged
    {
        private bool isFocused;

        internal GuideDialogOption(string label, Action choose)
        {
            Label = label;
            Choose = choose;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Label { get; }

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
}
