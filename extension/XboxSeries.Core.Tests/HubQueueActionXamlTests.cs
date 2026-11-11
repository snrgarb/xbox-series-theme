using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The Game hub's Queue action (#208): the theme places the extension's Queue (XboxSeries_QueueAction) after Manage,
    // empty, so with the theme alone it never shows; the extension's is an icon action as Manage is, the download glyph
    // named Queue above it while it has focus, and collapsed until the game is in the Queue. The XAML isn't run here;
    // these read its structure.
    public class HubQueueActionXamlTests
    {
        private static readonly XDocument Hub =
            XDocument.Load(Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard", "Views", "GameDetails.xaml"));

        private static readonly XDocument Control =
            XDocument.Load(Path.Combine(RepoRoot, "extension", "XboxSeries.Extension", "QueueActionControl.xaml"));

        [Fact]
        public void The_hub_places_the_extensions_Queue_right_after_Manage()
        {
            var manage = Named("PART_ButtonOptions");
            var queue = Named("XboxSeries_QueueAction");

            Assert.Equal("Actions", (string)queue.Parent.Attribute(X + "Name"));
            Assert.Same(queue, manage.ElementsAfterSelf().First());
            Assert.Equal("ContentControl", queue.Name.LocalName);
        }

        [Fact]
        public void With_the_theme_alone_the_place_is_empty_and_takes_no_focus()
        {
            var queue = Named("XboxSeries_QueueAction");

            Assert.Empty(queue.Elements());
            Assert.Null(queue.Attribute("Content"));
            Assert.Equal("False", (string)queue.Attribute("Focusable"));
            Assert.Equal("False", (string)queue.Attribute("IsTabStop"));
        }

        [Fact]
        public void The_icon_action_style_fits_any_button_so_the_extensions_wears_it()
        {
            var style = Hub.Descendants().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute(X + "Key") == "XboxHubIconAction");

            Assert.Equal("ButtonBase", (string)style.Attribute("TargetType"));
        }

        [Fact]
        public void The_extensions_Queue_is_an_icon_action_with_the_download_glyph_named_Queue()
        {
            var button = Control.Descendants().Single(e => e.Name.LocalName == "Button");

            Assert.Equal("{DynamicResource XboxHubIconAction}", (string)button.Attribute("Style"));
            Assert.Equal("Queue", (string)button.Attribute("Tag"));
            Assert.Equal("", (string)button.Attribute("Content"));
        }

        [Fact]
        public void The_extensions_Queue_starts_collapsed()
        {
            Assert.Equal("Collapsed", (string)Control.Root.Attribute("Visibility"));
            Assert.Equal("False", (string)Control.Root.Attribute("Focusable"));
        }

        private static XElement Named(string name) =>
            Hub.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);
    }
}
