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
            string coverImage = null)
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
    }

    public sealed class FilterPreset
    {
        public FilterPreset(Guid id, string name, bool showInFullscreenQuickSelection)
        {
            Id = id;
            Name = name;
            ShowInFullscreenQuickSelection = showInFullscreenQuickSelection;
        }

        public Guid Id { get; }
        public string Name { get; }

        /// <summary>The Playnite flag that makes this preset a Group on Home.</summary>
        public bool ShowInFullscreenQuickSelection { get; }
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
