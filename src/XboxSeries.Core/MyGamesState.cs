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

    /// <summary>The space on one drive, as the storage ring shows it.</summary>
    public sealed class StorageRing
    {
        private StorageRing(string drive, double usedFraction, string summary)
        {
            Drive = drive;
            UsedFraction = usedFraction;
            Summary = summary;
        }

        /// <summary>The ring for the drive at <paramref name="root"/>, such as C:\; null when its space couldn't be read.</summary>
        internal static StorageRing Of(string root, DriveSpace space) =>
            space == null || space.TotalBytes <= 0
                ? null
                : new StorageRing(
                    root.Substring(0, 2),
                    (double)(space.TotalBytes - space.FreeBytes) / space.TotalBytes,
                    $"{Size(space.FreeBytes)} free of {Size(space.TotalBytes)}");

        /// <summary>A size as Windows shows it: whole GB (or MB below one), tenths of a TB from 1,024 GB up.</summary>
        private static string Size(long bytes)
        {
            const double GB = 1L << 30;
            var culture = CultureInfo.CurrentCulture;
            var gb = Math.Round(bytes / GB);
            return gb >= 1024 ? (bytes / GB / 1024).ToString("0.#", culture) + " TB"
                : gb >= 1 ? gb.ToString("0", culture) + " GB"
                : Math.Round(bytes / (GB / 1024)).ToString("0", culture) + " MB";
        }

        /// <summary>The drive's name, such as C:.</summary>
        public string Drive { get; }

        /// <summary>How much of the drive is used, from 0 to 1: how far round the ring fills.</summary>
        public double UsedFraction { get; }

        /// <summary>Its free and total space, such as "412 GB free of 1.8 TB".</summary>
        public string Summary { get; }
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
