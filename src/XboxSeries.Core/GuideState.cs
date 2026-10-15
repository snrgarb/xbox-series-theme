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
        private IReadOnlyList<GuideItem> utilities = new GuideItem[0];
        private GuideFlyout flyout;
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

        /// <summary>
        /// The landing tab's row of utilities along the bottom of the panel, left to right: Notifications and Audio. Focus
        /// reaches them down from the tab's last entry. Empty on the other tabs.
        /// </summary>
        public IReadOnlyList<GuideItem> Utilities
        {
            get => utilities;
            internal set => Set(ref utilities, value, nameof(Utilities));
        }

        /// <summary>
        /// The flyout open beside the panel, over the tab it was opened from, which stays behind it dimmed; null when
        /// none is. It has the pad: focus is among its entries, and B closes it, focus going back to the entry that opened it.
        /// </summary>
        public GuideFlyout Flyout
        {
            get => flyout;
            internal set => Set(ref flyout, value, nameof(Flyout));
        }

        /// <summary>The entry with focus, among <see cref="Items"/> or <see cref="Utilities"/>, or the <see cref="Flyout"/>'s entries while it is open; null in an empty flyout. Each item's <see cref="GuideItem.IsFocused"/> follows it.</summary>
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

    /// <summary>
    /// A secondary panel the Guide opens beside its own when an entry is selected, as the console's profile flyout:
    /// the Notifications and Audio utilities open in one.
    /// </summary>
    public sealed class GuideFlyout
    {
        internal GuideFlyout(GuideItemKind kind, string title, IReadOnlyList<GuideItem> items)
        {
            Kind = kind;
            Title = title;
            Items = items;
        }

        /// <summary>The kind of entry that opened it, such as <see cref="GuideItemKind.Notifications"/>.</summary>
        public GuideItemKind Kind { get; }

        /// <summary>The heading along its top.</summary>
        public string Title { get; }

        /// <summary>Its entries, top to bottom.</summary>
        public IReadOnlyList<GuideItem> Items { get; }
    }

    /// <summary>A Guide tab, in the order they sit along its top.</summary>
    public enum GuideTabKind
    {
        Landing,

        /// <summary>The Discord tab, in the console's Parties &amp; chats slot: only there while Discord is connected.</summary>
        Discord,
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

        /// <summary>The landing tab's Notifications utility, whose flyout lists Playnite's notifications.</summary>
        Notifications,

        /// <summary>The landing tab's Audio utility, whose flyout has Windows' master volume and mute.</summary>
        Audio,

        /// <summary>One of Playnite's notifications, in the Notifications flyout.</summary>
        Notification,

        /// <summary>The Audio flyout's volume slider: left and right turn it down and up.</summary>
        Volume,

        /// <summary>The Audio flyout's mute switch: A turns it on and off.</summary>
        Mute,

        /// <summary>The Discord tab's card for the Discord user signed in, by name.</summary>
        DiscordAccount,

        /// <summary>The Discord tab's line saying the user isn't in a voice channel.</summary>
        DiscordIdle,
    }

    /// <summary>One entry on a Guide tab.</summary>
    public sealed class GuideItem : INotifyPropertyChanged
    {
        private bool isFocused;
        private int level;
        private bool isOn;

        internal GuideItem(GuideItemKind kind, string title, Guid? gameId = null, bool hasDividerAbove = false, string image = null, string source = null)
        {
            Source = source;
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

        /// <summary>The game's library source, such as Steam, badged on its cover as on the Home tiles; null for none.</summary>
        public string Source { get; }

        /// <summary>The volume slider's level, 0 to 100.</summary>
        public int Level
        {
            get => level;
            internal set
            {
                if (level == value)
                {
                    return;
                }

                level = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
            }
        }

        /// <summary>Whether the mute switch is on.</summary>
        public bool IsOn
        {
            get => isOn;
            internal set
            {
                if (isOn == value)
                {
                    return;
                }

                isOn = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOn)));
            }
        }

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
