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
}
