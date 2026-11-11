using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// What the extension asks of the Xbox store's catalog, which needs no sign-in, and what it makes of the answers:
    /// a search for a game's name, the product whose title is that name, and that product's box art and poster. The
    /// asking itself, over HTTP, is the extension's; this only builds the addresses and reads the JSON.
    /// </summary>
    public static class XboxStoreCatalog
    {
        /// <summary>
        /// The size the store's image server resizes box art to: the console's tiles are at most 245 at 1080p, so this
        /// stays sharp at 4K. The server re-encodes as JPEG, so a file is the same bytes every time it is fetched.
        /// </summary>
        private const string BoxArtSize = "?w=600&h=600&format=jpg&q=90";

        /// <summary>The poster's, at its own 2:3: a Group's cards are 194 by 296 at 1080p.</summary>
        private const string PosterSize = "?w=600&h=900&format=jpg&q=90";

        /// <summary>The catalog's search, in the US store's English, for products of the Games family only.</summary>
        public static string SearchUrl(string name) =>
            "https://displaycatalog.mp.microsoft.com/v7.0/productFamilies/autosuggest?market=US&languages=en-US&productFamilyNames=Games&query="
            + Uri.EscapeDataString(name ?? "");

        /// <summary>One product's details, its images among them.</summary>
        public static string ProductUrl(string productId) =>
            "https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=" + Uri.EscapeDataString(productId) + "&market=US&languages=en-US";

        /// <summary>
        /// A title as names are compared: trimmed, without ™, ® or ©, its runs of spaces made one, in lower case. So
        /// "Halo Infinite™" is "Halo Infinite", but "Hollow Knight: Silksong" isn't "Hollow Knight".
        /// </summary>
        public static string Normalise(string title)
        {
            if (title == null)
            {
                return "";
            }

            var text = new StringBuilder(title.Length);
            var space = false;
            foreach (var c in title.Trim())
            {
                if (c == '™' || c == '®' || c == '©')
                {
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }

                if (space && text.Length > 0)
                {
                    text.Append(' ');
                }

                space = false;
                text.Append(char.ToLowerInvariant(c));
            }

            return text.ToString();
        }

        /// <summary>
        /// The product a search found for <paramref name="name"/>: the first game whose title is the name, as
        /// <see cref="Normalise"/> compares them; null for none. Nothing looser: the search's first answer is often a
        /// different game of the same series ("Minecraft" finds Minecraft Legends first), and the wrong game's art is
        /// worse than the cover. Null too for an answer that isn't the catalog's JSON.
        /// </summary>
        public static string PickProduct(string name, string searchJson)
        {
            var wanted = Normalise(name);
            if (wanted.Length == 0)
            {
                return null;
            }

            return Products(searchJson)
                .Where(p => string.Equals((string)p["Type"] ?? "Game", "Game", StringComparison.OrdinalIgnoreCase))
                .Where(p => Normalise((string)p["Title"]) == wanted)
                .Select(p => (string)p["ProductId"])
                .FirstOrDefault(id => !string.IsNullOrEmpty(id));
        }

        /// <summary>
        /// The addresses of a product's box art and poster, at the sizes tiles want, from its details; null for one it
        /// hasn't. The catalog gives them without a scheme ("//store-images..."), so https is put before them.
        /// </summary>
        public static XboxStoreImages ArtOf(string productJson)
        {
            JArray images = null;
            try
            {
                images = JObject.Parse(productJson ?? "")["Products"]?.FirstOrDefault()?["LocalizedProperties"]?.FirstOrDefault()?["Images"] as JArray;
            }
            catch (Exception e) when (e is Newtonsoft.Json.JsonException || e is InvalidCastException || e is InvalidOperationException)
            {
            }

            string Of(string purpose, string size)
            {
                var uri = images?.OfType<JObject>()
                    .Where(i => string.Equals((string)i["ImagePurpose"], purpose, StringComparison.OrdinalIgnoreCase))
                    .Select(i => (string)i["Uri"])
                    .FirstOrDefault(u => !string.IsNullOrEmpty(u));
                if (uri == null)
                {
                    return null;
                }

                return (uri.StartsWith("//", StringComparison.Ordinal) ? "https:" + uri : uri) + size;
            }

            return new XboxStoreImages(Of("BoxArt", BoxArtSize), Of("Poster", PosterSize));
        }

        /// <summary>Every product a search's answer lists, across its results; none for an answer that isn't its JSON.</summary>
        private static IEnumerable<JObject> Products(string searchJson)
        {
            try
            {
                var results = JObject.Parse(searchJson ?? "")["Results"] as JArray;
                return results == null
                    ? Enumerable.Empty<JObject>()
                    : results.OfType<JObject>().SelectMany(r => (r["Products"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()).ToList();
            }
            catch (Exception e) when (e is Newtonsoft.Json.JsonException || e is InvalidCastException || e is InvalidOperationException)
            {
                return Enumerable.Empty<JObject>();
            }
        }
    }

    /// <summary>Where a product's box art and poster are to be had, at the sizes tiles want; null for one it hasn't.</summary>
    public sealed class XboxStoreImages
    {
        public XboxStoreImages(string boxArt, string poster)
        {
            BoxArt = boxArt;
            Poster = poster;
        }

        public string BoxArt { get; }

        public string Poster { get; }
    }
}
