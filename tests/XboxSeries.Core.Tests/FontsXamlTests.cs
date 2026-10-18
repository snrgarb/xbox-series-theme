using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Headers in Bahnschrift, body text in Segoe UI (#77). The fonts are named once, in the theme's Constants.xaml, and
    // every other piece of XAML reaches them through those resources, so a swap is one line.
    public class FontsXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string ConstantsPath = Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Constants.xaml");

        [Theory]
        [InlineData("XboxFont", "Segoe UI")]
        [InlineData("XboxFontSemibold", "Segoe UI Semibold")]
        [InlineData("XboxHeaderFont", "Bahnschrift")]
        [InlineData("XboxHeaderFontSemibold", "Bahnschrift SemiBold")]
        public void Each_shared_font_is_named_once_in_Constants(string key, string family)
        {
            var font = XDocument.Load(ConstantsPath).Descendants()
                .Single(e => e.Name.LocalName == "FontFamily" && (string)e.Attribute(X + "Key") == key);

            Assert.Equal(family, font.Value.Trim());
        }

        [Fact]
        public void No_XAML_names_a_font_other_than_through_the_shared_resources()
        {
            var literal = new Regex(@"FontFamily=""(?!\{)|Property=""FontFamily""\s+Value=""(?!\{)");
            var offenders = new[] { "XboxSeries.Dashboard", "XboxSeries.Extension" }
                .SelectMany(p => Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", p), "*.xaml", SearchOption.AllDirectories))
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                            && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                            && f != ConstantsPath)
                .Where(f => literal.IsMatch(File.ReadAllText(f)))
                .Select(f => f.Substring(RepoRoot.Length))
                .ToList();

            Assert.Empty(offenders);
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
