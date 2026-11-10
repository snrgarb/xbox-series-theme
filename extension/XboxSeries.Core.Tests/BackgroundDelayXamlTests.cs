using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of Personalisation's Background delay (#147): a checkbox setting as Playnite settings' Update after
    // downloads, on a row of its own under Personalisation's boxes, its info beside it while it has focus, reached and
    // turned over with the pad. The XAML isn't run here; these checks guard what the page shows and what A does.
    public class BackgroundDelayXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        private static XElement Named(XDocument doc, string name) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        [Fact]
        public void Personalisations_boxes_are_Boot_video_Profiles_Whos_playing_today_then_Background_delay_on_a_row_of_its_own()
        {
            var page = Named(Load("PersonalisationPane.xaml"), "PersonalisationPage");
            var boxes = page.Elements().Where(e => e.Name.LocalName == "Button").ToList();

            Assert.Equal(new[] { "BootVideoBox", "ProfilesBox", "AskBox", "BackgroundDelayBox" }, boxes.Select(b => (string)b.Attribute(X + "Name")));
            Assert.Equal(new[] { "0,0", "0,1", "1,0", "2,0" },
                boxes.Select(b => $"{(string)b.Attribute("Grid.Row")},{(string)b.Attribute("Grid.Column")}"));
            Assert.All(boxes, box =>
            {
                Assert.Equal("{StaticResource Box}", (string)box.Attribute("Style"));
                Assert.Equal("OnBoxClick", (string)box.Attribute("Click"));
            });
        }

        [Fact]
        public void Background_delay_shows_its_title_at_the_left_and_a_checkbox_at_the_right_as_Update_after_downloads_does()
        {
            var pane = Load("PersonalisationPane.xaml");
            var box = Named(pane, "BackgroundDelayBox");
            var check = Named(pane, "BackgroundDelayCheck");
            var theirs = Named(Load("PlaynitePane.xaml"), "UpdateAfterDownloadsBox");

            // No line icon: the title starts at the box's left.
            Assert.Null(box.Attribute("Tag"));
            Assert.Equal((string)theirs.Attribute("Padding"), (string)box.Attribute("Padding"));
            var title = box.Descendants().Single(e => e.Name.LocalName == "TextBlock" && e != check);
            Assert.Equal("Background delay", (string)title.Attribute("Text"));
            Assert.Equal("Right", (string)check.Attribute("HorizontalAlignment"));

            // An empty square off, the ticked one on, following the core's state.
            var style = check.Descendants().Single(e => e.Name.LocalName == "Style");
            Assert.Equal("{StaticResource Icon}", (string)style.Attribute("BasedOn"));
            Assert.Equal("", (string)style.Elements().Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "Text").Attribute("Value"));
            var on = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal("{Binding BackgroundDelay}", (string)on.Attribute("Binding"));
            Assert.Equal("True", (string)on.Attribute("Value"));
            Assert.Equal("", (string)on.Elements().Single().Attribute("Value"));
        }

        [Fact]
        public void Its_info_shows_beside_it_wrapping_while_it_has_focus()
        {
            var pane = Load("PersonalisationPane.xaml");
            var info = Named(pane, "BackgroundDelayInfo");
            var box = Named(pane, "BackgroundDelayBox");

            Assert.Equal("Wrap", (string)info.Attribute("TextWrapping"));
            Assert.StartsWith("Waits a moment on a game before Home's background shows its art.", (string)info.Attribute("Text"));
            Assert.Equal("{Binding IsKeyboardFocused, ElementName=BackgroundDelayBox, Converter={StaticResource Visibility}}",
                (string)info.Attribute("Visibility"));
            Assert.Equal((string)box.Attribute("Grid.Row"), (string)info.Attribute("Grid.Row"));
            Assert.Equal("1", (string)info.Attribute("Grid.Column"));
        }

        [Fact]
        public void The_pad_reaches_it_under_Whos_playing_today_and_A_turns_it_over_in_the_core()
        {
            var pane = Code("PersonalisationPane.xaml.cs");

            Assert.Matches(new Regex(@"new\[\] \{ BootVideoBox, ProfilesBox \},\s*new\[\] \{ AskBox \},\s*new\[\] \{ BackgroundDelayBox \},"), pane);
            Assert.Matches(new Regex(@"else if \(page == Page\.Personalisation\)\s*\{\s*MoveOnBoxes\(e\);"), pane);
            Assert.Matches(new Regex(@"button == BackgroundDelayBox\)\s*\{\s*core\.SetBackgroundDelay\(!State\.BackgroundDelay\);"), pane);
        }

        [Fact]
        public void The_log_says_when_Settings_opens_and_closes_and_Background_delay_is_turned_over_for_the_end_to_end_test()
        {
            var log = Code("SettingsLog.cs");

            Assert.Contains("[Settings] opened on {settings.Category}, page {settings.ThemePage}", log);
            Assert.Contains("[Settings] closed", log);
            Assert.Contains("[Settings] Background delay {verb} {(state.BackgroundDelay ? \"on\" : \"off\")}", log);
            Assert.Contains("[Settings] focus on {focused.Name}", log);
            Assert.Contains("SettingsLog.Follow(core);", Code("XboxSeriesPlugin.cs"));
            Assert.Contains("SettingsLog.FollowFocus(this);", Code("SettingsControl.xaml.cs"));
        }
    }
}
