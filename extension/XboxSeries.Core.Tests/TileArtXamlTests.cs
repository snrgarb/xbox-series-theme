using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Every game tile fills itself with its art, cropped to the tile's shape, with no bands, as on the console (it was
    // briefly the whole art over a blurred copy, #205, which left bands top and bottom). The art is the game's Xbox art of
    // the tile's shape where the extension has it (HomeTile.Art), else its cover, cropped from the top so a 2:3 cover's
    // logo stays on a square tile. One pair per package, TileArt in the extension and XboxTileArt in the theme, which
    // every listed tile wears rather than its own copy; the theme's tiles get the Xbox art from the extension's elements
    // over their covers. The XAML isn't run here; these read it.
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

        // No blurred copy and nothing over the art: no small copy, no darkening layer, no cache or mask, no blur.
        private static void AssertNoCopy(XElement template)
        {
            Assert.DoesNotContain(Named(template, "Image"), i => (string)i.Attribute("Width") == "24");
            Assert.Empty(Named(template, "Viewbox"));
            Assert.Empty(Named(template, "Rectangle"));
            Assert.Empty(Named(template, "BitmapCache"));
            Assert.Empty(Named(template, "VisualBrush"));
            Assert.Empty(Named(template, "BlurEffect"));
        }

        [Fact]
        public void The_extensions_pair_is_the_art_filling_the_tile()
        {
            var template = PairTemplate(Extension("HomeTiles.xaml"), "TileArt");
            AssertNoCopy(template);

            var art = Named(template, "Image").Single();
            Assert.Equal("{Binding Content, RelativeSource={RelativeSource TemplatedParent}}", (string)art.Attribute("Source"));
            Assert.Equal("UniformToFill", (string)art.Attribute("Stretch"));
            Assert.Equal("Center", (string)art.Attribute("HorizontalAlignment"));
            Assert.Equal("Top", (string)art.Attribute("VerticalAlignment"));
            Assert.Equal("Fant", (string)art.Attribute("RenderOptions.BitmapScalingMode"));
            // Cropped to the tile's edges.
            Assert.Equal("True", (string)Named(template, "Grid").First().Attribute("ClipToBounds"));
        }

        [Fact]
        public void The_themes_pair_is_the_art_filling_the_tile_inside_its_corners()
        {
            var template = PairTemplate(Theme("Constants.xaml"), "XboxTileArt");
            AssertNoCopy(template);

            // The art, filling, cropped from the top, painted in the tile's rounded shape.
            var art = Named(template, "ImageBrush").Single();
            Assert.Equal("{Binding Content, RelativeSource={RelativeSource TemplatedParent}}", (string)art.Attribute("ImageSource"));
            Assert.Equal("UniformToFill", (string)art.Attribute("Stretch"));
            Assert.Equal("Center", (string)art.Attribute("AlignmentX"));
            Assert.Equal("Top", (string)art.Attribute("AlignmentY"));
            Assert.Equal("{DynamicResource XboxTileArtCornerRadius}", (string)art.Parent.Parent.Attribute("CornerRadius"));
            Assert.Equal("6", Named(XDocument.Load(Theme("Constants.xaml")).Root, "CornerRadius")
                .Single(c => (string)c.Attribute(X + "Key") == "XboxTileArtCornerRadius").Value);
        }

        // A tile wearing the pair: a ContentControl in the package's style, its content the given art.
        private static void AssertWearsPair(XElement tile, string style, string art)
        {
            var pair = Named(tile, "ContentControl").SingleOrDefault(c => (string)c.Attribute("Style") == style);
            Assert.True(pair != null, $"no {style} in {tile.Name.LocalName} {tile.Attribute(X + "Key")}");
            Assert.Equal(art, (string)pair.Attribute("Content"));
            // No art of its own drawn beside it.
            Assert.DoesNotContain(Named(tile, "Image"), i => (string)i.Attribute("Stretch") == "UniformToFill");
        }

        [Fact]
        public void Homes_tiles_wear_the_pair()
        {
            // The Recent row, the Groups and Manage Queue's Recently installed all show HomeTileArt.
            var face = XDocument.Load(Extension("HomeTiles.xaml")).Descendants()
                .Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == "HomeTileArt");
            AssertWearsPair(face, "{StaticResource TileArt}", "{Binding Art, Converter={StaticResource Image}, ConverterParameter=300}");

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
            AssertWearsPair(tile, "{StaticResource TileArt}", "{Binding Art, Converter={StaticResource Image}, ConverterParameter=480}");

            // The title stands in for want of a picture shown, not of a path: a cover set but missing or corrupt shows it too.
            var cover = Named(tile, "ContentControl").Single(c => (string)c.Attribute("Style") == "{StaticResource TileArt}");
            Assert.Contains(Named(tile, "DataTrigger"), t => (string)t.Attribute("Binding") == $"{{Binding Content, ElementName={(string)cover.Attribute(X + "Name")}}}"
                                                             && (string)t.Attribute("Value") == "{x:Null}");
            Assert.DoesNotContain(Named(tile, "DataTrigger"), t => (string)t.Attribute("Binding") == "{Binding CoverImage}" || (string)t.Attribute("Binding") == "{Binding Art}");
        }

        [Fact]
        public void Play_history_and_Queue_rows_wear_the_pair()
        {
            var doc = XDocument.Load(Extension("MyGamesControl.xaml"));
            XElement Template(string list) => doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == list)
                .Descendants().Single(e => e.Name.LocalName.EndsWith(".ItemTemplate")).Elements().Single();

            AssertWearsPair(Template("History"), "{StaticResource TileArt}", "{Binding Tile.Art, Converter={StaticResource Image}, ConverterParameter=200}");
            AssertWearsPair(Template("QueueRows"), "{StaticResource TileArt}", "{Binding Cover, Converter={StaticResource Image}, ConverterParameter=300}");

            // A Queue row's Cover is its tile's art, the Xbox box art when the game has it.
            Assert.Contains("cover = value.Tile.Art;", File.ReadAllText(Extension("MyGamesControl.xaml.cs")));
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
            // The uninstalled darkening follows the pair's corners.
            Assert.Equal("{DynamicResource XboxTileArtCornerRadius}",
                (string)Named(tile, "Border").Single(b => (string)b.Attribute(X + "Name") == "BorderDarken").Attribute("CornerRadius"));
        }

        [Fact]
        public void Play_history_and_See_alls_collages_show_each_games_art_cropped_from_the_top()
        {
            var collage = StyleByKey(XDocument.Load(Extension("HomeTiles.xaml")), "TileCollage");
            var art = Named(collage, "Image").Single();

            Assert.Equal("{Binding Art, Converter={StaticResource Image}, ConverterParameter=150}", (string)art.Attribute("Source"));
            Assert.Equal("UniformToFill", (string)art.Attribute("Stretch"));
            Assert.Equal("Top", (string)art.Attribute("VerticalAlignment"));
        }

        // The extension's element over a theme tile's cover: named for the extension, inert, after the cover so it is
        // drawn over it, and never focused or hit, so the tile behaves as without it.
        private static void AssertXboxArtOverCover(XElement tile, string element)
        {
            var cover = Named(tile, "ContentControl").Single(c => (string)c.Attribute("Style") == "{DynamicResource XboxTileArt}");
            var over = cover.ElementsAfterSelf().FirstOrDefault();
            Assert.True(over != null && (string)over.Attribute(X + "Name") == element, $"no {element} right after the cover");
            Assert.Equal("ContentControl", over.Name.LocalName);
            Assert.Equal("False", (string)over.Attribute("Focusable"));
            Assert.Equal("False", (string)over.Attribute("IsTabStop"));
            Assert.Equal("False", (string)over.Attribute("IsHitTestVisible"));
        }

        [Fact]
        public void My_games_and_apps_grid_shows_the_extensions_box_art_over_its_cover()
        {
            var tile = StyleByKey(XDocument.Load(Theme("DerivedStyles", "ListGameItemTemplate.xaml")), "ListGameItemTemplate");
            AssertXboxArtOverCover(tile, "XboxSeries_BoxArt");
        }

        [Fact]
        public void The_Game_hubs_cover_card_shows_the_extensions_poster_over_its_cover_in_the_cards_corners()
        {
            var doc = XDocument.Load(Theme("Views", "GameDetails.xaml"));
            var card = doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PART_ImageCover").Ancestors().First(e => e.Name.LocalName == "ButtonEx");
            AssertXboxArtOverCover(card, "XboxSeries_Poster");

            // The card's corners are set for everything in it, the poster's XboxTileArt as well as the cover's.
            var corners = card.Descendants().Single(e => e.Name.LocalName == "CornerRadius" && (string)e.Attribute(X + "Key") == "XboxTileArtCornerRadius");
            Assert.Equal("Grid.Resources", corners.Parent.Name.LocalName);
        }

        [Fact]
        public void The_extension_offers_the_Xbox_art_elements_and_draws_them_in_the_themes_pair()
        {
            var plugin = File.ReadAllText(Extension("XboxSeriesPlugin.cs"));
            Assert.Contains("\"Hints\", \"BoxArt\", \"Poster\" }", plugin);
            Assert.Contains("case \"BoxArt\": return new XboxArtElement(core, TileShape.Square, 480);", plugin);
            Assert.Contains("case \"Poster\": return new XboxArtElement(core, TileShape.Tall, 300);", plugin);

            // Made before Home's build is counted or Start-up is begun: a grid tile isn't part of Home.
            Assert.True(plugin.IndexOf("case \"BoxArt\"", System.StringComparison.Ordinal) < plugin.IndexOf("BeginStartup();", plugin.IndexOf("GetGameViewControl", System.StringComparison.Ordinal), System.StringComparison.Ordinal));

            var element = File.ReadAllText(Extension("XboxArtElement.cs"));
            Assert.Contains("art.SetResourceReference(StyleProperty, \"XboxTileArt\");", element);
            Assert.Contains("core.XboxArtFor(gameId.Value, shape)", element);
            Assert.Contains("Unloaded += (s, e) => core.XboxArtChanged -= Refresh;", element);
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
