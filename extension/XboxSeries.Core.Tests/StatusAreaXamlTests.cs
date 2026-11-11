using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The PC's own battery in the Status area: on Home's top bar and in the Guide alike, before the pad's battery, and
    // hidden unless there is one, so a desktop shows nothing new.
    public class StatusAreaXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly XDocument StatusArea =
            XDocument.Load(Path.Combine(RepoRoot, "extension", "XboxSeries.Extension", "StatusArea.xaml"));

        [Theory]
        [InlineData("RowTemplate")]
        [InlineData("StackedTemplate")]
        public void The_PC_battery_shows_before_the_pad_battery(string template)
        {
            var templates = Template(template).Descendants()
                .Select(e => ResourceKey((string)e.Attribute("ContentTemplate")))
                .Where(key => key != null)
                .ToList();

            Assert.Contains("PcBatteryTemplate", templates);
            Assert.Contains("BatteryTemplate", templates);
            Assert.True(templates.IndexOf("PcBatteryTemplate") < templates.IndexOf("BatteryTemplate"));
        }

        [Fact]
        public void The_PC_battery_is_collapsed_until_there_is_one()
        {
            var pc = Template("PcBatteryTemplate");
            var panel = pc.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PcBattery");

            Assert.Equal("Collapsed", (string)panel.Attribute("Visibility"));
            Assert.Contains(pc.Descendants(), e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Binding") == "{Binding PcBattery.IsPresent}" && (string)e.Attribute("Value") == "True");
        }

        // Each thing the Status area shows has an icon of its own, so no two read as the same: the PC's battery once drew
        // the pad's cell, and a laptop on its pad showed two identical batteries side by side.
        [Theory]
        [InlineData("PcBatteryTemplate", "BatteryTemplate")]
        [InlineData("PcBatteryTemplate", "DiscordTemplate")]
        [InlineData("BatteryTemplate", "DiscordTemplate")]
        public void No_two_indicators_share_an_icon(string one, string other)
        {
            var shared = Icons(Template(one)).Intersect(Icons(Template(other))).ToList();

            Assert.True(shared.Count == 0, $"{one} and {other} both draw {string.Join(", ", shared)}");
        }

        [Fact]
        public void Every_indicator_has_an_icon()
        {
            foreach (var key in new[] { "PcBatteryTemplate", "BatteryTemplate", "DiscordTemplate" })
            {
                Assert.True(Icons(Template(key)).Any(), $"{key} draws no icon");
            }
        }

        [Fact]
        public void The_PC_battery_is_Windows_upright_battery_and_not_the_pads_cell()
        {
            var pc = Template("PcBatteryTemplate");
            var glyph = pc.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PcGlyph");

            Assert.Equal("{Binding PcBattery.Glyph}", (string)glyph.Attribute("Text"));
            Assert.Equal("XboxIconFont", ResourceKey((string)glyph.Attribute("FontFamily")));
            Assert.DoesNotContain(pc.Descendants(), e => ResourceKey((string)e.Attribute("ContentTemplate")) == "CellTemplate");
        }

        // What an indicator draws as its icon: the templates it nests, the geometries it paths, the glyphs it writes in the
        // icon font (literal or bound) and any shape it draws itself, by kind and size.
        private static IEnumerable<string> Icons(XElement template)
        {
            foreach (var e in template.Descendants())
            {
                var nested = ResourceKey((string)e.Attribute("ContentTemplate"));
                if (nested != null)
                {
                    yield return "template " + nested;
                    foreach (var icon in Icons(Template(nested)))
                    {
                        yield return icon;
                    }
                }

                var data = (string)e.Attribute("Data");
                if (e.Name.LocalName == "Path" && data != null)
                {
                    yield return "path " + (ResourceKey(data) ?? data);
                }

                var font = ResourceKey((string)e.Attribute("FontFamily"));
                var text = (string)e.Attribute("Text");
                if (font == "XboxIconFont" && text != null)
                {
                    yield return "glyph " + text;
                }

                if (e.Name.LocalName == "ProgressBar")
                {
                    yield return "fill bar";
                }
            }
        }

        private static XElement Template(string key) => StatusArea.Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == key);
    }
}
