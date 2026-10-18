using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// A filter and sort the Dashboard puts on Playnite's main list, through a filter preset the extension builds.
    /// Hidden games never match.
    /// </summary>
    public sealed class GameListFilter
    {
        /// <summary>My games &amp; apps' Games: installed games, A–Z.</summary>
        internal static readonly GameListFilter Installed = new GameListFilter(installedOnly: true, GameSort.Name, SortDirection.Ascending);

        /// <summary>Every game, last played first, so that every game in Play history, or that Search found, can be selected for its Game hub.</summary>
        internal static readonly GameListFilter LastPlayed = new GameListFilter(installedOnly: false, GameSort.LastActivity, SortDirection.Descending);

        private GameListFilter(bool installedOnly, GameSort sort, SortDirection direction)
        {
            InstalledOnly = installedOnly;
            Sort = sort;
            Direction = direction;
        }

        public bool InstalledOnly { get; }
        public GameSort Sort { get; }
        public SortDirection Direction { get; }

        /// <summary>The games Playnite lists under this filter, in its order.</summary>
        public IEnumerable<LibraryGame> Apply(IEnumerable<LibraryGame> games) =>
            Sorted(games.Where(g => !g.Hidden && (g.Installed || !InstalledOnly)), Sort, Direction);

        /// <summary>Games in the order of a sort, ties in name order, as Playnite lists them.</summary>
        internal static IEnumerable<LibraryGame> Sorted(IEnumerable<LibraryGame> games, GameSort sort, SortDirection direction)
        {
            var byName = StringComparer.CurrentCultureIgnoreCase;
            if (sort == GameSort.Name)
            {
                return direction == SortDirection.Ascending
                    ? games.OrderBy(g => g.SortingName, byName)
                    : games.OrderByDescending(g => g.SortingName, byName);
            }

            Func<LibraryGame, IComparable> key;
            switch (sort)
            {
                case GameSort.LastActivity: key = g => g.LastActivity; break;
                case GameSort.Added: key = g => g.Added; break;
                case GameSort.Modified: key = g => g.Modified; break;
                case GameSort.ReleaseDate: key = g => g.ReleaseDate; break;
                case GameSort.Playtime: key = g => g.Playtime; break;
                case GameSort.PlayCount: key = g => g.PlayCount; break;
                default: throw new ArgumentOutOfRangeException(nameof(sort), sort, null);
            }

            // Games without the field (never played, no release date) come last either way.
            var ordered = direction == SortDirection.Ascending
                ? games.OrderBy(g => key(g) == null).ThenBy(key)
                : games.OrderBy(g => key(g) == null).ThenByDescending(key);
            return ordered.ThenBy(g => g.SortingName, byName);
        }
    }
}
