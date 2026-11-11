using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The games' own art from the Xbox store, on their tiles as the console shows it: a square tile the game's box art,
    /// a tall one its poster (<see cref="TileArt"/>). Each library game is asked for once a session, the most recently
    /// played first, so Home's tiles get theirs soonest; the port does the asking off the UI thread and remembers what it
    /// learned across starts. Tiles show the cover until the art comes, and whenever Personalisation's Xbox store art is
    /// off.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        private readonly IXboxArt xboxArtSource;

        /// <summary>Each game's Xbox art, as saved on earlier starts and found since; a game with none isn't here.</summary>
        private readonly Dictionary<Guid, XboxArt> xboxArt = new Dictionary<Guid, XboxArt>();

        /// <summary>Each game asked for this session, by the name it was asked by, so a game renamed is asked again.</summary>
        private readonly Dictionary<Guid, string> xboxArtAsked = new Dictionary<Guid, string>();

        /// <summary>
        /// Raised on the UI thread when what <see cref="XboxArtFor"/> answers may have changed: art came, or Xbox store art
        /// was turned on or off. The theme's own tiles, which the extension's elements draw the art over, read it again.
        /// </summary>
        public event Action XboxArtChanged;

        /// <summary>
        /// The game's Xbox art for a tile of <paramref name="shape"/>, alone, for a tile that shows its cover beneath:
        /// null when it has none of that shape, or Xbox store art is off.
        /// </summary>
        public string XboxArtFor(Guid gameId, TileShape shape) => TileArt.XboxArtFor(shape, ShownXboxArt(gameId));

        /// <summary>
        /// Art the port found, or found it had none, for each game given: a null value is none. Every tile showing one of
        /// those games shows it at once.
        /// </summary>
        public void OnXboxArt(IReadOnlyDictionary<Guid, XboxArt> found)
        {
            var changed = false;
            foreach (var entry in found)
            {
                var had = xboxArt.TryGetValue(entry.Key, out var was) ? was : null;
                if (Equals(had, entry.Value))
                {
                    continue;
                }

                changed = true;
                if (entry.Value == null)
                {
                    xboxArt.Remove(entry.Key);
                }
                else
                {
                    xboxArt[entry.Key] = entry.Value;
                }
            }

            if (changed && Settings.StartUp.XboxArt)
            {
                RefreshArtTiles();
            }
        }

        /// <summary>The art saved on earlier starts, there from the first tile built, so a start doesn't show covers first.</summary>
        private void LoadXboxArt()
        {
            foreach (var saved in xboxArtSource.Saved)
            {
                if (saved.Value != null)
                {
                    xboxArt[saved.Key] = saved.Value;
                }
            }
        }

        /// <summary>
        /// Asks the port for each library game not yet asked for under its name this session, while Xbox store art is on:
        /// the most recently played first, then the most recently added, as Home and Search show them, so the tiles in
        /// sight get their art first.
        /// </summary>
        private void LookUpXboxArt()
        {
            if (!Settings.StartUp.XboxArt)
            {
                return;
            }

            var toAsk = library.Games
                .Where(g => !string.IsNullOrWhiteSpace(g.Name) && (!xboxArtAsked.TryGetValue(g.Id, out var askedAs) || askedAs != g.Name))
                .OrderByDescending(g => g.LastActivity ?? DateTime.MinValue)
                .ThenByDescending(g => g.Added ?? DateTime.MinValue)
                .ToList();
            foreach (var game in toAsk)
            {
                xboxArtAsked[game.Id] = game.Name;
                xboxArtSource.Look(game.Id, game.Name);
            }
        }

        /// <summary>
        /// Xbox store art turned on or off: the tiles show the art again, or the covers, at once. Off, the store is asked
        /// nothing more; on, every game is asked for again, the port asking the store only for those it is due on.
        /// </summary>
        private void XboxArtTurned(bool on)
        {
            xboxArtAsked.Clear();
            if (on)
            {
                LookUpXboxArt();
            }
            else
            {
                xboxArtSource.StopLooking();
            }

            RefreshArtTiles();
        }

        /// <summary>The game's Xbox art as tiles show it: none while Xbox store art is off.</summary>
        private XboxArt ShownXboxArt(Guid gameId) =>
            Settings.StartUp.XboxArt && xboxArt.TryGetValue(gameId, out var art) ? art : null;

        /// <summary>
        /// Every row of game tiles built again with the art as it is now: Home's, Play history, Recently installed and
        /// Search's; a row whose tiles read the same keeps them. The Queue's rows follow on its next poll, each second
        /// while anything installs. The theme's tiles are told too.
        /// </summary>
        private void RefreshArtTiles()
        {
            RefreshRecentlyInstalled();
            RefreshHomeRows();
            RefreshSearch();
            XboxArtChanged?.Invoke();
        }
    }
}
