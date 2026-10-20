using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of the rare achievement Toast: the console's blue pill with its spinning diamond, paging from its
    // header, Score and name to the description and the prompt. The XAML isn't run here (it was laid out and its timeline
    // rendered frame by frame on a harness hosting the real ToastWindow, beside the console's own frames); these checks
    // guard what it shows, where it sits and what it animates.
    public class AchievementToastXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XDocument Rare() => Load("AchievementToast.xaml");

        private static XElement Named(XContainer root, string name) =>
            root.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        // The values a style's DataTriggers on a binding set Visibility to Collapsed for.
        private static string HiddenWhen(XElement element, string binding) =>
            (string)element.Descendants().Where(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == binding)
                .SingleOrDefault(t => t.Elements().Any(s => (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Collapsed"))
                ?.Attribute("Value");

        private static XElement[] Texts(XContainer root) =>
            root.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToArray();

        [Fact]
        public void The_Toast_window_shows_it_in_place_of_the_banner_bottom_centred_where_the_banner_sits()
        {
            var toast = Load("ToastWindow.xaml");
            var card = Named(toast, "Card");
            var rare = Named(toast, "Rare");

            Assert.Equal("AchievementToast", rare.Name.LocalName);
            Assert.Same(card.Parent, rare.Parent);
            Assert.Equal(("Center", "Bottom", (string)card.Attribute("Margin")),
                ((string)rare.Attribute("HorizontalAlignment"), (string)rare.Attribute("VerticalAlignment"), (string)rare.Attribute("Margin")));
            Assert.Equal("Collapsed", (string)rare.Attribute("Visibility"));
        }

        [Fact]
        public void Its_pill_is_778_by_128_fully_rounded_in_the_consoles_blue_its_brighter_circle_flush_at_its_left()
        {
            var rare = Rare();
            var pill = Named(rare, "Pill");
            Assert.Equal(("778", "128"), ((string)pill.Attribute("Width"), (string)pill.Attribute("Height")));

            // Two round ends the pill's height and the span between them, so it unfurls by moving them, not stretching them.
            foreach (var end in new[] { "LeftEnd", "RightEnd" })
            {
                var cap = Named(rare, end);
                Assert.Equal("Ellipse", cap.Name.LocalName);
                Assert.Equal(("128", "128", "#0F4E7E"), ((string)cap.Attribute("Width"), (string)cap.Attribute("Height"), (string)cap.Attribute("Fill")));
            }
            var span = Named(rare, "Span");
            Assert.Equal(("64,0", "#0F4E7E"), ((string)span.Attribute("Margin"), (string)span.Attribute("Fill")));

            // The circle, a pixel inside the pill's left end.
            var circle = Named(rare, "Circle");
            Assert.Equal(("126", "126", "Left", "1,0,0,0"), ((string)circle.Attribute("Width"), (string)circle.Attribute("Height"),
                (string)circle.Attribute("HorizontalAlignment"), (string)circle.Attribute("Margin")));
            Assert.Equal("#0D7FC9", (string)Named(rare, "Bright").Attribute("Fill"));
            Assert.Equal("#0F5180", (string)Named(rare, "Dark").Attribute("Fill"));
        }

        [Fact]
        public void Its_header_is_the_Toasts_line_and_the_rarity_as_the_console_writes_it()
        {
            var header = Named(Rare(), "Header");
            var runs = header.Elements().Where(e => e.Name.LocalName == "Run").Select(r => (string)r.Attribute("Text")).ToArray();

            Assert.Equal(2, runs.Length);
            Assert.Equal("{Binding Text, Mode=OneWay}", runs[0]);
            var rarity = Regex.Match(runs[1], @"^\{Binding Rarity, Mode=OneWay, StringFormat='(.*)', TargetNullValue=''\}$");
            Assert.True(rarity.Success, runs[1]);

            // "Rare Achievement unlocked - 1%": whole numbers bare, small ones to a tenth.
            string Format(double value) => string.Format(CultureInfo.InvariantCulture, rarity.Groups[1].Value.Replace("{}", ""), value);
            Assert.Equal(" - 1%", Format(1));
            Assert.Equal(" - 0.3%", Format(0.3));
            Assert.Equal(" - 0.3%", Format(0.25));
            Assert.Equal(" - 4.7%", Format(4.71));
            Assert.Equal(" - 12%", Format(12));
        }

        [Fact]
        public void Its_second_line_is_the_Score_badge_the_Score_and_the_name_the_badge_and_Score_gone_without_one()
        {
            var rare = Rare();
            var line = Named(rare, "ScoreLine");
            var badge = Named(rare, "Badge");
            var score = Named(rare, "Score");
            var name = Named(rare, "AchievementName");

            Assert.True(badge.IsBefore(score) && score.IsBefore(name));
            Assert.All(new[] { badge, score, name }, e => Assert.Contains(line, e.Ancestors()));

            // A white disc with the pill's blue "G" cut in.
            Assert.Contains(badge.Elements(), e => e.Name.LocalName == "Ellipse" && (string)e.Attribute("Fill") == "#FCFFFF");
            var g = Texts(badge).Single();
            Assert.Equal(("G", "#0F4E7E"), ((string)g.Attribute("Text"), (string)g.Attribute("Foreground")));

            Assert.Equal("{Binding Score, StringFormat={}{0} -}", (string)score.Attribute("Text"));
            Assert.Equal("{Binding Detail}", (string)name.Attribute("Text"));
            Assert.Equal("CharacterEllipsis", (string)name.Attribute("TextTrimming"));
            Assert.Equal("{x:Null}", HiddenWhen(badge, "{Binding Score}"));
            Assert.Equal("{x:Null}", HiddenWhen(score, "{Binding Score}"));
        }

        [Fact]
        public void Its_second_page_is_the_description_on_at_most_two_lines()
        {
            var description = Named(Rare(), "Description");

            Assert.Equal("{Binding Description}", (string)description.Attribute("Text"));
            Assert.Equal("Wrap", (string)description.Attribute("TextWrapping"));
            Assert.Equal("WordEllipsis", (string)description.Attribute("TextTrimming"));
            Assert.Equal(2 * double.Parse((string)description.Attribute("LineHeight"), CultureInfo.InvariantCulture),
                double.Parse((string)description.Attribute("MaxHeight"), CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Its_last_page_is_our_prompt_Press_Guide_to_view_centred_not_the_consoles_Hold_to_launch()
        {
            var rare = Rare();
            var prompt = Named(rare, "Prompt");

            Assert.Equal("Center", (string)prompt.Attribute("HorizontalAlignment"));
            Assert.Equal(new[] { "Press", "to view" }, Texts(prompt).Select(t => (string)t.Attribute("Text")));
            var guide = prompt.Elements().Single(e => e.Name.LocalName == "Path");
            Assert.Equal(("{DynamicResource SourceXbox}", "White"), ((string)guide.Attribute("Data"), (string)guide.Attribute("Fill")));
            Assert.DoesNotContain(rare.Descendants().Attributes(), a => a.Value.Contains("launch"));
        }

        [Fact]
        public void Its_text_is_white_in_the_body_font_at_the_consoles_size_smaller_than_the_banners()
        {
            var rare = Rare();
            var texts = Texts(rare).Where(t => (string)t.Attribute("Text") != "G").ToArray();

            Assert.Equal(6, texts.Length);
            Assert.All(texts, t => Assert.Equal(("{DynamicResource XboxFont}", "28.5", "White"),
                ((string)t.Attribute("FontFamily"), (string)t.Attribute("FontSize"), (string)t.Attribute("Foreground"))));
        }

        [Fact]
        public void It_always_shows_the_drawn_diamond_never_the_achievements_icon_or_frames_cut_from_video()
        {
            var rare = Rare();

            Assert.DoesNotContain(rare.Descendants().Attributes(), a => a.Value.Contains("Binding Image"));
            Assert.DoesNotContain(rare.Descendants(), e => e.Name.LocalName == "Image" || e.Name.LocalName == "ImageBrush");
            Assert.Equal("Canvas", Named(rare, "Diamond").Name.LocalName);
        }

        [Fact]
        public void It_moves_only_by_transforms_and_opacity_with_no_live_effects_for_the_TVs_sake()
        {
            var rare = Rare();
            Assert.DoesNotContain(rare.Descendants(), e => e.Name.LocalName.EndsWith("Effect"));

            var code = File.ReadAllText(Path.Combine(ExtensionDir, "AchievementToast.xaml.cs"));
            var animated = Regex.Matches(code, @"\bAnimate\(\s*\w+,\s*[^,]+,\s*([\w.]+Property)\b").Cast<Match>()
                .Select(m => m.Groups[1].Value).Distinct().ToArray();
            var allowed = new[]
            {
                "OpacityProperty", "TranslateTransform.XProperty", "TranslateTransform.YProperty", "ScaleTransform.ScaleXProperty",
                "ScaleTransform.ScaleYProperty", "SkewTransform.AngleXProperty",
            };

            Assert.NotEmpty(animated);
            Assert.All(animated, p => Assert.Contains(p, allowed));
            Assert.DoesNotContain("BeginAnimation(", code);
        }

        [Fact]
        public void Its_first_page_leaves_by_the_consoles_distance_at_our_size_and_the_second_comes_from_below()
        {
            // The console's old page goes up about 50 and the new comes from about 120 below, in video pixels zoomed
            // about 2.078 times: 24 and 58 at our 128-tall pill.
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "AchievementToast.xaml.cs"));

            Assert.Contains("PushUp = -24, PushFrom = 58;", code);
        }

        [Fact]
        public void The_shine_is_a_gradient_band_clipped_to_the_pill_under_the_text()
        {
            var rare = Rare();
            var shine = Named(rare, "Shine");

            Assert.Contains(shine.Descendants(), e => e.Name.LocalName == "LinearGradientBrush");
            Assert.True(shine.IsBefore(Named(rare, "Pages")));
            Assert.Contains(shine.Ancestors(), a => a.Elements().Any(e => e.Name.LocalName.EndsWith(".Clip")));
        }
    }
}
