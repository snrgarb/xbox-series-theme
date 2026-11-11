using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Text entry over the page: the Console keyboard under a caption and its field. The XAML isn't run here; these checks
    // guard where the Dashboard hosts it, what it binds, that it reuses Search's keyboard and its one focus ring, its
    // fonts (#77), and a dim light enough that Full library is seen narrowing behind it.
    public class TextEntryXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        [Fact]
        public void The_host_is_drawn_last_over_everything_in_the_main_host()
        {
            var main = XDocument.Load(Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard", "Views", "Main.xaml"));
            var host = main.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PART_MainHost");
            var layers = host.Elements().Select(e => (string)e.Attribute(X + "Name")).ToList();

            Assert.Equal("XboxSeries_TextEntry", layers.Last());
            Assert.All(new[] { "PART_ViewHost", "PART_ElemGameDetails", "XboxSeries_Hints", "XboxSeries_TileMenu", "PART_ElemGameStatus" },
                layer => Assert.True(layers.IndexOf(layer) >= 0 && layers.IndexOf(layer) < layers.IndexOf("XboxSeries_TextEntry"), layer));

            var textEntry = host.Elements().Last();
            Assert.Equal("ContentControl", textEntry.Name.LocalName);
            Assert.Equal("False", (string)textEntry.Attribute("Focusable"));
            Assert.Equal("False", (string)textEntry.Attribute("IsTabStop"));
        }

        [Fact]
        public void The_extension_offers_text_entry_as_a_custom_element_with_its_state()
        {
            var plugin = File.ReadAllText(Path.Combine(ExtensionDir, "XboxSeriesPlugin.cs"));
            var control = File.ReadAllText(Path.Combine(ExtensionDir, "TextEntryControl.xaml.cs"));

            Assert.Contains("\"TextEntry\", \"Hints\" }", plugin);
            Assert.Contains("case \"TextEntry\": return new TextEntryControl(core);", plugin);
            Assert.Contains("public TextEntryState State", control);
        }

        [Fact]
        public void It_types_on_Searchs_Console_keyboard_whose_one_ring_is_the_only_glow()
        {
            var doc = Load("TextEntryControl.xaml");

            Assert.Single(doc.Descendants(), e => e.Name.LocalName == "ConsoleKeyboardControl");
            Assert.DoesNotContain(doc.Descendants(), e => (string)e.Attribute("Style") == "{DynamicResource XboxFocusGlow}");
        }

        [Fact]
        public void The_keyboard_sits_as_far_off_the_foot_as_on_Search()
        {
            int Bottom(XElement e) => int.Parse(((string)e.Attribute("Margin")).Split(',')[3], CultureInfo.InvariantCulture);
            var search = Load("SearchControl.xaml").Descendants().Single(e => e.Name.LocalName == "ConsoleKeyboardControl");
            var keys = Load("TextEntryControl.xaml").Descendants().Single(e => e.Name.LocalName == "ConsoleKeyboardControl");

            // Here the keyboard ends the stack holding the caption and the field, which is docked at the foot.
            Assert.Equal(0, Bottom(keys));
            Assert.Equal("Bottom", (string)keys.Parent.Attribute("VerticalAlignment"));
            Assert.Equal(Bottom(search), Bottom(keys.Parent));
        }

        [Fact]
        public void The_caption_is_a_heading_in_the_header_fonts_SemiLight_and_the_field_in_the_body_font()
        {
            var doc = Load("TextEntryControl.xaml");
            string StyleOf(string name) =>
                ResourceKey((string)doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name).Attribute("Style"));

            var caption = StyleByKey(doc, StyleOf("Caption"));
            Assert.Equal("XboxHeaderFont", FontOfStyle(doc, caption));
            Assert.Contains(caption.Elements(), s => (string)s.Attribute("Property") == "FontWeight" && (string)s.Attribute("Value") == "350");
            Assert.Equal("XboxFont", FontOfStyle(doc, StyleOf("BeforeCaret")));
            Assert.Equal("XboxFont", FontOfStyle(doc, StyleOf("AfterCaret")));
        }

        [Fact]
        public void The_dim_is_lighter_than_the_tile_menus_over_the_page_and_darker_behind_the_keyboard()
        {
            byte Alpha(string argb) => byte.Parse(argb.Substring(1, 2), NumberStyles.HexNumber);
            var tileMenu = Alpha((string)Load("TileMenuControl.xaml").Descendants()
                .Single(e => (string)e.Attribute(X + "Name") == "Dim").Attribute("Background"));
            var stops = Load("TextEntryControl.xaml").Descendants()
                .Single(e => (string)e.Attribute(X + "Name") == "Dim")
                .Descendants().Where(e => e.Name.LocalName == "GradientStop")
                .Select(e => Alpha((string)e.Attribute("Color")))
                .ToList();

            Assert.True(stops.First() < tileMenu, "Full library shows through the top of the dim");
            Assert.True(stops.Last() > tileMenu, "the caption and the field read clearly over its foot");
        }
    }
}
