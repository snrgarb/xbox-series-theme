using System;
using System.IO;
using System.Linq;
using System.Globalization;
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
        public void The_bar_wears_the_Dashboards_fonts_and_no_effect()
        {
            var xaml = HintBar();

            Assert.All(xaml.Descendants().Where(e => e.Name.LocalName == "TextBlock"),
                t => Assert.StartsWith("{DynamicResource Xbox", (string)t.Attribute("FontFamily")));
            Assert.DoesNotContain(xaml.Descendants(), e => e.Name.LocalName.EndsWith("Effect") || e.Name.LocalName.EndsWith(".Effect"));
        }

        // Measured on the console's My games capture (L1-games.png, 720 tall), times 1.5 for the 1080-tall page.
        [Fact]
        public void The_bar_is_the_consoles_opaque_near_black_rectangle_with_small_rounded_corners()
        {
            var bar = Bar();
            var background = (string)bar.Attribute("Background");

            Assert.Matches("^#FF[0-9A-F]{6}$", background);
            Assert.All(new[] { 3, 5, 7 }, i => Assert.True(int.Parse(background.Substring(i, 2), NumberStyles.HexNumber) < 0x10));
            Assert.Equal("6", (string)bar.Attribute("CornerRadius"));
            Assert.Equal("63", (string)bar.Attribute("Height"));
        }

        [Fact]
        public void The_bar_stands_60_in_from_the_right_and_52_up_from_the_bottom()
        {
            Assert.Equal("0,0,60,52", (string)Bar().Attribute("Margin"));
        }

        [Fact]
        public void Each_hint_is_a_36_ring_then_a_25_label()
        {
            var xaml = HintBar();
            var label = xaml.Descendants().Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding Text}");

            Assert.Equal("25", (string)label.Attribute("FontSize"));
            Assert.Equal("{DynamicResource XboxFont}", (string)label.Attribute("FontFamily"));
            Assert.All(new[] { "MenuGlyph", "YGlyph" }, name =>
            {
                var glyph = Named(xaml, name);
                Assert.Equal("36", (string)glyph.Attribute("Width"));
                Assert.Equal("36", (string)glyph.Attribute("Height"));
            });
        }

        [Fact]
        public void The_bar_comes_up_once_the_page_has_settled_without_a_fade_or_a_cache()
        {
            Assert.DoesNotContain(Bar().Elements(), e => e.Name.LocalName == "Border.CacheMode");
            Assert.Null(Bar().Attribute("Opacity"));
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "HintBarControl.xaml.cs"));
            Assert.DoesNotContain("DoubleAnimation", code);
            Assert.Contains("DispatcherPriority.ContextIdle", code);
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
            var glyph = Named(HintBar(), "MenuGlyph");

            Assert.Contains(glyph.Elements(), e => e.Name.LocalName == "Ellipse" && (string)e.Attribute("Stroke") == "{DynamicResource XboxTextBrush}");
            Assert.Contains(glyph.Elements(), e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "");
        }

        [Fact]
        public void Y_shows_a_yellow_Y_in_a_yellow_ring_in_Menus_place()
        {
            var xaml = HintBar();
            var glyph = Named(xaml, "YGlyph");
            var ring = glyph.Elements().Single(e => e.Name.LocalName == "Ellipse");
            var letter = glyph.Elements().Single(e => e.Name.LocalName == "TextBlock");

            Assert.Equal("Collapsed", (string)glyph.Attribute("Visibility"));
            Assert.Equal(XboxYellow, (string)ring.Attribute("Stroke"));
            Assert.Equal("Y", (string)letter.Attribute("Text"));
            Assert.Equal(XboxYellow, (string)letter.Attribute("Foreground"));

            var onY = xaml.Descendants().Single(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Value") == "Y");
            Assert.Equal("{Binding Button}", (string)onY.Attribute("Binding"));
            Assert.Contains(onY.Elements(), s => (string)s.Attribute("TargetName") == "YGlyph" && (string)s.Attribute("Value") == "Visible");
            Assert.Contains(onY.Elements(), s => (string)s.Attribute("TargetName") == "MenuGlyph" && (string)s.Attribute("Value") == "Collapsed");
        }

        private const string XboxYellow = "#FFFFB900";

        private static XElement Named(XDocument xaml, string name) =>
            xaml.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

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
