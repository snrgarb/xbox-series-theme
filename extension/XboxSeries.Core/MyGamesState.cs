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
        private IReadOnlyList<App> apps = new App[0];
        private bool hasApps;
        private IReadOnlyList<QueueEntry> queue = new QueueEntry[0];
        private IReadOnlyList<HomeTile> recentlyInstalled = new HomeTile[0];
        private LibrarySortOption librarySort;
        private string nameFilter = string.Empty;

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

        /// <summary>
        /// The game whose tile takes focus as the page opens: the one whose tile menu opened Settings, by Customise games
        /// &amp; apps, B coming back from it. Null when the page opens any other way, starting at its first tile.
        /// </summary>
        public Guid? ReturnsToGame { get; internal set; }

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

        /// <summary>
        /// The game whose tile focus goes back to once the Game hub over the page closes, the page having changed item
        /// behind the hub (B on Manage Queue opened from it); null otherwise. Taken once (<see cref="TakeTileBehindHub"/>).
        /// </summary>
        private Guid? tileBehindHub;

        /// <summary>
        /// Puts the page back on <paramref name="value"/> behind a Game hub opened over it: focus, which the hub has, stays
        /// there, and goes to <paramref name="tile"/>'s tile once the hub closes, rather than the item's first.
        /// </summary>
        internal void SetItemBehindHub(MyGamesItem value, Guid? tile)
        {
            tileBehindHub = tile;
            Item = value;
        }

        /// <summary>
        /// The game whose tile takes focus as the page shows again from behind a Game hub (<see cref="SetItemBehindHub"/>),
        /// once; null for none, the page then taking focus as it would.
        /// </summary>
        public Guid? TakeTileBehindHub()
        {
            var tile = tileBehindHub;
            tileBehindHub = null;
            return tile;
        }

        /// <summary>Whether Playnite's main list shows beside the nav as the page's grid of games. Play history, Apps, Manage and Manage Queue show their own.</summary>
        public bool ShowsGameGrid => IsShown && (Item == MyGamesItem.Games || Item == MyGamesItem.FullLibrary);

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

        /// <summary>Full library's Sort dropdown: its options, in the console's order.</summary>
        public IReadOnlyList<LibrarySortOption> SortOptions => LibrarySortOption.All;

        /// <summary>
        /// The sort Full library's grid is in, as its Sort dropdown names it: the one the user chose there, or else
        /// Playnite's own sort of the main list; null when that is none of the dropdown's options.
        /// </summary>
        public LibrarySortOption LibrarySort
        {
            get => librarySort;
            internal set
            {
                if (librarySort != value)
                {
                    librarySort = value;
                    Changed(nameof(LibrarySort));
                    Changed(nameof(SortLabel));
                }
            }
        }

        /// <summary>The Sort dropdown's own text, such as "Sorted by A–Z"; just "Sort" while the grid is in none of its options.</summary>
        public string SortLabel => LibrarySort == null ? "Sort" : "Sorted by " + LibrarySort.Name;

        /// <summary>
        /// The name Full library's grid is narrowed to, on top of the user's tab: Playnite's own name filter of the main
        /// list. Empty when it shows the whole tab.
        /// </summary>
        public string NameFilter
        {
            get => nameFilter;
            internal set
            {
                value = value ?? string.Empty;
                if (nameFilter != value)
                {
                    nameFilter = value;
                    Changed(nameof(NameFilter));
                    Changed(nameof(HasNameFilter));
                }
            }
        }

        /// <summary>Whether Full library's grid is narrowed by name.</summary>
        public bool HasNameFilter => NameFilter.Length > 0;

        /// <summary>Apps: Playnite's Software tools, as tiles in Playnite's order; empty when they couldn't be read.</summary>
        public IReadOnlyList<App> Apps
        {
            get => apps;
            internal set
            {
                apps = value;
                Changed(nameof(Apps));
            }
        }

        /// <summary>Whether the nav shows Apps: false when Software tools couldn't be read on this Playnite build.</summary>
        public bool HasApps
        {
            get => hasApps;
            internal set
            {
                if (hasApps != value)
                {
                    hasApps = value;
                    Changed(nameof(HasApps));
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

        /// <summary>
        /// The queue: every game Playnite is installing, the current download first, then those next up in the order
        /// they started installing. Empty when nothing is installing.
        /// </summary>
        public IReadOnlyList<QueueEntry> Queue
        {
            get => queue;
            internal set
            {
                queue = value;
                Changed(nameof(Queue));
                Changed(nameof(QueueCount));
                Changed(nameof(HasQueue));
                Changed(nameof(CurrentDownload));
            }
        }

        /// <summary>How many games the queue holds, for Manage's Queue box and the nav's Queue line.</summary>
        public int QueueCount => Queue.Count;

        /// <summary>Whether anything is queued: the nav shows its Queue line, Manage Queue its Installing section.</summary>
        public bool HasQueue => Queue.Count > 0;

        /// <summary>The download shown above All storage: the queue's first while it is downloading; null otherwise.</summary>
        public QueueEntry CurrentDownload => Queue.Count > 0 && Queue[0].IsCurrent ? Queue[0] : null;

        /// <summary>
        /// Manage Queue's Recently installed row: up to 10 installed games, those seen to finish installing since Playnite
        /// started first, the latest first, then the rest by when they were added.
        /// </summary>
        public IReadOnlyList<HomeTile> RecentlyInstalled
        {
            get => recentlyInstalled;
            internal set
            {
                recentlyInstalled = value;
                Changed(nameof(RecentlyInstalled));
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
            return new StorageRing(used, (used * 100).ToString("0.0", culture) + "%", Size(space.FreeBytes, 1) + " free");
        }

        /// <summary>
        /// A size as the console shows it, to <paramref name="decimals"/> places: in GB, in MB below one, in TB from
        /// 1,024 GB up. The unit goes by the size as rounded, so it never reads "1024.0 GB". The storage ring shows a
        /// tenth; a download's amount, two places.
        /// </summary>
        internal static string Size(long bytes, int decimals)
        {
            const double GB = 1L << 30;
            var culture = CultureInfo.CurrentCulture;
            var format = "0." + new string('0', decimals);
            var gb = Math.Round(bytes / GB, decimals);
            return gb >= 1024 ? (bytes / GB / 1024).ToString(format, culture) + " TB"
                : gb >= 1 ? gb.ToString(format, culture) + " GB"
                : (bytes / (GB / 1024)).ToString(format, culture) + " MB";
        }

        /// <summary>How much of the drive is used, from 0 to 1: how far round the ring fills.</summary>
        public double UsedFraction { get; }

        /// <summary>The share used, inside the ring, such as "55.8%".</summary>
        public string UsedPercent { get; }

        /// <summary>The space free, such as "354.6 GB free".</summary>
        public string Free { get; }
    }

    /// <summary>
    /// One game in the queue, as Manage Queue's row and the nav's block above All storage show it. Its tile takes focus,
    /// A and Menu as a Home tile does; View opens its Tile menu too.
    /// </summary>
    public sealed class QueueEntry
    {
        internal QueueEntry(HomeTile tile, bool isCurrent, string state, string speed, string amount, string percent, double fraction)
        {
            Tile = tile;
            IsCurrent = isCurrent;
            State = state;
            Speed = speed;
            Amount = amount;
            Percent = percent;
            Fraction = fraction;
        }

        public HomeTile Tile { get; }

        /// <summary>Whether this is the current download, rather than one next up.</summary>
        public bool IsCurrent { get; }

        /// <summary>"Installing..." or "Queued".</summary>
        public string State { get; }

        /// <summary>
        /// The download's speed, such as "106 Mbps" or "323 Kbps"; null when queued, when the library can't say, or until
        /// its bytes have first moved.
        /// </summary>
        public string Speed { get; }

        /// <summary>
        /// How much is down of the whole, such as "67.80 MB of 3.55 GB"; null when queued, when the library can't say, or
        /// while its client doesn't know the whole yet.
        /// </summary>
        public string Amount { get; }

        /// <summary>The share down, such as "1.9%" ("0.0%" when queued); null when the library can't say.</summary>
        public string Percent { get; }

        /// <summary>How far the bar fills, from 0 to 1.</summary>
        public double Fraction { get; }
    }

    /// <summary>
    /// One option of Full library's Sort dropdown: a sort of Playnite's own, the newest or most first except A–Z. Games
    /// without the field, never played or with no release date, come last.
    /// </summary>
    public sealed class LibrarySortOption
    {
        public static readonly LibrarySortOption AToZ = new LibrarySortOption("A–Z", GameSort.Name, SortDirection.Ascending);
        public static readonly LibrarySortOption LastPlayed = new LibrarySortOption("Last played", GameSort.LastActivity, SortDirection.Descending);
        public static readonly LibrarySortOption RecentlyAdded = new LibrarySortOption("Recently added", GameSort.Added, SortDirection.Descending);
        public static readonly LibrarySortOption Playtime = new LibrarySortOption("Playtime", GameSort.Playtime, SortDirection.Descending);
        public static readonly LibrarySortOption ReleaseDate = new LibrarySortOption("Release date", GameSort.ReleaseDate, SortDirection.Descending);

        /// <summary>Every option, in the dropdown's order.</summary>
        public static readonly IReadOnlyList<LibrarySortOption> All = new[] { AToZ, LastPlayed, RecentlyAdded, Playtime, ReleaseDate };

        private LibrarySortOption(string name, GameSort sort, SortDirection direction)
        {
            Name = name;
            Sort = sort;
            Direction = direction;
        }

        /// <summary>The option's name, such as "Last played".</summary>
        public string Name { get; }

        public GameSort Sort { get; }

        public SortDirection Direction { get; }

        /// <summary>The option for Playnite's sort of the main list; null when it is none of them.</summary>
        internal static LibrarySortOption Of(GameSort? sort, SortDirection direction) =>
            All.FirstOrDefault(o => o.Sort == sort && o.Direction == direction);

        public override string ToString() => Name;
    }

    /// <summary>The items of My games &amp; apps' left nav.</summary>
    public enum MyGamesItem
    {
        Games,
        Apps,
        PlayHistory,
        FullLibrary,

        /// <summary>Manage, below the nav's line: its Queue box.</summary>
        Manage,

        /// <summary>Manage Queue, opened from Manage's Queue box: Recently installed, then the queue's rows.</summary>
        ManageQueue,
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
