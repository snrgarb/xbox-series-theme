using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Apps' tiles, titled as Home's game tiles: focused, a black band over the art's foot holds the App's name in white. A Microsoft
    // Store app's tile art fills the art over the accent; any other App's icon sits on white, a soft shadow behind it. The
    // XAML isn't run here; these checks guard the face's look and that My games & apps and Search share the one face.
    public class AppTilesXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XElement Face() => Load("AppTiles.xaml").Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == "AppTileArt");

        private static XElement Named(XElement root, string name) =>
            root.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        [Fact]
        public void The_name_is_white_in_the_body_font_on_a_black_band_over_the_art_foot()
        {
            var name = Face().Descendants().Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding Name}");
            var band = name.Parent;

            Assert.Equal("TitleBand", (string)band.Attribute(X + "Name"));
            // Solid: Home's 90% black reads black over dark covers, but grey over an App's white.
            Assert.Equal("Black", (string)band.Attribute("Background"));
            Assert.Equal("Bottom", (string)band.Attribute("VerticalAlignment"));
            Assert.Null(band.Attribute("Grid.Row"));
            Assert.Equal("White", (string)name.Attribute("Foreground"));
            Assert.Equal("{DynamicResource XboxFont}", (string)name.Attribute("FontFamily"));
            Assert.Equal("Wrap", (string)name.Attribute("TextWrapping"));
            Assert.Equal("52", (string)name.Attribute("MaxHeight"));
        }

        [Fact]
        public void The_band_shows_only_while_the_tile_is_focused()
        {
            var face = Face();
            var focused = face.Descendants().Single(e => e.Name.LocalName == "DataTrigger"
                                                         && ((string)e.Attribute("Binding"))?.Contains("IsKeyboardFocused") == true);
            string To(string actions) => focused.Elements().Single(e => e.Name.LocalName == "DataTrigger." + actions)
                .Descendants().Single(e => e.Name.LocalName == "DoubleAnimation"
                                           && (string)e.Attribute("Storyboard.TargetName") == "TitleBand"
                                           && (string)e.Attribute("Storyboard.TargetProperty") == "Opacity")
                .Attribute("To").Value;

            Assert.Equal("0", (string)Named(face, "TitleBand").Attribute("Opacity"));
            Assert.Contains("AncestorType=ListBoxItem", (string)focused.Attribute("Binding"));
            Assert.Equal("True", (string)focused.Attribute("Value"));
            Assert.Equal("1", To("EnterActions"));
            Assert.Equal("0", To("ExitActions"));
        }

        [Fact]
        public void Any_other_App_is_on_white_its_icon_shadowed_from_a_cached_picture()
        {
            var face = Face();
            var icon = Named(face, "Icon");

            Assert.Equal("White", (string)Named(face, "Art").Attribute("Background"));
            Assert.Contains(icon.Descendants(), e => e.Name.LocalName == "DropShadowEffect");
            // The cache is the icon's parent's, as WPF caches an element under its own effect.
            Assert.Contains(icon.Parent.Descendants(), e => e.Name.LocalName == "BitmapCache");
            Assert.Equal("#FF333333", (string)Named(face, "NoIconGlyph").Attribute("Foreground"));
        }

        [Fact]
        public void A_Store_app_fills_the_art_over_the_accent_unshadowed()
        {
            var store = Face().Descendants().Single(e => e.Name.LocalName == "DataTrigger"
                                                         && (string)e.Attribute("Binding") == "{Binding IsStoreApp}");
            string Set(string target, string property) => store.Elements()
                .Where(s => (string)s.Attribute("TargetName") == target && (string)s.Attribute("Property") == property)
                .Select(s => (string)s.Attribute("Value")).Single();

            Assert.Equal("True", (string)store.Attribute("Value"));
            Assert.Equal("{DynamicResource GlyphBrush}", Set("Art", "Background"));
            Assert.Equal("0", Set("Shadowed", "Margin"));
            Assert.Equal("{x:Null}", Set("Icon", "Effect"));
            Assert.Equal("White", Set("NoIconGlyph", "Foreground"));
        }

        [Theory]
        [InlineData("MyGamesControl.xaml", "AppsList")]
        [InlineData("SearchControl.xaml", "AppsRow")]
        public void My_games_and_Search_show_Apps_with_the_one_face(string file, string list)
        {
            var doc = Load(file);

            Assert.Equal("{StaticResource AppTileArt}", (string)Named(doc.Root, list).Attribute("ItemTemplate"));
            Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "AppTiles");
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "DataTemplate"
                                                          && e.Descendants().Any(d => (string)d.Attribute("Text") == "\uECAA"));
        }
    }
}
