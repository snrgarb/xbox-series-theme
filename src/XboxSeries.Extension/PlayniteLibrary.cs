using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Models;
using XboxSeries.Core;
using FilterPreset = XboxSeries.Core.FilterPreset;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: reads Playnite's database into the core's <see cref="LibrarySnapshot"/>.</summary>
    internal static class PlayniteLibrary
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static LibrarySnapshot Snapshot(IPlayniteAPI api)
        {
            var database = api.Database;
            var games = database.Games
                .Select(g => new LibraryGame(
                    g.Id,
                    g.Name,
                    g.LastActivity,
                    g.Added,
                    g.Hidden,
                    g.IsInstalled,
                    g.Series?.Select(s => s.Name).ToList() ?? new List<string>(),
                    g.Genres?.Select(s => s.Name).ToList() ?? new List<string>(),
                    g.Source?.Name,
                    string.IsNullOrEmpty(g.InstallDirectory) ? null : api.ExpandGameVariables(g, g.InstallDirectory),
                    FullPath(database, g.BackgroundImage),
                    FullPath(database, g.CoverImage),
                    g.Favorite,
                    g.Playtime,
                    g.PlayCount,
                    g.ReleaseDate?.Date,
                    g.Modified,
                    g.SortingName,
                    g.Platforms?.FirstOrDefault()?.Name))
                .ToList();

            // In Playnite's own order, each with the games its filter matches, as Playnite's own lists evaluate it.
            var presets = SortedFilterPresets(api)
                .Select(p => new FilterPreset(
                    p.Id,
                    p.Name,
                    p.ShowInFullscreeQuickSelection,
                    p.Settings == null ? new List<Guid>() : database.GetFilteredGames(p.Settings).Select(g => g.Id).ToList(),
                    p.SortingOrder == null ? null : ToGameSort(p.SortingOrder.Value),
                    p.SortingOrderDirection == SortOrderDirection.Descending ? SortDirection.Descending : SortDirection.Ascending))
                .ToList();

            // In the order of Playnite's own Software tools menu: by name. Null, which hides Apps, when they can't be read.
            var apps = SoftwareTools()?
                .OrderBy(a => a.Name)
                .Select(a => new App(
                    a.Id,
                    a.Name,
                    ExpandVariables(api, a.Path),
                    ExpandVariables(api, a.Arguments),
                    FullPath(database, a.Icon),
                    ExpandVariables(api, a.WorkingDir)))
                .ToList();

            return new LibrarySnapshot(games, presets, apps);
        }

        /// <summary>
        /// Not in the SDK: Playnite's Software tools. Their model is the SDK's <see cref="AppSoftware"/>, but the SDK's
        /// database API doesn't list them; only Playnite's own database does. Null, and logged, on a build where it
        /// can't be found or read.
        /// </summary>
        internal static List<AppSoftware> SoftwareTools()
        {
            try
            {
                var app = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Playnite.PlayniteApplication", throwOnError: false))
                    .FirstOrDefault(t => t != null);
                var current = app?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var database = current?.GetType().GetProperty("Database")?.GetValue(current);
                if (database?.GetType().GetProperty("SoftwareApps")?.GetValue(database) is IEnumerable<AppSoftware> tools)
                {
                    return tools.ToList();
                }

                logger.Warn("Can't read Playnite's Software tools: this Playnite build has no Database.SoftwareApps");
                return null;
            }
            catch (Exception e)
            {
                logger.Warn(e, "Couldn't read Playnite's Software tools");
                return null;
            }
        }

        /// <summary>
        /// A Software tool's path, arguments or working directory with Playnite's variables filled in, as Playnite fills
        /// them in to start it; null when it has none.
        /// </summary>
        private static string ExpandVariables(IPlayniteAPI api, string value)
        {
            // A Software tool has no emulator, so Playnite empties {EmulatorDir}.
            return string.IsNullOrEmpty(value)
                ? null
                : value
                    .Replace("{PlayniteDir}", api.Paths.ApplicationPath)
                    .Replace("{EmulatorDir}", string.Empty);
        }

        /// <summary>The filter presets in the order the user gave them in Playnite, or the database's order if that can't be read.</summary>
        private static IEnumerable<Playnite.SDK.Models.FilterPreset> SortedFilterPresets(IPlayniteAPI api)
        {
            try
            {
                return api.MainView.GetSortedFilterPresets();
            }
            catch (Exception e)
            {
                // Home's elements can be built before Playnite's main view is fully up.
                logger.Warn(e, "Couldn't read Playnite's filter preset order; using the database's");
                return api.Database.FilterPresets;
            }
        }

        /// <summary>A preset's sort order as the core knows it; null for one it doesn't support, which then sorts by name.</summary>
        private static GameSort? ToGameSort(SortOrder order)
        {
            switch (order)
            {
                case SortOrder.Name: return GameSort.Name;
                case SortOrder.LastActivity: return GameSort.LastActivity;
                case SortOrder.Added: return GameSort.Added;
                case SortOrder.Modified: return GameSort.Modified;
                case SortOrder.ReleaseDate: return GameSort.ReleaseDate;
                case SortOrder.Playtime: return GameSort.Playtime;
                case SortOrder.PlayCount: return GameSort.PlayCount;
                default: return null;
            }
        }

        /// <summary>A database image (a file id, path or URL) as a full path or URL; null when the game has none.</summary>
        private static string FullPath(IGameDatabaseAPI database, string image)
        {
            if (string.IsNullOrEmpty(image))
            {
                return null;
            }

            // Only a file id is relative to the database's files; a URL or full path is used as it is.
            return image.StartsWith("http", StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(image)
                ? image
                : database.GetFullFilePath(image);
        }
    }
}
