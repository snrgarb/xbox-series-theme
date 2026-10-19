using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Guide's and the tile menu's menus and prompts (MenuPanel), measured against the console's context menu
    // (Menu on Call of Duty, captured at 720 and scaled to 1080): a 450 wide panel, the focused row's fill edge to edge
    // and its green ring standing out past the panel on both sides. The XAML isn't run here; these read its geometry.
    public class MenuPanelXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private readonly XDocument menu = XDocument.Load(Path.Combine(ExtensionDir, "MenuPanel.xaml"));

        [Fact]
        public void The_panel_is_450_wide_with_corners_of_6()
        {
            var panel = PanelBorder();

            Assert.Equal("450", (string)panel.Attribute("Width"));
            Assert.Equal("6", (string)panel.Attribute("CornerRadius"));
        }

        [Fact]
        public void The_focus_ring_stands_8_past_the_panels_left_and_right_edges()
        {
            var (left, right) = HorizontalInsetFromPanel(Named("FocusRing"));

            Assert.Equal(-8, left);
            Assert.Equal(-8, right);
        }

        [Fact]
        public void The_focused_rows_fill_spans_the_panel_edge_to_edge_in_an_opaque_lighter_grey()
        {
            var (left, right) = HorizontalInsetFromPanel(Named("Row"));

            Assert.Equal(0, left);
            Assert.Equal(0, right);
            Assert.Equal("#FF404040", FocusedSetter("Row", "Background"));
        }

        [Fact]
        public void The_ring_is_the_rows_full_72_height_4_thick_with_corners_of_7()
        {
            var ring = Named("FocusRing");
            var margin = Thickness(ring, "Margin");

            Assert.Equal(72, RowHeight() - margin.Top - margin.Bottom);
            Assert.Equal((4.0, 4.0, 4.0, 4.0), Thickness(ring, "BorderThickness"));
            Assert.Equal("7", (string)ring.Attribute("CornerRadius"));
        }

        [Fact]
        public void The_fill_is_56_tall_standing_4_inside_the_ring_all_round()
        {
            var ring = Thickness(Named("FocusRing"), "Margin");
            var fill = Thickness(Named("Row"), "Margin");
            var thickness = Thickness(Named("FocusRing"), "BorderThickness");

            Assert.Equal(56, RowHeight() - fill.Top - fill.Bottom);
            Assert.Equal(4, fill.Top - ring.Top - thickness.Top);
            Assert.Equal(4, fill.Bottom - ring.Bottom - thickness.Bottom);
            Assert.Equal(4, fill.Left - ring.Left - thickness.Left);
            Assert.Equal(4, fill.Right - ring.Right - thickness.Right);
        }

        [Fact]
        public void The_panel_ends_at_the_last_rows_foot_so_the_last_ring_never_hangs_below_it()
        {
            // The last row's centre then sits 36 above the panel's bottom edge, the console's ~35.
            var padding = Thickness(PanelBorder(), "Padding");

            Assert.Equal(0, padding.Bottom);
            Assert.True(Thickness(Named("FocusRing"), "Margin").Bottom >= 0);
        }

        [Fact]
        public void Rows_are_72_apart()
        {
            var row = RowTemplateRoot();
            var margin = Thickness(row, "Margin");

            Assert.Equal(72, RowHeight() + margin.Top + margin.Bottom);
        }

        [Fact]
        public void Customise_starts_the_settings_group_21_further_down_its_ring_moving_with_its_row()
        {
            var row = RowTemplateRoot();
            var trigger = menu.Descendants().Single(e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Binding") == "{Binding Icon}" && (string)e.Attribute("Value") == "Customise");
            var gap = trigger.Elements().Single(s => (string)s.Attribute("TargetName") == (string)row.Attribute(X + "Name")
                && (string)s.Attribute("Property") == "Margin");

            Assert.Equal("0,21,0,0", (string)gap.Attribute("Value"));
            Assert.Equal(72.0, RowHeight());
        }

        [Fact]
        public void The_icon_is_centred_44_in_and_the_label_starts_88_in()
        {
            var icon = Named("Icon");
            var label = menu.Descendants().Single(e => (string)e.Attribute("Text") == "{Binding Label}");
            var (iconLeft, _) = HorizontalInsetFromPanel(icon);
            var iconWidth = double.Parse((string)icon.Attribute("Width"));
            // The label follows the icon in a horizontal StackPanel.
            var labelLeft = iconLeft + iconWidth + Thickness(icon, "Margin").Right + Thickness(label, "Margin").Left;

            Assert.Equal(44, iconLeft + iconWidth / 2);
            Assert.Equal(88, labelLeft);
            Assert.Equal("22", (string)icon.Attribute("FontSize"));
            Assert.Equal("22", (string)label.Attribute("FontSize"));
            Assert.Equal("{DynamicResource XboxFont}", (string)label.Attribute("FontFamily"));
        }

        [Fact]
        public void The_title_is_the_header_font_at_33_set_28_in_36_down_and_24_above_the_rows()
        {
            var title = Title();

            Assert.Equal("{DynamicResource XboxHeaderFont}", (string)title.Attribute("FontFamily"));
            Assert.Equal("33", (string)title.Attribute("FontSize"));
            Assert.Equal("28,36,28,24", (string)title.Attribute("Margin"));
        }

        [Fact]
        public void A_menus_title_is_one_line_trimmed_with_an_ellipsis()
        {
            var title = Title();

            Assert.Null((string)title.Attribute("TextWrapping"));
            Assert.Null((string)title.Attribute("TextTrimming"));
            var style = title.Elements().Single(e => e.Name.LocalName == "TextBlock.Style").Elements().Single();
            Assert.Equal("NoWrap", TitleSetter(style.Elements(), "TextWrapping"));
            Assert.Equal("CharacterEllipsis", TitleSetter(style.Elements(), "TextTrimming"));
        }

        [Fact]
        public void A_prompts_question_wraps_rather_than_being_cut_short()
        {
            // "Quit Halo Infinite and start Hades?" has to be read whole.
            var trigger = Title().Descendants().Single(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == "{Binding IsPrompt}");

            Assert.Equal("True", (string)trigger.Attribute("Value"));
            Assert.Equal("Wrap", TitleSetter(trigger.Elements(), "TextWrapping"));
        }

        private XElement Title() => menu.Descendants().Single(e => (string)e.Attribute("Text") == "{Binding Title}");

        private static string TitleSetter(IEnumerable<XElement> setters, string property) =>
            (string)setters.Single(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == property).Attribute("Value");

        [Fact]
        public void Nothing_in_the_menu_clips_the_ring()
        {
            var clipping = menu.Descendants().Where(e => (string)e.Attribute("ClipToBounds") == "True");

            Assert.Empty(clipping);
        }

        [Theory]
        [InlineData("src/XboxSeries.Extension/TileMenuControl.xaml", "Panel")]
        [InlineData("src/XboxSeries.Extension/OverlayWindow.xaml", "DialogPanel")]
        [InlineData("src/XboxSeries.Dashboard/Views/Main.xaml", "XboxSeries_TileMenu")]
        public void No_parent_of_the_menu_clips_the_ring(string file, string host)
        {
            var doc = XDocument.Load(Path.Combine(RepoRoot, file));
            var element = doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == host);

            var clipping = element.AncestorsAndSelf().Where(Clips).Select(e => e.Name.LocalName).ToList();

            Assert.Empty(clipping);
        }

        private static bool Clips(XElement element) =>
            (string)element.Attribute("ClipToBounds") == "True"
            || element.Elements().Where(e => e.Name.LocalName.EndsWith(".Style"))
                .SelectMany(s => s.Descendants())
                .Any(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == "ClipToBounds"
                          && (string)s.Attribute("Value") == "True");

        private XElement PanelBorder() => menu.Root.Elements().Single(e => e.Name.LocalName == "Border");

        private XElement RowTemplateRoot() =>
            menu.Descendants().Single(e => e.Name.LocalName == "DataTemplate").Elements().First(e => !e.Name.LocalName.Contains("."));

        private double RowHeight() => double.Parse((string)RowTemplateRoot().Attribute("Height"));

        private XElement Named(string name) => menu.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        private string FocusedSetter(string target, string property) =>
            (string)menu.Descendants().Single(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == "{Binding IsFocused}")
                .Elements().Single(s => (string)s.Attribute("TargetName") == target && (string)s.Attribute("Property") == property)
                .Attribute("Value");

        // How far in from the panel's own left and right edges an element of the row template draws (negative: out past them).
        private (double left, double right) HorizontalInsetFromPanel(XElement element)
        {
            var padding = Thickness(PanelBorder(), "Padding");
            double left = padding.Left, right = padding.Right;
            foreach (var e in element.AncestorsAndSelf().TakeWhile(e => e.Name.LocalName != "DataTemplate"))
            {
                var margin = Thickness(e, "Margin");
                left += margin.Left;
                right += margin.Right;
            }
            return (left, right);
        }

        private static (double Left, double Top, double Right, double Bottom) Thickness(XElement element, string attribute)
        {
            var value = (string)element.Attribute(attribute);
            if (value == null)
            {
                return (0, 0, 0, 0);
            }
            var parts = value.Split(',').Select(double.Parse).ToArray();
            switch (parts.Length)
            {
                case 1: return (parts[0], parts[0], parts[0], parts[0]);
                case 2: return (parts[0], parts[1], parts[0], parts[1]);
                default: return (parts[0], parts[1], parts[2], parts[3]);
            }
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
