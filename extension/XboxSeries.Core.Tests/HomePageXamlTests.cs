using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Home is one page that scrolls (#206): the top bar, the hero's gap, the Recent row, the wide tiles, then the Groups,
    // one scroll moving them all. The XAML isn't run here; these checks guard that the Groups are laid out to follow the
    // wide tiles at the Recent row's scale, that the theme takes the top bar's scroll from the extension and stays as it
    // was without it, and that the two screens and their fly are gone.
    public class HomePageXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");
        private static readonly string MainPath = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard", "Views", "Main.xaml");

        [Fact]
        public void The_Groups_sit_in_the_Recent_rows_slot_and_start_under_the_wide_tiles_as_each_Group_under_the_one_before()
        {
            var main = XDocument.Load(MainPath);
            string Row(string name) => (string)main.Descendants().Single(e => (string)e.Attribute(X + "Name") == name).Attribute("Grid.Row");
            Assert.Equal(Row("XboxSeries_RecentRow"), Row("XboxSeries_Groups"));

            var recent = Xaml("RecentRowControl.xaml");
            var groups = Xaml("GroupsControl.xaml");
            var recentViewboxes = recent.Descendants().Where(e => e.Name.LocalName == "Viewbox").ToList();
            var groupsViewbox = groups.Descendants().Single(e => e.Name.LocalName == "Viewbox");

            // From the same 53 down the slot, with the same sides, so scaled the same and lined up.
            Assert.Equal((string)recentViewboxes[0].Attribute("Margin"), (string)groupsViewbox.Attribute("Margin"));

            // Room for the Recent row and the wide tiles over them, then the gap each Group leaves under it.
            var recentHeight = Number(recentViewboxes[0].Elements().Single().Attribute("Height"));
            var promosHeight = Number(recentViewboxes[1].Elements().Single().Attribute("Height"));
            var above = Named(groups, "AboveGroups");
            Assert.Equal(recentHeight + promosHeight, Number(above.Attribute("Height")));
            Assert.Null(above.Attribute("Background"));
            var groupGap = Thickness((string)GroupHeader(groups).Parent.Attribute("Margin"))[3];
            Assert.Equal(groupGap, Thickness((string)Named(groups, "View").Attribute("Margin"))[1]);
            Assert.Equal("Canvas", Named(groups, "View").Name.LocalName);
        }

        [Fact]
        public void At_the_wide_tiles_rest_the_first_Groups_header_and_a_third_of_its_cards_show_and_the_Recent_row_stays_under_the_top_bar()
        {
            var groups = Xaml("GroupsControl.xaml");
            var viewbox = groups.Descendants().Single(e => e.Name.LocalName == "Viewbox");
            var margin = Thickness((string)viewbox.Attribute("Margin"));
            var scale = (1920 - margin[0] - margin[2]) / Number(viewbox.Elements().Single().Attribute("Width"));

            // The slot starts 480 down (HomePromoRowTests); the first Group 40 under the room for the rows.
            var groupTop = 480 + margin[1] + (Number(Named(groups, "AboveGroups").Attribute("Height")) + 40) * scale;
            var header = GroupHeader(groups);
            var headerSize = Number(Xaml("HomeTiles.xaml").Descendants().Single(e => (string)e.Attribute(X + "Key") == "HomeGroupHeaderSize").Value);
            Assert.Equal("{DynamicResource HomeGroupHeaderSize}", (string)header.Attribute("FontSize"));
            var cardsTop = (headerSize * 1.2 + Thickness((string)header.Attribute("Margin"))[3]) * scale;
            var cardHeight = 296 * scale;
            var rest = HomeRests.At(HomeFocus.WideTiles, new HomePageLayout(1080, new[] { groupTop }, cardsTop, cardHeight));

            // The Groups begin below the screen's foot until the page rises.
            Assert.True(groupTop > 1080, $"The first Group starts at {groupTop:0}, already on screen");
            Assert.True(rest.PageOffset > 0);
            Assert.Equal(0, rest.TopBarOffset);
            // The Recent row's slot, risen, is still under the top bar's foot (84 + 60).
            Assert.True(480 + margin[1] - rest.PageOffset > HomeRests.TopBarBottom, $"The Recent row rises to {480 + margin[1] - rest.PageOffset:0}, under the top bar");
        }

        [Fact]
        public void The_theme_takes_the_top_bars_scroll_from_the_Recent_row_and_without_the_extension_it_stays()
        {
            var main = XDocument.Load(MainPath);
            var topBar = Named(main, "XboxTopBar");

            // No FallbackValue: with nothing to bind, the bar keeps its own untransformed place.
            Assert.Equal("{Binding Content.TopBarScroll, ElementName=XboxSeries_RecentRow}", (string)topBar.Attribute("RenderTransform"));
            Assert.Contains("public Transform TopBarScroll => page.TopBarScroll;", Code("RecentRowControl.xaml.cs"));
        }

        [Fact]
        public void One_scroll_moves_the_Recent_row_and_the_Groups_and_the_two_screens_and_their_fly_are_gone()
        {
            var recent = Code("RecentRowControl.xaml.cs");
            var groups = Code("GroupsControl.xaml.cs");

            Assert.Contains("Page.RenderTransform = page.Scroll;", recent);
            Assert.Contains("RenderTransform = page.Scroll;", groups);
            Assert.Equal("StackPanel", Named(Xaml("RecentRowControl.xaml"), "Page").Name.LocalName);
            foreach (var code in new[] { recent, groups, Code("HomeRows.cs") })
            {
                Assert.DoesNotContain("HomeScreen", code);
                Assert.DoesNotContain("Fly(", code);
                Assert.DoesNotContain("ShowWholeGroupsOnly", code);
            }
        }

        [Fact]
        public void Up_from_the_first_Group_goes_to_the_wide_tiles_and_B_on_the_Groups_to_the_Recent_rows_last_game()
        {
            var groups = Code("GroupsControl.xaml.cs");

            Assert.Contains("case Key.Up when group == 0: page.LeaveGroups(toWideTiles: true); break;", groups);
            Assert.Matches(new Regex(@"if \(PlayniteInput\.PadButtonOf\(e\) == ControllerInput\.B\)\s*\{\s*e\.Handled = true;\s*page\.LeaveGroups\(toWideTiles: false\);"), groups);
            // The row puts focus on the wide tiles, or on its last focused game, and the page rests where focus lands.
            Assert.Matches(new Regex(@"void OnRowWanted\(bool toWideTiles\)\s*\{\s*promosFocused = toWideTiles;\s*FocusTileSoon\(\);"), Code("RecentRowControl.xaml.cs"));
        }

        [Fact]
        public void Focus_says_where_the_page_rests_and_the_log_says_each_new_resting_point()
        {
            Assert.Contains("page.RestOn(promosFocused ? HomeFocus.WideTiles : HomeFocus.RecentRow);", Code("RecentRowControl.xaml.cs"));
            Assert.Contains("page.RestOn(HomeFocus.Group(lastGroup));", Code("GroupsControl.xaml.cs"));
            Assert.Contains("logger.Info(HomeRests.Says(now, ", Code("HomeRows.cs"));
        }

        private static XDocument Xaml(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        private static XElement Named(XDocument doc, string name) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        private static XElement GroupHeader(XDocument groups) =>
            groups.Descendants().Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding Name}");

        private static double Number(XAttribute attribute) => Number((string)attribute);

        private static double Number(string text) => double.Parse(text.Trim(), CultureInfo.InvariantCulture);

        private static double[] Thickness(string text)
        {
            var parts = text.Split(',').Select(Number).ToArray();
            return parts.Length == 1 ? new[] { parts[0], parts[0], parts[0], parts[0] } : parts.Length == 2 ? new[] { parts[0], parts[1], parts[0], parts[1] } : parts;
        }
    }
}
