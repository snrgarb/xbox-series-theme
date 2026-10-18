using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The hint bar at the Dashboard's bottom right. The XAML isn't run here; these checks guard where it sits, that it
    // follows the page's edges rather than fixed coordinates, and that it costs the 4K TV nothing as focus moves.
    public class HintBarXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        [Fact]
        public void The_bar_sits_over_the_Game_hub_and_under_the_tile_menus_dim()
        {
            var main = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Views", "Main.xaml"));
            var host = main.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PART_MainHost");
            var layers = host.Elements().Select(e => (string)e.Attribute(X + "Name")).ToList();

            var hints = layers.IndexOf("XboxSeries_Hints");
            Assert.True(hints > layers.IndexOf("PART_ElemGameDetails"));
            Assert.True(hints < layers.IndexOf("XboxSeries_TileMenu"));
        }

        [Fact]
        public void The_extension_offers_the_bar_as_a_custom_element()
        {
            var plugin = File.ReadAllText(Path.Combine(ExtensionDir, "XboxSeriesPlugin.cs"));

            Assert.Contains("\"Hints\" }", plugin);
            Assert.Contains("case \"Hints\": return new HintBarControl(core);", plugin);
        }

        [Fact]
        public void The_bar_is_anchored_to_the_bottom_right_corner_not_placed()
        {
            var bar = Bar();

            Assert.Equal("Right", (string)bar.Attribute("HorizontalAlignment"));
            Assert.Equal("Bottom", (string)bar.Attribute("VerticalAlignment"));
            Assert.Null(bar.Attribute("Width"));
            Assert.DoesNotContain(HintBar().Descendants(), e => e.Name.LocalName == "Canvas" || e.Name.LocalName == "Viewbox");
        }

        [Fact]
        public void The_bar_wears_the_Dashboards_brushes_and_fonts_and_no_effect()
        {
            var xaml = HintBar();

            Assert.Equal("{DynamicResource XboxChipBrush}", (string)Bar().Attribute("Background"));
            Assert.All(xaml.Descendants().Where(e => e.Name.LocalName == "TextBlock"),
                t => Assert.StartsWith("{DynamicResource Xbox", (string)t.Attribute("FontFamily")));
            Assert.DoesNotContain(xaml.Descendants(), e => e.Name.LocalName.EndsWith("Effect") || e.Name.LocalName.EndsWith(".Effect"));
        }

        [Fact]
        public void The_bar_is_drawn_once_into_a_picture_sharp_at_4K_and_only_faded()
        {
            var cache = Bar().Elements().Single(e => e.Name.LocalName == "Border.CacheMode").Elements().Single();

            Assert.Equal("BitmapCache", cache.Name.LocalName);
            Assert.Equal("2", (string)cache.Attribute("RenderAtScale"));
        }

        [Fact]
        public void The_bar_is_hidden_rather_than_collapsed_so_showing_it_lays_nothing_out()
        {
            Assert.Equal("Hidden", (string)Bar().Attribute("Visibility"));
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "HintBarControl.xaml.cs"));
            Assert.DoesNotContain("Visibility.Collapsed", code);
        }

        [Fact]
        public void More_options_shows_Menus_three_lines_in_a_white_ring()
        {
            var glyph = HintBar().Descendants().Single(e => (string)e.Attribute(X + "Name") == "MenuGlyph");

            Assert.Contains(glyph.Elements(), e => e.Name.LocalName == "Ellipse" && (string)e.Attribute("Stroke") == "{DynamicResource XboxTextBrush}");
            Assert.Contains(glyph.Elements(), e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "");
        }

        private static XDocument HintBar() => XDocument.Load(Path.Combine(ExtensionDir, "HintBarControl.xaml"));

        private static XElement Bar() => HintBar().Descendants().Single(e => (string)e.Attribute(X + "Name") == "Bar");

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
