using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Settings' Guide checkbox in the views: the box on Theme settings' Guide page, its pad and log line, and the Toasts
    /// following <see cref="ToastState.HasPrompt"/> rather than their kind to show "Press ⓧ to view".
    /// </summary>
    public class GuideOnXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static XDocument Settings => XDocument.Load(Path.Combine(ExtensionDir(), "SettingsControl.xaml"));

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir(), file));

        [Fact]
        public void The_Guide_page_has_the_checkbox_first_ticked_while_the_Guide_is_on_and_the_dropdown_under_it()
        {
            var settings = Settings;
            XElement Named(string name) => settings.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

            var pane = Named("GuidePane");
            var box = Named("GuideOnBox");
            Assert.Same(pane, box.Parent);
            Assert.True(box.IsBefore(Named("NowPlayingSourceBox")));
            Assert.Equal("{StaticResource CheckBox}", (string)box.Attribute("Style"));
            Assert.Equal("OnGuideOnBoxClick", (string)box.Attribute("Click"));

            var ticked = Named("GuideOnCheck").Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal("{Binding IsGuideOn}", (string)ticked.Attribute("Binding"));
            Assert.Equal("True", (string)ticked.Attribute("Value"));
        }

        [Fact]
        public void Opening_Theme_settings_Guide_page_moves_onto_the_checkbox_which_A_and_a_click_turn_over()
        {
            var code = Code("SettingsControl.xaml.cs");

            Assert.Matches(new Regex(@"case ThemeSettingsPage\.Guide:\s*GuideOnBox\.Focus\(\);"), code);
            Assert.Matches(new Regex(@"pad == ControllerInput\.A\)\s*\{\s*core\.SetGuideOn\(!core\.Settings\.Guide\.IsGuideOn\);"), code);
            Assert.Matches(new Regex(@"OnGuideOnBoxClick\(object sender, RoutedEventArgs e\) => core\.SetGuideOn\(!core\.Settings\.Guide\.IsGuideOn\);"), code);
        }

        [Fact]
        public void The_Guide_setting_is_logged_as_the_extension_starts_and_as_it_is_turned_over()
        {
            var code = Code("SettingsLog.cs");

            Assert.Contains("LogGuide(settings.Guide, \"is\");", code);
            Assert.Contains("LogGuide(settings.Guide, \"turned\");", code);
            Assert.Contains("[Settings] Guide {verb}", code);
        }

        [Fact]
        public void The_banner_cycles_to_its_prompt_only_for_a_Toast_with_one()
        {
            var code = Code("ToastWindow.xaml.cs");

            Assert.Matches(new Regex(@"private void CycleText\(\)\s*\{\s*if \(!toast\.HasPrompt\)\s*\{\s*textCycle\.Stop\(this\);"), code);
            Assert.DoesNotContain("toast.Kind == ToastKind.Greeting", code);
        }

        [Fact]
        public void The_achievement_pill_keeps_its_last_page_and_no_prompt_for_a_Toast_without_one()
        {
            var code = Code("AchievementToast.xaml.cs");

            Assert.Contains("var prompts = (DataContext as ToastState)?.HasPrompt ?? true;", code);
            Assert.Matches(new Regex(@"else if \(prompts\)\s*\{\s*opacity\.Add\(Jump\(times\.Cut, 0\)\);"), code);
        }

        private static string ExtensionDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XboxSeries.slnx")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repo root not found"), "extension", "XboxSeries.Extension");
        }
    }
}
