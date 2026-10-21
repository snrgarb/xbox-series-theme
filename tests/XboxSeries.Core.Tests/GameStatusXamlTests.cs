using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Playnite's game status view, "<Game> is running…", drawn as the Launch screen (#80): the game's art full screen,
    // chosen as the hero chooses it, with the cover and the status centred over it, and no Close: B leaves the view. The XAML isn't run here (it was
    // tried on a harness hosting Playnite's own GameStatus control); these checks guard the art, the scrim and the parts.
    public class GameStatusXamlTests
    {
        private const string Converter = "Converter={StaticResource ImageStringToImageConverter}";

        private static XDocument View() =>
            XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Views", "GameStatus.xaml"));

        private static XElement Named(XDocument doc, string name) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name || (string)e.Attribute("Name") == name);

        private static XElement Template(XDocument doc) =>
            doc.Descendants().Single(e => e.Name.LocalName == "ControlTemplate" && (string)e.Attribute("TargetType") == "{x:Type GameStatus}");

        private static IEnumerable<XElement> TemplateTriggers(XDocument doc) =>
            Template(doc).Elements().Single(e => e.Name.LocalName == "ControlTemplate.Triggers").Elements();

        [Fact]
        public void The_view_is_black_under_the_art_not_the_Dashboards_background_so_a_game_with_no_art_shows_black()
        {
            var host = Named(View(), "PART_ViewHost");
            Assert.Equal("Black", (string)host.Attribute("Background"));
            Assert.DoesNotContain(View().Descendants().SelectMany(e => e.Attributes()), a => a.Value.Contains("MainBackgourndBrush"));
        }

        [Fact]
        public void The_art_fills_the_whole_view_behind_everything_and_takes_no_focus_or_clicks()
        {
            var doc = View();
            var host = Named(doc, "PART_ViewHost");
            var art = Named(doc, "XboxGameStatusArt");

            Assert.Same(host, art.Parent);
            Assert.Same(art, host.Elements().First(e => !e.Name.LocalName.Contains(".")));
            Assert.Equal("4", (string)art.Attribute("Grid.RowSpan"));
            Assert.Equal("3", (string)art.Attribute("Grid.ColumnSpan"));
            Assert.Equal("True", (string)art.Attribute("ClipToBounds"));
            Assert.Equal("False", (string)art.Attribute("IsHitTestVisible"));
        }

        [Fact]
        public void Background_art_shows_as_it_is_as_the_Launch_screen_shows_it()
        {
            var doc = View();
            var background = Named(doc, "XboxGameStatusBackground");

            Assert.Equal("{Binding Game.BackgroundImage, " + Converter + "}", (string)background.Attribute("Source"));
            Assert.Equal("UniformToFill", (string)background.Attribute("Stretch"));
            Assert.Equal("Center", (string)background.Attribute("HorizontalAlignment"));
            Assert.Equal("Top", (string)background.Attribute("VerticalAlignment"));
            Assert.DoesNotContain(background.Descendants(), e => e.Name.LocalName.EndsWith(".Effect"));
            Assert.Null(background.Attribute("Margin"));
        }

        [Fact]
        public void Without_background_art_the_cover_shows_blurred_and_darkened_with_the_Launch_screens_numbers()
        {
            var doc = View();
            var layer = Named(doc, "XboxGameStatusCoverArt");
            var cover = layer.Descendants().Single(e => e.Name.LocalName == "Image");

            // The cover layer sits under the background art, so the background covers it when both are there.
            var art = Named(doc, "XboxGameStatusArt");
            Assert.True(art.Elements().ToList().IndexOf(layer) < art.Elements().ToList().IndexOf(Named(doc, "XboxGameStatusBackground")));

            Assert.Equal("UniformToFill", (string)cover.Attribute("Stretch"));
            Assert.Equal("Top", (string)cover.Attribute("VerticalAlignment"));
            // Blurred small and stretched (#95): laid out a fifteenth of the 1080p screen, 128 by 72, in a picture that size,
            // so its overhang and blur are a fifteenth of the Launch screen's 120 and 60.
            var small = cover.Parent;
            Assert.Equal(("128", "72"), ((string)small.Attribute("Width"), (string)small.Attribute("Height")));
            Assert.Contains(small.Elements(), e => e.Name.LocalName == "Grid.CacheMode");
            var stretched = small.Parent;
            Assert.Equal(("Viewbox", "UniformToFill", "Top"), (stretched.Name.LocalName, (string)stretched.Attribute("Stretch"), (string)stretched.Attribute("VerticalAlignment")));
            Assert.Equal("-8", (string)cover.Attribute("Margin"));
            var blur = cover.Descendants().Single(e => e.Name.LocalName == "BlurEffect");
            Assert.Equal("4", (string)blur.Attribute("Radius"));
            Assert.Equal("Performance", (string)blur.Attribute("RenderingBias"));

            var darken = layer.Elements().Single(e => e.Name.LocalName == "Rectangle");
            Assert.Equal("#99000000", (string)darken.Attribute("Fill"));
            Assert.True(layer.Elements().ToList().IndexOf(stretched) < layer.Elements().ToList().IndexOf(darken));
        }

        [Fact]
        public void The_cover_is_decoded_and_blurred_only_for_a_game_without_background_art()
        {
            var doc = View();
            var layer = Named(doc, "XboxGameStatusCoverArt");
            var cover = layer.Descendants().Single(e => e.Name.LocalName == "Image");

            // Out of the triggers the layer is collapsed and the cover has no picture, so under background art nothing is
            // decoded or blurred.
            Assert.Equal("Collapsed", (string)layer.Attribute("Visibility"));
            Assert.Null(cover.Attribute("Source"));

            var coverName = (string)cover.Attribute(X + "Name");
            foreach (var none in new[] { "{x:Null}", "" })
            {
                var trigger = TemplateTriggers(doc).Single(t => t.Name.LocalName == "DataTrigger"
                    && (string)t.Attribute("Binding") == "{Binding Game.BackgroundImage}" && (string)t.Attribute("Value") == none);
                Assert.Contains(trigger.Elements(), s => (string)s.Attribute("TargetName") == "XboxGameStatusCoverArt"
                    && (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Visible");
                Assert.Contains(trigger.Elements(), s => (string)s.Attribute("TargetName") == coverName
                    && (string)s.Attribute("Property") == "Source" && (string)s.Attribute("Value") == "{Binding Game.CoverImage, " + Converter + "}");
            }
        }

        [Fact]
        public void The_only_live_effect_is_the_covers_blur()
        {
            var effects = View().Descendants().Where(e => e.Name.LocalName.EndsWith("Effect") && !e.Name.LocalName.Contains(".")).ToList();
            Assert.Equal(new[] { "BlurEffect" }, effects.Select(e => e.Name.LocalName));
            Assert.DoesNotContain(View().Descendants().SelectMany(e => e.Attributes()), a => a.Name.LocalName == "OpacityMask" || a.Value.Contains("BitmapCache"));
            // The one picture is the cover's, the small one it is blurred in.
            var cache = View().Descendants().Single(e => e.Name.LocalName == "BitmapCache");
            Assert.Equal("XboxGameStatusCover", (string)cache.Parent.Parent.Elements().Single(e => e.Name.LocalName == "Image").Attribute(X + "Name"));
        }

        [Fact]
        public void A_scrim_over_the_art_keeps_the_status_readable_on_bright_art()
        {
            var doc = View();
            var host = Named(doc, "PART_ViewHost");
            var children = host.Elements().Where(e => !e.Name.LocalName.Contains(".")).ToList();
            var scrims = children.Where(e => e.Name.LocalName == "Rectangle").ToList();

            // Home's hero scrim, then a shade behind the centred column, both over the art and under the parts.
            Assert.Contains(scrims, s => (string)s.Attribute("Fill") == "{DynamicResource XboxHeroScrimBrush}");
            Assert.Contains(scrims, s => s.Descendants().Any(e => e.Name.LocalName == "RadialGradientBrush"));
            foreach (var scrim in scrims)
            {
                Assert.Equal("False", (string)scrim.Attribute("IsHitTestVisible"));
                Assert.Equal("4", (string)scrim.Attribute("Grid.RowSpan"));
                Assert.Equal("3", (string)scrim.Attribute("Grid.ColumnSpan"));
                Assert.True(children.IndexOf(Named(doc, "XboxGameStatusArt")) < children.IndexOf(scrim));
                Assert.True(children.IndexOf(scrim) < children.IndexOf(Named(doc, "PART_ImageCover")));
            }
        }

        [Fact]
        public void The_cover_and_the_status_stay_centred_in_the_middle_column_over_the_art()
        {
            var doc = View();
            foreach (var (part, row) in new[] { ("PART_ImageCover", "1"), ("PART_TextStatus", "2") })
            {
                var element = Named(doc, part);
                Assert.Equal(row, (string)element.Attribute("Grid.Row"));
                Assert.Equal("1", (string)element.Attribute("Grid.Column"));
                Assert.Equal("Center", (string)element.Attribute("HorizontalAlignment"));
            }
        }

        [Fact]
        public void There_is_no_Close_and_B_or_Escape_leaves_the_view_as_Close_did()
        {
            var doc = View();

            // Without the panel Playnite adds no Close.
            Assert.DoesNotContain(doc.Descendants(), e => (string)e.Attribute("Name") == "PART_PanelActionButtons");

            // Nothing in the view takes focus, so the view host does: Playnite focuses it as the view shows, and the
            // presses reach its bindings. It draws no focus rectangle.
            var host = Named(doc, "PART_ViewHost");
            Assert.Equal("True", (string)host.Attribute("Focusable"));
            Assert.Equal("{x:Null}", (string)host.Attribute("FocusVisualStyle"));

            const string close = "{MainViewModel CloseGameStatusCommand, DirectValue=True}";
            var bindings = host.Elements().Single(e => e.Name.LocalName == "Grid.InputBindings").Elements().ToList();
            Assert.Contains(bindings, b => b.Name.LocalName == "GameControllerInputBinding"
                && (string)b.Attribute("Button") == "B" && (string)b.Attribute("Command") == close);
            Assert.Contains(bindings, b => b.Name.LocalName == "KeyBinding"
                && (string)b.Attribute("Key") == "Escape" && (string)b.Attribute("Command") == close);
        }
    }
}
