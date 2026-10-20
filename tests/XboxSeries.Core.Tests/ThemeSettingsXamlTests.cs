using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of Settings' one Theme settings entry (#86): the rail reads Theme settings and Playnite settings, a
    // divider and More Playnite options, an action rather than a category, then Debug's divider and item; Theme settings' pane is Personalisation, Idle, Guide and Discord boxes in
    // Personalisation's design; and its pages are panes of the options row the code-behind shows by the core's ThemePage.
    // The XAML isn't run here; these checks guard what the page shows.
    public class ThemeSettingsXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XElement Named(XDocument doc, string name) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        [Fact]
        public void The_rail_is_Theme_settings_Playnite_settings_then_a_divider_More_Playnite_options_then_the_Debug_divider_and_item()
        {
            var rail = Named(Load("SettingsControl.xaml"), "Rail");

            Assert.Equal(
                new[] { "ThemeSettingsItem", "PlayniteSettingsItem", "MoreOptionsDivider", "MorePlayniteOptionsItem", "DebugDivider", "DebugItem" },
                rail.Elements().Select(e => (string)e.Attribute(X + "Name")));
            Assert.Equal("Theme settings", (string)Named(Load("SettingsControl.xaml"), "ThemeSettingsItem")
                .Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text"));
            Assert.Equal("OnCategoryFocused", (string)rail.Elements().First().Attribute("GotKeyboardFocus"));
        }

        [Fact]
        public void Guide_and_Discord_are_no_longer_rail_items()
        {
            var settings = Load("SettingsControl.xaml");
            var names = settings.Descendants().Select(e => (string)e.Attribute(X + "Name")).Where(n => n != null).ToList();

            Assert.DoesNotContain("PersonalisationItem", names);
            Assert.DoesNotContain("GuideItem", names);
            Assert.DoesNotContain("DiscordItem", names);
        }

        [Theory]
        [InlineData("ThemeSettingsPane", "ThemeSettingsPane")]
        [InlineData("PersonalisationPane", "PersonalisationPane")]
        [InlineData("IdlePane", "StackPanel")]
        [InlineData("GuidePane", "StackPanel")]
        [InlineData("DiscordPane", "StackPanel")]
        [InlineData("PlaynitePane", "PlaynitePane")]
        public void Each_pane_sits_in_the_options_row_collapsed_until_shown(string name, string kind)
        {
            var pane = Named(Load("SettingsControl.xaml"), name);

            Assert.Equal(kind, pane.Name.LocalName);
            Assert.Equal("1", (string)pane.Attribute("Grid.Row"));
            Assert.Equal("Collapsed", (string)pane.Attribute("Visibility"));
        }

        [Fact]
        public void Its_boxes_are_Personalisation_Idle_Guide_then_Discord_two_wide_each_a_line_icon_and_a_title_alone()
        {
            var pane = Load("ThemeSettingsPane.xaml");
            var boxes = pane.Descendants().Where(e => e.Name.LocalName == "Button").ToList();
            var grid = boxes[0].Parent;

            Assert.Equal(new[] { "PersonalisationBox", "IdleBox", "GuideBox", "DiscordBox" }, boxes.Select(b => (string)b.Attribute(X + "Name")));
            Assert.Equal(new[] { "Personalisation", "Idle", "Guide", "Discord" },
                boxes.Select(b => (string)b.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text")));
            Assert.Equal("UniformGrid", grid.Name.LocalName);
            Assert.Equal("2", (string)grid.Attribute("Columns"));
            Assert.All(boxes, box =>
            {
                // A title and nothing under it: no box says what its page holds in words.
                Assert.Single(box.Descendants(), e => e.Name.LocalName == "TextBlock");
                Assert.Same(grid, box.Parent);
                Assert.Equal("{StaticResource Box}", (string)box.Attribute("Style"));
                Assert.Equal("OnBoxClick", (string)box.Attribute("Click"));
                Assert.Single((string)box.Attribute("Tag"));
            });
        }

        [Fact]
        public void The_pad_moves_over_the_boxes_two_to_a_row_and_each_box_opens_its_own_page()
        {
            var pane = Code("ThemeSettingsPane.xaml.cs");

            Assert.Matches(new Regex(@"new\[\] \{ PersonalisationBox, IdleBox \},\s*new\[\] \{ GuideBox, DiscordBox \},"), pane);
            Assert.Matches(new Regex(@"box == IdleBox \? ThemeSettingsPage\.Idle"), pane);
            Assert.Matches(new Regex(@"case ThemeSettingsPage\.Idle: return IdleBox;"), pane);
        }

        [Fact]
        public void The_boxes_are_Personalisations_and_the_pane_handles_the_pad_on_KeyDown_where_Playnite_raises_A()
        {
            var pane = Load("ThemeSettingsPane.xaml");

            Assert.Equal("OnPaneKeyDown", (string)pane.Root.Attribute("KeyDown"));
            Assert.Null(pane.Root.Attribute("PreviewKeyDown"));
            Assert.Contains(pane.Descendants(), e => e.Name.LocalName == "SettingsBoxes");
            Assert.Contains("PlayniteInput.PadButtonOf(e)", Code("ThemeSettingsPane.xaml.cs"));
        }

        [Fact]
        public void A_box_asks_the_core_for_its_page_and_B_on_the_boxes_goes_back_to_the_rail()
        {
            var pane = Code("ThemeSettingsPane.xaml.cs");
            var settings = Code("SettingsControl.xaml.cs");

            Assert.Contains("core.OpenThemePage(page);", pane);
            Assert.Matches(new Regex(@"pad == ControllerInput\.B \|\| \(e\.Key == Key\.Left && column == 0\)\)\s*\{[^}]*BackToRail\?\.Invoke"), pane);
            Assert.Contains("ThemeSettingsPane.BackToRail += (s, e) => ThemeSettingsItem.Focus();", settings);
            Assert.Matches(new Regex(@"e\.OriginalSource == ThemeSettingsItem && \(pad == ControllerInput\.A \|\| e\.Key == Key\.Right\)\)\s*\{\s*e\.Handled = true;\s*ThemeSettingsPane\.FocusFirst\(\);"), settings);
        }

        [Fact]
        public void B_on_a_page_leaves_it_for_the_boxes_rather_than_focusing_a_rail_item()
        {
            var settings = Code("SettingsControl.xaml.cs");

            Assert.Contains("PersonalisationPane.BackToThemeSettings += (s, e) => core.LeaveThemePage();", settings);
            // The Guide's and Idle's dropdown boxes, shared, and Discord's buttons, each handled before the page's own B.
            Assert.Equal(2, Regex.Matches(settings, @"e\.Handled = true;\s*core\.LeaveThemePage\(\);").Count);
        }
    }
}
