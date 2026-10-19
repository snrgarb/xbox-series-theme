using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Search's page and the Console keyboard (#39). The XAML isn't run here; these checks guard what the Dashboard binds
    // to, the keyboard's grid matching the core's 5 by 12 layout, the fonts (#77), and that the keyboard's glow is one ring.
    public class SearchXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        [Fact]
        public void The_extension_offers_the_page_as_a_custom_element_with_what_the_Dashboard_binds()
        {
            var plugin = File.ReadAllText(Path.Combine(ExtensionDir, "XboxSeriesPlugin.cs"));
            var page = File.ReadAllText(Path.Combine(ExtensionDir, "SearchControl.xaml.cs"));

            Assert.Contains("\"Search\"", plugin);
            Assert.Contains("case \"Search\": return new SearchControl(core);", plugin);
            Assert.Contains("public SearchState State", page);
            Assert.Contains("public ICommand OpenSearchCommand", page);
        }

        [Fact]
        public void The_keyboards_grids_are_5_rows_by_12_columns()
        {
            var grids = Load("ConsoleKeyboardControl.xaml").Descendants().Where(e => e.Name.LocalName == "Grid"
                && e.Elements().Any(d => d.Name.LocalName == "Grid.RowDefinitions")).ToList();

            // The keys' grid and the focus ring's, laid over it, alike.
            Assert.Equal(2, grids.Count);
            foreach (var grid in grids)
            {
                Assert.Equal(5, grid.Descendants().Count(e => e.Name.LocalName == "RowDefinition"));
                Assert.Equal(12, grid.Descendants().Count(e => e.Name.LocalName == "ColumnDefinition"));
            }
        }

        [Fact]
        public void The_keyboard_glows_from_one_ring_not_from_every_key()
        {
            var bands = Load("ConsoleKeyboardControl.xaml").Descendants()
                .Count(e => (string)e.Attribute("Style") == "{DynamicResource XboxFocusGlow}");

            Assert.Equal(1, bands);
        }

        [Theory]
        [InlineData("Text=Recently added")]
        [InlineData("Text=Games")]
        [InlineData("Text=Apps")]
        public void The_rows_headings_are_in_the_header_fonts_SemiLight(string selector)
        {
            var doc = Load("SearchControl.xaml");
            var heading = doc.Descendants().Single(e => e.Name.LocalName == "TextBlock"
                                                        && (string)e.Attribute("Text") == selector.Substring(5));
            var style = StyleByKey(doc, ResourceKey((string)heading.Attribute("Style")));

            Assert.Equal("XboxHeaderFont", FontOfStyle(doc, style));
            Assert.Contains(style.Elements(), s => (string)s.Attribute("Property") == "FontWeight" && (string)s.Attribute("Value") == "350");
        }

        [Fact]
        public void The_prompt_and_the_tiles_are_in_the_body_font()
        {
            var doc = Load("SearchControl.xaml");
            var prompt = doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == "Prompt");

            Assert.Equal("XboxFont", FontOfStyle(doc, ResourceKey((string)prompt.Attribute("Style"))));
        }

        [Fact]
        public void The_Guides_Search_utility_shows_the_magnifier()
        {
            var trigger = Load("OverlayWindow.xaml").Descendants()
                .Single(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == "{Binding Kind}"
                             && (string)e.Attribute("Value") == "Search");

            Assert.Contains(trigger.Elements(), s => (string)s.Attribute("TargetName") == "Glyph"
                                                     && (string)s.Attribute("Value") == "");
        }

        private static XElement TileRow(XDocument doc) => StyleByKey(doc, "TileRow");

        [Fact]
        public void The_rows_slide_to_the_tile_taking_focus()
        {
            var row = TileRow(Load("SearchControl.xaml"));

            Assert.Contains(row.Elements(), s => (string)s.Attribute("Property") == "local:SlidingRow.Slides"
                                                 && (string)s.Attribute("Value") == "True");
        }

        [Fact]
        public void The_rows_are_left_aligned_and_cut_to_whole_tiles()
        {
            var row = TileRow(Load("SearchControl.xaml"));
            var width = row.Elements().Single(s => (string)s.Attribute("Property") == "Width");

            Assert.Contains(row.Elements(), s => (string)s.Attribute("Property") == "HorizontalAlignment"
                                                 && (string)s.Attribute("Value") == "Left");
            Assert.Contains("Converter={StaticResource WholeTilesWidth}", (string)width.Attribute("Value"));
            Assert.Contains("SearchRowWidth.ListWidth(", File.ReadAllText(Path.Combine(ExtensionDir, "SearchControl.xaml.cs")));
        }

        [Fact]
        public void The_rows_list_clips_its_tiles_to_its_scroll_viewer()
        {
            // The ScrollViewer clips to its viewport, so the tile after the last shown is cut off, not covered.
            var scroller = TileRow(Load("SearchControl.xaml")).Descendants().Single(e => e.Name.LocalName == "ScrollViewer");

            Assert.NotEqual("False", (string)scroller.Attribute("ClipToBounds"));
        }
    }
}
