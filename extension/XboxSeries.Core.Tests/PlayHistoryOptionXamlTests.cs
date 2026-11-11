using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The views' side of Personalisation's Show Play history (#210): a checkbox setting built as Background delay, on the
    // row under Reduce motion; the Recent row's Play history tile following the core; and the log line the end-to-end
    // test play-history-off reads. The XAML isn't run here; these checks guard what the page shows and what A does.
    public class PlayHistoryOptionXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        private static XElement Named(XContainer within, string name) =>
            within.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        [Fact]
        public void Show_Play_history_is_built_as_Background_delay_its_title_at_the_left_and_a_checkbox_at_the_right()
        {
            var pane = Load("PersonalisationPane.xaml");
            var box = Named(pane, "ShowPlayHistoryBox");
            var check = Named(pane, "ShowPlayHistoryCheck");
            var theirs = Named(pane, "BackgroundDelayBox");

            Assert.Null(box.Attribute("Tag"));
            Assert.Equal((string)theirs.Attribute("Padding"), (string)box.Attribute("Padding"));
            Assert.Equal("4", (string)box.Attribute("Grid.Row"));
            var title = box.Descendants().Single(e => e.Name.LocalName == "TextBlock" && e != check);
            Assert.Equal("Show Play history", (string)title.Attribute("Text"));
            Assert.Equal("Right", (string)check.Attribute("HorizontalAlignment"));

            // An empty square off, the ticked one on, following the core's state.
            var style = check.Descendants().Single(e => e.Name.LocalName == "Style");
            Assert.Equal("{StaticResource Icon}", (string)style.Attribute("BasedOn"));
            Assert.Equal("", (string)style.Elements().Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "Text").Attribute("Value"));
            var on = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal("{Binding ShowPlayHistory}", (string)on.Attribute("Binding"));
            Assert.Equal("True", (string)on.Attribute("Value"));
            Assert.Equal("", (string)on.Elements().Single().Attribute("Value"));
        }

        [Fact]
        public void Its_info_shows_beside_it_while_it_has_focus_and_Reduce_motions_never_pushes_it_down()
        {
            var pane = Load("PersonalisationPane.xaml");
            var info = Named(pane, "ShowPlayHistoryInfo");

            Assert.Equal("Wrap", (string)info.Attribute("TextWrapping"));
            Assert.Contains("Play history stays in My games & apps.", (string)info.Attribute("Text"));
            Assert.Equal("{Binding IsKeyboardFocused, ElementName=ShowPlayHistoryBox, Converter={StaticResource Visibility}}",
                (string)info.Attribute("Visibility"));
            Assert.Equal("4", (string)info.Attribute("Grid.Row"));
            Assert.Equal("1", (string)info.Attribute("Grid.Column"));
            Assert.Equal("2", (string)Named(pane, "ReduceMotionInfo").Attribute("Grid.RowSpan"));
        }

        [Fact]
        public void The_pad_reaches_it_under_Reduce_motion_and_A_turns_it_over_in_the_core()
        {
            var pane = Code("PersonalisationPane.xaml.cs");

            Assert.Matches(new Regex(@"new\[\] \{ ReduceMotionBox \},\s*new\[\] \{ ShowPlayHistoryBox \},\s*\};"), pane);
            Assert.Matches(new Regex(@"button == ShowPlayHistoryBox\)\s*\{\s*core\.SetShowPlayHistory\(!State\.ShowPlayHistory\);"), pane);
        }

        [Fact]
        public void The_Play_history_tile_shows_only_while_Home_has_it_and_is_left_out_of_the_tiles_that_grow_when_gone()
        {
            var tile = Named(Load("RecentRowControl.xaml"), "PlayHistoryTile");
            var code = Code("RecentRowControl.xaml.cs");

            Assert.Equal("{Binding HasPlayHistoryTile, Converter={StaticResource Visibility}}", (string)tile.Attribute("Visibility"));
            Assert.Matches(new Regex(@"if \(core\.Home\.HasPlayHistoryTile\)\s*\{\s*tiles\.Add\(PlayHistoryTile\);"), code);
            Assert.Matches(new Regex(@"e\.PropertyName == nameof\(HomeState\.HasPlayHistoryTile\)\)\s*\{\s*allTiles = null;\s*growth\.Start\(\);"), code);
        }

        [Fact]
        public void The_log_says_how_Show_Play_history_is_and_when_it_is_turned_over_for_the_end_to_end_test()
        {
            var log = Code("SettingsLog.cs");

            Assert.Contains("LogPlayHistory(settings.StartUp, \"is\");", log);
            Assert.Contains("LogPlayHistory(settings.StartUp, \"turned\");", log);
            Assert.Contains("[Settings] Play history {verb} {(state.ShowPlayHistory ? \"on\" : \"off\")}", log);
        }
    }
}
