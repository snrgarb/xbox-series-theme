using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: reads Playnite's database into the core's <see cref="LibrarySnapshot"/>.</summary>
    internal static class PlayniteLibrary
    {
        public static LibrarySnapshot Snapshot(IGameDatabaseAPI database)
        {
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
                    g.Source?.Name))
                .ToList();

            var presets = database.FilterPresets
                .Select(p => new FilterPreset(p.Id, p.Name, p.ShowInFullscreeQuickSelection))
                .ToList();

            // Software tools aren't in the SDK; reading them by reflection lands with Apps (#38). Until then Apps are unavailable.
            return new LibrarySnapshot(games, presets, apps: null);
        }
    }
}
