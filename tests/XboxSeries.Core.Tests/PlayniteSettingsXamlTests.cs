using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of Settings > Playnite settings as a page of boxes (#82): the rail item now a category, the pane's
    // boxes in Personalisation's design with Update after downloads' checkbox and its info beside it as the console's
    // My profile shows a checkbox setting, and the Library updated Toast's glyph and second line. The XAML isn't run
    // here; these checks guard what the page and the Toast show.
    public class PlayniteSettingsXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XElement Named(XDocument doc, string name) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        // A box's title texts: every TextBlock in it but the checkbox's glyph.
        private static XElement[] Titles(XElement box) =>
            box.Descendants().Where(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute(X + "Name") != "UpdateAfterDownloadsCheck").ToArray();

        // What a TextBlock reads: its own Text, else its style's.
        private static string TextOf(XElement text) =>
            (string)text.Attribute("Text") ?? (string)text.Descendants()
                .First(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "Text").Attribute("Value");

        private static string TitleOf(XElement box) => TextOf(Titles(box).Single());

        [Fact]
        public void Playnite_settings_on_the_rail_is_a_category_straight_after_Theme_settings_and_More_Playnite_options_stays_an_action()
        {
            var settings = Load("SettingsControl.xaml");
            var item = Named(settings, "PlayniteSettingsItem");
            var more = Named(settings, "MorePlayniteOptionsItem");

            Assert.Equal("OnCategoryFocused", (string)item.Attribute("GotKeyboardFocus"));
            Assert.Null(item.Attribute("Click"));
            Assert.Equal("OnPlayniteEntryClick", (string)more.Attribute("Click"));
            // No divider between the theme's entry and Playnite's any more (#86).
            Assert.Same(Named(settings, "ThemeSettingsItem"), item.ElementsBeforeSelf().Last());
            Assert.Same(item, more.ElementsBeforeSelf().Last());
        }

        [Fact]
        public void The_category_shows_the_Playnite_pane_in_the_options_row()
        {
            var pane = Named(Load("SettingsControl.xaml"), "PlaynitePane");

            Assert.Equal("PlaynitePane", pane.Name.LocalName);
            Assert.Equal("1", (string)pane.Attribute("Grid.Row"));
            Assert.Equal("Collapsed", (string)pane.Attribute("Visibility"));
        }

        [Fact]
        public void Its_boxes_are_Download_metadata_Playnite_settings_then_Update_after_downloads_each_a_title_alone()
        {
            var pane = Load("PlaynitePane.xaml");
            var boxes = pane.Descendants().Where(e => e.Name.LocalName == "Button").ToList();

            Assert.Equal(new[] { "DownloadMetadataBox", "PlayniteSettingsBox", "UpdateAfterDownloadsBox" },
                boxes.Select(b => (string)b.Attribute(X + "Name")));
            Assert.Equal(new[] { "Download metadata", "Playnite settings", "Update after downloads" }, boxes.Select(TitleOf));
            Assert.All(boxes, box =>
            {
                // A title and nothing under it: no box says what it's set to in words.
                Assert.Single(Titles(box));
                Assert.Equal("OnBoxClick", (string)box.Attribute("Click"));
            });
            Assert.Equal("", (string)Named(pane, "DownloadMetadataBox").Attribute("Tag"));
            Assert.Equal("", (string)Named(pane, "PlayniteSettingsBox").Attribute("Tag"));
        }

        [Fact]
        public void The_boxes_are_Personalisations_and_the_pane_handles_the_pad_on_KeyDown_where_Playnite_raises_A()
        {
            var pane = Load("PlaynitePane.xaml");

            Assert.Equal("OnPaneKeyDown", (string)pane.Root.Attribute("KeyDown"));
            Assert.Null(pane.Root.Attribute("PreviewKeyDown"));
            Assert.Contains(pane.Descendants(), e => e.Name.LocalName == "SettingsBoxes");
            Assert.All(pane.Descendants().Where(e => e.Name.LocalName == "Button"),
                box => Assert.Equal("{StaticResource Box}", (string)box.Attribute("Style")));
        }

        [Fact]
        public void While_metadata_downloads_Download_metadata_reads_Cancel()
        {
            var title = Named(Load("PlaynitePane.xaml"), "DownloadMetadataTitle");
            var trigger = title.Descendants().Single(e => e.Name.LocalName == "DataTrigger");

            Assert.Equal("Download metadata", TextOf(title));
            Assert.Equal("{Binding IsDownloadingMetadata}", (string)trigger.Attribute("Binding"));
            Assert.Equal("True", (string)trigger.Attribute("Value"));
            var cancel = trigger.Elements().Single();
            Assert.Equal(("Text", "Cancel"), ((string)cancel.Attribute("Property"), (string)cancel.Attribute("Value")));
        }

        [Fact]
        public void Update_after_downloads_shows_its_title_at_the_left_and_a_checkbox_at_the_right_as_the_console_does()
        {
            var pane = Load("PlaynitePane.xaml");
            var box = Named(pane, "UpdateAfterDownloadsBox");
            var check = Named(pane, "UpdateAfterDownloadsCheck");

            // No line icon: the title starts at the box's left, as the console's checkbox setting.
            Assert.Null(box.Attribute("Tag"));
            Assert.True(box.Descendants().Contains(check));
            Assert.Equal("Right", (string)check.Attribute("HorizontalAlignment"));
            // An icon's size, as the boxes' line icons.
            var style = check.Descendants().Single(e => e.Name.LocalName == "Style");
            Assert.Equal("{StaticResource Icon}", (string)style.Attribute("BasedOn"));
            Assert.Equal("34", (string)style.Elements().Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "FontSize").Attribute("Value"));

            // An empty square off, the ticked one on.
            Assert.Equal("", (string)style.Elements().Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "Text").Attribute("Value"));
            var on = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal("{Binding UpdateAfterDownloads}", (string)on.Attribute("Binding"));
            Assert.Equal("True", (string)on.Attribute("Value"));
            Assert.Equal("", (string)on.Elements().Single().Attribute("Value"));
        }

        [Fact]
        public void Its_info_shows_beside_it_wrapping_while_it_has_focus()
        {
            var pane = Load("PlaynitePane.xaml");
            var info = Named(pane, "UpdateAfterDownloadsInfo");
            var box = Named(pane, "UpdateAfterDownloadsBox");

            Assert.Equal("Wrap", (string)info.Attribute("TextWrapping"));
            Assert.Contains("after a download finishes", (string)info.Attribute("Text"));
            Assert.Equal("{Binding IsKeyboardFocused, ElementName=UpdateAfterDownloadsBox, Converter={StaticResource Visibility}}",
                (string)info.Attribute("Visibility"));

            // In the same row as the box, in the column after it.
            Assert.Equal((string)box.Attribute("Grid.Row"), (string)info.Attribute("Grid.Row"));
            Assert.Equal("0", (string)box.Attribute("Grid.Column") ?? "0");
            Assert.Equal("1", (string)info.Attribute("Grid.Column"));
        }

        [Fact]
        public void A_Library_updated_Toast_shows_the_Update_library_glyph_on_its_tile_in_place_from_the_start()
        {
            var images = Load("ToastImages.xaml");
            var text = images.Descendants().Single(e => (string)e.Attribute(X + "Key") == "ToastImage.LibraryUpdated").Elements().Single();
            var installed = images.Descendants().Single(e => (string)e.Attribute(X + "Key") == "ToastImage.Installed").Elements().Single();

            // The theme's main menu Update library entry's glyph.
            Assert.Equal("", (string)text.Attribute("Text"));
            Assert.Equal((string)installed.Attribute("FontSize"), (string)text.Attribute("FontSize"));
            Assert.Equal("False", images.Descendants().Single(e => (string)e.Attribute(X + "Key") == "ToastSlide.LibraryUpdated").Value);
        }

        [Fact]
        public void The_banner_has_a_second_line_for_the_Detail_of_a_Library_updated_Toast_alone_fading_with_its_line()
        {
            var toast = Load("ToastWindow.xaml");
            var detail = Named(toast, "DetailLine");
            var lines = Named(toast, "Lines");

            Assert.Equal("{Binding Detail}", (string)detail.Attribute("Text"));
            Assert.True(lines.Descendants().Contains(detail));
            Assert.True(lines.Descendants().Contains(Named(toast, "Line")));

            // Hidden but for Library updated, and for it while there is no Detail.
            var style = detail.Descendants().Single(e => e.Name.LocalName == "Style");
            Assert.Equal("Collapsed", (string)style.Elements().Single(e => e.Name.LocalName == "Setter").Attribute("Value"));
            var triggers = style.Descendants().Where(e => e.Name.LocalName == "DataTrigger")
                .Select(t => ((string)t.Attribute("Binding"), (string)t.Attribute("Value"), (string)t.Elements().Single().Attribute("Value")))
                .ToList();
            Assert.Equal(new[]
            {
                ("{Binding Kind}", "{x:Static core:ToastKind.LibraryUpdated}", "Visible"),
                ("{Binding Detail}", "{x:Null}", "Collapsed"),
                ("{Binding Detail}", "", "Collapsed"),
            }, triggers);

            // The text cycle fades the line and its detail together.
            Assert.Contains(toast.Descendants(), e => e.Name.LocalName == "DoubleAnimationUsingKeyFrames"
                && (string)e.Attribute("Storyboard.TargetName") == "Lines");
            Assert.DoesNotContain(toast.Descendants(), e => (string)e.Attribute("Storyboard.TargetName") == "Line");
        }
    }
}
