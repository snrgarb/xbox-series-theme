using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What the Guide shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class GuideState : INotifyPropertyChanged
    {
        /// <summary>How many tiles a row of the Discord tab's shortcuts holds.</summary>
        public const int DiscordShortcutColumns = 3;

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
        internal GuideFlyout(GuideItemKind kind, string title, IReadOnlyList<GuideItem> items, bool isLoading = false)
        {
            Kind = kind;
            Title = title;
            Items = items;
            IsLoading = isLoading;
        }

        /// <summary>
        /// The kind of entry that opened it, such as <see cref="GuideItemKind.Notifications"/>; a Discord server's
        /// voice channels open from a <see cref="GuideItemKind.DiscordServer"/>, inside the Servers flyout.
        /// </summary>
        public GuideItemKind Kind { get; }

        /// <summary>The heading along its top.</summary>
        public string Title { get; }

        /// <summary>Its entries, top to bottom.</summary>
        public IReadOnlyList<GuideItem> Items { get; }

        /// <summary>Whether its entries are still being asked of Discord: it has none yet, and shows that they're coming.</summary>
        public bool IsLoading { get; }
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

        /// <summary>
        /// The Discord tab's only line while it can't show Discord: "Connecting to Discord" while a connect is under way,
        /// "Discord isn't running" while the extension waits to try it again, and "Reconnect Discord in settings" once
        /// nothing is trying it. A does nothing on it.
        /// </summary>
        DiscordUnavailable,

        /// <summary>
        /// The Discord tab's first entry while a known DM rings: "Answer", with who is calling as its
        /// <see cref="GuideItem.Detail"/> and the DM's avatar as its image. A answers, asking first "Leave X and answer?"
        /// in a call.
        /// </summary>
        DiscordIncomingCall,

        /// <summary>The Discord tab's heading in a call: the channel's name, or a DM's, by the other people in it.</summary>
        DiscordChannel,

        /// <summary>
        /// One person in the call, on the Discord tab: their name and avatar, and whether they are speaking, muted and
        /// deafened. The user comes first.
        /// </summary>
        DiscordParticipant,

        /// <summary>The user's Discord mute, on the Discord tab and in the Audio flyout in a call: A asks Discord to turn it on or off.</summary>
        DiscordMute,

        /// <summary>The user's Discord deafen, on the Discord tab and in the Audio flyout in a call: A asks Discord to turn it on or off.</summary>
        DiscordDeafen,

        /// <summary>The Discord tab's Leave: A asks Discord to leave the call.</summary>
        DiscordLeave,

        /// <summary>
        /// A tile on the Discord tab for a DM or voice channel the user has joined before, by name, with the DM's
        /// avatar or the server's icon, and the server's name as its <see cref="GuideItem.Detail"/>. A asks Discord to join
        /// it; Menu offers Remove shortcut. The tiles sit in rows of <see cref="GuideState.DiscordShortcutColumns"/>.
        /// </summary>
        DiscordShortcut,

        /// <summary>The Discord tab's Servers, last: A opens a flyout of the user's servers.</summary>
        DiscordServers,

        /// <summary>One of the user's servers, by name with its icon, in the Servers flyout: A opens its voice channels.</summary>
        DiscordServer,

        /// <summary>
        /// One of a server's voice channels, by name, in its flyout, with the avatars of the people in it as its
        /// <see cref="GuideItem.Occupants"/>: A asks Discord to join it.
        /// </summary>
        DiscordVoiceChannel,
    }

    /// <summary>One entry on a Guide tab.</summary>
    public sealed class GuideItem : INotifyPropertyChanged
    {
        private bool isFocused;
        private int level;
        private bool isOn;
        private bool isSpeaking;
        private bool isMuted;
        private bool isDeafened;
        private IReadOnlyList<string> occupants;

        internal GuideItem(
            GuideItemKind kind, string title, Guid? gameId = null, bool hasDividerAbove = false, string image = null, string source = null,
            string detail = null, bool isDirectMessage = false)
        {
            Detail = detail;
            IsDirectMessage = isDirectMessage;
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

        /// <summary>
        /// A full path or URL to the picture shown beside the title: a game's cover, the account picture, a Discord
        /// avatar or server icon; null for none.
        /// </summary>
        public string Image { get; }

        /// <summary>The game's library source, such as Steam, badged on its cover as on the Home tiles; null for none.</summary>
        public string Source { get; }

        /// <summary>A second line under the title: the server's name on a voice channel's shortcut; null for none.</summary>
        public string Detail { get; }

        /// <summary>True for a DM's or group DM's shortcut, pictured on a disc; false for a voice channel's and other entries.</summary>
        public bool IsDirectMessage { get; }

        /// <summary>
        /// The avatar URLs of the people in a server's voice channel: null until Discord has said who is there, empty
        /// when nobody is, which shows as Empty.
        /// </summary>
        public IReadOnlyList<string> Occupants
        {
            get => occupants;
            internal set
            {
                if (occupants == value)
                {
                    return;
                }

                occupants = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Occupants)));
            }
        }

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

        /// <summary>Whether the switch is on: Windows' mute, or the user's Discord mute or deafen.</summary>
        public bool IsOn
        {
            get => isOn;
            internal set => Set(ref isOn, value, nameof(IsOn));
        }

        /// <summary>Whether the person in the call is speaking, which rings their avatar.</summary>
        public bool IsSpeaking
        {
            get => isSpeaking;
            internal set => Set(ref isSpeaking, value, nameof(IsSpeaking));
        }

        /// <summary>Whether the person in the call is muted, by themselves or the server.</summary>
        public bool IsMuted
        {
            get => isMuted;
            internal set => Set(ref isMuted, value, nameof(IsMuted));
        }

        /// <summary>Whether the person in the call is deafened, by themselves or the server.</summary>
        public bool IsDeafened
        {
            get => isDeafened;
            internal set => Set(ref isDeafened, value, nameof(IsDeafened));
        }

        /// <summary>The Discord user a participant stands for; null for other entries.</summary>
        internal string UserId { get; set; }

        /// <summary>The Discord channel a shortcut or voice channel stands for; null for other entries.</summary>
        internal string ChannelId { get; set; }

        /// <summary>The Discord server a server entry stands for; null for other entries.</summary>
        internal string GuildId { get; set; }

        /// <summary>
        /// Whether <paramref name="other"/>, built afresh, is this same entry, so that this one can take its state
        /// in place rather than be replaced.
        /// </summary>
        internal bool IsSameEntry(GuideItem other) =>
            Kind == other.Kind && Title == other.Title && GameId == other.GameId && HasDividerAbove == other.HasDividerAbove
            && Image == other.Image && Source == other.Source && Detail == other.Detail && IsDirectMessage == other.IsDirectMessage
            && UserId == other.UserId
            && ChannelId == other.ChannelId && GuildId == other.GuildId;

        /// <summary>Takes the changeable state of <paramref name="other"/>, the same entry built afresh.</summary>
        internal void TakeStateFrom(GuideItem other)
        {
            Level = other.Level;
            IsOn = other.IsOn;
            IsSpeaking = other.IsSpeaking;
            IsMuted = other.IsMuted;
            IsDeafened = other.IsDeafened;
        }

        private void Set(ref bool field, bool value, string name)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>Whether Menu opens a menu on this entry: the running game's, with Quit, and a Discord shortcut's, with Remove shortcut.</summary>
        public bool HasMenu => Kind == GuideItemKind.RunningGame || Kind == GuideItemKind.DiscordShortcut;

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
