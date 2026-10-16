using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // As on the console, whatever has focus wears a faint green glow. The XAML isn't run here; these checks guard that
    // every focus visual takes the one glow the Dashboard defines, so it is tuned in one place.
    public class FocusGlowTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();

        [Fact]
        public void The_Dashboard_defines_one_soft_green_focus_glow()
        {
            var constants = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Constants.xaml"));

            var glow = constants.Descendants().Single(e => (string)e.Attribute(X + "Key") == "XboxFocusGlow");

            Assert.Equal("DropShadowEffect", glow.Name.LocalName);
            Assert.Equal("False", (string)glow.Attribute(X + "Shared"));
            Assert.Equal("0", (string)glow.Attribute("ShadowDepth"));
            Assert.Equal("18", (string)glow.Attribute("BlurRadius"));
            Assert.Equal("0.6", (string)glow.Attribute("Opacity"));
            Assert.Equal("{StaticResource GlyphLightColor}", (string)glow.Attribute("Color"));
        }

        [Fact]
        public void No_view_draws_a_green_glow_of_its_own()
        {
            var ownGlows = from file in ThemeAndExtensionXaml()
                           from effect in XDocument.Load(file).Descendants()
                           where effect.Name.LocalName == "DropShadowEffect"
                                 && (string)effect.Attribute(X + "Key") != "XboxFocusGlow"
                                 && ((string)effect.Attribute("Color") ?? "").Contains("Glyph")
                           select Relative(file);

            Assert.Empty(ownGlows);
        }

        [Fact]
        public void Every_view_that_shows_focus_glows_with_the_shared_glow()
        {
            string[] focusTriggers = { "IsFocused", "IsKeyboardFocused", "IsKeyboardFocusWithin", "IsMouseOver" };
            // Views that watch focus without drawing it: a library tile's content only hides its source icon on focus;
            // the tile's ring is its container's, in ListGameItemStyle.
            string[] drawNoFocus = { Path.Combine("DerivedStyles", "ListGameItemTemplate.xaml") };

            var unglowing = ThemeAndExtensionXaml()
                .Where(file => !drawNoFocus.Any(file.EndsWith))
                .Where(file =>
                {
                    var xaml = File.ReadAllText(file);
                    return focusTriggers.Any(xaml.Contains) && !xaml.Contains("XboxFocusGlow");
                })
                .Select(Relative);

            Assert.Empty(unglowing);
        }

        private static string[] ThemeAndExtensionXaml() =>
            new[] { "XboxSeries.Dashboard", "XboxSeries.Extension" }
                .SelectMany(project => Directory.GetFiles(Path.Combine(RepoRoot, "src", project), "*.xaml", SearchOption.AllDirectories))
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                               && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .ToArray();

        private static string Relative(string file) => file.Substring(RepoRoot.Length + 1);

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
