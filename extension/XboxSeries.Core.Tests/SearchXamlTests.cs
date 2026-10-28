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
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

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
            // Cut as each row's room changes size, not by a binding to its ActualWidth, which kept the width it had while
            // the row was hidden, one tile, once it showed.
            var doc = Load("SearchControl.xaml");
            var row = TileRow(doc);
            var lists = doc.Descendants().Where(e => e.Name.LocalName == "ListBox" && (string)e.Attribute("Style") == "{StaticResource TileRow}").ToList();

            Assert.Contains(row.Elements(), s => (string)s.Attribute("Property") == "HorizontalAlignment"
                                                 && (string)s.Attribute("Value") == "Left");
            Assert.DoesNotContain(row.Elements(), s => (string)s.Attribute("Property") == "Width");
            Assert.Equal(3, lists.Count);
            Assert.All(lists, list => Assert.Equal("OnRowSizeChanged", (string)list.Parent.Attribute("SizeChanged")));
            Assert.Matches(new System.Text.RegularExpressions.Regex(@"void OnRowSizeChanged\(object sender, SizeChangedEventArgs e\).*?\.Width = SearchRowWidth\.ListWidth\(e\.NewSize\.Width\);", System.Text.RegularExpressions.RegexOptions.Singleline),
                File.ReadAllText(Path.Combine(ExtensionDir, "SearchControl.xaml.cs")));
        }

        [Fact]
        public void The_rows_measurements_are_those_their_width_is_worked_out_from()
        {
            // Every place a row rests at is a whole number of tiles along. Its ScrollViewer starts a gap in from the list,
            // so a tile slid off to the left is cut off before the gutter, while the first tile shown, the gutter in, keeps
            // its ring, 8 out from it; its panel's viewport is then whole tiles wide, so it scrolls no further than one.
            var doc = Load("SearchControl.xaml");
            var scroller = TileRow(doc).Descendants().Single(e => e.Name.LocalName == "ScrollViewer");
            var presenter = scroller.Elements().Single(e => e.Name.LocalName == "ItemsPresenter");
            var tile = StyleByKey(doc, "Tile");

            Assert.Equal(SearchRowWidth.Gap + ",0,0,0", (string)scroller.Attribute("Margin"));
            Assert.Equal((SearchRowWidth.Gutter - SearchRowWidth.Gap) + "," + SearchRowWidth.Gutter + ",0," + SearchRowWidth.Gutter, (string)presenter.Attribute("Margin"));
            Assert.True(SearchRowWidth.Gutter - 8 >= SearchRowWidth.Gap);
            Assert.Contains(tile.Elements(), s => (string)s.Attribute("Property") == "Width" && (string)s.Attribute("Value") == SearchRowWidth.TileWidth.ToString());
            Assert.Contains(tile.Elements(), s => (string)s.Attribute("Property") == "Margin" && (string)s.Attribute("Value") == "0,0," + SearchRowWidth.Gap + ",0");
        }
    }
}
