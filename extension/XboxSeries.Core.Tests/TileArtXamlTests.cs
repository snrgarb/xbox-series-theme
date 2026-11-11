using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Every game tile shows its whole art (#205): the art Uniform, centred, over a copy of it drawn 24 wide and stretched
    // to fill the tile, under a 40% black layer. One pair per package, TileArt in the extension and XboxTileArt in the
    // theme, which every listed tile wears rather than its own copy. The copy is the art the tile already loaded, drawn
    // small, so no tile decodes its art twice. The XAML isn't run here; these read it.
    public class TileArtXamlTests
    {
        private static string Extension(string file) => Path.Combine(RepoRoot, "extension", "XboxSeries.Extension", file);

        private static string Theme(params string[] parts) =>
            Path.Combine(new[] { RepoRoot, "theme", "XboxSeries.Dashboard" }.Concat(parts).ToArray());

        private static IEnumerable<XElement> Named(XElement root, string localName) =>
            root.DescendantsAndSelf().Where(e => e.Name.LocalName == localName);

        private static XElement PairTemplate(string file, string key)
        {
            var style = StyleByKey(XDocument.Load(file), key);
            Assert.Equal("ContentControl", (string)style.Attribute("TargetType"));
            return Named(style, "ControlTemplate").Single();
        }

        // The copy: an Image 24 wide of the tile's art, drawn into a picture its own size (BitmapCache) and stretched by a
        // Viewbox to fill the tile, scaled down to it in high quality; then the 40% black layer over it.
        private static void AssertBlurredCopy(XElement template)
        {
            var copy = Named(template, "Image").Single(i => (string)i.Attribute("Width") == "24");
            Assert.Equal("{Binding Content, RelativeSource={RelativeSource TemplatedParent}}", (string)copy.Attribute("Source"));
            Assert.Equal("Uniform", (string)copy.Attribute("Stretch"));
            Assert.Equal("HighQuality", (string)copy.Attribute("RenderOptions.BitmapScalingMode"));

            var cached = copy.Parent;
            Assert.Contains(cached.Elements(), c => c.Name.LocalName.EndsWith(".CacheMode") && c.Elements().Single().Name.LocalName == "BitmapCache");
            var viewbox = cached.Parent;
            Assert.Equal("Viewbox", viewbox.Name.LocalName);
            Assert.Equal("UniformToFill", (string)viewbox.Attribute("Stretch"));

            Assert.Contains(viewbox.ElementsAfterSelf(), e => e.Name.LocalName == "Rectangle" && (string)e.Attribute("Fill") == "#66000000");
            Assert.Empty(Named(template, "BlurEffect"));
        }

        [Fact]
        public void The_extensions_pair_is_the_art_whole_over_its_blurred_copy()
        {
            var template = PairTemplate(Extension("HomeTiles.xaml"), "TileArt");
            AssertBlurredCopy(template);

            var art = Named(template, "Image").Single(i => (string)i.Attribute("Width") != "24");
            Assert.Equal("{Binding Content, RelativeSource={RelativeSource TemplatedParent}}", (string)art.Attribute("Source"));
            Assert.Equal("Uniform", (string)art.Attribute("Stretch"));
            Assert.True(art.IsAfter(Named(template, "Rectangle").Single()), "the art is drawn over its copy");
            // Inside the tile: the copy, filling, is cut to the tile's edges.
            Assert.Equal("True", (string)Named(template, "Grid").First().Attribute("ClipToBounds"));
        }

        [Fact]
        public void The_themes_pair_is_the_art_whole_over_its_blurred_copy_inside_the_tiles_corners()
        {
            var template = PairTemplate(Theme("Constants.xaml"), "XboxTileArt");
            AssertBlurredCopy(template);

            // The art, Uniform and centred, painted in the tile's rounded shape over the copy.
            var art = Named(template, "ImageBrush").Single();
            Assert.Equal("{Binding Content, RelativeSource={RelativeSource TemplatedParent}}", (string)art.Attribute("ImageSource"));
            Assert.Equal("Uniform", (string)art.Attribute("Stretch"));
            Assert.Equal("{DynamicResource XboxTileArtCornerRadius}", (string)art.Parent.Parent.Attribute("CornerRadius"));
            Assert.True(art.IsAfter(Named(template, "Rectangle").Single()), "the art is drawn over its copy");

            // The copy is cut to the same rounded shape, the tile's corner radius, by a mask drawn once with it.
            var shape = Named(template, "Border").Single(b => (string)b.Attribute(X + "Name") == "Shape");
            Assert.Equal("{DynamicResource XboxTileArtCornerRadius}", (string)shape.Attribute("CornerRadius"));
            var mask = Named(template, "VisualBrush").Single();
            Assert.Equal("{Binding ElementName=Shape}", (string)mask.Attribute("Visual"));
            Assert.Contains(mask.Ancestors(), a => a.Name.LocalName == "Grid.OpacityMask");
            Assert.Equal("6", Named(XDocument.Load(Theme("Constants.xaml")).Root, "CornerRadius")
                .Single(c => (string)c.Attribute(X + "Key") == "XboxTileArtCornerRadius").Value);
        }

        // A tile wearing the pair: a ContentControl in the package's style, its content the given art.
        private static void AssertWearsPair(XElement tile, string style, string art)
        {
            var pair = Named(tile, "ContentControl").SingleOrDefault(c => (string)c.Attribute("Style") == style);
            Assert.True(pair != null, $"no {style} in {tile.Name.LocalName} {tile.Attribute(X + "Key")}");
            Assert.Equal(art, (string)pair.Attribute("Content"));
            // No art of its own drawn beside it, filling and cropped.
            Assert.DoesNotContain(Named(tile, "Image"), i => (string)i.Attribute("Stretch") == "UniformToFill");
        }

        [Fact]
        public void Homes_tiles_wear_the_pair()
        {
            // The Recent row, the Groups and Manage Queue's Recently installed all show HomeTileArt.
            var face = XDocument.Load(Extension("HomeTiles.xaml")).Descendants()
                .Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == "HomeTileArt");
            AssertWearsPair(face, "{StaticResource TileArt}", "{Binding CoverImage, Converter={StaticResource Image}, ConverterParameter=300}");

            foreach (var file in new[] { "RecentRowControl.xaml", "GroupsControl.xaml" })
            {
                Assert.Contains(XDocument.Load(Extension(file)).Descendants(),
                    e => (string)e.Attribute("ContentTemplate") == "{StaticResource HomeTileArt}");
            }
        }

        [Fact]
        public void Searchs_game_tiles_wear_the_pair()
        {
            var tile = XDocument.Load(Extension("SearchControl.xaml")).Descendants()
                .Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == "GameTile");
            AssertWearsPair(tile, "{StaticResource TileArt}", "{Binding CoverImage, Converter={StaticResource Image}, ConverterParameter=480}");
        }

        [Fact]
        public void Play_history_and_Queue_rows_wear_the_pair()
        {
            var doc = XDocument.Load(Extension("MyGamesControl.xaml"));
            XElement Template(string list) => doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == list)
                .Descendants().Single(e => e.Name.LocalName.EndsWith(".ItemTemplate")).Elements().Single();

            AssertWearsPair(Template("History"), "{StaticResource TileArt}", "{Binding Tile.CoverImage, Converter={StaticResource Image}, ConverterParameter=200}");
            AssertWearsPair(Template("QueueRows"), "{StaticResource TileArt}", "{Binding Cover, Converter={StaticResource Image}, ConverterParameter=300}");
        }

        [Fact]
        public void My_games_and_apps_grid_wears_the_pair_in_place_of_the_cover_stretch_setting()
        {
            var tile = StyleByKey(XDocument.Load(Theme("DerivedStyles", "ListGameItemTemplate.xaml")), "ListGameItemTemplate");
            AssertWearsPair(tile, "{DynamicResource XboxTileArt}", "{Binding Source, ElementName=PART_ImageCover}");

            // Playnite still loads the art, as it always has, into its part, which only hands it on.
            var part = Named(tile, "Image").Single(i => (string)i.Attribute(X + "Name") == "PART_ImageCover");
            Assert.Equal("Hidden", (string)part.Attribute("Visibility"));
            Assert.DoesNotContain(tile.DescendantsAndSelf().Attributes(), a => a.Value.Contains("CoverArtStretch"));
        }

        [Fact]
        public void The_Game_hubs_cover_card_wears_the_pair_in_its_own_corners()
        {
            var doc = XDocument.Load(Theme("Views", "GameDetails.xaml"));
            var part = doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PART_ImageCover");
            Assert.Equal("Hidden", (string)part.Attribute("Visibility"));

            var card = part.Ancestors().First(e => e.Name.LocalName == "ButtonEx");
            AssertWearsPair(card, "{DynamicResource XboxTileArt}", "{Binding Source, ElementName=PART_ImageCover}");
            // The card's corners (XboxHubCardTemplate's 4), not a grid tile's 6.
            Assert.Contains(card.Descendants(), e => e.Name.LocalName == "CornerRadius"
                                                     && (string)e.Attribute(X + "Key") == "XboxTileArtCornerRadius" && e.Value == "4");
        }
    }
}
