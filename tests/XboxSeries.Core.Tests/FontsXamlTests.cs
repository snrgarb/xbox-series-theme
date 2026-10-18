using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Headers in Bahnschrift, body text in Segoe UI (#77). The fonts are named once, in the theme's Constants.xaml, and
    // every other piece of XAML reaches them through those resources, so a swap is one line.
    public class FontsXamlTests
    {
        private const string Xmlns =
            "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";

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
            var offenders = new[] { "XboxSeries.Dashboard", "XboxSeries.Extension" }
                .SelectMany(p => Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", p), "*.xaml", SearchOption.AllDirectories))
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                            && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                            && f != ConstantsPath)
                .SelectMany(f => FontsNotShared(XDocument.Load(f)).Select(o => $"{f.Substring(RepoRoot.Length)}: {o}"))
                .ToList();

            Assert.Empty(offenders);
        }

        [Theory]
        [InlineData("<TextBlock FontFamily='Segoe UI' />")]
        [InlineData("<TextBlock FontFamily='{DynamicResource SomeOtherFont}' />")]
        [InlineData("<TextBlock FontFamily='{x:Static SystemFonts.MessageFontFamily}' />")]
        [InlineData("<TextBlock><TextBlock.FontFamily>Segoe UI</TextBlock.FontFamily></TextBlock>")]
        [InlineData("<Style TargetType='TextBlock'><Setter Property='FontFamily' Value='Bahnschrift' /></Style>")]
        [InlineData("<Style TargetType='TextBlock'><Setter Value='Bahnschrift' Property='FontFamily' /></Style>")]
        [InlineData("<Style TargetType='TextBlock'><Setter Property='FontFamily'><Setter.Value>Bahnschrift</Setter.Value></Setter></Style>")]
        [InlineData("<Style TargetType='TextBlock'><Setter Property='TextElement.FontFamily' Value='Arial' /></Style>")]
        [InlineData("<Grid><Grid.Resources><FontFamily x:Key='Mine'>Arial</FontFamily></Grid.Resources></Grid>")]
        public void The_check_catches_a_font_set_any_other_way(string xaml)
        {
            Assert.NotEmpty(FontsNotShared(Parse(xaml)));
        }

        [Theory]
        [InlineData("<TextBlock FontFamily='{DynamicResource XboxHeaderFont}' />")]
        [InlineData("<TextBlock FontFamily='{StaticResource XboxIconFont}' />")]
        [InlineData("<Style TargetType='TextBlock'><Setter Value='{DynamicResource XboxFont}' Property='FontFamily' /></Style>")]
        public void The_check_lets_the_shared_fonts_through(string xaml)
        {
            Assert.Empty(FontsNotShared(Parse(xaml)));
        }

        private static XDocument Parse(string xaml) => XDocument.Parse($"<Grid {Xmlns}>{xaml}</Grid>");
    }
}
