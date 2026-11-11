using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The Game hub as a panel over the screen it was opened from (#207, spec #201's Game hub > Backdrop). Back to front:
    // the game's art full screen, as before; with the extension, over it above and below the band, a snapshot of the
    // screen the hub was opened from, darkened 60%; then the band, through which the art shows under #B3000000 rather
    // than a flat grey. With the theme alone, or no snapshot, the snapshot's layer stays collapsed and the art fills the
    // screen as before. The XAML isn't run here; these read its structure.
    public class GameHubBackdropXamlTests
    {
        private const string Snapshot = "{PluginSettings Plugin=XboxSeries, Path=HubBackdrop.Snapshot}";
        private const string HasSnapshot = "{PluginSettings Plugin=XboxSeries, Path=HubBackdrop.HasSnapshot, FallbackValue=False}";

        private static readonly string HubPath = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard", "Views", "GameDetails.xaml");
        private static readonly XDocument Hub = XDocument.Load(HubPath);

        [Fact]
        public void The_layers_are_the_art_then_the_snapshot_then_the_band_then_the_hubs_content()
        {
            var art = Named("XboxHubArt");
            var backdrop = Named("XboxHubBackdrop");
            var band = Named("Band");

            Assert.Same(art.Parent, backdrop.Parent);
            Assert.Same(art.Parent, band.Parent);
            Assert.Equal("PART_ViewHost", (string)art.Parent.Attribute(X + "Name"));
            Assert.True(backdrop.IsAfter(art), "the snapshot is drawn over the art");
            Assert.True(band.IsAfter(backdrop), "the band is drawn over the snapshot");
            Assert.Equal("Grid", band.ElementsAfterSelf().First().Name.LocalName);
        }

        [Fact]
        public void The_art_is_the_games_background_else_its_cover_full_screen()
        {
            var art = Named("XboxHubArt");
            var names = art.Descendants().Select(e => (string)e.Attribute(X + "Name")).Where(n => n != null).ToList();

            Assert.Contains("PART_ImageBackground", names);
            Assert.Contains("XboxGameDetailsCover", names);
            Assert.Null(art.Attribute("Margin"));
            Assert.Equal("True", (string)art.Attribute("ClipToBounds"));
        }

        [Fact]
        public void Theme_alone_nothing_hides_the_full_screen_art()
        {
            // Without the extension PluginSettings finds nothing: the art must not depend on it to show.
            var art = Named("XboxHubArt");

            Assert.DoesNotContain(art.DescendantsAndSelf().Attributes(), a => a.Value.Contains("PluginSettings"));
            Assert.DoesNotContain(Hub.Descendants(), e => e.Name.LocalName == "Setter"
                && (string)e.Attribute("TargetName") == "XboxHubArt");
        }

        [Theory]
        [InlineData("XboxHubBackdropTop", "Top")]
        [InlineData("XboxHubBackdropBottom", "Bottom")]
        public void With_a_snapshot_the_screen_it_was_opened_from_shows_only_outside_the_band_darkened_60_percent(string name, string edge)
        {
            // A strip the band's gap high, clipping a layer the whole screen high to its own edge: the snapshot is
            // stretched over the whole screen, so what shows lines up with what was there.
            var backdrop = Named("XboxHubBackdrop");
            var strip = Named(name);
            var gap = ((string)Named("Band").Attribute("Margin")).Split(',');
            var screen = strip.Elements().Single(e => !e.Name.LocalName.Contains("."));
            var image = screen.Elements().Single(e => e.Name.LocalName == "Image");
            var dim = screen.Elements().Single(e => e.Name.LocalName == "Rectangle");

            Assert.Same(backdrop, strip.Parent);
            Assert.Equal(edge == "Top" ? gap[1] : gap[3], (string)strip.Attribute("Height"));
            Assert.Equal(edge, (string)strip.Attribute("VerticalAlignment"));
            Assert.Equal("True", (string)strip.Attribute("ClipToBounds"));
            Assert.Equal("{Binding ActualHeight, ElementName=XboxHubBackdrop}", (string)screen.Attribute("Height"));
            Assert.Equal(edge, (string)screen.Attribute("VerticalAlignment"));
            Assert.Equal(Snapshot, (string)image.Attribute("Source"));
            Assert.Equal("Fill", (string)image.Attribute("Stretch"));
            Assert.Equal("#99000000", (string)dim.Attribute("Fill"));
            Assert.True(dim.IsAfter(image));
            Assert.Null(backdrop.Attribute("Margin"));
            Assert.Equal("False", (string)backdrop.Attribute("IsHitTestVisible"));
        }

        [Fact]
        public void The_snapshot_shows_only_while_the_extension_has_one()
        {
            var style = Named("XboxHubBackdrop").Elements().Single(e => e.Name.LocalName == "Grid.Style").Elements().Single();

            Assert.Equal("Collapsed", Setter(style, "Visibility"));
            var trigger = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal(HasSnapshot, (string)trigger.Attribute("Binding"));
            Assert.Equal("True", (string)trigger.Attribute("Value"));
            Assert.Equal("Visible", Setter(trigger, "Visibility"));
        }

        [Fact]
        public void The_band_lets_the_games_art_show_through_under_B3_black_not_a_flat_grey()
        {
            var band = Named("Band");

            Assert.Equal("0,88,0,88", (string)band.Attribute("Margin"));
            Assert.Equal("#B3000000", (string)band.Attribute("Background"));
            Assert.Empty(band.Elements());
            Assert.DoesNotContain(Hub.Descendants().Attributes(), a => a.Value == "#E61F1F1F");
            Assert.DoesNotContain(Hub.Descendants(), e => e.Name.LocalName == "VisualBrush");
        }

        private static XElement Named(string name) =>
            Hub.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        private static string Setter(XElement owner, string property) =>
            (string)owner.Elements().Single(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == property).Attribute("Value");
    }
}
