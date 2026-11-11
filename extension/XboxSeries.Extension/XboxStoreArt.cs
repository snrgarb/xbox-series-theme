using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The games' art from the Xbox store (<see cref="XboxStoreCatalog"/>), in a folder of the extension's own, XboxArt,
    /// never in Playnite's library, so the user's game metadata is never touched. Each game is looked up once: what was
    /// learned is kept in index.json beside the art (<see cref="XboxArtIndex"/>), a game with no match asked again only
    /// after some days. Lookups run on the thread pool, <see cref="Concurrency"/> at a time, once <see cref="Start"/> is
    /// called, as Start-up ends, so they never hold up Start-up or Home's first paint; each one's art is raised by
    /// <see cref="Found"/>, from that thread. A failure leaves the game's cover and is asked again next start; after
    /// <see cref="FailuresBeforeGivingUp"/> in a row, as with no connection, the rest wait for the next start.
    /// Each lookup is logged on one line, "[Xbox art] &lt;game&gt;: box art and poster", "...: no match" and so on,
    /// which the end-to-end tests read. Plain C#, without Playnite, so it is checked against a fake store in the tests.
    /// </summary>
    internal sealed class XboxStoreArt : IXboxArt
    {
        /// <summary>How many games are looked up at once: a few, to be polite to the store.</summary>
        private const int Concurrency = 3;

        /// <summary>How many lookups may fail in a row before the session stops asking: the store, or the connection, is down.</summary>
        private const int FailuresBeforeGivingUp = 3;

        private const string IndexFile = "index.json";

        /// <summary>A product id as the store gives them, safe for a file name: "C3D891Z6TNQM".</summary>
        private static readonly Regex ProductId = new Regex("^[A-Za-z0-9]{1,32}$");

        private readonly HttpClient client;
        private readonly string folder;
        private readonly Action<string> log;
        private readonly object gate = new object();
        private readonly XboxArtIndex index;
        private readonly Queue<KeyValuePair<Guid, string>> waiting = new Queue<KeyValuePair<Guid, string>>();
        private int running;
        private int failuresInARow;
        private bool started;
        private bool gaveUp;

        /// <param name="log">Writes one line to the extension's log.</param>
        public XboxStoreArt(HttpClient client, string folder, Action<string> log)
        {
            this.client = client;
            this.folder = folder;
            this.log = log;
            index = ReadIndex();
            Saved = index.All
                .Where(entry => entry.Value.ProductId != null)
                .Select(entry => new KeyValuePair<Guid, XboxArt>(entry.Key, ArtOf(entry.Value)))
                .Where(entry => entry.Value != null)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            log($"[Xbox art] {Saved.Count} games' art saved from before");
        }

        /// <summary>A game's art found, or found to be none (null), raised from a thread-pool thread.</summary>
        public event Action<Guid, XboxArt> Found;

        public IReadOnlyDictionary<Guid, XboxArt> Saved { get; }

        public void Look(Guid gameId, string name)
        {
            lock (gate)
            {
                if (gaveUp)
                {
                    return;
                }

                waiting.Enqueue(new KeyValuePair<Guid, string>(gameId, name));
            }

            Pump();
        }

        public void StopLooking()
        {
            lock (gate)
            {
                waiting.Clear();
            }
        }

        /// <summary>Lets the lookups asked for so far, and from now on, begin. Called once Start-up has ended; again does nothing.</summary>
        public void Start()
        {
            lock (gate)
            {
                started = true;
            }

            Pump();
        }

        /// <summary>Starts a worker for each free place, up to <see cref="Concurrency"/>, while games wait.</summary>
        private void Pump()
        {
            lock (gate)
            {
                while (started && !gaveUp && running < Concurrency && waiting.Count > 0)
                {
                    running++;
                    Task.Run(() => WorkAsync());
                }
            }
        }

        /// <summary>One worker: looks up waiting games one after another until none wait.</summary>
        private async Task WorkAsync()
        {
            while (true)
            {
                KeyValuePair<Guid, string> next;
                lock (gate)
                {
                    if (gaveUp || waiting.Count == 0)
                    {
                        running--;
                        return;
                    }

                    next = waiting.Dequeue();
                }

                try
                {
                    await LookUpAsync(next.Key, next.Value).ConfigureAwait(false);
                    lock (gate)
                    {
                        failuresInARow = 0;
                    }
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is IOException || e is UnauthorizedAccessException)
                {
                    log($"[Xbox art] {next.Value}: couldn't fetch ({e.Message}); its cover stays");
                    lock (gate)
                    {
                        if (++failuresInARow >= FailuresBeforeGivingUp && !gaveUp)
                        {
                            gaveUp = true;
                            waiting.Clear();
                            log($"[Xbox art] {FailuresBeforeGivingUp} lookups failed in a row; the rest wait for the next start");
                        }
                    }
                }
                catch (Exception e)
                {
                    log($"[Xbox art] {next.Value}: lookup failed: {e}");
                }
            }
        }

        /// <summary>
        /// The game's lookup: nothing when it isn't due (its art, if any, is in <see cref="Saved"/> already); else the
        /// store searched for its name, and for a match the box art and poster saved, each once per product.
        /// </summary>
        internal async Task LookUpAsync(Guid gameId, string name)
        {
            lock (gate)
            {
                var known = index.Of(gameId);
                if (!index.IsDue(gameId, name, DateTime.UtcNow, known == null || FilesThere(known)))
                {
                    return;
                }
            }

            var productId = XboxStoreCatalog.PickProduct(name, await client.GetStringAsync(XboxStoreCatalog.SearchUrl(name)).ConfigureAwait(false));
            if (productId == null || !ProductId.IsMatch(productId))
            {
                Record(gameId, name, null, null, null);
                log($"[Xbox art] {name}: no match");
                Found?.Invoke(gameId, null);
                return;
            }

            var images = XboxStoreCatalog.ArtOf(await client.GetStringAsync(XboxStoreCatalog.ProductUrl(productId)).ConfigureAwait(false));
            var boxArt = await SaveAsync(images.BoxArt, productId + ".box.jpg").ConfigureAwait(false);
            var poster = await SaveAsync(images.Poster, productId + ".poster.jpg").ConfigureAwait(false);
            var lookup = Record(gameId, name, productId, boxArt, poster);
            var art = ArtOf(lookup);
            log($"[Xbox art] {name}: {Said(boxArt != null, poster != null)}");
            Found?.Invoke(gameId, art);
        }

        private static string Said(bool boxArt, bool poster) =>
            boxArt && poster ? "box art and poster"
            : boxArt ? "box art only"
            : poster ? "poster only"
            : "no box art or poster";

        /// <summary>
        /// The image at <paramref name="url"/> saved as <paramref name="file"/>, unless saved already (another game of the
        /// same product); its file name, or null with no URL. Written whole, then moved in, so a failure is never a
        /// broken file.
        /// </summary>
        private async Task<string> SaveAsync(string url, string file)
        {
            if (url == null)
            {
                return null;
            }

            var path = Path.Combine(folder, file);
            if (File.Exists(path))
            {
                return file;
            }

            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            Directory.CreateDirectory(folder);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, bytes);
            try
            {
                File.Move(temp, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another worker saved the same product's art meanwhile.
                File.Delete(temp);
            }

            return file;
        }

        /// <summary>Notes the lookup in the index and saves it, whole, then swapped in, so it is never read half written.</summary>
        private XboxArtLookup Record(Guid gameId, string name, string productId, string boxArt, string poster)
        {
            lock (gate)
            {
                index.Record(gameId, name, DateTime.UtcNow, productId, boxArt, poster);
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, IndexFile);
                File.WriteAllText(path + ".tmp", index.ToJson());
                if (File.Exists(path))
                {
                    File.Replace(path + ".tmp", path, null);
                }
                else
                {
                    File.Move(path + ".tmp", path);
                }

                return index.Of(gameId);
            }
        }

        /// <summary>The index saved before; empty when there is none or it can't be read, so every game is looked up again.</summary>
        private XboxArtIndex ReadIndex()
        {
            try
            {
                var path = Path.Combine(folder, IndexFile);
                return XboxArtIndex.FromJson(File.Exists(path) ? File.ReadAllText(path) : null);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                log($"[Xbox art] couldn't read the index ({e.Message}); every game is looked up again");
                return new XboxArtIndex();
            }
        }

        /// <summary>Whether each art file the lookup names is still there.</summary>
        private bool FilesThere(XboxArtLookup lookup) =>
            (lookup.BoxArt == null || File.Exists(Path.Combine(folder, lookup.BoxArt)))
            && (lookup.Poster == null || File.Exists(Path.Combine(folder, lookup.Poster)));

        /// <summary>The art a lookup saved, by full path, only the files still there; null for none.</summary>
        private XboxArt ArtOf(XboxArtLookup lookup)
        {
            string There(string file) => file != null && File.Exists(Path.Combine(folder, file)) ? Path.Combine(folder, file) : null;
            var boxArt = There(lookup.BoxArt);
            var poster = There(lookup.Poster);
            return boxArt == null && poster == null ? null : new XboxArt(boxArt, poster);
        }
    }
}
