using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The views' side of Personalisation's Xbox store art: a checkbox setting built as Background delay, beside Who's
    // playing today? (a sixth row would run off a 1080p screen), its info under it while focused, and the log lines the
    // end-to-end tests read. The XAML isn't run here; these check what the page shows and what A does.
    public class XboxArtOptionXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        private static XElement Named(XContainer within, string name) =>
            within.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        [Fact]
        public void Xbox_store_art_is_built_as_Background_delay_beside_Whos_playing_today()
        {
            var pane = Load("PersonalisationPane.xaml");
            var box = Named(pane, "XboxArtBox");
            var check = Named(pane, "XboxArtCheck");

            Assert.Null(box.Attribute("Tag"));
            Assert.Equal((string)Named(pane, "BackgroundDelayBox").Attribute("Padding"), (string)box.Attribute("Padding"));
            Assert.Equal(("1", "1"), ((string)box.Attribute("Grid.Row"), (string)box.Attribute("Grid.Column")));
            var title = box.Descendants().Single(e => e.Name.LocalName == "TextBlock" && e != check);
            Assert.Equal("Xbox store art", (string)title.Attribute("Text"));

            var on = check.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal("{Binding XboxArt}", (string)on.Attribute("Binding"));
            Assert.Equal("True", (string)on.Attribute("Value"));
        }

        [Fact]
        public void Its_info_shows_under_it_while_it_has_focus_in_Background_delays_infos_place()
        {
            var pane = Load("PersonalisationPane.xaml");
            var info = Named(pane, "XboxArtInfo");
            var theirs = Named(pane, "BackgroundDelayInfo");

            Assert.Equal("{Binding IsKeyboardFocused, ElementName=XboxArtBox, Converter={StaticResource Visibility}}", (string)info.Attribute("Visibility"));
            Assert.Equal((string)theirs.Attribute("Grid.Row"), (string)info.Attribute("Grid.Row"));
            Assert.Equal("1", (string)info.Attribute("Grid.Column"));
            Assert.Contains("Xbox store", (string)info.Attribute("Text"));
        }

        [Fact]
        public void The_pad_reaches_it_right_of_Whos_playing_today_and_A_turns_it_over_in_the_core()
        {
            var pane = Code("PersonalisationPane.xaml.cs");

            Assert.Contains("new[] { AskBox, XboxArtBox },", pane);
            Assert.Matches(new Regex(@"button == XboxArtBox\)\s*\{\s*core\.SetXboxArt\(!State\.XboxArt\);"), pane);
        }

        [Fact]
        public void The_log_says_how_Xbox_store_art_is_and_when_it_is_turned_over()
        {
            var log = Code("SettingsLog.cs");

            Assert.Contains("LogXboxArt(settings.StartUp, \"is\");", log);
            Assert.Contains("LogXboxArt(settings.StartUp, \"turned\");", log);
            Assert.Contains("[Settings] Xbox art {verb} {(state.XboxArt ? \"on\" : \"off\")}", log);
        }

        [Fact]
        public void The_lookups_begin_only_as_Start_up_ends_and_their_art_reaches_the_core_in_bursts_on_the_UI_thread()
        {
            var plugin = Code("XboxSeriesPlugin.cs");

            Assert.Contains("core.StartupEnded += StartXboxArt;", plugin);
            Assert.Contains("xboxArt: xboxStoreArt);", plugin);
            Assert.Contains("core.OnXboxArt(found)", plugin);
        }
    }
}
