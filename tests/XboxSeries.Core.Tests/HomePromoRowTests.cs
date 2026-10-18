using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Home's first screen: the Recent row, and under it the console's row of four wide tiles, placeholders for now (#65).
    // The XAML isn't run here; these checks guard what the row shows, that focus only lights a tile, and that it lines
    // up with the Recent row and fits on the first screen whatever the screen's width at 1080 tall.
    public class HomePromoRowTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string ExtensionDir = Path.Combine(FindRepoRoot(), "src", "XboxSeries.Extension");

        [Fact]
        public void The_row_shows_four_placeholder_tiles_with_dummy_text()
        {
            var tiles = Tiles();

            Assert.Equal(4, tiles.Length);
            Assert.Equal("Browse the store", Title(tiles[0]));
            Assert.Equal("Best-rated games", Title(tiles[2]));

            // The promos, 2 and 4, carry a title and a dimmer subtitle, as the console's.
            foreach (var promo in new[] { tiles[1], tiles[3] })
            {
                var text = promo.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToArray();
                Assert.Equal(2, text.Length);
                Assert.All(text, t => Assert.False(string.IsNullOrWhiteSpace((string)t.Attribute("Text"))));
                Assert.Equal("{DynamicResource XboxTextDimBrush}", (string)text[1].Attribute("Foreground"));
            }

            // Browse the store wears the store glyph, and Best-rated games five stars, centred.
            Assert.Contains(Glyphs(tiles[0]), g => g == "");
            Assert.Equal(5, Glyphs(tiles[2]).Single().Count(c => c == ''));
        }

        [Fact]
        public void A_focused_tile_shows_the_shared_glow_and_nothing_else()
        {
            var row = PromoRow();
            var style = row.Descendants().Single(e => (string)e.Attribute(X + "Key") == "PromoTile");
            var template = style.Descendants().Single(e => e.Name.LocalName == "ControlTemplate");

            // The ring carries the shared glow under its cache, hidden (never collapsed) until focus.
            var ring = template.Descendants().Single(e => (string)e.Attribute(X + "Name") == "FocusRing");
            Assert.Equal("Hidden", (string)ring.Attribute("Visibility"));
            var cache = ring.Elements().Single();
            Assert.Equal("{DynamicResource XboxFocusGlowCache}", (string)cache.Attribute("Style"));
            Assert.Equal("{DynamicResource XboxFocusGlow}", (string)cache.Elements().Single().Attribute("Style"));

            // Focus shows the ring, and that is all it does: no growing, no title change.
            var setters = template.Descendants().Where(e => e.Name.LocalName == "ControlTemplate.Triggers")
                .SelectMany(t => t.Descendants().Where(e => e.Name.LocalName == "Setter")).ToArray();
            var setter = Assert.Single(setters);
            Assert.Equal("FocusRing", (string)setter.Attribute("TargetName"));
            Assert.Equal("Visibility", (string)setter.Attribute("Property"));
            Assert.Equal("Visible", (string)setter.Attribute("Value"));
            string[] growth = { "Storyboard", "ScaleTransform", "RenderTransform", "LayoutTransform", "EnterActions", "DoubleAnimation" };
            Assert.DoesNotContain(row.Descendants(), e => growth.Any(g => e.Name.LocalName.Contains(g)));
            Assert.All(Tiles(), tile => Assert.Equal("{StaticResource PromoTile}", (string)tile.Attribute("Style")));
        }

        [Fact]
        public void The_tiles_fill_the_Recent_rows_width_with_even_gaps()
        {
            var (recent, promos) = Viewboxes();

            // Each row at the same 1080p width, in a Viewbox with the same side margins, so both scale alike and line up.
            Assert.Equal(Side(recent.Attribute("Margin")), Side(promos.Attribute("Margin")));
            Assert.Equal(Width(recent), Width(promos));

            var grid = PromoRow().Descendants().Single(e => e.Name.LocalName == "Grid" && (string)e.Attribute(X + "Name") == "Tiles");
            var columns = grid.Descendants().Where(e => e.Name.LocalName == "ColumnDefinition").Select(c => (string)c.Attribute("Width")).ToArray();
            Assert.Equal(new[] { "*", "30", "*", "30", "*", "30", "*" }, columns);
        }

        [Theory]
        [InlineData(1663)]
        [InlineData(1920)]
        public void The_Recent_row_and_the_tiles_fit_on_the_first_screen(double screenWidth)
        {
            // The Recent row's slot in Main.xaml starts 480 down a 1080-tall screen: under the 200 of the top bar's row and
            // the 280 left of the 880 below it by its 560 and the 40 under it.
            const double slotTop = 480;
            var (recent, promos) = Viewboxes();
            var margin = Numbers(recent.Attribute("Margin"));
            var scale = (screenWidth - margin[0] - margin[2]) / Width(recent);

            var recentHeight = Number(recent.Elements().Single().Attribute("Height"));
            var promosContent = promos.Elements().Single();
            var promosHeight = Number(promosContent.Attribute("Height"));
            var bottom = slotTop + margin[1] + (recentHeight + promosHeight) * scale;

            // The tiles' ring stands 8 out from them, and clears the screen's bottom edge.
            Assert.True(bottom + 8 * scale <= 1080, $"tiles end at {bottom:0} on a {screenWidth}-wide screen");
        }

        [Fact]
        public void The_tiles_sit_under_the_Recent_row_in_the_same_control_so_they_fly_with_it()
        {
            var recentRow = XDocument.Load(Path.Combine(ExtensionDir, "RecentRowControl.xaml"));
            var host = recentRow.Descendants().Single(e => e.Name.LocalName == "PromoRow");
            Assert.Equal("Promos", (string)host.Attribute(X + "Name"));
        }

        private static XElement PromoRow() => XDocument.Load(Path.Combine(ExtensionDir, "PromoRow.xaml")).Root;

        private static XElement[] Tiles() =>
            PromoRow().Descendants().Single(e => e.Name.LocalName == "Grid" && (string)e.Attribute(X + "Name") == "Tiles")
                .Elements().Where(e => e.Name.LocalName == "Button").ToArray();

        private static string Title(XElement tile) =>
            (string)tile.Descendants().First(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Style") == "{StaticResource Title}").Attribute("Text");

        private static string[] Glyphs(XElement tile) =>
            tile.Descendants().Where(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Style") == "{StaticResource Glyph}")
                .Select(e => (string)e.Attribute("Text")).ToArray();

        /// <summary>The Recent row's Viewbox and the tiles' one, in RecentRowControl.xaml.</summary>
        private static (XElement Recent, XElement Promos) Viewboxes()
        {
            var recentRow = XDocument.Load(Path.Combine(ExtensionDir, "RecentRowControl.xaml"));
            var boxes = recentRow.Descendants().Where(e => e.Name.LocalName == "Viewbox").ToArray();
            Assert.Equal(2, boxes.Length);
            return (boxes[0], boxes[1]);
        }

        private static double Width(XElement viewbox) => Number(viewbox.Elements().Single().Attribute("Width"));

        private static string Side(XAttribute margin)
        {
            var m = Numbers(margin);
            return m[0] + "," + m[2];
        }

        private static double[] Numbers(XAttribute attribute) =>
            ((string)attribute).Split(',').Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToArray();

        private static double Number(XAttribute attribute) => double.Parse((string)attribute, CultureInfo.InvariantCulture);

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XboxSeries.slnx")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
        }
    }
}
