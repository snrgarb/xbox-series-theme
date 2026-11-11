using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XboxSeries.Extension;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The games' Xbox art looked up and saved in a folder of the extension's own (XboxStoreArt.cs, compiled in here),
    // against a fake Xbox store: each game once, its art once per product, what was learned kept across starts, a line
    // in the log for each lookup, and the store left alone once it keeps failing.
    public sealed class XboxStoreArtTests : IDisposable
    {
        private const string HadesId = "9P8DL6W5MTJ9";

        private readonly string folder = Path.Combine(Path.GetTempPath(), "XboxStoreArtTests", Guid.NewGuid().ToString());
        private readonly FakeStore store = new FakeStore();
        private readonly List<string> logged = new List<string>();
        private readonly List<KeyValuePair<Guid, XboxArt>> found = new List<KeyValuePair<Guid, XboxArt>>();
        private readonly Guid hades = Guid.NewGuid();
        private XboxStoreArt art;

        public XboxStoreArtTests()
        {
            store.Products["Hades"] = HadesId;
            store.Images[HadesId] = new[] { "BoxArt", "Poster" };
            Restart();
        }

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>A new instance over the same folder, as when Playnite starts again.</summary>
        private void Restart()
        {
            art = new XboxStoreArt(new HttpClient(store), folder, line =>
            {
                lock (logged)
                {
                    logged.Add(line);
                }
            });
            art.Found += (game, xboxArt) =>
            {
                lock (found)
                {
                    found.Add(new KeyValuePair<Guid, XboxArt>(game, xboxArt));
                }
            };
        }

        private string FileOf(string name) => Path.Combine(folder, name);

        [Fact]
        public async Task A_match_saves_its_box_art_and_poster_in_the_folder_and_says_so()
        {
            await art.LookUpAsync(hades, "Hades");

            var xboxArt = found.Single(f => f.Key == hades).Value;
            Assert.Equal(FileOf(HadesId + ".box.jpg"), xboxArt.BoxArt);
            Assert.Equal(FileOf(HadesId + ".poster.jpg"), xboxArt.Poster);
            Assert.Equal(FakeStore.Bytes("image/" + HadesId + "/BoxArt"), File.ReadAllBytes(xboxArt.BoxArt));
            Assert.Equal(FakeStore.Bytes("image/" + HadesId + "/Poster"), File.ReadAllBytes(xboxArt.Poster));
            Assert.Contains("[Xbox art] Hades: box art and poster", logged);
            Assert.Contains("https://store-images.s-microsoft.com/image/" + HadesId + "/BoxArt?w=600&h=600&format=jpg&q=90", store.Asked);
            Assert.Contains("https://store-images.s-microsoft.com/image/" + HadesId + "/Poster?w=600&h=900&format=jpg&q=90", store.Asked);
            Assert.True(File.Exists(FileOf("index.json")));
        }

        [Fact]
        public async Task A_game_matched_before_is_saved_on_the_next_start_and_the_store_isnt_asked_again()
        {
            await art.LookUpAsync(hades, "Hades");
            store.Asked.Clear();

            Restart();
            await art.LookUpAsync(hades, "Hades");

            Assert.Equal(FileOf(HadesId + ".box.jpg"), art.Saved[hades].BoxArt);
            Assert.Equal(FileOf(HadesId + ".poster.jpg"), art.Saved[hades].Poster);
            Assert.Empty(store.Asked);
            Assert.Contains("[Xbox art] 1 games' art saved from before", logged);
        }

        [Fact]
        public async Task Art_deleted_since_is_fetched_again()
        {
            await art.LookUpAsync(hades, "Hades");
            File.Delete(FileOf(HadesId + ".poster.jpg"));
            store.Asked.Clear();

            Restart();
            Assert.Null(art.Saved[hades].Poster);
            await art.LookUpAsync(hades, "Hades");

            Assert.True(File.Exists(FileOf(HadesId + ".poster.jpg")));
            Assert.DoesNotContain(store.Asked, url => url.Contains("/BoxArt?"));
        }

        [Fact]
        public async Task No_match_is_said_and_remembered_so_the_next_start_doesnt_ask()
        {
            var homebrew = Guid.NewGuid();

            await art.LookUpAsync(homebrew, "My Homebrew");
            Restart();
            store.Asked.Clear();
            await art.LookUpAsync(homebrew, "My Homebrew");

            Assert.Contains("[Xbox art] My Homebrew: no match", logged);
            Assert.Null(found.Single(f => f.Key == homebrew).Value);
            Assert.False(art.Saved.ContainsKey(homebrew));
            Assert.Empty(store.Asked);
        }

        [Fact]
        public async Task A_product_without_a_poster_saves_only_its_box_art()
        {
            store.Images[HadesId] = new[] { "BoxArt" };

            await art.LookUpAsync(hades, "Hades");

            Assert.Contains("[Xbox art] Hades: box art only", logged);
            Assert.Null(found.Single().Value.Poster);
            Assert.False(File.Exists(FileOf(HadesId + ".poster.jpg")));
        }

        [Fact]
        public async Task Two_games_of_one_product_share_its_art_fetched_once()
        {
            await art.LookUpAsync(hades, "Hades");
            store.Asked.Clear();

            await art.LookUpAsync(Guid.NewGuid(), "Hades");

            Assert.DoesNotContain(store.Asked, url => url.StartsWith("https://store-images.", StringComparison.Ordinal));
            Assert.Equal(2, found.Count(f => f.Value?.BoxArt == FileOf(HadesId + ".box.jpg")));
        }

        [Fact]
        public async Task A_store_that_fails_throws_and_remembers_nothing_so_the_game_is_asked_again()
        {
            store.Down = true;

            await Assert.ThrowsAsync<HttpRequestException>(() => art.LookUpAsync(hades, "Hades"));
            store.Down = false;
            await art.LookUpAsync(hades, "Hades");

            Assert.Contains("[Xbox art] Hades: box art and poster", logged);
        }

        [Fact]
        public void Nothing_is_looked_up_before_Start()
        {
            art.Look(hades, "Hades");

            Thread.Sleep(200);
            Assert.Empty(store.Asked);
        }

        [Fact]
        public async Task Once_started_each_game_asked_for_is_looked_up_in_the_background()
        {
            art.Look(hades, "Hades");
            art.Start();

            await Until(() => found.Count == 1);
            Assert.Equal(hades, found.Single().Key);
        }

        [Fact]
        public async Task After_three_failures_in_a_row_the_rest_wait_for_the_next_start()
        {
            store.Down = true;
            art.Start();
            for (var i = 0; i < 10; i++)
            {
                art.Look(Guid.NewGuid(), "Game " + i);
            }

            await Until(() => Logged("lookups failed in a row"));
            await Task.Delay(200);
            var asked = store.Asked.Count;
            art.Look(hades, "Hades");
            await Task.Delay(200);

            Assert.True(asked < 10 + 3, $"{asked} asked");
            Assert.Equal(asked, store.Asked.Count);
            Assert.Contains(logged, line => line.StartsWith("[Xbox art] Game ", StringComparison.Ordinal) && line.Contains("couldn't fetch"));
        }

        private bool Logged(string part)
        {
            lock (logged)
            {
                return logged.Any(line => line.Contains(part));
            }
        }

        private static async Task Until(Func<bool> condition)
        {
            for (var waited = 0; waited < 5000 && !condition(); waited += 20)
            {
                await Task.Delay(20);
            }

            Assert.True(condition());
        }

        /// <summary>
        /// The Xbox store's catalog and image server: a search finds the products named in <see cref="Products"/> by
        /// their exact title, a product has the images of the purposes in <see cref="Images"/>, and an image is bytes
        /// naming it. Down, every request fails with a server error.
        /// </summary>
        private sealed class FakeStore : HttpMessageHandler
        {
            public Dictionary<string, string> Products { get; } = new Dictionary<string, string>();

            public Dictionary<string, string[]> Images { get; } = new Dictionary<string, string[]>();

            public List<string> Asked { get; } = new List<string>();

            public volatile bool Down;

            public static byte[] Bytes(string path) => System.Text.Encoding.UTF8.GetBytes(path);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var uri = request.RequestUri;
                lock (Asked)
                {
                    Asked.Add(uri.AbsoluteUri);
                }

                if (Down)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                }

                if (uri.Host == "store-images.s-microsoft.com")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes(uri.AbsolutePath.TrimStart('/'))) });
                }

                var query = Uri.UnescapeDataString(uri.Query);
                string json;
                if (uri.AbsolutePath.EndsWith("/autosuggest", StringComparison.Ordinal))
                {
                    var name = query.Substring(query.IndexOf("query=", StringComparison.Ordinal) + "query=".Length);
                    json = Products.TryGetValue(name, out var id)
                        ? "{\"Results\":[{\"Products\":[{\"ProductId\":\"" + id + "\",\"Type\":\"Game\",\"Title\":\"" + name + "\"}]}]}"
                        : "{\"Results\":[],\"TotalResultCount\":0}";
                }
                else
                {
                    var id = query.Substring(query.IndexOf("bigIds=", StringComparison.Ordinal) + "bigIds=".Length).Split('&')[0];
                    var images = (Images.TryGetValue(id, out var purposes) ? purposes : new string[0])
                        .Select(p => "{\"ImagePurpose\":\"" + p + "\",\"Uri\":\"//store-images.s-microsoft.com/image/" + id + "/" + p + "\"}");
                    json = "{\"Products\":[{\"LocalizedProperties\":[{\"Images\":[" + string.Join(",", images) + "]}]}]}";
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }
        }
    }
}
