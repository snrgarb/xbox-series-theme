using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // A game tile's face shows its title for want of a cover: for none, and for one set but that won't load (a deleted
    // cache, a bad download), so it is never an empty square. The XAML isn't run here; this guards that the title follows
    // the picture shown, not the cover's path.
    public class HomeTilesXamlTests
    {
        private static XElement Face() => XDocument.Load(Path.Combine(RepoRoot, "extension", "XboxSeries.Extension", "HomeTiles.xaml"))
            .Descendants().Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == "HomeTileArt");

        [Fact]
        public void The_title_shows_whenever_the_cover_shows_no_picture()
        {
            var face = Face();
            // The cover is the art pair (TileArt), whose content is the picture decoded.
            var cover = face.Descendants().Single(e => e.Name.LocalName == "ContentControl" && ((string)e.Attribute("Content"))?.Contains("{Binding Art,") == true);
            var title = face.Descendants().Single(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding Title}");
            var shown = face.Descendants().Single(e => e.Name.LocalName == "DataTrigger"
                                                      && (string)e.Attribute("Binding") == $"{{Binding Content, ElementName={(string)cover.Attribute(X + "Name")}}}");

            Assert.Equal("Collapsed", (string)title.Attribute("Visibility"));
            Assert.Equal("{x:Null}", (string)shown.Attribute("Value"));
            Assert.Contains(shown.Elements(), s => (string)s.Attribute("TargetName") == (string)title.Attribute(X + "Name")
                                                   && (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Visible");
            Assert.DoesNotContain(face.Descendants(), e => e.Name.LocalName == "DataTrigger" && ((string)e.Attribute("Binding") == "{Binding CoverImage}" || (string)e.Attribute("Binding") == "{Binding Art}"));
        }
    }
}
