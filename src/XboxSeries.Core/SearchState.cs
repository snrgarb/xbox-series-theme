using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What the extension's Search page shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class SearchState : INotifyPropertyChanged
    {
        private bool isShown;
        private bool isKeyboardShown;
        private IReadOnlyList<HomeTile> recentlyAdded = new HomeTile[0];
        private IReadOnlyList<HomeTile> games = new HomeTile[0];
        private IReadOnlyList<App> apps = new App[0];

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

        /// <summary>Where the page was opened from, which B goes back to.</summary>
        public SearchOrigin OpenedFrom { get; internal set; }

        /// <summary>Whether the console keyboard is up, with the pad: B puts it away before it leaves the page.</summary>
        public bool IsKeyboardShown
        {
            get => isKeyboardShown;
            internal set
            {
                if (isKeyboardShown != value)
                {
                    isKeyboardShown = value;
                    Changed(nameof(IsKeyboardShown));
                }
            }
        }

        /// <summary>The console keyboard, whose text is what the page searches for.</summary>
        public ConsoleKeyboard Keyboard { get; } = new ConsoleKeyboard();

        /// <summary>With nothing typed: the games most recently added to the library, newest first, never hidden ones.</summary>
        public IReadOnlyList<HomeTile> RecentlyAdded
        {
            get => recentlyAdded;
            internal set
            {
                recentlyAdded = value;
                Changed(nameof(RecentlyAdded));
            }
        }

        /// <summary>The games whose name holds the text typed, never hidden ones; empty with nothing typed.</summary>
        public IReadOnlyList<HomeTile> Games
        {
            get => games;
            internal set
            {
                games = value;
                Changed(nameof(Games));
                Changed(nameof(ShowsGames));
            }
        }

        /// <summary>The Apps whose name holds the text typed; empty with nothing typed, or without Apps.</summary>
        public IReadOnlyList<App> Apps
        {
            get => apps;
            internal set
            {
                apps = value;
                Changed(nameof(Apps));
                Changed(nameof(ShowsApps));
            }
        }

        /// <summary>Whether the recently added row shows: with nothing typed.</summary>
        public bool ShowsRecentlyAdded { get; private set; } = true;

        /// <summary>Whether the Games row shows: something typed, and games found.</summary>
        public bool ShowsGames => Games.Count > 0;

        /// <summary>Whether the Apps row shows: something typed, and Apps found.</summary>
        public bool ShowsApps => Apps.Count > 0;

        /// <summary>Sets <see cref="ShowsRecentlyAdded"/> from whether anything is typed.</summary>
        internal void SetHasText(bool hasText)
        {
            if (ShowsRecentlyAdded == hasText)
            {
                ShowsRecentlyAdded = !hasText;
                Changed(nameof(ShowsRecentlyAdded));
            }
        }

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    /// <summary>Where Search was opened from, which B goes back to.</summary>
    public enum SearchOrigin
    {
        /// <summary>Home's top bar's Search button, which gets focus back.</summary>
        TopBar,

        /// <summary>The Guide's Search utility: B goes to Home.</summary>
        Guide,
    }
}
