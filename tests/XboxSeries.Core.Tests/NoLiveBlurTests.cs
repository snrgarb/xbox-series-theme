using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // When Windows sleeps, WPF loses the graphics card and draws on the CPU until it gets it back: every cached picture is
    // drawn again, and every blur and drop shadow is worked out in software at the 4K screen's size. One frame of that
    // took minutes (#95). So nothing on screen is blurred or shadowed live: shadows are soft-edged shapes or pictures
    // drawn once, and blurred art is drawn small and stretched. These checks read the XAML; they don't run it.
    public class NoLiveBlurTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();

        // Blurs left in, each with its reason, by the file and the key or name of what wears it.
        private static readonly (string File, string Key)[] Kept =
        {
            // The focus glow, being replaced on its own (#95).
            (Path.Combine("XboxSeries.Dashboard", "Constants.xaml"), "XboxFocusGlow"),
            // Blurred art the theme can't make once in code, as the extension does: each is laid out small, drawn into a
            // picture that size and blurred there, then stretched to fill the screen, so the blur is of a picture 128
            // wide (times the screen's scaling), not of the 4K screen (see Every_blur_left_is_of_a_small_picture).
            (Path.Combine("XboxSeries.Dashboard", "Views", "GameDetails.xaml"), "XboxGameDetailsCover"),
            (Path.Combine("XboxSeries.Dashboard", "Views", "GameDetails.xaml"), "PART_ImageBackground"),
            (Path.Combine("XboxSeries.Dashboard", "Views", "GameStatus.xaml"), "XboxGameStatusCover"),
        };

        [Fact]
        public void No_view_blurs_or_shadows_anything_live()
        {
            var live = from file in ThemeAndExtensionXaml()
                       from element in XDocument.Load(file).Descendants()
                       where element.Name.LocalName == "BlurEffect" || element.Name.LocalName == "DropShadowEffect"
                             || (string)element.Attribute("IsBlurEnabled") == "True"
                       where !Kept.Any(k => file.EndsWith(k.File) && Describe(element) == k.Key)
                       select $"{Relative(file)}: <{element.Name.LocalName}> in {Describe(element)}";

            Assert.Empty(live);
        }

        [Fact]
        public void Every_blur_left_is_of_a_small_picture()
        {
            // A Viewbox stretches it to the screen; under it, the blurred element, or what holds it, is at most 128 wide and
            // drawn into a picture its own size: a BitmapCache, or a FadeImage, which caches itself.
            foreach (var (file, name) in Kept.Skip(1))
            {
                var doc = XDocument.Load(Path.Combine(RepoRoot, "src", file));
                var blurred = doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);
                var small = blurred.AncestorsAndSelf().First(e => e.Attribute("Width") != null);

                Assert.True(double.Parse((string)small.Attribute("Width")) <= 128, $"{name} is blurred {small.Attribute("Width")} wide");
                Assert.True(small.Name.LocalName == "FadeImage"
                            || small.Elements().Any(c => c.Name.LocalName.EndsWith(".CacheMode") && c.Elements().Single().Name.LocalName == "BitmapCache"),
                    $"{name} isn't drawn into a picture its own size");
                Assert.Equal("Viewbox", small.Parent.Name.LocalName);
                var radius = blurred.Descendants().Where(e => e.Name.LocalName == "BlurEffect").Select(e => (string)e.Attribute("Radius"))
                    .DefaultIfEmpty((string)blurred.Attribute("BlurAmount")).Single();
                Assert.True(double.Parse(radius) <= 4, $"{name} is blurred by {radius}");
            }
        }

        // The style or resource the element is defined in, by key, or its nearest named ancestor.
        private static string KeyOf(XElement element) =>
            element.AncestorsAndSelf().Select(e => (string)e.Attribute(X + "Key")).FirstOrDefault(k => k != null);

        // Its key, or else the name of it or its nearest named ancestor.
        private static string Describe(XElement element) =>
            KeyOf(element) ?? element.AncestorsAndSelf().Select(e => (string)e.Attribute(X + "Name")).FirstOrDefault(n => n != null)
            ?? element.Parent?.Name.LocalName;

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
