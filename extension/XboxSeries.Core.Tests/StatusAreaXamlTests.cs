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

        private static XElement Template(string key) => StatusArea.Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == key);
    }
}
