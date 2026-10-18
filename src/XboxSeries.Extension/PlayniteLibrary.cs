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
            var games = database.Games.Select(g => GameOf(api, g)).ToList();

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

            return new LibrarySnapshot(games, presets, Apps(api));
        }

        private static LibraryGame GameOf(IPlayniteAPI api, Game g)
        {
            var directory = string.IsNullOrEmpty(g.InstallDirectory) ? null : api.ExpandGameVariables(g, g.InstallDirectory);
            return new LibraryGame(
                g.Id,
                g.Name,
                g.LastActivity,
                g.Added,
                g.Hidden,
                g.IsInstalled,
                g.Series?.Select(s => s.Name).ToList() ?? new List<string>(),
                g.Genres?.Select(s => s.Name).ToList() ?? new List<string>(),
                g.Source?.Name,
                directory,
                FullPath(api.Database, g.BackgroundImage),
                FullPath(api.Database, g.CoverImage),
                g.Favorite,
                g.Playtime,
                g.PlayCount,
                g.ReleaseDate?.Date,
                g.Modified,
                g.SortingName,
                g.Platforms?.FirstOrDefault()?.Name,
                installing: g.IsInstalling,
                installedAt: g.IsInstalled ? CreationTimeOf(directory) : null);
        }

        /// <summary>
        /// When the folder was made, which for a game's install folder is when its client began installing it: Playnite
        /// keeps no install date. Null when there's no folder or it can't be read.
        /// </summary>
        private static DateTime? CreationTimeOf(string directory)
        {
            try
            {
                return directory != null && Directory.Exists(directory) ? Directory.GetCreationTime(directory) : (DateTime?)null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                return null;
            }
        }

        /// <summary>
        /// The Apps, in the order of Playnite's own Software tools menu: by name. Null, which hides Apps, and logged, when
        /// Software tools can't be read, so a failure reading them leaves the games as they are.
        /// </summary>
        private static List<App> Apps(IPlayniteAPI api)
        {
            try
            {
                return SoftwareTools()?
                    .OrderBy(a => a.Name)
                    .Select(a => new App(
                        a.Id,
                        a.Name,
                        ExpandVariables(api, a.Path),
                        ExpandVariables(api, a.Arguments),
                        FullPath(api.Database, a.Icon),
                        ExpandVariables(api, a.WorkingDir)))
                    .ToList();
            }
            catch (Exception e)
            {
                logger.Warn(e, "Couldn't read Playnite's Software tools");
                return null;
            }
        }

        /// <summary>
        /// Not in the SDK: Playnite's Software tools, live. Their model is the SDK's <see cref="AppSoftware"/>, but the
        /// SDK's database API doesn't list them; only Playnite's own database does, as an SDK
        /// <see cref="IItemCollection{TItem}"/> that tells when they change. Null, and logged, on a build where it
        /// can't be found.
        /// </summary>
        internal static IEnumerable<AppSoftware> SoftwareTools()
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
                    return tools;
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

        /// <summary>A preset's or the main list's sort order as the core knows it; null for one it doesn't support.</summary>
        internal static GameSort? ToGameSort(SortOrder order)
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
