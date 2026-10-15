using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Dashboard is loose XAML that Playnite loads with or without the extension, so the XAML itself
    // isn't run here. These checks guard the theme's boundaries instead: with the extension, and with the parts Playnite drives.
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
        public void Home_hosts_the_extensions_hero_Recent_row_Groups_tile_menu_My_games_Settings_and_status_area_as_custom_elements()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var elements = main.Descendants()
                .Where(e => e.Name.LocalName == "ContentControl")
                .Select(e => (string)e.Attribute(x + "Name"))
                .Where(name => name != null && name.StartsWith("XboxSeries_"));

            Assert.Equal(new[] { "XboxSeries_Groups", "XboxSeries_Hero", "XboxSeries_MyGames", "XboxSeries_RecentRow", "XboxSeries_Settings", "XboxSeries_Status", "XboxSeries_TileMenu" }, elements.OrderBy(n => n));
        }

        [Fact]
        public void Home_names_the_top_bar_the_extension_moves_Left_and_Right_within()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var topBar = main.Descendants().Single(e => (string)e.Attribute(x + "Name") == "XboxTopBar");

            Assert.Contains(topBar.Descendants(), e => (string)e.Attribute(x + "Name") == "XboxSeries_Status");
            Assert.Contains(topBar.Descendants(), e => (string)e.Attribute("Tag") == "My games & apps");
            Assert.Contains(topBar.Descendants(), e => ((string)e.Attribute("Style") ?? "").Contains("XboxTopBarProfile"));
        }

        [Fact]
        public void Top_bar_Settings_is_Playnites_main_menu_theme_only_and_the_extensions_page_with_it()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var topBar = main.Descendants().Single(e => (string)e.Attribute(x + "Name") == "XboxTopBar");
            var mainMenu = topBar.Descendants().Single(e => (string)e.Attribute(x + "Name") == "PART_ButtonMainMenu");
            var settings = topBar.Descendants().Single(e => (string)e.Attribute(x + "Name") == "XboxTopBarSettings");

            // Playnite drives the part's own properties, so it is hidden with the extension through its wrapper.
            Assert.Contains("XboxThemeOnly", (string)mainMenu.Parent.Attribute("Style"));
            Assert.Contains("ElementName=XboxSeries_Settings", (string)settings.Attribute("Command"));
            Assert.Contains(settings.Descendants(), e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Binding") == $"{{PluginStatus Plugin={ExtensionId()}, Status=Installed}}"
                && (string)e.Attribute("Value") == "False");
            Assert.Equal(mainMenu.Parent.ElementsAfterSelf().First(), settings);
        }

        [Fact]
        public void Game_hub_keeps_the_parts_Playnite_drives()
        {
            var hub = XDocument.Load(Path.Combine(DashboardDir, "Views", "GameDetails.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var names = hub.Descendants()
                .Select(e => (string)e.Attribute(x + "Name"))
                .Where(name => name != null)
                .ToList();

            Assert.All(
                new[] { "PART_ViewHost", "PART_ButtonContext", "PART_ButtonOptions", "PART_ImageCover", "PART_ImageBackground", "PART_HtmlDescription", "PART_ScrollHtmlDescription" },
                part => Assert.Contains(part, names));
        }

        [Fact]
        public void Game_hub_sits_on_a_full_width_band_centred_with_equal_gaps_above_and_below()
        {
            var hub = XDocument.Load(Path.Combine(DashboardDir, "Views", "GameDetails.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var band = hub.Descendants().Single(e => (string)e.Attribute(x + "Name") == "Band");
            var content = band.ElementsAfterSelf().First();
            int[] Margin(XElement e) => ((string)e.Attribute("Margin")).Split(',').Select(int.Parse).ToArray();

            var gaps = Margin(band);
            Assert.Equal(0, gaps[0]);
            Assert.Equal(0, gaps[2]);
            Assert.True(gaps[1] > 0);
            Assert.Equal(gaps[1], gaps[3]);
            Assert.True(Margin(content)[1] > gaps[1], "the header starts inside the band");
        }

        [Fact]
        public void Every_plugin_hook_names_the_extension_by_its_id()
        {
            var plugins = DashboardXaml()
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{Plugin\w+ Plugin=(XboxSeries[^,}\s]*)").Cast<Match>())
                .Select(match => match.Groups[1].Value);

            Assert.All(plugins, plugin => Assert.Equal(ExtensionId(), plugin));
        }

        [Theory]
        [InlineData("navigation.wav")]
        [InlineData("activation.wav")]
        public void Dashboard_ships_the_console_sounds_Playnite_lets_it_replace(string file)
        {
            // Playnite plays these itself, with or without the extension, from the theme's audio folder.
            var bytes = File.ReadAllBytes(Path.Combine(DashboardDir, "audio", file));

            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
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
