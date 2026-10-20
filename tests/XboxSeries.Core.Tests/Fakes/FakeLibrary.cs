using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>A Playnite library built up by a test, then handed to the core as a <see cref="LibrarySnapshot"/>.</summary>
    public sealed class FakeLibrary
    {
        private readonly List<LibraryGame> games = new List<LibraryGame>();
        private readonly List<FilterPreset> filterPresets = new List<FilterPreset>();
        private readonly List<string[]> presetGames = new List<string[]>();
        private readonly List<App> apps = new List<App>();
        private bool appsAvailable = true;

        public FakeLibrary WithGame(
            string name,
            DateTime? lastActivity = null,
            DateTime? added = null,
            bool hidden = false,
            bool installed = true,
            string[] series = null,
            string[] genres = null,
            string source = null,
            string installDirectory = null,
            string backgroundImage = null,
            string coverImage = null,
            bool favorite = false,
            ulong playtime = 0,
            ulong playCount = 0,
            DateTime? releaseDate = null,
            DateTime? modified = null,
            string sortingName = null,
            string platform = null,
            bool installing = false,
            DateTime? installedAt = null,
            bool custom = false,
            string[] otherActions = null,
            bool hdr = false) =>
            WithGame(Guid.NewGuid(), name, lastActivity, added, hidden, installed, series, genres, source, installDirectory, backgroundImage, coverImage,
                favorite, playtime, playCount, releaseDate, modified, sortingName, platform, installing, installedAt, custom, otherActions, hdr);

        /// <summary>A game with a known id, to stand for the same game in a later snapshot.</summary>
        public FakeLibrary WithGame(
            Guid id,
            string name,
            DateTime? lastActivity = null,
            DateTime? added = null,
            bool hidden = false,
            bool installed = true,
            string[] series = null,
            string[] genres = null,
            string source = null,
            string installDirectory = null,
            string backgroundImage = null,
            string coverImage = null,
            bool favorite = false,
            ulong playtime = 0,
            ulong playCount = 0,
            DateTime? releaseDate = null,
            DateTime? modified = null,
            string sortingName = null,
            string platform = null,
            bool installing = false,
            DateTime? installedAt = null,
            bool custom = false,
            string[] otherActions = null,
            bool hdr = false)
        {
            games.Add(new LibraryGame(
                id,
                name,
                lastActivity,
                added,
                hidden,
                installed,
                series ?? new string[0],
                genres ?? new string[0],
                source,
                installDirectory,
                backgroundImage,
                coverImage,
                favorite,
                playtime,
                playCount,
                releaseDate,
                modified,
                sortingName,
                platform,
                installing,
                installedAt,
                custom,
                otherActions,
                hdr));
            return this;
        }

        /// <summary>
        /// A filter preset matching the games named in <paramref name="games"/>, as Playnite evaluates its filter,
        /// in no particular order. Presets reach the core in the order they are added, standing for Playnite's order.
        /// </summary>
        public FakeLibrary WithFilterPreset(
            string name,
            bool showInFullscreenQuickSelection = false,
            string[] games = null,
            GameSort? sort = null,
            SortDirection direction = SortDirection.Ascending)
        {
            presetGames.Add(games ?? new string[0]);
            filterPresets.Add(new FilterPreset(Guid.NewGuid(), name, showInFullscreenQuickSelection, new Guid[0], sort, direction));
            return this;
        }

        public FakeLibrary WithApp(string name, string path, string arguments = null, string icon = null)
        {
            apps.Add(new App(Guid.NewGuid(), name, path, arguments, icon));
            return this;
        }

        /// <summary>Apps with known ids, to stand for the same Apps in a later snapshot.</summary>
        public FakeLibrary WithApps(params App[] known)
        {
            apps.AddRange(known);
            return this;
        }

        /// <summary>Simulates a Playnite build where reading Software tools fails.</summary>
        public FakeLibrary WithoutApps()
        {
            appsAvailable = false;
            return this;
        }

        /// <summary>The id of the game called <paramref name="name"/>.</summary>
        public Guid Id(string name) => games.Single(g => g.Name == name).Id;

        /// <summary>The id of the filter preset called <paramref name="name"/>.</summary>
        public Guid PresetId(string name) => filterPresets.Single(p => p.Name == name).Id;

        public LibrarySnapshot Snapshot() =>
            new LibrarySnapshot(
                games.ToList(),
                filterPresets
                    .Select((p, i) => new FilterPreset(
                        p.Id, p.Name, p.ShowInFullscreenQuickSelection, presetGames[i].Select(Id).ToList(), p.Sort, p.SortDirection))
                    .ToList(),
                appsAvailable ? apps.ToList() : null);
    }
}
