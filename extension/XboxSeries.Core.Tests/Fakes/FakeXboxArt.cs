using System;
using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// The Xbox art port: art saved before as a test sets it, and every lookup asked for and every stop recorded. A test
    /// answers a lookup through <see cref="ExtensionCore.OnXboxArt"/>, as the extension would.
    /// </summary>
    public sealed class FakeXboxArt : IXboxArt
    {
        public Dictionary<Guid, XboxArt> SavedArt { get; } = new Dictionary<Guid, XboxArt>();

        public IReadOnlyDictionary<Guid, XboxArt> Saved => SavedArt;

        /// <summary>Every lookup asked for, in order.</summary>
        public List<(Guid GameId, string Name)> Looked { get; } = new List<(Guid, string)>();

        public int Stops { get; private set; }

        public void Look(Guid gameId, string name) => Looked.Add((gameId, name));

        public void StopLooking() => Stops++;
    }
}
