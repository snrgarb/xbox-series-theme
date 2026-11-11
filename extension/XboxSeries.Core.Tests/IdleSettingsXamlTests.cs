using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Theme settings' Idle page (#90): Dim after and Show things after, each a dropdown as the Guide page's Now Playing
    // source is, showing the core's IdleState and handing a choice back to the core, which holds the ordering rule. The
    // XAML isn't run here; these checks guard what the page shows and where the pad goes.
    public class IdleSettingsXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

        private static readonly XDocument Settings = XDocument.Load(Path.Combine(ExtensionDir, "SettingsControl.xaml"));

        private static readonly string Code = File.ReadAllText(Path.Combine(ExtensionDir, "SettingsControl.xaml.cs"));

        private static XElement Named(string name) =>
            Settings.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        [Fact]
        public void The_Idle_pane_is_Dim_after_then_Show_things_after_handling_the_pad_on_KeyDown_where_Playnite_raises_A()
        {
            var pane = Named("IdlePane");
            var titles = pane.Descendants()
                .Where(e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Style") == "{StaticResource Text}"
                    && e.Attribute("Text") != null && e.Ancestors().All(a => a.Name.LocalName != "Button"))
                .Select(e => (string)e.Attribute("Text"));

            Assert.Equal(new[] { "Dim after", "Show things after" }, titles);
            Assert.Equal("OnIdlePaneKeyDown", (string)pane.Attribute("KeyDown"));
            Assert.Null(pane.Attribute("PreviewKeyDown"));
            Assert.True(Named("DimAfterBox").IsBefore(Named("ShowThingsAfterBox")));
        }

        [Theory]
        [InlineData("DimAfter")]
        [InlineData("ShowThingsAfter")]
        public void Each_is_a_dropdown_box_with_its_list_under_it_laid_over_what_is_below(string name)
        {
            var box = Named(name + "Box");
            var list = Named(name + "List");

            Assert.Equal("{StaticResource DropBox}", (string)box.Attribute("Style"));
            Assert.NotNull(Named(name + "Text"));
            Assert.True(box.IsBefore(list));
            Assert.Equal("Canvas", list.Parent.Name.LocalName);
            Assert.Equal("0", (string)list.Parent.Attribute("Height"));
            Assert.Equal("Collapsed", (string)list.Attribute("Visibility"));
            Assert.Contains(list.Descendants(), e => (string)e.Attribute(X + "Name") == name + "Options");

            // Its box and list are raised together while focus is in them, so Dim after's open list covers Show things
            // after rather than sitting under it.
            Assert.Equal("{StaticResource RaisedWhenFocused}", (string)box.Parent.Parent.Attribute("Style"));
            Assert.Same(box.Parent, list.Parent.Parent);
        }

        [Fact]
        public void Opening_the_Idle_page_moves_onto_Dim_after_under_the_header_Idle()
        {
            Assert.Matches(new Regex(@"case ThemeSettingsPage\.Idle:\s*DimAfterBox\.Focus\(\);"), Code);
            Assert.Matches(new Regex(@"case ThemeSettingsPage\.Idle: return IdlePane;"), Code);
            Assert.Matches(new Regex(@"page == ThemeSettingsPage\.Idle \? ""Idle"""), Code);
        }

        [Fact]
        public void The_lists_are_the_cores_steps_and_a_choice_goes_to_the_core()
        {
            Assert.Contains("IdleState.Steps", Code);
            Assert.Contains("core.Idle.ShowThingsAfterSteps", Code);
            Assert.Contains("core.SetDimAfter", Code);
            Assert.Contains("core.SetShowThingsAfter", Code);
            Assert.Contains("IdleState.Describe", Code);
        }

        [Fact]
        public void The_boxes_follow_the_state_as_it_changes()
        {
            Assert.Contains("core.Idle.PropertyChanged += OnIdleChanged;", Code);
            Assert.Contains("core.Idle.PropertyChanged -= OnIdleChanged;", Code);
        }

        [Fact]
        public void The_Guide_and_Idle_pages_share_one_dropdowns_pad_and_open_choose_close()
        {
            Assert.Contains("OnDropdownsKeyDown(e, nowPlayingSource);", Code);
            Assert.Matches(new Regex(@"OnIdlePaneKeyDown\(object sender, KeyEventArgs e\) =>\s*OnDropdownsKeyDown\("), Code);
            Assert.DoesNotContain("OpenNowPlayingSources", Code);
        }
    }
}
