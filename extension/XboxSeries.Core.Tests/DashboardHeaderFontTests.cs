using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Headers in Bahnschrift, body text in Segoe UI (#77), in the Dashboard theme's own XAML. The XAML isn't run here;
    // these checks read which shared font each header and a few representative pieces of body text reach for. Weight
    // follows what each used before: a Semibold header becomes Bahnschrift SemiBold.
    public class DashboardHeaderFontTests
    {
        private static readonly string ViewsDir = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard", "Views");

        private const string HeaderSemibold = "XboxHeaderFontSemibold";
        private const string Body = "XboxFont";

        [Fact]
        public void The_Game_hubs_title_is_a_header()
        {
            var title = GameDetails().Descendants()
                .Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding DisplayName}");

            Assert.Equal(HeaderSemibold, ResourceKey((string)title.Attribute("FontFamily")));
        }

        [Fact]
        public void The_Game_hubs_card_titles_are_headers()
        {
            Assert.Equal(HeaderSemibold, FontOfStyle(GameDetails(), "XboxHubCardTitle"));

            var titles = GameDetails().Descendants()
                .Where(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Style") == "{StaticResource XboxHubCardTitle}")
                .Select(e => (string)e.Attribute("Text"))
                .ToList();
            Assert.Equal(new[] { "Info", "Your game stats", "About", "Description" }, titles);
            Assert.DoesNotContain(GameDetails().Descendants(),
                e => (string)e.Attribute("Style") == "{StaticResource XboxHubCardTitle}" && e.Attribute("FontFamily") != null);
        }

        [Fact]
        public void Homes_theme_only_list_heading_is_a_header()
        {
            var heading = Main().Descendants()
                .Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding ActiveFilterPreset.Name}");

            Assert.Equal(HeaderSemibold, ResourceKey((string)heading.Attribute("FontFamily")));
        }

        [Fact]
        public void The_Game_hubs_body_text_stays_in_Segoe_UI()
        {
            Assert.Equal(Body, FontOfStyle(GameDetails(), "XboxHubText"));
            Assert.Equal(Body, FontOfStyle(GameDetails(), "XboxHubRowLabel"));
            Assert.Equal(Body, FontOfStyle(GameDetails(), "XboxHubRowValue"));
            Assert.Equal("XboxFontSemibold", FontOfStyle(GameDetails(), "XboxHubAction"));
            Assert.All(GameDetails().Descendants().Where(e => e.Name.LocalName == "HtmlTextView"),
                v => Assert.Equal(Body, ResourceKey((string)v.Attribute("HtmlFontFamily"))));
        }

        [Fact]
        public void Homes_body_text_stays_in_Segoe_UI()
        {
            Assert.Equal(Body, FontOfStyle(Main(), "XboxText"));

            var clock = Main().Descendants().Single(e => (string)e.Attribute(X + "Name") == "PART_TextClock");
            Assert.Equal("XboxFontSemibold", ResourceKey((string)clock.Attribute("FontFamily")));
        }

        [Theory]
        [InlineData("CustomControls", "FilterPresetSelector.xaml")]
        [InlineData("DerivedStyles", "ListGameItemStyle.xaml")]
        [InlineData("DerivedStyles", "ListGameItemTemplate.xaml")]
        public void Tiles_and_tabs_stay_in_Segoe_UI(string dir, string file)
        {
            var doc = XDocument.Load(Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard", dir, file));
            var fonts = doc.Descendants()
                .Where(e => e.Name.LocalName == "TextBlock" && e.Attribute("FontFamily") != null)
                .Select(e => ResourceKey((string)e.Attribute("FontFamily")))
                .Where(f => f != "XboxIconFont")
                .ToList();

            Assert.NotEmpty(fonts);
            Assert.All(fonts, f => Assert.Equal(Body, f));
        }

        private static XDocument Main() => XDocument.Load(Path.Combine(ViewsDir, "Main.xaml"));

        private static XDocument GameDetails() => XDocument.Load(Path.Combine(ViewsDir, "GameDetails.xaml"));
    }
}
