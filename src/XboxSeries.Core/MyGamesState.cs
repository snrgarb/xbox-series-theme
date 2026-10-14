using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What the extension's My games &amp; apps page shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class MyGamesState : INotifyPropertyChanged
    {
        private bool isShown;
        private MyGamesItem item;
        private IReadOnlyList<PlayHistorySection> playHistory = new PlayHistorySection[0];
        private int gameCount;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Whether the page is up, in place of Home's rows.</summary>
        public bool IsShown
        {
            get => isShown;
            internal set
            {
                if (isShown != value)
                {
                    isShown = value;
                    Changed(nameof(IsShown));
                    Changed(nameof(ShowsGameGrid));
                }
            }
        }

        /// <summary>The left nav's item the page is showing.</summary>
        public MyGamesItem Item
        {
            get => item;
            internal set
            {
                if (item != value)
                {
                    item = value;
                    Changed(nameof(Item));
                    Changed(nameof(ShowsGameGrid));
                }
            }
        }

        /// <summary>Whether Playnite's main list shows beside the nav as the page's grid of games.</summary>
        public bool ShowsGameGrid => IsShown && Item == MyGamesItem.Games;

        /// <summary>How many games Games shows, for the nav and the page's header.</summary>
        public int GameCount
        {
            get => gameCount;
            internal set
            {
                if (gameCount != value)
                {
                    gameCount = value;
                    Changed(nameof(GameCount));
                }
            }
        }

        /// <summary>Play history: played games, newest first, in sections by when they were last played.</summary>
        public IReadOnlyList<PlayHistorySection> PlayHistory
        {
            get => playHistory;
            internal set
            {
                playHistory = value;
                Changed(nameof(PlayHistory));
            }
        }

        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>The items of My games &amp; apps' left nav.</summary>
    public enum MyGamesItem
    {
        Games,
        PlayHistory,
    }

    /// <summary>One section of Play history: Today, Yesterday, This week and so on.</summary>
    public sealed class PlayHistorySection
    {
        internal PlayHistorySection(string title, IReadOnlyList<PlayHistoryEntry> entries)
        {
            Title = title;
            Entries = entries;
        }

        public string Title { get; }
        public IReadOnlyList<PlayHistoryEntry> Entries { get; }
    }

    /// <summary>One game in Play history. Its tile takes focus, A and Menu as a Home tile does.</summary>
    public sealed class PlayHistoryEntry
    {
        internal PlayHistoryEntry(HomeTile tile, DateTime lastPlayed, string platform)
        {
            Tile = tile;
            LastPlayed = lastPlayed;
            Platform = platform;
        }

        public HomeTile Tile { get; }
        public DateTime LastPlayed { get; }

        /// <summary>The game's platform; null for games without one.</summary>
        public string Platform { get; }
    }
}
