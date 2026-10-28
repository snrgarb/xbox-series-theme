using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Full library's tabs are Playnite's own filter preset selector, placed by the Dashboard outside My games & apps'
    // content, so they move only as the extension moves them. The XAML and the slide aren't run here; these checks guard
    // that the tabs never animate by themselves, stay put as LB/RB switch tabs, and come in with the rest of Full
    // library's content as focus moves along the nav onto it, as its header and Sort and Filter row do.
    public class FullLibraryTabsTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard");
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

        [Fact]
        public void The_tabs_never_animate_by_themselves()
        {
            var main = XDocument.Load(Path.Combine(DashboardDir, "Views", "Main.xaml"));
            var selector = main.Descendants().Single(e => (string)e.Attribute(X + "Name") == "PART_FilterPresetSelector");
            var tabs = XDocument.Load(Path.Combine(DashboardDir, "CustomControls", "FilterPresetSelector.xaml"));

            foreach (var element in selector.Parent.DescendantsAndSelf().Concat(tabs.Descendants()))
            {
                var name = element.Name.LocalName;
                Assert.False(name.Contains("Storyboard") || name.EndsWith("Animation") || name.Contains("AnimationUsingKeyFrames")
                    || name.EndsWith(".RenderTransform") || name.EndsWith(".LayoutTransform") || name.EndsWith("Transition"),
                    $"Full library's tabs animate in the Dashboard: {name}");
                Assert.NotEqual("RenderTransform", (string)element.Attribute("Property"));
                Assert.NotEqual("Opacity", (string)element.Attribute("Property"));
            }
        }

        [Fact]
        public void Switching_tabs_moves_only_the_grid_never_the_tabs()
        {
            var tabChecked = Method(MyGamesCode(), "OnLibraryTabChecked");

            var across = Regex.Matches(tabChecked, @"PaneFlyIn\.\w+\((?<args>[^;]*)\);");
            Assert.Single(across);
            Assert.Contains("FollowAcross", across[0].Value);
            Assert.EndsWith(", mainList", across[0].Groups["args"].Value);
        }

        [Fact]
        public void Moving_along_the_nav_onto_Full_library_brings_its_tabs_in_with_its_header_and_row()
        {
            var flyIn = Method(MyGamesCode(), "FlyInItem");

            // The tabs ride the page's own slide, as a picture that fades in with it, not a slide of their own.
            var follow = Regex.Match(flyIn, @"PaneFlyIn\.Follow\((?<args>[^;]*)\);");
            Assert.True(follow.Success, "FlyInItem follows the nav with PaneFlyIn.Follow");
            Assert.Contains("ItemHost", follow.Groups["args"].Value);
            Assert.Contains("libraryTabSelector", follow.Groups["args"].Value);
            Assert.Matches(@"MyGamesItem\.FullLibrary\s*\?\s*libraryTabSelector\s*:\s*null", follow.Groups["args"].Value);
        }

        [Fact]
        public void The_page_going_mid_slide_leaves_the_tabs_where_they_settle()
        {
            var unloaded = Method(MyGamesCode(), "OnUnloaded");

            Assert.Matches(@"PaneFlyIn\.Stop\([^;]*libraryTabSelector[^;]*\);", unloaded);
        }

        private static string MyGamesCode() => File.ReadAllText(Path.Combine(ExtensionDir, "MyGamesControl.xaml.cs"));

        /// <summary>The body of the method <paramref name="name"/>, from its signature to the brace that closes it.</summary>
        private static string Method(string code, string name)
        {
            var start = Regex.Match(code, @"\bvoid " + name + @"\(");
            Assert.True(start.Success, $"no method {name}");
            var open = code.IndexOf('{', start.Index);
            var depth = 0;
            for (var i = open; i < code.Length; i++)
            {
                if (code[i] == '{')
                {
                    depth++;
                }
                else if (code[i] == '}' && --depth == 0)
                {
                    return code.Substring(start.Index, i - start.Index + 1);
                }
            }

            throw new InvalidOperationException($"{name} doesn't close");
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
