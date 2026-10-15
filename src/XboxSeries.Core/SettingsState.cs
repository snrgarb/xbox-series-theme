using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What the extension's Settings page shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class SettingsState : INotifyPropertyChanged
    {
        private bool isShown;
        private SettingsCategory category;

        public event PropertyChangedEventHandler PropertyChanged;

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

        /// <summary>Where the page was opened from, which B goes back to.</summary>
        public SettingsOrigin OpenedFrom { get; internal set; }

        /// <summary>The Discord category: the Connect checklist, the Client ID and Secret, and the status line.</summary>
        public DiscordSettingsState Discord { get; } = new DiscordSettingsState();

        /// <summary>Personalisation's Start up: the boot video, the profiles, and whether Who's playing today? asks.</summary>
        public StartUpSettingsState StartUp { get; } = new StartUpSettingsState();

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    /// <summary>The Settings rail's categories, in its order. Playnite's own entries below its divider are actions, not categories.</summary>
    public enum SettingsCategory
    {
        /// <summary>Its boxes, Start up's for now: the boot video, the profiles, and whether Who's playing today? asks.</summary>
        Personalisation,
        Guide,
        Discord,
#if DEBUG

        /// <summary>Debug builds only: the Debug menu, below Playnite's entries and a divider of its own, for trying Toasts on the TV.</summary>
        Debug,
#endif
    }

    /// <summary>Where Settings was opened from, which B goes back to.</summary>
    public enum SettingsOrigin
    {
        /// <summary>Home's top bar's Settings button, which gets focus back.</summary>
        TopBar,

        /// <summary>The Guide's Settings entry: B goes to Home.</summary>
        Guide,
    }
}
