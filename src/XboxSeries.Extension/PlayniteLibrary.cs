using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            // Software tools aren't in the SDK; reading them by reflection lands with Apps (#38). Until then Apps are unavailable.
            return new LibrarySnapshot(games, presets, apps: null);
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
