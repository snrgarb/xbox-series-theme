using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The 100 most liked Xbox 360 gamerpics on xboxgamer.pics, kept in a folder of their own so a profile can wear one
    /// with no connection, apart from Playnite so it can be checked on its own. Each is saved once, by its id, so the
    /// path a profile wears never changes; the order is the last list fetched, kept beside them in order.txt. A
    /// gamerpic that falls out of the list stays on disk for whoever wears it, but isn't offered any more.
    /// </summary>
    internal sealed class PopularGamerpics
    {
        private const int Count = 100;
        private const string OrderFile = "order.txt";

        /// <summary>An id is the title's and the picture's, in hex: "fffe07d1-20002".</summary>
        private static readonly Regex Id = new Regex("^([0-9a-f]{8})-([0-9a-f]+)$", RegexOptions.IgnoreCase);

        private readonly HttpClient client;
        private readonly string folder;

        public PopularGamerpics(HttpClient client, string folder)
        {
            this.client = client;
            this.folder = folder;
        }

        /// <summary>The saved gamerpics in the last list's order, by full path; empty before the first fetch. Throws on a bad read.</summary>
        public IReadOnlyList<string> Pictures
        {
            get
            {
                var order = Path.Combine(folder, OrderFile);
                if (!File.Exists(order))
                {
                    return new string[0];
                }

                return File.ReadAllLines(order)
                    .Where(id => Id.IsMatch(id))
                    .Select(PathOf)
                    .Where(File.Exists)
                    .ToList();
            }
        }

        /// <summary>
        /// Fetches the list and saves each gamerpic not saved already, then the list's order. Throws when the list can't
        /// be had; a gamerpic that fails is left out until the next fetch.
        /// </summary>
        public async Task FetchAsync()
        {
            var json = await client.GetStringAsync($"https://xboxgamer.pics/api/popular?limit={Count}").ConfigureAwait(false);
            var ids = JArray.Parse(json).Select(item => (string)item["id"]).Where(id => id != null && Id.IsMatch(id)).ToList();

            Directory.CreateDirectory(folder);
            await Task.WhenAll(ids.Where(id => !File.Exists(PathOf(id))).Select(SaveAsync)).ConfigureAwait(false);

            // Written whole, then swapped in, so Pictures never reads half a list.
            var order = Path.Combine(folder, OrderFile);
            File.WriteAllLines(order + ".tmp", ids);
            if (File.Exists(order))
            {
                File.Replace(order + ".tmp", order, null);
            }
            else
            {
                File.Move(order + ".tmp", order);
            }
        }

        private string PathOf(string id) => Path.Combine(folder, id.ToLowerInvariant() + ".png");

        /// <summary>One gamerpic, written only once it has all come, so a failed one is never a broken file.</summary>
        private async Task SaveAsync(string id)
        {
            var parts = Id.Match(id).Groups;
            try
            {
                var bytes = await client.GetByteArrayAsync($"https://assets.xboxgamer.pics/titles/{parts[1].Value}/{parts[2].Value}.png").ConfigureAwait(false);
                File.WriteAllBytes(PathOf(id), bytes);
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}
