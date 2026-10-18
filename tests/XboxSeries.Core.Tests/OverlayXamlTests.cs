using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Overlay's XAML isn't run here. These checks guard what the Guide does as it opens: every entry is built anew
    // each time, so whatever its template decodes is decoded again on every open.
    public class OverlayXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private const string ImageConverter = "Converter={StaticResource Image}";

        [Fact]
        public void A_Guide_entry_decodes_its_image_only_for_the_part_its_kind_shows()
        {
            var entry = GuideEntryTemplate();
            var triggers = entry.Elements().Single(e => e.Name.LocalName == "DataTemplate.Triggers");

            // Outside its triggers, no part of an entry decodes the image: a game's cover, the profile's picture, a
            // Discord avatar or icon each take it from the trigger on the entry's kind that shows that part.
            var parts = entry.Elements().Where(e => e != triggers).SelectMany(e => e.DescendantsAndSelf())
                .Where(e => !e.Ancestors().Any(a => a.Name.LocalName == "DataTemplate" && a != entry));
            Assert.DoesNotContain(parts.SelectMany(e => e.Attributes()), a => a.Value.Contains(ImageConverter));

            // And each kind's trigger decodes it once, at the size of the one part it shows.
            var decodingTriggers = triggers.Elements()
                .Select(t => t.Elements().Count(s => ((string)s.Attribute("Value") ?? "").Contains(ImageConverter)))
                .Where(count => count > 0)
                .ToList();
            Assert.NotEmpty(decodingTriggers);
            Assert.All(decodingTriggers, count => Assert.Equal(1, count));
        }

        [Fact]
        public void Now_Playing_has_a_glyph_for_its_tab_icon_a_heading_and_a_card_with_the_art_as_the_extension_decoded_it()
        {
            var overlay = Overlay();
            var tabTemplate = overlay.Descendants().Single(e => e.Name.LocalName == "ItemsControl" && (string)e.Attribute("ItemsSource") == "{Binding Tabs}")
                .Descendants().Single(e => e.Name.LocalName == "DataTemplate");
            var tabTrigger = KindTrigger(tabTemplate, "NowPlaying");
            Assert.Contains(tabTrigger.Elements(), s => (string)s.Attribute("TargetName") == "TabGlyph" && (string)s.Attribute("Value") == "Visible");
            Assert.Contains(tabTrigger.Elements(), s => (string)s.Attribute("TargetName") == "Logo" && (string)s.Attribute("Value") == "Collapsed");

            Assert.Contains(overlay.Descendants(), e => e.Name.LocalName == "Setter" && (string)e.Attribute("Value") == "Now Playing");

            var entry = GuideEntryTemplate();
            var card = KindTrigger(entry, "NowPlaying");
            Assert.Contains(card.Elements(), s => (string)s.Attribute("TargetName") == "Media" && (string)s.Attribute("Value") == "Visible");
            Assert.Contains(entry.Descendants(), e => e.Name.LocalName == "ImageBrush" && (string)e.Attribute("ImageSource") == "{Binding Art}");
            Assert.NotNull(KindTrigger(entry, "NothingPlaying"));
        }

        private static XElement KindTrigger(XElement template, string kind) =>
            template.Elements().Single(e => e.Name.LocalName == "DataTemplate.Triggers").Elements()
                .Single(t => (string)t.Attribute("Binding") == "{Binding Kind}" && (string)t.Attribute("Value") == kind);

        private static XDocument Overlay() =>
            XDocument.Load(Path.Combine(FindRepoRoot(), "src", "XboxSeries.Extension", "OverlayWindow.xaml"));

        private static XElement GuideEntryTemplate()
        {
            return Overlay().Descendants().Single(e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == "GuideEntry");
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
