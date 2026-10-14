using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>The parts of the Playnite library the core works from, captured at one moment.</summary>
    public sealed class LibrarySnapshot
    {
        public static readonly LibrarySnapshot Empty =
            new LibrarySnapshot(new LibraryGame[0], new FilterPreset[0], new App[0]);

        public LibrarySnapshot(
            IReadOnlyList<LibraryGame> games,
            IReadOnlyList<FilterPreset> filterPresets,
            IReadOnlyList<App> apps)
        {
            Games = games;
            FilterPresets = filterPresets;
            Apps = apps;
        }

        public IReadOnlyList<LibraryGame> Games { get; }
        public IReadOnlyList<FilterPreset> FilterPresets { get; }

        /// <summary>The Apps (Playnite's Software tools); null when they couldn't be read on this Playnite build.</summary>
        public IReadOnlyList<App> Apps { get; }
    }

    public sealed class LibraryGame
    {
        public LibraryGame(
            Guid id,
            string name,
            DateTime? lastActivity,
            DateTime? added,
            bool hidden,
            bool installed,
            IReadOnlyList<string> series,
            IReadOnlyList<string> genres,
            string source,
            string installDirectory,
            string backgroundImage = null,
            string coverImage = null,
            bool favorite = false,
            ulong playtime = 0,
            ulong playCount = 0,
            DateTime? releaseDate = null,
            DateTime? modified = null,
            string sortingName = null)
        {
            Id = id;
            Name = name;
            LastActivity = lastActivity;
            Added = added;
            Hidden = hidden;
            Installed = installed;
            Series = series;
            Genres = genres;
            Source = source;
            InstallDirectory = installDirectory;
            BackgroundImage = backgroundImage;
            CoverImage = coverImage;
            Favorite = favorite;
            Playtime = playtime;
            PlayCount = playCount;
            ReleaseDate = releaseDate;
            Modified = modified;
            SortingName = string.IsNullOrEmpty(sortingName) ? name : sortingName;
        }

        public Guid Id { get; }
        public string Name { get; }
        public DateTime? LastActivity { get; }
        public DateTime? Added { get; }
        public bool Hidden { get; }
        public bool Installed { get; }
        public IReadOnlyList<string> Series { get; }
        public IReadOnlyList<string> Genres { get; }

        /// <summary>The library source (Steam, Xbox, ...), or null for games without one.</summary>
        public string Source { get; }

        /// <summary>Where the game is installed, with Playnite's variables expanded; null when unknown.</summary>
        public string InstallDirectory { get; }

        /// <summary>The full path or URL of the game's background art; null when it has none.</summary>
        public string BackgroundImage { get; }

        /// <summary>The full path or URL of the game's cover art; null when it has none.</summary>
        public string CoverImage { get; }

        public bool Favorite { get; }

        /// <summary>Total time played, in seconds.</summary>
        public ulong Playtime { get; }

        public ulong PlayCount { get; }
        public DateTime? ReleaseDate { get; }

        /// <summary>When the game's data was last changed in Playnite.</summary>
        public DateTime? Modified { get; }

        /// <summary>The name Playnite sorts the game by: its sorting name when it has one, otherwise its name.</summary>
        public string SortingName { get; }
    }

    /// <summary>A Playnite filter preset. Snapshots list them in Playnite's own order.</summary>
    public sealed class FilterPreset
    {
        public FilterPreset(
            Guid id,
            string name,
            bool showInFullscreenQuickSelection,
            IReadOnlyList<Guid> games = null,
            GameSort? sort = null,
            SortDirection sortDirection = SortDirection.Ascending)
        {
            Id = id;
            Name = name;
            ShowInFullscreenQuickSelection = showInFullscreenQuickSelection;
            Games = games ?? new Guid[0];
            Sort = sort;
            SortDirection = sortDirection;
        }

        public Guid Id { get; }
        public string Name { get; }

        /// <summary>The Playnite flag that makes this preset a Group on Home.</summary>
        public bool ShowInFullscreenQuickSelection { get; }

        /// <summary>The games the preset's filter matches, as Playnite evaluates it, in no particular order.</summary>
        public IReadOnlyList<Guid> Games { get; }

        /// <summary>The preset's own sort; null when it has none, or one the core doesn't support, so name order applies.</summary>
        public GameSort? Sort { get; }

        public SortDirection SortDirection { get; }
    }

    /// <summary>The fields a Group can be sorted by: those of Playnite's sort orders the core supports.</summary>
    public enum GameSort
    {
        Name,
        LastActivity,
        Added,
        Modified,
        ReleaseDate,
        Playtime,
        PlayCount,
    }

    public enum SortDirection
    {
        Ascending,
        Descending,
    }

    /// <summary>One of Playnite's Software tools, shown under My games &amp; apps. Not a game.</summary>
    public sealed class App
    {
        public App(Guid id, string name, string path, string arguments)
        {
            Id = id;
            Name = name;
            Path = path;
            Arguments = arguments;
        }

        public Guid Id { get; }
        public string Name { get; }
        public string Path { get; }
        public string Arguments { get; }
    }
}
