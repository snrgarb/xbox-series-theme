using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace XboxSeries.Core
{
    /// <summary>
    /// What the extension has learned from the Xbox store, game by game, kept as JSON beside the art it saved so that a
    /// game is looked up once, not on every start: the name it was looked up by, the product found (none for no match),
    /// when, and the art files saved for it. A game the store had no match for is asked again only after
    /// <see cref="RetryNoMatchAfter"/>, in case it has come to the store since; one renamed in Playnite is asked again at
    /// once. Not thread-safe: the extension holds it under a lock.
    /// </summary>
    public sealed class XboxArtIndex
    {
        /// <summary>How long a game the store had no match for waits before it is asked again.</summary>
        public static readonly TimeSpan RetryNoMatchAfter = TimeSpan.FromDays(14);

        private readonly Dictionary<Guid, XboxArtLookup> lookups;

        public XboxArtIndex()
            : this(new Dictionary<Guid, XboxArtLookup>())
        {
        }

        private XboxArtIndex(Dictionary<Guid, XboxArtLookup> lookups)
        {
            this.lookups = lookups;
        }

        /// <summary>The index saved as <paramref name="json"/>; empty for none, or for JSON that can't be read, so every game is asked again.</summary>
        public static XboxArtIndex FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new XboxArtIndex();
            }

            try
            {
                var saved = JsonConvert.DeserializeObject<Dictionary<Guid, XboxArtLookup>>(json);
                return new XboxArtIndex((saved ?? new Dictionary<Guid, XboxArtLookup>())
                    .Where(entry => entry.Value != null && entry.Value.Name != null)
                    .ToDictionary(entry => entry.Key, entry => entry.Value));
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is FormatException)
            {
                return new XboxArtIndex();
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(lookups, Formatting.Indented);

        /// <summary>What is known of the game; null when it was never looked up.</summary>
        public XboxArtLookup Of(Guid gameId) => lookups.TryGetValue(gameId, out var lookup) ? lookup : null;

        /// <summary>Every game looked up, by id.</summary>
        public IEnumerable<KeyValuePair<Guid, XboxArtLookup>> All => lookups;

        /// <summary>
        /// Whether the game, called <paramref name="name"/> now, is to be looked up: never looked up, renamed since, or
        /// without a match for <see cref="RetryNoMatchAfter"/>. A game the store matched isn't asked again; its art is
        /// fetched again only if its files have gone (<paramref name="filesThere"/> false).
        /// </summary>
        public bool IsDue(Guid gameId, string name, DateTime now, bool filesThere = true)
        {
            var lookup = Of(gameId);
            if (lookup == null || lookup.Name != name)
            {
                return true;
            }

            return lookup.ProductId == null ? now - lookup.CheckedAt >= RetryNoMatchAfter : !filesThere;
        }

        /// <summary>Notes what a lookup found: the product and the art files saved for it, or no match with all null.</summary>
        public void Record(Guid gameId, string name, DateTime now, string productId, string boxArtFile, string posterFile) =>
            lookups[gameId] = new XboxArtLookup
            {
                Name = name,
                CheckedAt = now,
                ProductId = productId,
                BoxArt = productId == null ? null : boxArtFile,
                Poster = productId == null ? null : posterFile,
            };
    }

    /// <summary>One game's lookup, as <see cref="XboxArtIndex"/> keeps it.</summary>
    public sealed class XboxArtLookup
    {
        /// <summary>The game's name as it was looked up by.</summary>
        public string Name { get; set; }

        /// <summary>When it was looked up, in UTC.</summary>
        public DateTime CheckedAt { get; set; }

        /// <summary>The store's product for the game; null when the store had no match.</summary>
        public string ProductId { get; set; }

        /// <summary>The box art's file name in the art folder; null for none.</summary>
        public string BoxArt { get; set; }

        /// <summary>The poster's file name in the art folder; null for none.</summary>
        public string Poster { get; set; }
    }
}
