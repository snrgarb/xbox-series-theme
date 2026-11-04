using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using XboxSeries.Extension;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Whether Playnite shows the Dashboard, the only place the extension runs (DashboardPresence.cs, compiled in here):
    // Fullscreen, with the Dashboard's resources loaded. Elsewhere it stays dormant, so nothing of the Bundle's shows
    // over Desktop mode or another Fullscreen theme.
    public class DashboardPresenceTests
    {
        private static readonly object Font = new object();

        private static Func<string, object> Resources(params string[] keys)
        {
            var found = keys.ToDictionary(k => k, _ => Font);
            return key => found.TryGetValue(key, out var value) ? value : null;
        }

        [Fact]
        public void Fullscreen_with_the_Dashboard_loaded_runs_the_extension()
        {
            Assert.True(DashboardPresence.IsShowing(true, Resources(DashboardPresence.ResourceKey)));
        }

        [Fact]
        public void Fullscreen_on_another_theme_leaves_it_dormant()
        {
            // Default's resources, or the Dashboard's refused and Default's shown in its place.
            Assert.False(DashboardPresence.IsShowing(true, Resources("GlyphBrush", "TextBlockBaseStyle")));
        }

        [Fact]
        public void Desktop_mode_leaves_it_dormant_whatever_its_resources()
        {
            Assert.False(DashboardPresence.IsShowing(false, Resources(DashboardPresence.ResourceKey)));
            Assert.False(DashboardPresence.IsShowing(false, Resources()));
        }

        [Fact]
        public void The_Dashboard_defines_the_resource_the_extension_knows_it_by()
        {
            // Renamed or moved out of the theme, the extension would stay dormant on the Dashboard too.
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var dashboard = Path.Combine(FindRepoRoot(), "theme", "XboxSeries.Dashboard");
            var keys = Directory.GetFiles(dashboard, "*.xaml", SearchOption.AllDirectories)
                .SelectMany(file => XDocument.Load(file).Descendants().Attributes(x + "Key"))
                .Select(a => a.Value);

            Assert.Contains(DashboardPresence.ResourceKey, keys);
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
