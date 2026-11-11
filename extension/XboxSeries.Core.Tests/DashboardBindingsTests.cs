using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // What the Dashboard reads of the extension through {PluginSettings Plugin=XboxSeries, Path=...}: Playnite binds the
    // path to the plugin's DashboardSettings, a DashboardBindings. A path that doesn't resolve fails silently in the
    // theme, falling back as if there were no extension, so each one the theme uses is checked here.
    public class DashboardBindingsTests
    {
        [Fact]
        public void Every_path_the_theme_reads_is_a_public_property_of_the_bindings()
        {
            var paths = Directory.GetFiles(Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard"), "*.xaml", SearchOption.AllDirectories)
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{PluginSettings Plugin=XboxSeries, Path=([\w.]+)").Cast<Match>())
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .ToList();

            Assert.Contains("StartUp.ReduceMotion", paths);
            Assert.Contains("HubBackdrop.Snapshot", paths);
            Assert.Contains("HubBackdrop.HasSnapshot", paths);
            Assert.All(paths, path =>
            {
                var type = typeof(DashboardBindings);
                foreach (var name in path.Split('.'))
                {
                    var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    Assert.True(property != null, $"{path}: {type.Name} has no public {name}");
                    type = property.PropertyType;
                }
            });
        }

        [Fact]
        public void The_bindings_are_the_cores_own_states()
        {
            var startUp = new StartUpSettingsState();
            var backdrop = new HubBackdropState();

            var bindings = new DashboardBindings(startUp, backdrop);

            Assert.Same(startUp, bindings.StartUp);
            Assert.Same(backdrop, bindings.HubBackdrop);
        }
    }
}
