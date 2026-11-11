using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Text at the console's sizes (#203, spec #201's Typography): a focused tile's title is Segoe UI Regular 26 px on a
    // 1080p screen, two lines then an ellipsis, as Xbox Accessibility Guideline 101's 26 px floor; Group headers are
    // Bahnschrift SemiLight 36; the Game hub's cards' text is 24. Home's rows are laid out at 1080p and scaled to the
    // screen in a Viewbox, so their sizes are worked out here from the Viewbox's scale at 1920x1080. The XAML isn't run.
    public class ConsoleTypeSizesXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard");

        // WPF's line for Segoe UI is 1.33 em ((2210 + 514) / 2048 font units). Bahnschrift's is taken as 1.25 em, a
        // little over its own, so a layout that fits here fits on screen.
        private const double SegoeLine = 1.33;
        private const double BahnschriftLine = 1.25;

        private const double ScreenWidth = 1920;

        [Fact]
        public void The_Recent_row_and_the_Groups_are_scaled_alike_at_1080p()
        {
            Assert.Equal(1700.0 / 1526, Scale("RecentRowControl.xaml"), 6);
            Assert.Equal(Scale("RecentRowControl.xaml"), Scale("GroupsControl.xaml"), 6);
        }

        [Theory]
        [InlineData("RecentRowControl.xaml")]
        [InlineData("GroupsControl.xaml")]
        public void A_focused_Home_tiles_title_and_status_are_26_px_on_a_1080p_screen(string row)
        {
            var tiles = Extension("HomeTiles.xaml");
            var band = TitleBand(tiles);

            Assert.Equal(2, band.Count);
            Assert.All(band, t => Assert.Equal(26.0, Size(tiles, t) * Scale(row), 1));
        }

        [Fact]
        public void Manage_Queues_Recently_installed_tiles_unscaled_set_the_title_at_26_still_two_lines()
        {
            var myGames = Extension("MyGamesControl.xaml");
            var title = TitleBand(Extension("HomeTiles.xaml"))[0];
            var size = ResourceKey((string)title.Attribute("FontSize"));
            var maxHeight = ResourceKey((string)title.Attribute("MaxHeight"));

            Assert.NotNull(size);
            Assert.NotNull(maxHeight);
            Assert.Equal(26.0, Number(Keyed(myGames, size).Value));
            Assert.InRange(Number(Keyed(myGames, maxHeight).Value), 2 * 26 * SegoeLine, 3 * 26 * SegoeLine - 1);
        }

        [Fact]
        public void My_games_grids_focused_title_and_Installing_are_26()
        {
            var style = Dashboard("DerivedStyles", "ListGameItemStyle.xaml");
            var band = style.Descendants().Single(e => (string)e.Attribute(X + "Name") == "TitleBand")
                .Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();

            Assert.Equal(2, band.Count);
            Assert.All(band, t => Assert.Equal(26.0, Size(style, t)));
            // 26 on screen as the grid's focused tile doesn't grow with the extension: its growth runs only without it.
            var grow = style.Descendants().Single(e => e.Name.LocalName == "BeginStoryboard"
                                                       && (string)e.Attribute("Storyboard") == "{StaticResource GrowOn}")
                .Ancestors().First(e => e.Name.LocalName == "MultiDataTrigger");
            Assert.Contains(grow.Descendants(), c => c.Name.LocalName == "Condition"
                                                     && ((string)c.Attribute("Binding")).Contains("Status=Installed")
                                                     && (string)c.Attribute("Value") == "False");
        }

        [Fact]
        public void Searchs_game_tile_title_and_status_are_26()
        {
            var search = Extension("SearchControl.xaml");
            var caption = SearchCaption(search);

            Assert.Equal(2, caption.Count);
            Assert.All(caption, t => Assert.Equal(26.0, Size(search, t)));
        }

        [Fact]
        public void Each_tiles_title_runs_to_two_lines_then_an_ellipsis()
        {
            var tiles = Extension("HomeTiles.xaml");
            var style = Dashboard("DerivedStyles", "ListGameItemStyle.xaml");
            var search = Extension("SearchControl.xaml");
            var titles = new[]
            {
                (tiles, TitleBand(tiles)[0]),
                (style, style.Descendants().Single(e => (string)e.Attribute(X + "Name") == "TitleBand")
                    .Descendants().First(e => e.Name.LocalName == "TextBlock")),
                (search, SearchCaption(search)[0]),
            };

            Assert.All(titles, t =>
            {
                var (doc, title) = t;
                var line = Size(doc, title) * SegoeLine;
                Assert.Equal("Wrap", (string)title.Attribute("TextWrapping"));
                Assert.Equal("CharacterEllipsis", Trimming(doc, title));
                Assert.InRange(Resolved(doc, (string)title.Attribute("MaxHeight")), 2 * line, 3 * line - 1);
                Assert.Null(title.Attribute("FontWeight"));
                Assert.Equal("XboxFont", ResourceKey((string)title.Attribute("FontFamily")) ?? FontOfStyle(doc, ResourceKey((string)title.Attribute("Style"))));
            });
        }

        [Fact]
        public void Searchs_caption_holds_two_lines_of_title_and_the_status_under_them()
        {
            var search = Extension("SearchControl.xaml");
            var tile = Keyed(search, "GameTile");
            var caption = Number((string)tile.Descendants().Where(e => e.Name.LocalName == "RowDefinition").ElementAt(1).Attribute("Height"));
            var stack = SearchCaption(search)[0].Parent;
            var margin = Thickness((string)stack.Attribute("Margin"));

            Assert.Equal("StackPanel", stack.Name.LocalName);
            Assert.True(caption >= margin[1] + margin[3] + 3 * 26 * SegoeLine,
                $"The caption, {caption}, is shorter than two lines of title and the status");
        }

        [Fact]
        public void Group_headers_are_Bahnschrift_SemiLight_36_on_a_1080p_screen()
        {
            var groups = Extension("GroupsControl.xaml");
            var header = GroupHeader(groups);

            Assert.Equal("XboxHeaderFont", ResourceKey((string)header.Attribute("FontFamily")));
            Assert.Equal("350", (string)header.Attribute("FontWeight"));
            Assert.Equal("{DynamicResource HomeGroupHeaderSize}", (string)header.Attribute("FontSize"));
            Assert.Equal(36.0, Size(Extension("HomeTiles.xaml"), header) * Scale("GroupsControl.xaml"), 1);
        }

        [Fact]
        public void Two_whole_Groups_and_part_of_a_third_show_under_the_larger_header_at_a_Groups_rest()
        {
            var groups = Extension("GroupsControl.xaml");
            var header = GroupHeader(groups);
            var group = header.Parent;
            var scale = Scale("GroupsControl.xaml");

            var headerHeight = Size(Extension("HomeTiles.xaml"), header) * BahnschriftLine + Thickness((string)header.Attribute("Margin"))[3];
            var card = Number(Keyed(groups, "HomeTileCoverHeight").Value);
            var gap = Thickness((string)group.Attribute("Margin"))[3];

            // A Group with focus has its header 64 from the screen's top (#206): it and the next end above the foot, and
            // the one after them starts above it.
            Assert.True(64 + (2 * (headerHeight + card) + gap) * scale <= 1080, "The second Group is cut by the screen's foot");
            Assert.True(64 + 2 * (headerHeight + card + gap) * scale < 1080, "No part of a third Group shows");
        }

        [Fact]
        public void The_Game_hubs_card_titles_labels_values_and_About_are_24()
        {
            var hub = Hub();

            Assert.Equal(24.0, StyleSize(hub, StyleByKey(hub, "XboxHubCardTitle")));
            Assert.Equal(24.0, StyleSize(hub, StyleByKey(hub, "XboxHubRowLabel")));
            Assert.Equal(24.0, StyleSize(hub, StyleByKey(hub, "XboxHubRowValue")));
            // About, and the whole description on Game details.
            var html = hub.Descendants().Where(e => e.Name.LocalName == "HtmlTextView").ToList();
            Assert.Equal(2, html.Count);
            Assert.All(html, v => Assert.Equal("24", (string)v.Attribute("HtmlFontSize")));
            // Achievements' count and percent, values as the rest.
            var achievements = hub.Descendants().Single(e => (string)e.Attribute(X + "Name") == "StatsAchievements")
                .Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();
            Assert.Equal(5, achievements.Count);
            Assert.All(achievements, t => Assert.Equal(24.0, Size(hub, t)));
        }

        [Fact]
        public void The_Game_hubs_buttons_are_Bahnschrift_Bold_24_and_their_icons_and_labels_unbolded()
        {
            var hub = Hub();
            var action = StyleByKey(hub, "XboxHubAction");

            Assert.Equal("XboxHeaderFont", FontOfStyle(hub, action));
            Assert.Equal("Bold", SetterValue(action, "FontWeight"));
            Assert.Equal(24.0, StyleSize(hub, action));
            // A weight the icon font hasn't is drawn by thickening its glyphs, so the icons stay at their own.
            Assert.Equal("Normal", SetterValue(StyleByKey(hub, "XboxHubIconAction"), "FontWeight"));
            var toggleIcon = hub.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PageToggle")
                .Descendants().First(e => e.Name.LocalName == "TextBlock");
            Assert.Equal("XboxIconFont", ResourceKey((string)toggleIcon.Attribute("FontFamily")));
            Assert.Equal("Normal", (string)toggleIcon.Attribute("FontWeight"));
            // The hub's text, as the name over an icon action and the main action's source, keeps its Regular in a button.
            Assert.Equal("Normal", SetterValue(StyleByKey(hub, "XboxHubText"), "FontWeight"));
        }

        [Fact]
        public void Your_game_stats_fits_its_card_with_every_line_shown()
        {
            var hub = Hub();
            var card = StyleByKey(hub, "XboxHubCard");
            var padding = Thickness(SetterValue(card, "Padding"));
            var room = Number(SetterValue(card, "Height")) - padding[1] - padding[3];

            var title = StyleByKey(hub, "XboxHubCardTitle");
            var label = StyleSize(hub, StyleByKey(hub, "XboxHubRowLabel")) * SegoeLine;
            var value = StyleByKey(hub, "XboxHubRowValue");
            var valueMargin = Thickness(SetterValue(value, "Margin"));
            var row = label + StyleSize(hub, value) * SegoeLine + valueMargin[1] + valueMargin[3];

            var achievements = hub.Descendants().Single(e => (string)e.Attribute(X + "Name") == "StatsAchievements");
            var count = achievements.Elements().Single(e => e.Name.LocalName == "DockPanel");
            var countMargin = Thickness((string)count.Attribute("Margin"));
            var bar = Number(SetterValue(StyleByKey(hub, "XboxHubProgressBar"), "Height"));

            var height = StyleSize(hub, title) * BahnschriftLine + Thickness(SetterValue(title, "Margin"))[3]
                         + 4 * row
                         + label + countMargin[1] + 24 * SegoeLine + countMargin[3] + bar;

            Assert.True(height <= room, $"Your game stats is {height:0.#} tall with every line, in a card with {room} inside");
        }

        private static XDocument Hub() => Dashboard("Views", "GameDetails.xaml");

        private static string SetterValue(XElement style, string property) =>
            (string)style.Elements().Single(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == property).Attribute("Value");

        // A style's font size, its own or down its BasedOn.
        private static double StyleSize(XDocument doc, XElement style)
        {
            while (true)
            {
                var setter = style.Elements().FirstOrDefault(s => (string)s.Attribute("Property") == "FontSize");
                if (setter != null)
                {
                    return Number((string)setter.Attribute("Value"));
                }

                style = StyleByKey(doc, ResourceKey((string)style.Attribute("BasedOn")));
            }
        }

        // Home's tile title band: the title, then the install status under it.
        private static List<XElement> TitleBand(XDocument tiles) =>
            tiles.Descendants().Single(e => (string)e.Attribute(X + "Name") == "TitleBand")
                .Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();

        // Search's game tile caption: its title, then its status.
        private static List<XElement> SearchCaption(XDocument search) =>
            Keyed(search, "GameTile").Descendants()
                .Where(e => e.Name.LocalName == "TextBlock" && ((string)e.Attribute("Text") == "{Binding Title}" && e.Attribute("Style") != null
                                                                || (string)e.Attribute("Text") == "{Binding Status}"))
                .ToList();

        private static XElement GroupHeader(XDocument groups) =>
            groups.Descendants().Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding Name}");

        // The scale of a row's Viewbox at 1920 wide: the width it has (the screen's less its side margins) over the width
        // its content is laid out at (less any margin the content takes back).
        private static double Scale(string file)
        {
            var viewbox = Extension(file).Descendants().First(e => e.Name.LocalName == "Viewbox");
            var outer = Thickness((string)viewbox.Attribute("Margin"));
            var content = viewbox.Elements().Single();
            var inner = content.Attribute("Margin") == null ? new double[4] : Thickness((string)content.Attribute("Margin"));
            var width = Number((string)content.Attribute("Width")) + inner[0] + inner[2];

            return (ScreenWidth - outer[0] - outer[2]) / width;
        }

        // A TextBlock's font size: its own, a resource it names, or its style's (down its BasedOn).
        private static double Size(XDocument doc, XElement text)
        {
            var own = (string)text.Attribute("FontSize");
            if (own != null)
            {
                return Resolved(doc, own);
            }

            return StyleSize(doc, StyleByKey(doc, ResourceKey((string)text.Attribute("Style"))));
        }

        // A number written in place, or the sys:Double resource it names.
        private static double Resolved(XDocument doc, string value)
        {
            var key = ResourceKey(value);
            return key == null ? Number(value) : Number(Keyed(doc, key).Value);
        }

        private static string Trimming(XDocument doc, XElement text) =>
            (string)text.Attribute("TextTrimming")
            ?? (string)StyleByKey(doc, ResourceKey((string)text.Attribute("Style"))).Elements()
                .Single(s => (string)s.Attribute("Property") == "TextTrimming").Attribute("Value");

        private static XElement Keyed(XDocument doc, string key) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Key") == key);

        private static double[] Thickness(string text)
        {
            var parts = text.Split(',').Select(Number).ToArray();
            return parts.Length == 1 ? new[] { parts[0], parts[0], parts[0], parts[0] }
                : parts.Length == 2 ? new[] { parts[0], parts[1], parts[0], parts[1] }
                : parts;
        }

        private static double Number(string text) => double.Parse(text.Trim(), CultureInfo.InvariantCulture);

        private static XDocument Extension(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XDocument Dashboard(string dir, string file) => XDocument.Load(Path.Combine(DashboardDir, dir, file));
    }
}
