using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: reads Playnite's database into the core's <see cref="LibrarySnapshot"/>.</summary>
    internal static class PlayniteLibrary
    {
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
                    FullPath(database, g.CoverImage)))
                .ToList();

            var presets = database.FilterPresets
                .Select(p => new FilterPreset(p.Id, p.Name, p.ShowInFullscreeQuickSelection))
                .ToList();

            // Software tools aren't in the SDK; reading them by reflection lands with Apps (#38). Until then Apps are unavailable.
            return new LibrarySnapshot(games, presets, apps: null);
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
