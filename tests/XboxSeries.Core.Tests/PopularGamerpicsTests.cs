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
    // The 100 most liked gamerpics on xboxgamer.pics, fetched into a folder of their own so a profile can wear one with
    // no connection (PopularGamerpics.cs, compiled in here), against a fake site.
    public sealed class PopularGamerpicsTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "PopularGamerpicsTests", Guid.NewGuid().ToString());
        private readonly FakeSite site = new FakeSite();
        private readonly PopularGamerpics popular;

        public PopularGamerpicsTests()
        {
            popular = new PopularGamerpics(new HttpClient(site), folder);
        }

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        private string PathOf(string id) => Path.Combine(folder, id + ".png");

        [Fact]
        public async Task The_top_100_are_asked_for_and_each_saved_by_its_id_most_liked_first()
        {
            site.List = @"[{""id"":""fffe07d1-20002"",""likes"":1185},{""id"":""444d07d1-2808e"",""likes"":570}]";

            await popular.FetchAsync();

            Assert.Contains("https://xboxgamer.pics/api/popular?limit=100", site.Asked);
            Assert.Contains("https://assets.xboxgamer.pics/titles/fffe07d1/20002.png", site.Asked);
            Assert.Contains("https://assets.xboxgamer.pics/titles/444d07d1/2808e.png", site.Asked);
            Assert.Equal(new[] { PathOf("fffe07d1-20002"), PathOf("444d07d1-2808e") }, popular.Pictures);
            Assert.Equal(FakeSite.Png("titles/fffe07d1/20002.png"), File.ReadAllBytes(PathOf("fffe07d1-20002")));
        }

        [Fact]
        public void Nothing_is_offered_before_the_first_fetch()
        {
            Assert.Empty(popular.Pictures);
        }

        [Fact]
        public async Task A_gamerpic_saved_already_is_not_fetched_again_and_the_new_order_is_kept()
        {
            site.List = @"[{""id"":""fffe07d1-20002""},{""id"":""444d07d1-2808e""}]";
            await popular.FetchAsync();
            site.Asked.Clear();

            site.List = @"[{""id"":""444d07d1-2808e""},{""id"":""fffe07d1-20002""}]";
            await popular.FetchAsync();

            Assert.Equal(new[] { "https://xboxgamer.pics/api/popular?limit=100" }, site.Asked);
            Assert.Equal(new[] { PathOf("444d07d1-2808e"), PathOf("fffe07d1-20002") }, popular.Pictures);
        }

        [Fact]
        public async Task One_that_falls_out_of_the_list_stays_on_disk_for_whoever_wears_it_but_is_not_offered()
        {
            site.List = @"[{""id"":""fffe07d1-20002""},{""id"":""444d07d1-2808e""}]";
            await popular.FetchAsync();

            site.List = @"[{""id"":""444d07d1-2808e""}]";
            await popular.FetchAsync();

            Assert.Equal(new[] { PathOf("444d07d1-2808e") }, popular.Pictures);
            Assert.True(File.Exists(PathOf("fffe07d1-20002")));
        }

        [Fact]
        public async Task A_gamerpic_that_fails_is_left_out_with_no_file_and_tried_again_next_time()
        {
            site.List = @"[{""id"":""fffe07d1-20002""},{""id"":""444d07d1-2808e""}]";
            site.Missing.Add("titles/444d07d1/2808e.png");
            await popular.FetchAsync();

            Assert.Equal(new[] { PathOf("fffe07d1-20002") }, popular.Pictures);
            Assert.False(File.Exists(PathOf("444d07d1-2808e")));

            site.Missing.Clear();
            await popular.FetchAsync();
            Assert.Equal(new[] { PathOf("fffe07d1-20002"), PathOf("444d07d1-2808e") }, popular.Pictures);
        }

        [Fact]
        public async Task When_the_list_cant_be_had_the_fetch_throws_and_those_saved_before_are_still_offered()
        {
            site.List = @"[{""id"":""fffe07d1-20002""}]";
            await popular.FetchAsync();

            site.List = null;
            await Assert.ThrowsAsync<HttpRequestException>(() => popular.FetchAsync());
            Assert.Equal(new[] { PathOf("fffe07d1-20002") }, popular.Pictures);
        }

        [Fact]
        public async Task An_id_that_isnt_a_titles_and_a_pictures_in_hex_is_never_asked_for()
        {
            site.List = @"[{""id"":""../../evil""},{""id"":""fffe07d1-20002""},{""likes"":3}]";

            await popular.FetchAsync();

            Assert.Equal(new[] { PathOf("fffe07d1-20002") }, popular.Pictures);
            Assert.DoesNotContain(site.Asked, u => u.Contains("evil"));
        }

        /// <summary>xboxgamer.pics: the list, null for a 500, and each gamerpic, its bytes its own path, unless missing.</summary>
        private sealed class FakeSite : HttpMessageHandler
        {
            public string List { get; set; } = "[]";

            public HashSet<string> Missing { get; } = new HashSet<string>();

            public List<string> Asked { get; } = new List<string>();

            public static byte[] Png(string path) => System.Text.Encoding.UTF8.GetBytes(path);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri.ToString();
                lock (Asked)
                {
                    Asked.Add(url);
                }

                if (request.RequestUri.Host == "xboxgamer.pics")
                {
                    return Task.FromResult(List == null
                        ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(List) });
                }

                var path = request.RequestUri.AbsolutePath.TrimStart('/');
                return Task.FromResult(Missing.Contains(path)
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Png(path)) });
            }
        }
    }
}
