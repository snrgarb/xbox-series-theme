using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Playnite's own progress bar is the top bar's PART_ElemProgressIndicator, Home's alone. Settings' Download metadata
    // starts a run under it from a page that hides the top bar, as My games & apps and Search do, so the Dashboard shows
    // a copy over those pages.
    public class DashboardProgressTests
    {
        private static readonly string MainPath =
            Path.Combine(FindRepoRoot(), "src", "XboxSeries.Dashboard", "Views", "Main.xaml");

        [Fact]
        public void Playnites_progress_shows_over_each_page_hiding_the_top_bar_while_it_runs_bound_as_Playnite_binds_its_own()
        {
            var main = XDocument.Load(MainPath);
            var copy = main.Descendants().Single(e => (string)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "XboxPageProgress");

            Assert.Contains(copy.Descendants(), e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == "{Binding ProgressStatus}");
            Assert.Contains(copy.Descendants(), e => e.Name.LocalName == "ContentControl"
                && (string)e.Attribute("Style") == "{DynamicResource ProgressIndicator}");

            foreach (var page in new[] { "XboxSeries_MyGames", "XboxSeries_Settings", "XboxSeries_Search" })
            {
                var shown = copy.Descendants().Where(e => e.Name.LocalName == "MultiDataTrigger")
                    .Single(t => t.ToString().Contains("ElementName=" + page + "}"));
                var conditions = shown.Descendants().Where(e => e.Name.LocalName == "Condition")
                    .Select(c => ((string)c.Attribute("Binding"), (string)c.Attribute("Value"))).ToList();
                Assert.Contains(("{Binding ProgressActive}", "True"), conditions);
                Assert.Contains(("{Binding Content.State.IsShown, ElementName=" + page + "}", "True"), conditions);
                Assert.Contains(shown.Descendants(), s => (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Visible");
            }
            Assert.Contains(copy.Descendants(), s => s.Name.LocalName == "Setter" && s.Parent.Name.LocalName == "Style"
                && (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Collapsed");
        }

        private static string FindRepoRoot()
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
