using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Settings' Discord category: its Client ID and Secret, each typed over the page or pasted.</summary>
    public class DiscordSettingsXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Theory]
        [InlineData("TypeClientIdButton", "PasteClientIdButton")]
        [InlineData("TypeClientSecretButton", "PasteClientSecretButton")]
        public void Type_comes_before_Paste_side_by_side_as_the_pane_buttons_they_are(string type, string paste)
        {
            var settings = XDocument.Load(Path.Combine(RepoRoot(), "src", "XboxSeries.Extension", "SettingsControl.xaml"));
            XElement Named(string name) => settings.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

            var typeButton = Named(type);
            var pasteButton = Named(paste);
            Assert.Same(typeButton.Parent, pasteButton.Parent);
            Assert.Equal("Horizontal", (string)typeButton.Parent.Attribute("Orientation"));
            Assert.True(typeButton.IsBefore(pasteButton));
            Assert.Equal("Type", (string)typeButton.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text"));
            Assert.Equal("Paste", (string)pasteButton.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text"));
            Assert.All(new[] { typeButton, pasteButton }, button =>
            {
                Assert.Equal("{StaticResource PaneButton}", (string)button.Attribute("Style"));
                Assert.Equal("OnDiscordButtonClick", (string)button.Attribute("Click"));
            });
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XboxSeries.slnx")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
        }
    }
}
