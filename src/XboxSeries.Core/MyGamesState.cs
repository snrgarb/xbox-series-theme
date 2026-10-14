using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>What the extension's My games &amp; apps page shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class MyGamesState : INotifyPropertyChanged
    {
        private bool isShown;
        private MyGamesItem item;
        private IReadOnlyList<PlayHistorySection> playHistory = new PlayHistorySection[0];
        private int gameCount;
        private int? fullLibraryCount;
        private StorageRing storage;

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
                    Changed(nameof(ShowsFullLibrary));
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
                    Changed(nameof(ShowsFullLibrary));
                }
            }
        }

        /// <summary>Whether Playnite's main list shows beside the nav as the page's grid of games.</summary>
        public bool ShowsGameGrid => IsShown && Item != MyGamesItem.PlayHistory;

        /// <summary>
        /// Whether the page shows Full library: the main list under the user's own filter, with their filter presets as
        /// tabs that Playnite's LB/RB switch. Elsewhere on the page LB/RB do nothing, as they would change the grid.
        /// </summary>
        public bool ShowsFullLibrary => IsShown && Item == MyGamesItem.FullLibrary;

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
                Changed(nameof(PlayHistoryCount));
            }
        }

        /// <summary>How many games Play history lists, for the nav.</summary>
        public int PlayHistoryCount => PlayHistory.Sum(s => s.Entries.Count);

        /// <summary>How many games Full library shows under the user's own filter, for the nav and the header; null when unknown.</summary>
        public int? FullLibraryCount
        {
            get => fullLibraryCount;
            internal set
            {
                if (fullLibraryCount != value)
                {
                    fullLibraryCount = value;
                    Changed(nameof(FullLibraryCount));
                }
            }
        }

        /// <summary>The nav's storage ring: the drive holding most installed games; null when there is none to show.</summary>
        public StorageRing Storage
        {
            get => storage;
            internal set
            {
                storage = value;
                Changed(nameof(Storage));
            }
        }

        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// The space on the drive holding most installed games, as the console's storage ring shows its storage: the share
    /// used, round the ring and as a percentage inside it, and the space free.
    /// </summary>
    public sealed class StorageRing
    {
        private StorageRing(double usedFraction, string usedPercent, string free)
        {
            UsedFraction = usedFraction;
            UsedPercent = usedPercent;
            Free = free;
        }

        /// <summary>The ring for a drive's space; null when it couldn't be read.</summary>
        internal static StorageRing Of(DriveSpace space)
        {
            if (space == null || space.TotalBytes <= 0)
            {
                return null;
            }

            var used = (double)(space.TotalBytes - space.FreeBytes) / space.TotalBytes;
            var culture = CultureInfo.CurrentCulture;
            return new StorageRing(used, (used * 100).ToString("0.0", culture) + "%", Size(space.FreeBytes) + " free");
        }

        /// <summary>A size as the console shows it, to a tenth: in GB, in MB below one, in TB from 1,024 GB up.</summary>
        private static string Size(long bytes)
        {
            const double GB = 1L << 30;
            var culture = CultureInfo.CurrentCulture;
            var gb = Math.Round(bytes / GB, 1);
            return gb >= 1024 ? (bytes / GB / 1024).ToString("0.0", culture) + " TB"
                : gb >= 1 ? gb.ToString("0.0", culture) + " GB"
                : (bytes / (GB / 1024)).ToString("0.0", culture) + " MB";
        }

        /// <summary>How much of the drive is used, from 0 to 1: how far round the ring fills.</summary>
        public double UsedFraction { get; }

        /// <summary>The share used, inside the ring, such as "55.8%".</summary>
        public string UsedPercent { get; }

        /// <summary>The space free, such as "354.6 GB free".</summary>
        public string Free { get; }
    }

    /// <summary>The items of My games &amp; apps' left nav.</summary>
    public enum MyGamesItem
    {
        Games,
        PlayHistory,
        FullLibrary,
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
