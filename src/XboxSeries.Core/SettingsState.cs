using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What the extension's Settings page shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class SettingsState : INotifyPropertyChanged
    {
        private bool isShown;
        private SettingsCategory category;
        private ThemeSettingsPage themePage;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The page was opened, by any route, also while it was up already: the Guide's Settings entry over it can leave
        /// every property as it was, and focus still goes back to the rail.
        /// </summary>
        public event EventHandler Opened;

        /// <summary>Whether the page is up, in place of Home.</summary>
        public bool IsShown
        {
            get => isShown;
            internal set
            {
                if (isShown != value)
                {
                    isShown = value;
                    Changed(nameof(IsShown));
                }
            }
        }

        /// <summary>The rail's category the page is showing beside it.</summary>
        public SettingsCategory Category
        {
            get => category;
            internal set
            {
                if (category != value)
                {
                    category = value;
                    Changed(nameof(Category));
                }
            }
        }

        /// <summary>Theme settings' page showing in the pane: one of its boxes' pages, or <see cref="ThemeSettingsPage.None"/> for the boxes.</summary>
        public ThemeSettingsPage ThemePage
        {
            get => themePage;
            internal set
            {
                if (themePage != value)
                {
                    themePage = value;
                    Changed(nameof(ThemePage));
                }
            }
        }

        /// <summary>
        /// Whether focus goes into the category's options as the page opens, rather than to its rail entry: as Customise
        /// opens it on Theme settings' Personalisation, or a tile under Home's Recent row on Theme settings' Discord or on
        /// Playnite settings. Off again once B leaves that page for Theme settings' boxes.
        /// </summary>
        public bool OpensInPane { get; internal set; }

        /// <summary>Where the page was opened from, which B goes back to.</summary>
        public SettingsOrigin OpenedFrom { get; internal set; }

        /// <summary>
        /// The game whose tile's menu opened the page, by Customise Home or Customise games &amp; apps, which focus goes
        /// back to; null when the top bar or the Guide opened it.
        /// </summary>
        public Guid? OpenedFromGame { get; internal set; }

        /// <summary>Theme settings' Guide page: its behaviour options, the Now Playing source for now.</summary>
        public GuideSettingsState Guide { get; } = new GuideSettingsState();

        /// <summary>Theme settings' Discord page: the Connect checklist, the Client ID and Secret, and the status line.</summary>
        public DiscordSettingsState Discord { get; } = new DiscordSettingsState();

        /// <summary>Theme settings' Personalisation's Start up: the boot video, the profiles, and whether Who's playing today? asks.</summary>
        public StartUpSettingsState StartUp { get; } = new StartUpSettingsState();

        /// <summary>The Playnite settings category: Download metadata, Update after downloads, and Playnite's own window.</summary>
        public PlayniteSettingsState Playnite { get; } = new PlayniteSettingsState();

        internal void RaiseOpened() => Opened?.Invoke(this, EventArgs.Empty);

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    /// <summary>Settings' Playnite settings category.</summary>
    public sealed class PlayniteSettingsState : INotifyPropertyChanged
    {
        private bool updateAfterDownloads = true;
        private bool isDownloadingMetadata;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Whether a game finishing downloading is followed by a library update and metadata for the games it brings,
        /// ending with the Library updated Toast. On by default, saved at once.
        /// </summary>
        public bool UpdateAfterDownloads
        {
            get => updateAfterDownloads;
            internal set
            {
                if (updateAfterDownloads != value)
                {
                    updateAfterDownloads = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateAfterDownloads)));
                }
            }
        }

        /// <summary>Whether the Download metadata box's run is going, which B or Cancel stops.</summary>
        public bool IsDownloadingMetadata
        {
            get => isDownloadingMetadata;
            internal set
            {
                if (isDownloadingMetadata != value)
                {
                    isDownloadingMetadata = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloadingMetadata)));
                }
            }
        }
    }

    /// <summary>Theme settings' Guide page, shared by every profile.</summary>
    public sealed class GuideSettingsState : INotifyPropertyChanged
    {
        /// <summary>The Now Playing source that follows Windows' current media session.</summary>
        public const string AnySource = "Any";

        /// <summary>The Now Playing source chosen at first.</summary>
        public const string DefaultSource = "Spotify";

        private string nowPlayingSource = DefaultSource;
        private IReadOnlyList<string> nowPlayingSources = new[] { AnySource, DefaultSource };

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The app, as people know it, whose media session the Audio flyout's Now Playing card follows while it has one,
        /// else Windows' current; <see cref="AnySource"/> for Windows' current always.
        /// </summary>
        public string NowPlayingSource
        {
            get => nowPlayingSource;
            internal set
            {
                if (nowPlayingSource != value)
                {
                    nowPlayingSource = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NowPlayingSource)));
                }
            }
        }

        /// <summary>
        /// The dropdown's choices: <see cref="AnySource"/>, Spotify, then every other app that has had a media session
        /// this run, by the name people know it by.
        /// </summary>
        public IReadOnlyList<string> NowPlayingSources
        {
            get => nowPlayingSources;
            internal set
            {
                if (nowPlayingSources != value)
                {
                    nowPlayingSources = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NowPlayingSources)));
                }
            }
        }
    }

    /// <summary>
    /// The Settings rail's categories, in its order: Theme settings and Playnite settings are each a category of boxes,
    /// and More Playnite options, below them, an action.
    /// </summary>
    public enum SettingsCategory
    {
        /// <summary>Its boxes, Personalisation, Guide and Discord, each opening its <see cref="ThemeSettingsPage"/> in the pane.</summary>
        ThemeSettings,

        /// <summary>Its boxes: Download metadata, Update after downloads, and one opening Playnite's own Settings window.</summary>
        PlayniteSettings,
#if DEBUG

        /// <summary>Debug builds only: the Debug menu, below Playnite's entries and a divider of its own, for trying Toasts on the TV.</summary>
        Debug,
#endif
    }

    /// <summary>
    /// Theme settings' pages, one for each of its boxes, which A on the box opens in the pane and B leaves for the boxes
    /// again. Personalisation's own Start up page is one level further in, the view's own.
    /// </summary>
    public enum ThemeSettingsPage
    {
        /// <summary>The boxes themselves.</summary>
        None,

        /// <summary>Its boxes, Start up's for now: the boot video, the profiles, and whether Who's playing today? asks.</summary>
        Personalisation,

        /// <summary>The Guide's behaviour options, the Now Playing source for now.</summary>
        Guide,

        /// <summary>The Connect checklist, the Client ID and Secret, and the status line.</summary>
        Discord,
    }

    /// <summary>Where Settings was opened from, which B goes back to.</summary>
    public enum SettingsOrigin
    {
        /// <summary>Home's top bar's Settings button, which gets focus back.</summary>
        TopBar,

        /// <summary>The Guide's Settings entry: B goes to Home.</summary>
        Guide,

        /// <summary>
        /// The tile menu's Customise Home, on a Home tile, or a tile under the Recent row: B, once back on Theme settings'
        /// boxes, goes back to Home, its rows focusing that tile again.
        /// </summary>
        HomeTile,

        /// <summary>
        /// The tile menu's Customise games &amp; apps, on a tile of My games &amp; apps: B, once back on Theme settings'
        /// boxes, opens the page again on the item it showed, focus on that tile.
        /// </summary>
        MyGamesTile,
    }
}
