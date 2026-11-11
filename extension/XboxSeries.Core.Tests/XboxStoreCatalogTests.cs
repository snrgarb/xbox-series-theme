using System;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // What the extension asks of the Xbox store's catalog and makes of its answers: a game's product is the one whose
    // title is the game's name, nothing looser, and its box art and poster come at the sizes tiles want. Which art a tile
    // shows is chosen for its shape.
    public class XboxStoreCatalogTests
    {
        private static string Search(params string[] products) =>
            "{\"Results\":[{\"ProductFamilyName\":\"Games\",\"Products\":[" + string.Join(",", products) + "]}],\"TotalResultCount\":1}";

        private static string Product(string id, string title, string type = "Game") =>
            $"{{\"ProductId\":\"{id}\",\"Type\":\"{type}\",\"Title\":\"{title}\"}}";

        [Theory]
        [InlineData("Stardew Valley", "stardew valley")]
        [InlineData("  Halo Infinite™ ", "halo infinite")]
        [InlineData("Forza Horizon 5®", "forza horizon 5")]
        [InlineData("Ori and   the Will\tof the Wisps", "ori and the will of the wisps")]
        [InlineData("Minecraft© Dungeons", "minecraft dungeons")]
        [InlineData(null, "")]
        public void A_title_is_compared_trimmed_without_marks_its_spaces_made_one_in_lower_case(string title, string normalised)
        {
            Assert.Equal(normalised, XboxStoreCatalog.Normalise(title));
        }

        [Fact]
        public void The_product_is_the_first_game_whose_title_is_the_name()
        {
            var json = "{\"Results\":["
                       + "{\"Products\":[" + Product("C3D891Z6TNQM", "Stardew Valley") + "]},"
                       + "{\"Products\":[" + Product("9MWR1NC6VQ6L", "Stardew Valley") + "]}]}";

            Assert.Equal("C3D891Z6TNQM", XboxStoreCatalog.PickProduct("Stardew Valley", json));
        }

        [Fact]
        public void A_title_with_a_mark_or_other_case_still_matches()
        {
            Assert.Equal("9NP1P1WFS0LB", XboxStoreCatalog.PickProduct("halo infinite", Search(Product("9NP1P1WFS0LB", "Halo Infinite™"))));
        }

        [Theory]
        [InlineData("Hollow Knight", "Hollow Knight: Silksong")]
        [InlineData("Minecraft", "Minecraft Legends")]
        public void A_different_game_of_the_same_series_is_no_match(string name, string found)
        {
            Assert.Null(XboxStoreCatalog.PickProduct(name, Search(Product("9N116V7ZXKLW", found))));
        }

        [Fact]
        public void An_exact_match_further_down_is_picked_over_the_first_answer()
        {
            var json = Search(Product("AAAA", "Minecraft Legends"), Product("BBBB", "Minecraft"));

            Assert.Equal("BBBB", XboxStoreCatalog.PickProduct("Minecraft", json));
        }

        [Fact]
        public void A_product_that_isnt_a_game_is_no_match()
        {
            Assert.Null(XboxStoreCatalog.PickProduct("Hades", Search(Product("AAAA", "Hades", type: "Durable"))));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{\"Results\":null}")]
        [InlineData("{\"Results\":[]}")]
        public void An_answer_that_isnt_the_catalogs_JSON_or_finds_nothing_is_no_match(string json)
        {
            Assert.Null(XboxStoreCatalog.PickProduct("Hades", json));
        }

        [Fact]
        public void An_empty_name_is_never_looked_up_against_an_empty_title()
        {
            Assert.Null(XboxStoreCatalog.PickProduct("  ", Search(Product("AAAA", ""))));
        }

        [Fact]
        public void The_search_and_product_addresses_carry_the_name_and_id_escaped()
        {
            Assert.Equal(
                "https://displaycatalog.mp.microsoft.com/v7.0/productFamilies/autosuggest?market=US&languages=en-US&productFamilyNames=Games&query=Ori%20and%20the%20Will%20of%20the%20Wisps%20%26%20more",
                XboxStoreCatalog.SearchUrl("Ori and the Will of the Wisps & more"));
            Assert.Equal("https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=C3D891Z6TNQM&market=US&languages=en-US",
                XboxStoreCatalog.ProductUrl("C3D891Z6TNQM"));
        }

        [Fact]
        public void A_products_box_art_and_poster_come_with_https_at_the_sizes_tiles_want()
        {
            var json = "{\"Products\":[{\"LocalizedProperties\":[{\"Images\":["
                       + "{\"ImagePurpose\":\"Screenshot\",\"Uri\":\"//store-images.s-microsoft.com/image/shot\",\"Width\":1920,\"Height\":1080},"
                       + "{\"ImagePurpose\":\"Poster\",\"Uri\":\"//store-images.s-microsoft.com/image/poster\",\"Width\":720,\"Height\":1080},"
                       + "{\"ImagePurpose\":\"BoxArt\",\"Uri\":\"//store-images.s-microsoft.com/image/box\",\"Width\":1080,\"Height\":1080},"
                       + "{\"ImagePurpose\":\"BoxArt\",\"Uri\":\"//store-images.s-microsoft.com/image/box2\",\"Width\":1080,\"Height\":1080}"
                       + "]}]}]}";

            var art = XboxStoreCatalog.ArtOf(json);

            Assert.Equal("https://store-images.s-microsoft.com/image/box?w=600&h=600&format=jpg&q=90", art.BoxArt);
            Assert.Equal("https://store-images.s-microsoft.com/image/poster?w=600&h=900&format=jpg&q=90", art.Poster);
        }

        [Fact]
        public void A_product_without_a_poster_has_only_its_box_art()
        {
            var json = "{\"Products\":[{\"LocalizedProperties\":[{\"Images\":["
                       + "{\"ImagePurpose\":\"BoxArt\",\"Uri\":\"https://store-images.s-microsoft.com/image/box\"}]}]}]}";

            var art = XboxStoreCatalog.ArtOf(json);

            Assert.Equal("https://store-images.s-microsoft.com/image/box?w=600&h=600&format=jpg&q=90", art.BoxArt);
            Assert.Null(art.Poster);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not json")]
        [InlineData("{\"Products\":[]}")]
        [InlineData("{\"Products\":[{\"LocalizedProperties\":[]}]}")]
        public void Details_that_arent_the_catalogs_JSON_have_no_art(string json)
        {
            var art = XboxStoreCatalog.ArtOf(json);

            Assert.Null(art.BoxArt);
            Assert.Null(art.Poster);
        }

        [Fact]
        public void A_square_tile_shows_the_box_art_and_a_tall_one_the_poster()
        {
            var art = new XboxArt(@"C:\art\box.jpg", @"C:\art\poster.jpg");

            Assert.Equal(@"C:\art\box.jpg", TileArt.For(TileShape.Square, @"C:\cover.jpg", art));
            Assert.Equal(@"C:\art\poster.jpg", TileArt.For(TileShape.Tall, @"C:\cover.jpg", art));
        }

        [Fact]
        public void Without_Xbox_art_of_its_shape_a_tile_shows_the_cover()
        {
            Assert.Equal(@"C:\cover.jpg", TileArt.For(TileShape.Square, @"C:\cover.jpg", null));
            Assert.Equal(@"C:\cover.jpg", TileArt.For(TileShape.Tall, @"C:\cover.jpg", new XboxArt(@"C:\art\box.jpg", null)));
            Assert.Equal(@"C:\cover.jpg", TileArt.For(TileShape.Square, @"C:\cover.jpg", new XboxArt(null, @"C:\art\poster.jpg")));
            Assert.Null(TileArt.XboxArtFor(TileShape.Tall, new XboxArt(@"C:\art\box.jpg", null)));
        }
    }

    // What the extension remembers of each game's lookup across starts: a matched game isn't asked again while its art
    // is there, one with no match only after some days, and one renamed at once.
    public class XboxArtIndexTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);
        private readonly Guid game = Guid.NewGuid();

        [Fact]
        public void A_game_never_looked_up_is_due()
        {
            Assert.True(new XboxArtIndex().IsDue(game, "Hades", Now));
        }

        [Fact]
        public void A_game_matched_is_not_due_again_while_its_art_is_there()
        {
            var index = new XboxArtIndex();
            index.Record(game, "Hades", Now, "9P8DL6W5MTJ9", "9P8DL6W5MTJ9.box.jpg", "9P8DL6W5MTJ9.poster.jpg");

            Assert.False(index.IsDue(game, "Hades", Now.AddYears(1)));
            Assert.True(index.IsDue(game, "Hades", Now, filesThere: false));
        }

        [Fact]
        public void A_game_with_no_match_is_due_again_only_after_some_days()
        {
            var index = new XboxArtIndex();
            index.Record(game, "My Homebrew", Now, null, "ignored.jpg", null);

            Assert.Null(index.Of(game).BoxArt);
            Assert.False(index.IsDue(game, "My Homebrew", Now.AddDays(1)));
            Assert.False(index.IsDue(game, "My Homebrew", Now + XboxArtIndex.RetryNoMatchAfter - TimeSpan.FromMinutes(1)));
            Assert.True(index.IsDue(game, "My Homebrew", Now + XboxArtIndex.RetryNoMatchAfter));
        }

        [Fact]
        public void A_game_renamed_is_due_at_once()
        {
            var index = new XboxArtIndex();
            index.Record(game, "Hades", Now, null, null, null);

            Assert.True(index.IsDue(game, "Hades II", Now));
        }

        [Fact]
        public void It_is_kept_as_JSON_and_read_back_the_same()
        {
            var index = new XboxArtIndex();
            index.Record(game, "Hades", Now, "9P8DL6W5MTJ9", "9P8DL6W5MTJ9.box.jpg", null);

            var read = XboxArtIndex.FromJson(index.ToJson()).Of(game);

            Assert.Equal("Hades", read.Name);
            Assert.Equal("9P8DL6W5MTJ9", read.ProductId);
            Assert.Equal("9P8DL6W5MTJ9.box.jpg", read.BoxArt);
            Assert.Null(read.Poster);
            Assert.Equal(Now, read.CheckedAt.ToUniversalTime());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("[1, 2]")]
        public void An_index_that_cant_be_read_is_empty_so_every_game_is_asked_again(string json)
        {
            Assert.Empty(XboxArtIndex.FromJson(json).All);
        }
    }
}
