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
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard");

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
        public void Dashboard_xaml_uses_nothing_only_compiled_xaml_allows()
        {
            // Playnite reads the theme as loose XAML, which refuses x:Shared and so fails the whole theme.
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            foreach (var file in DashboardXaml())
            {
                var compiledOnly = XDocument.Load(file).Descendants().Attributes().Where(a => a.Name == x + "Shared");

                Assert.Empty(compiledOnly);
            }
        }

        [Fact]
        public void Dashboard_xaml_static_references_only_keys_its_own_file_or_Default_defines()
        {
            // Playnite loads each theme file on its own to check it before applying the theme, with only Default's
            // resources about, and falls back to Default if any fails. So a StaticResource can't reach a Dashboard key
            // (Xbox*) another file defines; it must be defined earlier in the same file, or be a DynamicResource.
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var unreachable = DashboardXaml().SelectMany(file =>
            {
                var defined = new System.Collections.Generic.HashSet<string>();
                var missing = new System.Collections.Generic.List<string>();
                foreach (var element in XDocument.Load(file).Descendants())
                {
                    foreach (var attribute in element.Attributes())
                    {
                        foreach (Match m in Regex.Matches(attribute.Value, @"\{StaticResource (Xbox\w+)\}"))
                        {
                            if (!defined.Contains(m.Groups[1].Value))
                            {
                                missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}");
                            }
                        }
                    }
                    var key = (string)element.Attribute(x + "Key");
                    if (key != null)
                    {
                        defined.Add(key);
                    }
                }
                return missing;
            });

            Assert.Empty(unreachable);
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
        public void Home_hosts_the_extensions_hero_Recent_row_Groups_tile_menu_My_games_Settings_Search_status_area_hint_bar_and_text_entry_as_custom_elements()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var elements = main.Descendants()
                .Where(e => e.Name.LocalName == "ContentControl")
                .Select(e => (string)e.Attribute(x + "Name"))
                .Where(name => name != null && name.StartsWith("XboxSeries_"));

            Assert.Equal(new[] { "XboxSeries_Groups", "XboxSeries_Hero", "XboxSeries_Hints", "XboxSeries_MyGames", "XboxSeries_RecentRow", "XboxSeries_Search", "XboxSeries_Settings", "XboxSeries_Status", "XboxSeries_TextEntry", "XboxSeries_TileMenu" }, elements.OrderBy(n => n));
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
        public void Top_bar_profile_shows_the_Score_under_the_gamertag_and_none_without_totals()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var profile = main.Descendants().Single(e => (string)e.Attribute("Style") == "{StaticResource XboxTopBarProfile}");
            var style = main.Root.Elements().Single(e => (string)e.Attribute(x + "Key") == "XboxTopBarProfile");
            XElement Named(string name) => style.Descendants().Single(e => (string)e.Attribute(x + "Name") == name);

            // The whole Status State, so the template reads both its Profile and its Achievements.
            Assert.Equal("{Binding Content.State, ElementName=XboxSeries_Status}", (string)profile.Attribute("Tag"));

            var score = Named("Score");
            Assert.Equal(Named("GamertagAndScore"), score.Parent);
            Assert.Contains(score.Descendants(), e => e.Name.LocalName == "TextBlock"
                && ((string)e.Attribute("Text") ?? "").StartsWith("{Binding Tag.Achievements.Score,")
                && ((string)e.Attribute("Text")).Contains("StringFormat={}{0:N0}"));

            // The gamertag and Score fade together while the real name shows.
            Assert.Contains(style.Descendants(), e => e.Name.LocalName == "DoubleAnimationUsingKeyFrames"
                && (string)e.Attribute("Storyboard.TargetName") == "GamertagAndScore");

            // No totals, or theme-only where the binding falls back to null: no Score, the gamertag at full size.
            var none = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger"
                && ((string)e.Attribute("Binding") ?? "").StartsWith("{Binding Tag.Achievements,")
                && ((string)e.Attribute("Binding")).Contains("FallbackValue={x:Null}")
                && (string)e.Attribute("Value") == "{x:Null}");
            Assert.Contains(none.Elements(), e => (string)e.Attribute("TargetName") == "Score"
                && (string)e.Attribute("Property") == "Visibility" && (string)e.Attribute("Value") == "Collapsed");
            Assert.Contains(none.Elements(), e => (string)e.Attribute("TargetName") == "Gamertag"
                && (string)e.Attribute("Property") == "FontSize" && (string)e.Attribute("Value") == "26");
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
        public void Top_bar_Search_is_Playnites_name_filter_theme_only_and_the_extensions_page_with_it()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var topBar = main.Descendants().Single(e => (string)e.Attribute(x + "Name") == "XboxTopBar");
            var filter = topBar.Descendants().Single(e => (string)e.Attribute(x + "Name") == "PART_ButtonSearch");
            // The extension finds the button by this name, to give it focus back as Search closes.
            var search = topBar.Descendants().Single(e => (string)e.Attribute(x + "Name") == "XboxTopBarSearch");

            // Playnite drives the part's own properties, so it is hidden with the extension through its wrapper.
            Assert.Contains("XboxThemeOnly", (string)filter.Parent.Attribute("Style"));
            Assert.Equal("{Binding Content.OpenSearchCommand, ElementName=XboxSeries_Search}", (string)search.Attribute("Command"));
            Assert.Equal("Search", (string)search.Attribute("Tag"));
            Assert.Contains(search.Descendants(), e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Binding") == $"{{PluginStatus Plugin={ExtensionId()}, Status=Installed}}"
                && (string)e.Attribute("Value") == "False");
            Assert.Contains(search.Descendants(), e => (string)e.Attribute("Style") == "{StaticResource XboxSearchIcon}");

            // Focus order either way: My games & apps, Search, Settings.
            Assert.Equal(filter.Parent.ElementsAfterSelf().First(), search);
            var settingsWrapper = topBar.Descendants().Single(e => (string)e.Attribute(x + "Name") == "PART_ButtonMainMenu").Parent;
            Assert.Equal(search.ElementsAfterSelf().First(), settingsWrapper);
            Assert.Equal("My games & apps", (string)filter.Parent.ElementsBeforeSelf().Last().Attribute("Tag"));
        }

        [Theory]
        [InlineData("XboxHomeOnly")]
        [InlineData("XboxMainList")]
        public void Home_hides_on_Search_as_it_does_on_Settings(string key)
        {
            // Search replaces Home as Settings does: whatever hides under Settings hides under Search too.
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var style = main.Root.Elements().Single(e => (string)e.Attribute(x + "Key") == key);
            string Hides(string page) => style.Descendants()
                .Where(e => e.Name.LocalName == "DataTrigger"
                    && (string)e.Attribute("Binding") == $"{{Binding Content.State.IsShown, ElementName={page}}}"
                    && (string)e.Attribute("Value") == "True")
                .SelectMany(e => e.Elements().Where(c => c.Name.LocalName == "Setter"))
                .Select(c => $"{(string)c.Attribute("Property")}={(string)c.Attribute("Value")}")
                .SingleOrDefault();

            Assert.Equal("Visibility=Collapsed", Hides("XboxSeries_Settings"));
            Assert.Equal("Visibility=Collapsed", Hides("XboxSeries_Search"));
        }

        [Fact]
        public void Every_trigger_on_Settings_being_up_has_its_match_for_Search()
        {
            var main = File.ReadAllText(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            int Count(string page) => Regex.Matches(main, Regex.Escape($"Content.State.IsShown, ElementName={page}}}")).Count;

            Assert.True(Count("XboxSeries_Settings") > 0);
            Assert.Equal(Count("XboxSeries_Settings"), Count("XboxSeries_Search"));
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
        public void Full_library_fades_its_list_under_the_pages_colour_rather_than_masking_the_list()
        {
            // An OpacityMask draws the whole list into a picture of its own, at the 4K screen's size, every frame it changes.
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            Assert.DoesNotContain(main.Descendants(), e => (string)e.Attribute("Property") == "OpacityMask"
                || e.Name.LocalName.EndsWith(".OpacityMask"));

            var fade = main.Descendants().Single(e => (string)e.Attribute(x + "Name") == "XboxFullLibraryFade");
            var list = fade.Parent.Elements().Single(e => (string)e.Attribute(x + "Name") == "PART_ListGameItems");
            Assert.Equal("False", (string)fade.Attribute("IsHitTestVisible"));
            Assert.Equal((string)list.Attribute("Margin"), (string)fade.Attribute("Margin"));
            Assert.True(fade.IsAfter(list), "the fade is drawn over the list");

            // The page under the list is My games & apps' flat colour, which the fade starts in and clears to.
            var myGames = XDocument.Load(Path.Combine(RepoRoot, "extension", "XboxSeries.Extension", "MyGamesControl.xaml"));
            var page = ((string)myGames.Descendants().Single(e => (string)e.Attribute(x + "Key") == "PageBrush").Attribute("Color")).Substring(3);
            var stops = fade.Descendants().Where(e => e.Name.LocalName == "GradientStop").Select(e => (string)e.Attribute("Color")).ToArray();
            Assert.Equal(new[] { "#FF" + page, "#00" + page }, stops);
            Assert.Contains(fade.Descendants(), e => e.Name.LocalName == "DataTrigger"
                && ((string)e.Attribute("Binding")).Contains("Content.State.ShowsFullLibrary"));
        }

        [Fact]
        public void Library_tiles_draw_their_shadow_and_glow_once_into_a_cached_picture()
        {
            // As Home's tiles: an effect left uncached is drawn again for every tile on every frame the list scrolls.
            var tile = XDocument.Load(Path.Combine(DashboardDir, "DerivedStyles", "ListGameItemTemplate.xaml"));
            var container = XDocument.Load(Path.Combine(DashboardDir, "DerivedStyles", "ListGameItemStyle.xaml"));
            var effected = tile.Descendants().Concat(container.Descendants())
                .Where(e => e.Elements().Any(c => c.Name.LocalName.EndsWith(".Effect"))
                            || (string)e.Attribute("Style") == "{DynamicResource XboxFocusGlow}")
                .ToList();

            // The source badge's shadow is a picture made once (#95); only the glow is an effect.
            Assert.Single(effected);
            var badge = tile.Descendants().Single(e => (string)e.Attribute((XNamespace)"http://schemas.microsoft.com/winfx/2006/xaml" + "Name") == "SourceBadge");
            Assert.Equal("2", (string)badge.Descendants().Single(e => e.Name.LocalName == "BitmapCache").Attribute("RenderAtScale"));
            var shadows = tile.Descendants().Where(e => (string)e.Attribute("TargetName") == "SourceShadow" && (string)e.Attribute("Property") == "Source")
                .Select(e => (string)e.Attribute("Value"));
            Assert.Equal(new[] { "{StaticResource XboxSourceSteamShadow}", "{StaticResource XboxSourceXboxShadow}" }, shadows);
            Assert.All(new[] { "steam", "xbox" }, source => Assert.True(File.Exists(Path.Combine(RepoRoot, "shared", "Images", $"source-{source}-shadow.png"))));
            Assert.All(effected, e =>
            {
                // Cached by its parent, as WPF caches an element under its own effect: a cache on the element itself
                // would hold it before its effect, which would still be drawn again every frame.
                Assert.Empty(e.Elements().Where(c => c.Name.LocalName.EndsWith(".CacheMode")));
                if ((string)e.Attribute("Style") == "{DynamicResource XboxFocusGlow}")
                {
                    Assert.Equal("{DynamicResource XboxFocusGlowCache}", (string)e.Parent.Attribute("Style"));
                    return;
                }
                var cache = e.Parent.Elements().Single(c => c.Name.LocalName.EndsWith(".CacheMode")).Elements().Single();
                Assert.Equal("BitmapCache", cache.Name.LocalName);
                Assert.Equal("2", (string)cache.Attribute("RenderAtScale"));
            });
        }

        [Fact]
        public void Every_plugin_hook_names_the_extension_by_its_id_or_for_its_settings_by_their_source_name()
        {
            var hooks = DashboardXaml()
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{(Plugin\w+) Plugin=(XboxSeries[^,}\s]*)").Cast<Match>())
                .Select(match => (Hook: match.Groups[1].Value, Plugin: match.Groups[2].Value))
                .ToList();

            // Playnite keys an add-on's status by its id, but its settings by the SourceName it hands AddSettingsSupport
            // (#156), which is the one its custom elements go by.
            Assert.Contains(hooks, hook => hook.Hook == "PluginStatus");
            Assert.Contains(hooks, hook => hook.Hook == "PluginSettings");
            Assert.All(hooks, hook => Assert.Equal(hook.Hook == "PluginSettings" ? "XboxSeries" : ExtensionId(), hook.Plugin));
            Assert.Contains("AddSettingsSupport(new AddSettingsSupportArgs { SourceName = \"XboxSeries\",",
                File.ReadAllText(Path.Combine(RepoRoot, "extension", "XboxSeries.Extension", "XboxSeriesPlugin.cs")));
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
            var project = XDocument.Load(Path.Combine(RepoRoot, "extension", "XboxSeries.Extension", "XboxSeries.Extension.csproj"));
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
