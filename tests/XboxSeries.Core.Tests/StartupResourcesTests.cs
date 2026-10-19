using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Start-up window runs on a thread of its own and never looks in Playnite's application resources, which
    // belong to the UI thread: the theme's resources it uses are handed over as copies. The XAML isn't run here; these
    // checks guard that every resource the window, its swirl and its spinner ask for is one handed over, and that the
    // window stops the lookup before it reaches the application.
    public class StartupResourcesTests
    {
        private static readonly string ExtensionDir = Path.Combine(FindRepoRoot(), "src", "XboxSeries.Extension");

        [Fact]
        public void Every_theme_resource_the_window_asks_for_is_handed_over()
        {
            var asked = new[] { "StartupWindow.xaml", "SwirlBackground.xaml", "LoadingSpinner.cs" }
                .SelectMany(file => Regex.Matches(File.ReadAllText(Path.Combine(ExtensionDir, file)),
                    @"DynamicResource (\w+)|SetResourceReference\(\w+, ""(\w+)""\)").Cast<Match>())
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                .Distinct()
                .ToList();
            var handedOver = Regex.Matches(File.ReadAllText(Path.Combine(ExtensionDir, "StartupResources.cs")), @"""(\w+)""")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

            Assert.NotEmpty(asked);
            Assert.Empty(asked.Except(handedOver));
        }

        [Fact]
        public void The_window_never_looks_in_the_application_resources()
        {
            var window = File.ReadAllText(Path.Combine(ExtensionDir, "StartupWindow.xaml.cs"));

            Assert.Contains("InheritanceBehavior = InheritanceBehavior.SkipToThemeNext;", window);
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
