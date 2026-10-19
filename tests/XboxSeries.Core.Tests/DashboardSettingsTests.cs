using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Playnite settings: Playnite's own Fullscreen Settings window, restyled by the Dashboard's Views/SettingsMenus.xaml
    // over Default's. The window and its sections are Playnite's, so the XAML isn't run here; these checks guard what the
    // theme hands it: the keys it overrides, the rail and header it draws, and the install notice theme-only users see.
    public class DashboardSettingsTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard");
        private static readonly string SettingsMenusPath = Path.Combine(DashboardDir, "Views", "SettingsMenus.xaml");

        [Fact]
        public void Theme_only_the_header_names_the_extension_and_its_GitHub_page()
        {
            var header = KeyedOf(Load(SettingsMenusPath), "SettingsMenuHeaderTemplate");
            var notice = header.Descendants().Single(e => (string)e.Attribute(X + "Name") == "XboxInstallNotice");

            Assert.Contains(notice.DescendantsAndSelf(), e => ((string)e.Attribute("Text") ?? "").Contains("Xbox Series extension")
                && ((string)e.Attribute("Text")).Contains("github.com/snrgarb/xbox-series-theme"));
            Assert.Contains(notice.Descendants(), e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Binding") == $"{{PluginStatus Plugin={ExtensionId()}, Status=Installed}}"
                && (string)e.Attribute("Value") == "True"
                && e.Elements().Any(s => (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Collapsed"));
        }

        [Fact]
        public void Only_keys_Default_defines_for_the_settings_window_are_overridden_besides_the_Dashboards_own()
        {
            // Default's Views/SettingsMenus.xaml, which Playnite's window and sections look up by these names; a key
            // misspelled here would leave Default's in place unnoticed.
            string[] defaults =
            {
                "SettingsMenuBorder", "SettingsSectionBorder", "SettingsMenuButton", "SettingsMenuHeaderTemplate",
                "SettingsSectionItemSpacing", "SettingsSectionSlider", "SettingsSectionCheckbox", "SettingsSectionCombobox",
                "SettingsSectionText", "SettingsMenuGeneralButtonTemplate", "SettingsMenuVisualsButtonTemplate",
                "SettingsMenuAudioButtonTemplate", "SettingsMenuLayoutButtonTemplate", "SettingsMenuMenusButtonTemplate",
                "SettingsMenuInputButtonTemplate",
            };
            var keys = Load(SettingsMenusPath).Root.Elements().Select(e => (string)e.Attribute(X + "Key")).ToList();

            Assert.All(keys, key => Assert.True(defaults.Contains(key) || key.StartsWith("Xbox"), key));
            Assert.All(defaults, key => Assert.Contains(key, keys));
        }

        [Fact]
        public void The_menu_covers_the_window_from_its_column_and_holds_its_rail_to_the_first_441()
        {
            // Playnite's grid is the viewport's width, W, not 1920: columns 2*, 3*, 400 and 100, the menu's (2) at x W-500
            // to W-100, set on the Border itself. Spanned to the right edge, room to its left and W wide, right-aligned,
            // it is x 0 to W, whatever W is.
            var menu = KeyedOf(Load(SettingsMenusPath), "SettingsMenuBorder");

            Assert.Equal("2", Setter(menu, "Grid.ColumnSpan"));
            Assert.Equal("Right", Setter(menu, "HorizontalAlignment"));
            Assert.Equal("{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Grid}}", Setter(menu, "Width"));
            Assert.True(Thickness(Setter(menu, "Margin"))[0] <= -3500, "room for a 21:9 or wider grid");
            var rail = menu.Descendants().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "StackPanel");
            Assert.Equal("441", Setter(rail, "Width"));
            Assert.Equal("Left", Setter(rail, "HorizontalAlignment"));
        }

        [Fact]
        public void The_category_starts_77_right_of_the_rails_divider_and_ends_170_from_the_edge()
        {
            // The category's column (1) is x 0.4(W-500) to W-500, its row (1) y 150 to 930; its content sits 20 in.
            var section = KeyedOf(Load(SettingsMenusPath), "SettingsSectionBorder");
            var margin = Thickness(Setter(section, "Margin"));

            Assert.Equal("3", Setter(section, "Grid.ColumnSpan"));
            Assert.Equal("Right", Setter(section, "HorizontalAlignment"));
            Assert.Equal("{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Grid}, Converter={StaticResource MathConverter}, ConverterParameter=x-650}",
                Setter(section, "Width"));
            const int w = 1663;
            var right = w - margin[2];
            var left = right - (w - 650);
            Assert.Equal(441 + 2 + 77, left + 20);
            Assert.Equal(w - 170, right - 20);
            Assert.True(margin[0] <= -3500, "room for a 21:9 or wider grid");
            Assert.Equal(190, 150 + margin[1] + 20);
            Assert.True(930 + 150 - margin[3] <= 1080, "the category stays on the page");
        }

        [Fact]
        public void With_no_category_open_a_header_of_gear_title_and_profile_shows_beside_the_rail()
        {
            var header = KeyedOf(Load(SettingsMenusPath), "SettingsMenuHeaderTemplate");
            var empty = header.Descendants().Single(e => (string)e.Attribute(X + "Name") == "XboxSettingsEmptyHeader");

            Assert.Contains(empty.Descendants(), e => (string)e.Attribute("Text") == "");
            Assert.Contains(empty.Descendants(), e => (string)e.Attribute("Text") == "{DynamicResource LOCSettingsLabel}");
            Assert.Contains(empty.Descendants(), e => (string)e.Attribute("Text") == "{x:Static sys:Environment.UserName}");
            Assert.Contains(empty.Descendants(), e => e.Name.LocalName == "DataTrigger"
                && ((string)e.Attribute("Binding")).Contains("DataContext.IsSubMenuOpened")
                && (string)e.Attribute("Value") == "True"
                && e.Elements().Any(s => (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Collapsed"));
            Assert.True(int.Parse((string)empty.Attribute("Canvas.Left")) >= 441 + 2, "beside the rail, past its divider");
        }

        [Theory]
        [InlineData("CheckBox")]
        [InlineData("ComboBox")]
        [InlineData("Slider")]
        [InlineData("ToggleButton")]
        public void Each_restyled_control_restyles_Playnites_Ex_control_with_it(string control)
        {
            // Playnite loads each theme file on its own, after Default's file of the same name, whose Ex style was based
            // on Default's control style when it loaded: the Ex style is made again here, on this file's, or the
            // Ex control, which is what Playnite's own windows use, keeps Default's look.
            var doc = Load(Path.Combine(DashboardDir, "DefaultControls", control + ".xaml"));
            var styles = doc.Root.Elements().Where(e => e.Name.LocalName == "Style" && e.Attribute(X + "Key") == null).ToList();

            Assert.Contains(styles, s => (string)s.Attribute("TargetType") == control && s.Attribute("BasedOn") == null);
            var ex = styles.Single(s => (string)s.Attribute("TargetType") == control + "Ex");
            Assert.Equal($"{{StaticResource {{x:Type {control}}}}}", (string)ex.Attribute("BasedOn"));
            Assert.True(ex.IsAfter(styles.First(s => (string)s.Attribute("TargetType") == control)));
        }

        [Fact]
        public void A_dropdowns_choices_are_restyled_with_it()
        {
            var doc = Load(Path.Combine(DashboardDir, "DefaultControls", "ComboBox.xaml"));

            Assert.Contains(doc.Root.Elements(), e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "{x:Type ComboBoxItem}");
        }

        private static string Setter(XElement style, string property) =>
            (string)style.Elements().Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == property)
                .Attribute("Value");

        private static int[] Thickness(string value)
        {
            var parts = value.Split(',').Select(int.Parse).ToArray();
            return parts.Length == 4 ? parts : new[] { parts[0], parts[1], parts[0], parts[1] };
        }

        private static XDocument Load(string path) => XDocument.Load(path);

        private static XElement KeyedOf(XDocument doc, string key) =>
            doc.Root.Elements().Single(e => (string)e.Attribute(X + "Key") == key);

        private static string ExtensionId()
        {
            var project = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Extension", "XboxSeries.Extension.csproj"));
            return $"XboxSeries_{project.Descendants("PluginGuid").Single().Value}";
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
