using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Headers in Bahnschrift, body text in Segoe UI (#77), in the Dashboard theme's own XAML. The XAML isn't run here;
    // these checks read which shared font each header and a few representative pieces of body text reach for. Weight
    // follows what each used before: a Semibold header becomes Bahnschrift SemiBold.
    public class DashboardHeaderFontTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string ViewsDir = Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Views");

        private const string HeaderSemibold = "{DynamicResource XboxHeaderFontSemibold}";
        private const string Body = "{DynamicResource XboxFont}";

        [Fact]
        public void The_Game_hubs_title_is_a_header()
        {
            var title = GameDetails().Descendants()
                .Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding DisplayName}");

            Assert.Equal(HeaderSemibold, (string)title.Attribute("FontFamily"));
        }

        [Fact]
        public void The_Game_hubs_card_titles_are_headers()
        {
            Assert.Equal(HeaderSemibold, StyleFont(GameDetails(), "XboxHubCardTitle"));

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

            Assert.Equal(HeaderSemibold, (string)heading.Attribute("FontFamily"));
        }

        [Fact]
        public void The_Game_hubs_body_text_stays_in_Segoe_UI()
        {
            Assert.Equal(Body, StyleFont(GameDetails(), "XboxHubText"));
            Assert.Null(StyleFont(GameDetails(), "XboxHubRowLabel"));
            Assert.Null(StyleFont(GameDetails(), "XboxHubRowValue"));
            Assert.Equal("{DynamicResource XboxFontSemibold}", StyleFont(GameDetails(), "XboxHubAction"));
            Assert.All(GameDetails().Descendants().Where(e => e.Name.LocalName == "HtmlTextView"),
                v => Assert.Equal(Body, (string)v.Attribute("HtmlFontFamily")));
        }

        [Fact]
        public void Homes_body_text_stays_in_Segoe_UI()
        {
            Assert.Equal(Body, StyleFont(Main(), "XboxText"));

            var clock = Main().Descendants().Single(e => (string)e.Attribute(X + "Name") == "PART_TextClock");
            Assert.Equal("{DynamicResource XboxFontSemibold}", (string)clock.Attribute("FontFamily"));
        }

        [Theory]
        [InlineData("CustomControls", "FilterPresetSelector.xaml")]
        [InlineData("DerivedStyles", "ListGameItemStyle.xaml")]
        [InlineData("DerivedStyles", "ListGameItemTemplate.xaml")]
        public void Tiles_and_tabs_stay_in_Segoe_UI(string dir, string file)
        {
            var doc = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", dir, file));
            var fonts = doc.Descendants()
                .Where(e => e.Name.LocalName == "TextBlock" && e.Attribute("FontFamily") != null)
                .Select(e => (string)e.Attribute("FontFamily"))
                .Where(f => f != "{DynamicResource XboxIconFont}")
                .ToList();

            Assert.NotEmpty(fonts);
            Assert.All(fonts, f => Assert.Equal(Body, f));
        }

        private static XDocument Main() => XDocument.Load(Path.Combine(ViewsDir, "Main.xaml"));

        private static XDocument GameDetails() => XDocument.Load(Path.Combine(ViewsDir, "GameDetails.xaml"));

        // The FontFamily a keyed style sets itself, or null when it inherits its BasedOn's.
        private static string StyleFont(XDocument doc, string key)
        {
            var style = doc.Descendants().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute(X + "Key") == key);
            return style.Elements()
                .Where(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == "FontFamily")
                .Select(s => (string)s.Attribute("Value"))
                .SingleOrDefault();
        }

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
