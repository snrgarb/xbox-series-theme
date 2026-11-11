using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Theme settings' Guide page: the Now Playing source's dropdown, drawn as the Guide's Audio dropdowns.</summary>
    public class NowPlayingSourceXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void The_Guide_pane_shows_the_source_chosen_in_a_dropdown_box_with_its_list_under_it()
        {
            var settings = XDocument.Load(Path.Combine(ExtensionDir(), "SettingsControl.xaml"));
            XElement Named(string name) => settings.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

            var pane = Named("GuidePane");
            var box = Named("NowPlayingSourceBox");
            var list = Named("NowPlayingSourceList");
            Assert.Equal("{Binding Guide}", (string)pane.Attribute("DataContext"));
            Assert.Equal("OnGuidePaneKeyDown", (string)pane.Attribute("KeyDown"));
            Assert.Equal("{StaticResource DropBox}", (string)box.Attribute("Style"));
            Assert.Equal("{Binding NowPlayingSource}", (string)box.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text"));
            Assert.True(box.IsBefore(list));
            Assert.Equal("Canvas", list.Parent.Name.LocalName);
            Assert.Equal("Collapsed", (string)list.Attribute("Visibility"));
        }

        [Fact]
        public void Up_on_the_dropdown_goes_to_the_Guide_checkbox_above_it()
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir(), "SettingsControl.xaml.cs"));

            Assert.Matches(
                new Regex(@"e\.OriginalSource == NowPlayingSourceBox && e\.Key == Key\.Up\)\s*\{\s*e\.Handled = true;\s*GuideOnBox\.Focus\(\);"),
                code);
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
