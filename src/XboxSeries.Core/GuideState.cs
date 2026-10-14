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
        private bool showsPanel = true;
        private GuideTabKind tab;
        private IReadOnlyList<GuideTab> tabs = new GuideTab[0];
        private IReadOnlyList<GuideItem> items = new GuideItem[0];
        private GuideItem focusedItem;
        private Dialog dialog;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Whether the Guide has the pad. Its panel is in while this and <see cref="ShowsPanel"/> are true: it slides in
        /// as they turn true and out as either turns false.
        /// </summary>
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

        /// <summary>
        /// False while Power, opened by holding the Guide button, stands on its own over the dim, as on the console;
        /// the panel stays out until the Guide next opens.
        /// </summary>
        public bool ShowsPanel
        {
            get => showsPanel;
            internal set => Set(ref showsPanel, value, nameof(ShowsPanel));
        }

        /// <summary>The Guide's tabs, left to right, as icons along its top. LB and RB move between them.</summary>
        public IReadOnlyList<GuideTab> Tabs
        {
            get => tabs;
            internal set => Set(ref tabs, value, nameof(Tabs));
        }

        /// <summary>The tab showing, whose entries are <see cref="Items"/>.</summary>
        public GuideTabKind Tab
        {
            get => tab;
            internal set => Set(ref tab, value, nameof(Tab));
        }

        /// <summary>The hints beside the panel: power options always, and more options on an entry with a menu.</summary>
        public IReadOnlyList<GuideHint> Hints
        {
            get
            {
                var power = new GuideHint(ControllerButton.Guide, "Hold for power options");
                return focusedItem != null && focusedItem.HasMenu
                    ? new[] { power, new GuideHint(ControllerButton.Start, "More options") }
                    : new[] { power };
            }
        }

        /// <summary>The showing tab's entries, top to bottom.</summary>
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hints)));
            }
        }

        /// <summary>
        /// The menu or prompt shown over the Guide, which has the pad until it is answered; null when there is none.
        /// </summary>
        public Dialog Dialog
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

    /// <summary>A Guide tab. The Discord tab slots in between the two later.</summary>
    public enum GuideTabKind
    {
        Landing,
        ProfileAndSystem,
    }

    /// <summary>One of the Guide's tabs, as its icon along the top.</summary>
    public sealed class GuideTab
    {
        internal GuideTab(GuideTabKind kind, bool isSelected, string image = null)
        {
            Kind = kind;
            IsSelected = isSelected;
            Image = image;
        }

        public GuideTabKind Kind { get; }

        /// <summary>Whether this is the tab showing; it wears the green underline.</summary>
        public bool IsSelected { get; }

        /// <summary>A full path to the picture the icon shows, as Profile &amp; system shows the account picture; null for a glyph.</summary>
        public string Image { get; }
    }

    /// <summary>A hint beside the Guide panel: a pad button and what it does.</summary>
    public sealed class GuideHint
    {
        internal GuideHint(ControllerButton button, string text)
        {
            Button = button;
            Text = text;
        }

        public ControllerButton Button { get; }
        public string Text { get; }
    }

    /// <summary>What a Guide entry is, which decides what A does on it.</summary>
    public enum GuideItemKind
    {
        Home,
        MyGamesAndApps,
        RunningGame,
        RecentGame,
        Profile,
        Settings,
        Power,
    }

    /// <summary>One entry on a Guide tab.</summary>
    public sealed class GuideItem : INotifyPropertyChanged
    {
        private bool isFocused;

        internal GuideItem(GuideItemKind kind, string title, Guid? gameId = null, bool hasDividerAbove = false, string image = null)
        {
            Kind = kind;
            Title = title;
            GameId = gameId;
            HasDividerAbove = hasDividerAbove;
            Image = image;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public GuideItemKind Kind { get; }
        public string Title { get; }

        /// <summary>The game this entry stands for, or null for Home and My games &amp; apps.</summary>
        public Guid? GameId { get; }

        /// <summary>True for an entry a divider separates from those above it, such as the first game under Home and My games &amp; apps.</summary>
        public bool HasDividerAbove { get; }

        /// <summary>A full path to the picture shown beside the title: a game's cover, the account picture; null for none.</summary>
        public string Image { get; }

        /// <summary>Whether Menu opens a menu on this entry. Only the running game has one, with Quit.</summary>
        public bool HasMenu => Kind == GuideItemKind.RunningGame;

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
