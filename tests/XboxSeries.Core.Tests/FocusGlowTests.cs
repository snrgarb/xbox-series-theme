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

        private static readonly string[] GlowKeys = { "XboxFocusGlow", "XboxFocusGlowRound" };
        private static readonly string[] GlowStyles = GlowKeys.Select(k => "{DynamicResource " + k + "}").ToArray();

        [Fact]
        public void The_Dashboard_defines_a_soft_green_focus_glow_for_rounded_and_round_shapes()
        {
            var constants = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Constants.xaml"));

            // A Control, so its template can lay the glow's rings, which a Border's style can't; never focused itself.
            foreach (var key in GlowKeys)
            {
                var glow = constants.Descendants().Single(e => (string)e.Attribute(X + "Key") == key);
                Assert.Equal("Style", glow.Name.LocalName);
                Assert.Equal("Control", (string)glow.Attribute("TargetType"));
                Assert.Null(glow.Attribute("BasedOn"));
                var setters = glow.Elements().Where(e => e.Name.LocalName == "Setter")
                    .ToDictionary(s => (string)s.Attribute("Property"), s => s);
                Assert.Equal("-7.5", (string)setters["Margin"].Attribute("Value"));
                Assert.Equal("False", (string)setters["IsHitTestVisible"].Attribute("Value"));
                Assert.Equal("False", (string)setters["Focusable"].Attribute("Value"));
                Assert.Equal("False", (string)setters["IsTabStop"].Attribute("Value"));
                // Drawn in the accent, each ring and spot faint by its brush's opacity.
                var brushes = glow.Descendants().Where(e => e.Name.LocalName.EndsWith("Brush")).ToList();
                Assert.NotEmpty(brushes);
                Assert.All(brushes, b => Assert.Contains(b.DescendantsAndSelf(),
                    e => (string)e.Attribute("Color") == "{StaticResource GlyphLightColor}"));
            }
        }

        [Fact]
        public void The_glow_is_drawn_without_an_effect()
        {
            // A blur is redrawn on the CPU, at the 4K screen's size, in every glow's cache when Windows sleeps and WPF
            // loses the GPU, hidden ones included: Playnite froze for minutes on the wake. The glow's softness is drawn.
            var constants = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Constants.xaml"));

            foreach (var key in GlowKeys.Concat(new[] { "XboxFocusGlowCache" }))
            {
                var style = constants.Descendants().Single(e => (string)e.Attribute(X + "Key") == key);
                Assert.DoesNotContain(style.Descendants(), e => e.Name.LocalName.EndsWith("Effect")
                                                                || (string)e.Attribute("Property") == "Effect");
            }
        }

        [Fact]
        public void The_round_glow_lays_the_same_rings_rounded_right_round()
        {
            var constants = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Constants.xaml"));

            var rings = GlowKeys.Select(key => constants.Descendants().Single(e => (string)e.Attribute(X + "Key") == key)
                    .Descendants().Where(e => e.Name.LocalName == "Border" && e.Attribute("BorderThickness") != null).ToList())
                .ToArray();

            Assert.True(rings[0].Count > 1);
            Assert.Equal(rings[0].Select(Ring), rings[1].Select(Ring));
            Assert.All(rings[0], r => Assert.Equal("12", (string)r.Attribute("CornerRadius")));
            Assert.All(rings[1], r => Assert.Equal("999", (string)r.Attribute("CornerRadius")));
        }

        private static string Ring(XElement r) =>
            $"{(string)r.Attribute("Margin")} {(string)r.Attribute("BorderThickness")} {(string)r.Attribute("Opacity")}";

        [Fact]
        public void The_Dashboard_defines_the_cache_the_glow_is_drawn_once_into()
        {
            var constants = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Constants.xaml"));

            var host = constants.Descendants().Single(e => (string)e.Attribute(X + "Key") == "XboxFocusGlowCache");

            Assert.Equal("Border", (string)host.Attribute("TargetType"));
            var cache = host.Elements().Single(s => (string)s.Attribute("Property") == "CacheMode")
                .Descendants().Single(e => e.Name.LocalName == "BitmapCache");
            Assert.Equal("2", (string)cache.Attribute("RenderAtScale"));
            Assert.Equal("True", (string)cache.Attribute("SnapsToDevicePixels"));
        }

        [Fact]
        public void Every_glow_is_drawn_once_into_a_cache_that_focus_never_collapses()
        {
            // The glow alone under its cache, so the cache holds nothing else; and an element coming back from Collapsed
            // is drawn again, so whatever hides the glow (the cache itself, or the ring it sits in) does it with Hidden,
            // which only draws it at opacity 0.
            var faults = new System.Collections.Generic.List<string>();
            var glows = 0;
            foreach (var file in ThemeAndExtensionXaml())
            {
                var doc = XDocument.Load(file);
                foreach (var glow in doc.Descendants().Where(e => GlowStyles.Contains((string)e.Attribute("Style"))))
                {
                    glows++;
                    var host = glow.Parent;
                    if (glow.Name.LocalName != "Control" || host == null
                        || (string)host.Attribute("Style") != "{DynamicResource XboxFocusGlowCache}" || host.Elements().Count() != 1)
                    {
                        faults.Add($"{Relative(file)}: a glow not a Control alone under an XboxFocusGlowCache");
                        continue;
                    }
                    foreach (var shown in new[] { host, host.Parent })
                    {
                        if (shown != null && IsCollapsed(doc, shown))
                        {
                            faults.Add($"{Relative(file)}: <{shown.Name.LocalName} {(string)shown.Attribute(X + "Name")}> hides the glow by Collapsed");
                        }
                    }
                }
            }

            Assert.NotEqual(0, glows);
            Assert.Empty(faults);
        }

        [Fact]
        public void A_round_ring_glows_with_the_round_glow()
        {
            // The glow's shape is its style's; a ring drawn right round (CornerRadius 999) takes the round one.
            var faults = from file in ThemeAndExtensionXaml()
                         from glow in XDocument.Load(file).Descendants()
                         where GlowStyles.Contains((string)glow.Attribute("Style"))
                         let ring = glow.Parent?.Parent
                         where glow.Attribute("CornerRadius") != null
                               || (ring != null && (string)ring.Attribute("CornerRadius") == "999"
                                   && (string)glow.Attribute("Style") != "{DynamicResource XboxFocusGlowRound}")
                         select Relative(file);

            Assert.Empty(faults);
        }

        // Collapsed by its own Visibility, by a style of the file's it takes or sets inline, or by a trigger naming it.
        private static bool IsCollapsed(XDocument doc, XElement element)
        {
            if ((string)element.Attribute("Visibility") == "Collapsed")
            {
                return true;
            }
            var style = (string)element.Attribute("Style") ?? "";
            if (style.StartsWith("{StaticResource "))
            {
                var key = style.Substring("{StaticResource ".Length).TrimEnd('}');
                var defined = doc.Descendants().FirstOrDefault(e => (string)e.Attribute(X + "Key") == key);
                if (defined != null && CollapsesItself(defined))
                {
                    return true;
                }
            }
            if (element.Elements().Any(e => e.Name.LocalName.EndsWith(".Style") && e.Elements().Any(CollapsesItself)))
            {
                return true;
            }
            var name = (string)element.Attribute(X + "Name");
            return name != null && doc.Descendants().Any(e => e.Name.LocalName == "Setter"
                                                              && (string)e.Attribute("TargetName") == name
                                                              && (string)e.Attribute("Property") == "Visibility"
                                                              && (string)e.Attribute("Value") == "Collapsed");
        }

        private static bool CollapsesItself(XElement style) =>
            style.Elements().Any(s => s.Name.LocalName == "Setter"
                                      && (string)s.Attribute("Property") == "Visibility"
                                      && (string)s.Attribute("Value") == "Collapsed");

        [Fact]
        public void Every_view_wears_the_glow_as_a_control_not_as_an_effect()
        {
            // The glow is a style for a Control, its cache one for a Border; set on anything else it is refused when
            // the view loads.
            var misused = from file in ThemeAndExtensionXaml()
                          from element in XDocument.Load(file).Descendants()
                          from attribute in element.Attributes()
                          where attribute.Value.Contains("XboxFocusGlow")
                                && attribute.Name != X + "Key"
                                && !(attribute.Name.LocalName == "Style"
                                      && element.Name.LocalName == (attribute.Value.Contains("XboxFocusGlowCache") ? "Border" : "Control"))
                          select $"{Relative(file)}: <{element.Name.LocalName} {attribute.Name.LocalName}=...>";

            Assert.Empty(misused);
        }

        [Fact]
        public void No_view_draws_a_green_glow_of_its_own()
        {
            var ownGlows = from file in ThemeAndExtensionXaml()
                           from effect in XDocument.Load(file).Descendants()
                           where effect.Name.LocalName == "DropShadowEffect"
                                 && (string)effect.Attribute(X + "Key") != "XboxFocusGlow"
                                 && IsGreen((string)effect.Attribute("Color") ?? "")
                           select Relative(file);

            Assert.Empty(ownGlows);
        }

        [Fact]
        public void Every_view_that_shows_focus_glows_with_the_shared_glow()
        {
            string[] focusTriggers = { "IsFocused", "IsKeyboardFocused", "IsKeyboardFocusWithin", "IsMouseOver" };
            // Views that watch focus without drawing it: a library tile's content only hides its source icon on focus;
            // the tile's ring is its container's, in ListGameItemStyle. An App's tile face only shows its name on focus;
            // its ring is its container's, in My games & apps and Search. Playnite settings only shows Update after
            // downloads' info while that box has focus; the box's ring is SettingsBoxes' Box.
            string[] drawNoFocus = { Path.Combine("DerivedStyles", "ListGameItemTemplate.xaml"), "AppTiles.xaml", "PlaynitePane.xaml" };

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

        // The accent by name, or the console's greens written out: the Dashboard's light accent and Xbox green.
        private static bool IsGreen(string color)
        {
            string[] greens = { "2CA243", "107C10" };
            var hex = color.TrimStart('#').ToUpperInvariant();
            return color.Contains("Glyph") || greens.Any(green => hex == green || hex == "FF" + green);
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
