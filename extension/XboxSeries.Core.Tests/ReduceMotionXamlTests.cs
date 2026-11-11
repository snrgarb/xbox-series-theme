using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The views' and the theme's side of Personalisation's Reduce motion (#156): a checkbox setting built as Background
    // delay, on the row under it; the extension's animations asking Motion whether to move; and the Dashboard's own
    // storyboards gated on the setting, read through Playnite's PluginSettings markup, off without the extension. The
    // XAML isn't run here; these checks guard what the page shows, what A does, and what the gates are.
    public class ReduceMotionXamlTests
    {
        /// <summary>The theme's reading of the setting, wherever it gates a storyboard: off when there is no extension to ask.</summary>
        private const string Reduced = "{PluginSettings Plugin=XboxSeries, Path=StartUp.ReduceMotion, FallbackValue=False}";
        private const string Focused = "{Binding IsFocused, RelativeSource={RelativeSource Self}}";

        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        private static XElement Named(XContainer within, string name) =>
            within.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        /// <summary>A MultiDataTrigger's conditions, as "binding = value", in order.</summary>
        private static string[] Conditions(XElement trigger) => trigger.Descendants()
            .Where(e => e.Name.LocalName == "Condition")
            .Select(c => $"{(string)c.Attribute("Binding")} = {(string)c.Attribute("Value")}")
            .ToArray();

        /// <summary>The MultiDataTrigger an element of a storyboard is begun or removed by.</summary>
        private static XElement TriggerOf(XElement element) => element.Ancestors().First(e => e.Name.LocalName == "MultiDataTrigger");

        [Fact]
        public void Reduce_motion_is_built_as_Background_delay_its_title_at_the_left_and_a_checkbox_at_the_right()
        {
            var pane = Load("PersonalisationPane.xaml");
            var box = Named(pane, "ReduceMotionBox");
            var check = Named(pane, "ReduceMotionCheck");
            var theirs = Named(pane, "BackgroundDelayBox");

            // No line icon: the title starts at the box's left.
            Assert.Null(box.Attribute("Tag"));
            Assert.Equal((string)theirs.Attribute("Padding"), (string)box.Attribute("Padding"));
            var title = box.Descendants().Single(e => e.Name.LocalName == "TextBlock" && e != check);
            Assert.Equal("Reduce motion", (string)title.Attribute("Text"));
            Assert.Equal("Right", (string)check.Attribute("HorizontalAlignment"));

            // An empty square off, the ticked one on, following the core's state.
            var style = check.Descendants().Single(e => e.Name.LocalName == "Style");
            Assert.Equal("{StaticResource Icon}", (string)style.Attribute("BasedOn"));
            Assert.Equal("", (string)style.Elements().Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "Text").Attribute("Value"));
            var on = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal("{Binding ReduceMotion}", (string)on.Attribute("Binding"));
            Assert.Equal("True", (string)on.Attribute("Value"));
            Assert.Equal("", (string)on.Elements().Single().Attribute("Value"));
        }

        [Fact]
        public void Its_info_shows_beside_it_wrapping_while_it_has_focus_and_Background_delays_never_pushes_it_down()
        {
            var pane = Load("PersonalisationPane.xaml");
            var info = Named(pane, "ReduceMotionInfo");
            var box = Named(pane, "ReduceMotionBox");

            Assert.Equal("Wrap", (string)info.Attribute("TextWrapping"));
            Assert.Contains("Turn it on if the Dashboard feels slow on this PC.", (string)info.Attribute("Text"));
            Assert.Equal("{Binding IsKeyboardFocused, ElementName=ReduceMotionBox, Converter={StaticResource Visibility}}",
                (string)info.Attribute("Visibility"));
            Assert.Equal("3", (string)box.Attribute("Grid.Row"));
            Assert.Equal("3", (string)info.Attribute("Grid.Row"));
            Assert.Equal("1", (string)info.Attribute("Grid.Column"));

            // A row for each box's row, and the info above spanning this row's empty place, so its height isn't its
            // row's.
            Assert.Equal(4, Named(pane, "PersonalisationPage").Descendants().Count(e => e.Name.LocalName == "RowDefinition"));
            Assert.Equal("2", (string)Named(pane, "BackgroundDelayInfo").Attribute("Grid.RowSpan"));
        }

        [Fact]
        public void The_pad_reaches_it_under_Background_delay_and_A_turns_it_over_in_the_core()
        {
            var pane = Code("PersonalisationPane.xaml.cs");

            Assert.Matches(new Regex(@"new\[\] \{ AskBox \},\s*new\[\] \{ BackgroundDelayBox \},\s*new\[\] \{ ReduceMotionBox \},\s*\};"), pane);
            Assert.Matches(new Regex(@"button == ReduceMotionBox\)\s*\{\s*core\.SetReduceMotion\(!State\.ReduceMotion\);"), pane);
        }

        [Fact]
        public void The_log_says_how_Reduce_motion_is_and_when_it_is_turned_over_and_whether_the_swirl_moves_for_the_end_to_end_test()
        {
            var log = Code("SettingsLog.cs");
            var swirl = Code("SwirlBackground.xaml.cs");

            Assert.Contains("LogReduceMotion(settings.StartUp, \"is\");", log);
            Assert.Contains("LogReduceMotion(settings.StartUp, \"turned\");", log);
            Assert.Contains("[Settings] Reduce motion {verb} {(state.ReduceMotion ? \"on\" : \"off\")}", log);
            Assert.Contains("logger.Info(moves ? \"[Motion] swirl moving\" : \"[Motion] swirl still\");", swirl);
            Assert.Contains("var moves = inFront && !Motion.Reduced;", swirl);
            Assert.Contains("Motion.Changed += OnMotionChanged;", swirl);
            Assert.Contains("Motion.Changed -= OnMotionChanged;", swirl);
        }

        [Fact]
        public void The_plugin_follows_the_setting_before_any_view_shows_and_hands_the_theme_its_settings()
        {
            var plugin = Code("XboxSeriesPlugin.cs");

            // In the constructor, straight after the core is made: Home's elements are asked for before
            // OnApplicationStarted.
            Assert.Matches(new Regex(@"core = new ExtensionCore\(.*?Motion\.Follow\(core\.Settings\.StartUp\);.*?AddCustomElementSupport\(", RegexOptions.Singleline), plugin);

            // Playnite's PluginSettings binds to the plugin, at SettingsRoot then the theme's Path.
            Assert.Contains("AddSettingsSupport(new AddSettingsSupportArgs { SourceName = \"XboxSeries\", SettingsRoot = nameof(DashboardSettings) });", plugin);
            Assert.Contains("public SettingsState DashboardSettings => core.Settings;", plugin);
            Assert.Contains("public StartUpSettingsState StartUp { get; }", File.ReadAllText(Path.Combine(RepoRoot, "extension", "XboxSeries.Core", "SettingsState.cs")));
            Assert.Contains("public bool ReduceMotion", File.ReadAllText(Path.Combine(RepoRoot, "extension", "XboxSeries.Core", "StartupState.cs")));
        }

        [Theory]
        [InlineData("TileGrowth.cs", "var step = Motion.Reduced ? 1 : ")]
        [InlineData("HomeRows.cs", "new DoubleAnimation(to, Motion.Of(SlideDuration))")]
        [InlineData("PaneFlyIn.cs", "return along && !Motion.Reduced ? Math.Sign(indexOf(now) - indexOf(from)) : 0;")]
        [InlineData("SlidingRow.cs", "if (Math.Abs(to - from) < 0.5 || Motion.Reduced)")]
        [InlineData("OverlayWindow.xaml.cs", "duration = Motion.Of(duration);")]
        [InlineData("OverlayWindow.xaml.cs", "BeginTime = Motion.Of(ContentLag),")]
        [InlineData("WideDialogPanel.xaml.cs", "new DoubleAnimation(0, Motion.Of(OverlayWindow.OpenDuration))")]
        [InlineData("TileMenuControl.xaml.cs", "duration = Motion.Of(duration);")]
        [InlineData("TileMenuControl.xaml.cs", "new DoubleAnimation(-FlyoutInDistance, 0, Motion.Of(FlyoutInDuration))")]
        [InlineData("StartupWindow.xaml.cs", "if (animate && !Motion.Reduced)")]
        [InlineData("ToastWindow.xaml.cs", "Scale(new DoubleAnimation(GrowFrom, 1, Motion.Of(GrowDuration))")]
        [InlineData("ToastWindow.xaml.cs", "if (Slides && !Motion.Reduced)")]
        [InlineData("HeroControl.cs", "var crossfade = Motion.Reduced ? ReducedCrossfadeTime : CrossfadeTime;")]
        public void What_slides_flies_grows_or_scales_asks_Motion_first(string file, string asks)
        {
            Assert.Contains(asks, Code(file));
        }

        [Fact]
        public void Homes_reveal_leaves_the_row_in_place_and_the_top_bar_alone_and_a_Toast_goes_without_its_pop()
        {
            // The row put in place without animating, then nothing more: the top bar's drop and its disabling never
            // begin.
            Assert.Matches(new Regex(@"Settle\(row\);\s*if \(Motion\.Reduced\)\s*\{\s*return;\s*\}"), Code("HomeRows.cs"));

            // The fade out, and the window hiding as it ends, stay outside the gate.
            Assert.Matches(
                new Regex(@"if \(!Motion\.Reduced\)\s*\{\s*var scale = .*?Scale\(scale\);\s*\}\s*Card\.BeginAnimation\(OpacityProperty, fade\);", RegexOptions.Singleline),
                Code("ToastWindow.xaml.cs"));
        }

        [Fact]
        public void The_hero_crossfades_in_150_ms_reduced_and_400_ms_otherwise_and_still_says_when()
        {
            var hero = Code("HeroControl.cs");

            Assert.Contains("Duration CrossfadeTime = TimeSpan.FromMilliseconds(400);", hero);
            Assert.Contains("Duration ReducedCrossfadeTime = TimeSpan.FromMilliseconds(150);", hero);
            Assert.Contains("HeroTiming.LogNextFrame($\"{name} fading in\");", hero);
            Assert.Contains("HeroTiming.Log($\"{name} shown\");", hero);
        }

        [Fact]
        public void The_theme_reads_the_setting_only_as_off_without_the_extension()
        {
            var readings = Directory.GetFiles(DashboardDir, "*.xaml", SearchOption.AllDirectories)
                .SelectMany(file => XDocument.Load(file).Descendants().Attributes())
                .Select(a => a.Value)
                .Where(value => value.Contains("Plugin=XboxSeries,"))
                .ToList();

            // The top bar's icons, the name swap, and the title band fading and at once.
            Assert.Equal(4, readings.Count);
            Assert.All(readings, reading => Assert.Equal(Reduced, reading));
        }

        [Fact]
        public void A_top_bar_buttons_icon_grows_on_focus_only_while_motion_isnt_reduced()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var grows = main.Descendants()
                .Where(e => e.Name.LocalName == "DoubleAnimation" && (string)e.Attribute("Storyboard.TargetName") == "IconScale")
                .ToList();

            Assert.Equal(new[] { "1.2", "1.2", "1", "1" }, grows.Select(g => (string)g.Attribute("To")));
            var trigger = grows.Select(TriggerOf).Distinct().Single();
            Assert.Equal(new[] { $"{Focused} = True", $"{Reduced} = False" }, Conditions(trigger));
            Assert.All(grows.Take(2), g => Assert.Contains(g.Ancestors(), a => a.Name.LocalName == "MultiDataTrigger.EnterActions"));
            Assert.All(grows.Skip(2), g => Assert.Contains(g.Ancestors(), a => a.Name.LocalName == "MultiDataTrigger.ExitActions"));
        }

        [Fact]
        public void The_gamertag_swaps_with_the_real_name_only_while_the_profile_shows_and_motion_isnt_reduced()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var swap = Named(main, "NameSwap");
            var trigger = TriggerOf(swap);

            Assert.Equal(
                new[]
                {
                    "{Binding Tag.Profile.HasRealName, RelativeSource={RelativeSource Self}, FallbackValue=False} = True",
                    "{Binding IsVisible, RelativeSource={RelativeSource Self}} = True",
                    $"{Reduced} = False",
                },
                Conditions(trigger));
            Assert.Equal("MultiDataTrigger.EnterActions", swap.Parent.Name.LocalName);

            // Gone with any condition, the gamertag is back at its own opacity and the real name unseen.
            var remove = trigger.Descendants().Single(e => e.Name.LocalName == "RemoveStoryboard");
            Assert.Equal("MultiDataTrigger.ExitActions", remove.Parent.Name.LocalName);
            Assert.Equal("NameSwap", (string)remove.Attribute("BeginStoryboardName"));

            // A slow fade: 30 frames a second is enough.
            var storyboard = swap.Elements().Single(e => e.Name.LocalName == "Storyboard");
            Assert.Equal("Forever", (string)storyboard.Attribute("RepeatBehavior"));
            Assert.Equal("30", (string)storyboard.Attribute("Timeline.DesiredFrameRate"));
        }

        [Fact]
        public void A_library_tiles_title_band_fades_on_focus_or_shows_at_once_reduced()
        {
            var style = XDocument.Load(Path.Combine(DashboardDir, "DerivedStyles", "ListGameItemStyle.xaml"));
            XElement Storyboard(string key) => style.Descendants().Single(e => e.Name.LocalName == "Storyboard" && (string)e.Attribute(X + "Key") == key);
            XElement Begins(string key) => style.Descendants()
                .Single(e => e.Name.LocalName == "BeginStoryboard" && (string)e.Attribute("Storyboard") == "{StaticResource " + key + "}");

            // The twins: to the same opacities, no time long, and no wait before.
            foreach (var (twin, of) in new[] { ("FocusedOnAtOnce", "FocusedOn"), ("FocusedOffAtOnce", "FocusedOff") })
            {
                var atOnce = Storyboard(twin).Elements().Single();
                var fades = Storyboard(of).Elements().Single();
                Assert.Equal("TitleBand", (string)atOnce.Attribute("Storyboard.TargetName"));
                Assert.Equal("Opacity", (string)atOnce.Attribute("Storyboard.TargetProperty"));
                Assert.Equal((string)fades.Attribute("To"), (string)atOnce.Attribute("To"));
                Assert.Equal("0:0:0", (string)atOnce.Attribute("Duration"));
                Assert.Null(atOnce.Attribute("BeginTime"));
            }

            var fading = TriggerOf(Begins("FocusedOn"));
            Assert.Equal(fading, TriggerOf(Begins("FocusedOff")));
            Assert.Equal(new[] { $"{Focused} = True", $"{Reduced} = False" }, Conditions(fading));
            Assert.Equal("MultiDataTrigger.EnterActions", Begins("FocusedOn").Parent.Name.LocalName);
            Assert.Equal("MultiDataTrigger.ExitActions", Begins("FocusedOff").Parent.Name.LocalName);

            var atOnceTrigger = TriggerOf(Begins("FocusedOnAtOnce"));
            Assert.Equal(atOnceTrigger, TriggerOf(Begins("FocusedOffAtOnce")));
            Assert.Equal(new[] { $"{Focused} = True", $"{Reduced} = True" }, Conditions(atOnceTrigger));
            Assert.Equal("MultiDataTrigger.EnterActions", Begins("FocusedOnAtOnce").Parent.Name.LocalName);
            Assert.Equal("MultiDataTrigger.ExitActions", Begins("FocusedOffAtOnce").Parent.Name.LocalName);
        }
    }
}
