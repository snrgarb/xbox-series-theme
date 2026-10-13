using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Dashboard is loose XAML that Playnite loads with or without the extension, so the XAML itself
    // isn't run here. These checks guard the theme's boundary with the extension instead.
    public class DashboardBoundaryTests
    {
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard");

        private static string[] DashboardXaml() =>
            Directory.GetFiles(DashboardDir, "*.xaml", SearchOption.AllDirectories);

        [Fact]
        public void Dashboard_xaml_is_well_formed()
        {
            foreach (var file in DashboardXaml())
            {
                XDocument.Load(file);
            }
        }

        [Fact]
        public void Dashboard_never_references_extension_types()
        {
            foreach (var file in DashboardXaml())
            {
                var xaml = File.ReadAllText(file);

                Assert.DoesNotContain("clr-namespace:XboxSeries", xaml);
                Assert.DoesNotContain("assembly=XboxSeries", xaml);
            }
        }

        [Fact]
        public void Home_gates_the_install_notice_on_the_extension()
        {
            var main = File.ReadAllText(Path.Combine(DashboardDir, "Views", "Main.xaml"));

            Assert.Contains($"{{PluginStatus Plugin={ExtensionId()}, Status=Installed}}", main);
        }

        [Fact]
        public void Home_hosts_the_extensions_hero_and_Recent_row_as_custom_elements()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var elements = main.Descendants()
                .Where(e => e.Name.LocalName == "ContentControl")
                .Select(e => (string)e.Attribute(x + "Name"))
                .Where(name => name != null && name.StartsWith("XboxSeries_"));

            Assert.Equal(new[] { "XboxSeries_Hero", "XboxSeries_RecentRow" }, elements.OrderBy(n => n));
        }

        [Fact]
        public void Every_plugin_hook_names_the_extension_by_its_id()
        {
            var plugins = DashboardXaml()
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{Plugin\w+ Plugin=(XboxSeries[^,}\s]*)").Cast<Match>())
                .Select(match => match.Groups[1].Value);

            Assert.All(plugins, plugin => Assert.Equal(ExtensionId(), plugin));
        }

        private static string ExtensionId()
        {
            var project = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Extension", "XboxSeries.Extension.csproj"));
            var guid = project.Descendants("PluginGuid").Single().Value;
            return $"XboxSeries_{guid}";
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
