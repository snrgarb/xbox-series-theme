using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Home's Groups, as the console's rows: six tall portrait cards and a See all card, spanning the Recent row. The XAML
    // isn't run here; these checks guard the card's shape and that the row adds up to the Recent row's width.
    public class GroupsXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string ExtensionDir = Path.Combine(FindRepoRoot(), "src", "XboxSeries.Extension");

        [Fact]
        public void Six_Group_cards_and_See_all_span_the_Recent_rows_width_with_even_gaps()
        {
            var groups = XDocument.Load(Path.Combine(ExtensionDir, "GroupsControl.xaml"));
            var recent = XDocument.Load(Path.Combine(ExtensionDir, "RecentRowControl.xaml"));

            var width = Number(Keyed(groups, "HomeTileCoverWidth").Value);
            var gap = Number(((string)Setter(groups, "Margin")).Split(',')[2]);
            var recentWidth = Number((string)recent.Descendants().First(e => e.Name.LocalName == "StackPanel").Attribute("Width"));

            Assert.Equal(recentWidth, 7 * width + 6 * gap);
            // Every Group's row is 1526 wide, at the Recent row's own scale.
            Assert.Equal(recentWidth.ToString(CultureInfo.InvariantCulture),
                (string)groups.Descendants().Single(e => (string)e.Attribute(X + "Name") == "Rows").Attribute("Width"));
        }

        [Fact]
        public void A_Group_card_is_portrait_as_the_consoles_and_its_cover_is_laid_out_at_its_size()
        {
            var groups = XDocument.Load(Path.Combine(ExtensionDir, "GroupsControl.xaml"));

            var width = Number(Keyed(groups, "HomeTileCoverWidth").Value);
            var height = Number(Keyed(groups, "HomeTileCoverHeight").Value);

            // The console's 285 x 435.
            Assert.InRange(height / width, 435.0 / 285 - 0.01, 435.0 / 285 + 0.01);
            Assert.Equal("{StaticResource HomeTileCoverWidth}", (string)Setter(groups, "Width"));
            Assert.Equal("{StaticResource HomeTileCoverHeight}", (string)Setter(groups, "Height"));
        }

        [Fact]
        public void Every_tile_in_a_Group_game_or_See_all_is_a_card_and_none_grows()
        {
            var groups = XDocument.Load(Path.Combine(ExtensionDir, "GroupsControl.xaml"));

            var buttons = groups.Descendants().Where(e => e.Name.LocalName == "Button").ToList();

            Assert.Equal(2, buttons.Count);
            Assert.All(buttons, b => Assert.Equal("{StaticResource GroupCard}", (string)b.Attribute("Style")));
            Assert.Contains(buttons, b => (string)b.Attribute("Tag") == "See all");
            Assert.DoesNotContain("TileGrowth", File.ReadAllText(Path.Combine(ExtensionDir, "GroupsControl.xaml.cs")));
        }

        private static XElement Keyed(XDocument doc, string key) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Key") == key);

        private static XAttribute Setter(XDocument doc, string property) =>
            Keyed(doc, "GroupCard").Elements().Single(s => (string)s.Attribute("Property") == property).Attribute("Value");

        private static double Number(string text) => double.Parse(text.Trim(), CultureInfo.InvariantCulture);

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
