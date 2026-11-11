using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// Port for the games' art from the Xbox store (<see cref="XboxStoreCatalog"/>), saved in the extension's own data
    /// folder, never in Playnite's library. Art found reaches <see cref="ExtensionCore.OnXboxArt"/> on the UI thread.
    /// </summary>
    public interface IXboxArt
    {
        /// <summary>The art saved on earlier starts, by game, read from disk once; quick, and never asks the store.</summary>
        IReadOnlyDictionary<Guid, XboxArt> Saved { get; }

        /// <summary>
        /// Asks for the game's art, off the UI thread, a few at a time, in the order asked. One looked up before is asked
        /// of the store again only when it is due (<see cref="XboxArtIndex.IsDue"/>); what is found, or that nothing
        /// matched, reaches <see cref="ExtensionCore.OnXboxArt"/>. A failure, or no connection, leaves the cover.
        /// </summary>
        void Look(Guid gameId, string name);

        /// <summary>Drops every game still waiting to be looked up; a lookup under way finishes.</summary>
        void StopLooking();
    }

    /// <summary>No Xbox art, ever: for a core given no port.</summary>
    internal sealed class NoXboxArt : IXboxArt
    {
        public static readonly NoXboxArt Instance = new NoXboxArt();

        public IReadOnlyDictionary<Guid, XboxArt> Saved { get; } = new Dictionary<Guid, XboxArt>();

        public void Look(Guid gameId, string name)
        {
        }

        public void StopLooking()
        {
        }
    }
}
