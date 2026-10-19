using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Apps' tiles, titled as Home's game tiles: the App's art over a black band holding its name in white. A Microsoft
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
        public void The_name_is_white_in_the_body_font_on_a_black_band_on_one_line()
        {
            var name = Face().Descendants().Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding Name}");
            var band = name.Parent;

            Assert.Equal("Border", band.Name.LocalName);
            Assert.Equal("Black", (string)band.Attribute("Background"));
            Assert.Equal("White", (string)name.Attribute("Foreground"));
            Assert.Equal("{DynamicResource XboxFont}", (string)name.Attribute("FontFamily"));
            Assert.Equal("NoWrap", (string)name.Attribute("TextWrapping"));
            Assert.Equal("CharacterEllipsis", (string)name.Attribute("TextTrimming"));
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
            // The art is wider than tall over the name's band, so the square tile art covers it, not fits in it.
            Assert.Equal("UniformToFill", Set("Icon", "Stretch"));
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
