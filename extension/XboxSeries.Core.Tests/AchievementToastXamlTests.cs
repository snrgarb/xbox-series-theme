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
    // The view's side of the achievement Toasts: the console's rare achievement pill, blue with its spinning diamond for a
    // rare one and green with the achievement's icon for any other, paging from its header, Score and name through the
    // whole description to the prompt. The XAML isn't run here (it was laid out and its timeline rendered frame by frame
    // on a harness hosting the real ToastWindow, beside the console's own frames); these checks guard what it shows,
    // where it sits and what it animates. Its paging is checked in AchievementPagesTests.
    public class AchievementToastXamlTests
    {
        private const string Trophy = "\U0001F3C6";
        private const string Ordinary = "{x:Static core:ToastKind.AchievementUnlocked}";
        private const string RareKind = "{x:Static core:ToastKind.RareAchievementUnlocked}";

        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XDocument Pill() => Load("AchievementToast.xaml");

        private static string Code() => File.ReadAllText(Path.Combine(ExtensionDir, "AchievementToast.xaml.cs"));

        private static XElement Named(XContainer root, string name) =>
            root.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        // The values a style's DataTriggers on a binding set Visibility to Collapsed for.
        private static string HiddenWhen(XElement element, string binding) =>
            (string)element.Descendants().Where(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == binding)
                .SingleOrDefault(t => t.Elements().Any(s => (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Collapsed"))
                ?.Attribute("Value");

        private static XElement[] Texts(XContainer root) =>
            root.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToArray();

        // A palette style's own value for a property, the rare one's, and the value its one trigger gives it for an
        // ordinary achievement.
        private static (string rare, string ordinary) Palette(XDocument doc, string key, string property)
        {
            var style = doc.Descendants().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute(X + "Key") == key);
            string Value(XElement parent) => (string)parent.Elements()
                .Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == property).Attribute("Value");
            var trigger = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
            Assert.Equal(("{Binding Kind}", Ordinary), ((string)trigger.Attribute("Binding"), (string)trigger.Attribute("Value")));
            return (Value(style), Value(trigger));
        }

        private static (double h, double s, double l) Hsl(string hex)
        {
            var rgb = Enumerable.Range(0, 3).Select(i => int.Parse(hex.Substring(1 + 2 * i, 2), NumberStyles.HexNumber) / 255.0).ToArray();
            double max = rgb.Max(), min = rgb.Min(), l = (max + min) / 2, c = max - min;
            var s = c == 0 ? 0 : c / (1 - Math.Abs(2 * l - 1));
            var h = c == 0 ? 0
                : max == rgb[0] ? 60 * (((rgb[1] - rgb[2]) / c + 6) % 6)
                : max == rgb[1] ? 60 * ((rgb[2] - rgb[0]) / c + 2)
                : 60 * ((rgb[0] - rgb[1]) / c + 4);
            return (h, s, l);
        }

        [Fact]
        public void The_Toast_window_shows_it_in_place_of_the_banner_bottom_centred_where_the_banner_sits()
        {
            var toast = Load("ToastWindow.xaml");
            var card = Named(toast, "Card");
            var pill = Named(toast, "Achievement");

            Assert.Equal("AchievementToast", pill.Name.LocalName);
            Assert.Same(card.Parent, pill.Parent);
            Assert.Equal(("Center", "Bottom", (string)card.Attribute("Margin")),
                ((string)pill.Attribute("HorizontalAlignment"), (string)pill.Attribute("VerticalAlignment"), (string)pill.Attribute("Margin")));
            Assert.Equal("Collapsed", (string)pill.Attribute("Visibility"));
        }

        [Fact]
        public void The_Toast_window_plays_it_for_both_achievement_kinds_and_the_banner_for_every_other()
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "ToastWindow.xaml.cs"));

            Assert.Contains("toast.Kind == ToastKind.AchievementUnlocked || toast.Kind == ToastKind.RareAchievementUnlocked", code);
        }

        [Fact]
        public void Its_pill_is_778_by_128_fully_rounded_in_the_consoles_blue_its_brighter_circle_flush_at_its_left()
        {
            var doc = Pill();
            var pill = Named(doc, "Pill");
            Assert.Equal(("778", "128"), ((string)pill.Attribute("Width"), (string)pill.Attribute("Height")));

            // Two round ends the pill's height and the span between them, so it unfurls by moving them, not stretching them.
            foreach (var end in new[] { "LeftEnd", "RightEnd" })
            {
                var cap = Named(doc, end);
                Assert.Equal("Ellipse", cap.Name.LocalName);
                Assert.Equal(("128", "128", "{StaticResource PillFill}"),
                    ((string)cap.Attribute("Width"), (string)cap.Attribute("Height"), (string)cap.Attribute("Style")));
            }
            var span = Named(doc, "Span");
            Assert.Equal(("64,0", "{StaticResource PillFill}"), ((string)span.Attribute("Margin"), (string)span.Attribute("Style")));
            Assert.Equal("#0F4E7E", Palette(doc, "PillFill", "Fill").rare);

            // The circle, a pixel inside the pill's left end.
            var circle = Named(doc, "Circle");
            Assert.Equal(("126", "126", "Left", "1,0,0,0"), ((string)circle.Attribute("Width"), (string)circle.Attribute("Height"),
                (string)circle.Attribute("HorizontalAlignment"), (string)circle.Attribute("Margin")));
            Assert.Equal("{StaticResource BrightFill}", (string)Named(doc, "Bright").Attribute("Style"));
            Assert.Equal("#0D7FC9", Palette(doc, "BrightFill", "Fill").rare);
            Assert.Equal("{StaticResource DarkFill}", (string)Named(doc, "Dark").Attribute("Style"));
            Assert.Equal("#0F5180", Palette(doc, "DarkFill", "Fill").rare);
        }

        [Fact]
        public void An_ordinary_achievements_pill_is_the_Toasts_green_its_circle_a_lighter_green_as_the_rare_ones_is_a_brighter_blue()
        {
            var doc = Pill();
            Assert.Equal("{DynamicResource XboxGreenBrush}", Palette(doc, "PillFill", "Fill").ordinary);
            Assert.Equal((string)Named(Load("ToastWindow.xaml"), "Card").Attribute("Background"), Palette(doc, "PillFill", "Fill").ordinary);

            // The rare circle's step from its pill, in lightness and colour, taken from the Toast's green, #107C10.
            var blue = Hsl("#0F4E7E");
            var bright = Hsl(Palette(doc, "BrightFill", "Fill").rare);
            var green = Hsl("#107C10");
            var light = Hsl(Palette(doc, "BrightFill", "Fill").ordinary);
            Assert.Equal(green.h, light.h, 0);
            Assert.InRange(light.l - green.l, bright.l - blue.l - 0.03, bright.l - blue.l + 0.03);
            Assert.InRange(light.s - green.s, bright.s - blue.s - 0.05, bright.s - blue.s + 0.05);
        }

        [Fact]
        public void An_ordinary_achievements_going_is_in_greens_as_the_rare_ones_is_in_blues_the_dark_circle_the_pills_the_halo_darker()
        {
            var doc = Pill();
            Assert.Equal("{StaticResource BrightFill}", (string)Named(doc, "Ring").Attribute("Style"));
            Assert.Equal("{StaticResource HaloFill}", (string)Named(doc, "Halo").Attribute("Style"));
            Assert.Equal("#0B3456", Palette(doc, "HaloFill", "Fill").rare);
            Assert.Equal("{DynamicResource XboxGreenBrush}", Palette(doc, "DarkFill", "Fill").ordinary);

            var halo = Hsl(Palette(doc, "HaloFill", "Fill").ordinary);
            Assert.Equal(120, halo.h, 0);
            Assert.True(halo.l < Hsl("#107C10").l);

            // The Score badge's "G" is cut in the pill's colour.
            var g = Texts(Named(doc, "Badge")).Single();
            Assert.Equal("{StaticResource BadgeLetter}", (string)g.Attribute("Style"));
            Assert.Equal(("#0F4E7E", "{DynamicResource XboxGreenBrush}"), Palette(doc, "BadgeLetter", "Foreground"));
        }

        [Fact]
        public void Its_header_is_the_Toasts_line_and_the_rarity_as_the_console_writes_it()
        {
            var header = Named(Pill(), "Header");
            var runs = header.Elements().Where(e => e.Name.LocalName == "Run").Select(r => (string)r.Attribute("Text")).ToArray();

            Assert.Equal(2, runs.Length);
            Assert.Equal("{Binding Text, Mode=OneWay}", runs[0]);
            var rarity = Regex.Match(runs[1], @"^\{Binding Rarity, Mode=OneWay, StringFormat='(.*)', TargetNullValue=''\}$");
            Assert.True(rarity.Success, runs[1]);

            // "Rare Achievement unlocked - 1%": whole numbers bare, small ones to a tenth; nothing for an ordinary one's null.
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
            var doc = Pill();
            var line = Named(doc, "ScoreLine");
            var badge = Named(doc, "Badge");
            var score = Named(doc, "Score");
            var name = Named(doc, "AchievementName");

            Assert.True(badge.IsBefore(score) && score.IsBefore(name));
            Assert.All(new[] { badge, score, name }, e => Assert.Contains(line, e.Ancestors()));

            // A white disc with the "G" cut in.
            Assert.Contains(badge.Elements(), e => e.Name.LocalName == "Ellipse" && (string)e.Attribute("Fill") == "#FCFFFF");
            Assert.Equal("G", (string)Texts(badge).Single().Attribute("Text"));

            Assert.Equal("{Binding Score, StringFormat={}{0} -}", (string)score.Attribute("Text"));
            Assert.Equal("{Binding Detail}", (string)name.Attribute("Text"));
            Assert.Equal("CharacterEllipsis", (string)name.Attribute("TextTrimming"));
            Assert.Equal("{x:Null}", HiddenWhen(badge, "{Binding Score}"));
            Assert.Equal("{x:Null}", HiddenWhen(score, "{Binding Score}"));
        }

        [Fact]
        public void Without_a_name_the_second_line_is_gone_so_several_unlocks_are_their_header_alone_centred_in_the_pill()
        {
            var doc = Pill();

            Assert.Equal("{x:Null}", HiddenWhen(Named(doc, "ScoreLine"), "{Binding Detail}"));
            Assert.Equal("Center", (string)Named(doc, "Page1").Attribute("VerticalAlignment"));
        }

        [Fact]
        public void The_description_is_seen_whole_wrapped_two_lines_to_a_page_never_cut_short_with_an_ellipsis()
        {
            var doc = Pill();
            var page = doc.Descendants().Single(e => (string)e.Attribute(X + "Key") == "DescriptionPage").Elements().Single();

            // Each page's text is set by the code, a page of the description as AchievementPages splits it.
            Assert.Equal("TextBlock", page.Name.LocalName);
            Assert.Null(page.Attribute("Text"));
            Assert.Equal("Wrap", (string)page.Attribute("TextWrapping"));
            Assert.Null(page.Attribute("TextTrimming"));
            Assert.Null(page.Attribute("MaxHeight"));
            Assert.Contains(Named(doc, "Pages"), Named(doc, "Descriptions").Ancestors());

            // Split at the text column's width as a page wraps it, in its font, and paged through on the plan's times.
            var code = Code();
            Assert.Contains("AchievementPages.Split(", code);
            Assert.Contains("AchievementPages.Plan(", code);
            Assert.Contains("Pill.Width - Pages.Margin.Left - Pages.Margin.Right", code);
            Assert.Contains("MaxTextWidth", code);
            Assert.DoesNotContain("Ellipsis", code);
        }

        [Fact]
        public void Its_last_page_is_our_prompt_Press_Guide_to_view_centred_not_the_consoles_Hold_to_launch()
        {
            var doc = Pill();
            var prompt = Named(doc, "Prompt");

            Assert.Equal("Center", (string)prompt.Attribute("HorizontalAlignment"));
            Assert.Equal(new[] { "Press", "to view" }, Texts(prompt).Select(t => (string)t.Attribute("Text")));
            var guide = prompt.Elements().Single(e => e.Name.LocalName == "Path");
            Assert.Equal(("{DynamicResource SourceXbox}", "White"), ((string)guide.Attribute("Data"), (string)guide.Attribute("Fill")));
            Assert.DoesNotContain(doc.Descendants().Attributes(), a => a.Value.Contains("launch"));
        }

        [Fact]
        public void Its_text_is_white_in_the_body_font_at_the_consoles_size_smaller_than_the_banners()
        {
            var doc = Pill();
            var texts = Texts(doc).Where(t => (string)t.Attribute("Text") != "G" && (string)t.Attribute("Text") != Trophy).ToArray();

            Assert.Equal(6, texts.Length);
            Assert.All(texts, t => Assert.Equal(("{DynamicResource XboxFont}", "28.5", "White"),
                ((string)t.Attribute("FontFamily"), (string)t.Attribute("FontSize"), (string)t.Attribute("Foreground"))));
        }

        [Fact]
        public void A_rare_one_always_shows_the_drawn_diamond_never_the_achievements_icon_or_frames_cut_from_video()
        {
            var doc = Pill();

            Assert.Equal("Canvas", Named(doc, "Diamond").Name.LocalName);
            Assert.Equal(RareKind, HiddenWhen(Named(doc, "Icon"), "{Binding Kind}"));
            Assert.Single(doc.Descendants(), e => e.Name.LocalName == "Image");
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "ImageBrush" || e.Name.LocalName == "MediaElement");
        }

        [Fact]
        public void An_ordinary_ones_circle_is_the_achievements_icon_clipped_round_over_a_white_trophy_for_none_and_no_diamond()
        {
            var doc = Pill();
            var icon = Named(doc, "Icon");

            // Neither the diamond nor the inside of the gem.
            Assert.Equal(Ordinary, HiddenWhen(Named(doc, "Gem"), "{Binding Kind}"));

            // Over the circle, after the logo, clipped to it whole.
            Assert.Same(Named(doc, "Circle"), icon.Parent);
            Assert.True(Named(doc, "Logo").IsBefore(icon));
            var clip = icon.Elements().Single(e => e.Name.LocalName == "Grid.Clip").Elements().Single();
            Assert.Equal(("EllipseGeometry", "63,63", "63", "63"), (clip.Name.LocalName, (string)clip.Attribute("Center"),
                (string)clip.Attribute("RadiusX"), (string)clip.Attribute("RadiusY")));

            // The trophy, white and centred, under the icon, which fills the circle, decoded at twice its size for the TV.
            var trophy = Texts(icon).Single();
            Assert.Equal((Trophy, "White", "{DynamicResource XboxFont}", "Center", "Center"), ((string)trophy.Attribute("Text"),
                (string)trophy.Attribute("Foreground"), (string)trophy.Attribute("FontFamily"),
                (string)trophy.Attribute("HorizontalAlignment"), (string)trophy.Attribute("VerticalAlignment")));
            var image = icon.Descendants().Single(e => e.Name.LocalName == "Image");
            Assert.True(trophy.IsBefore(image));
            Assert.Equal("{Binding Image, Converter={StaticResource Icon}, ConverterParameter=252}", (string)image.Attribute("Source"));
            Assert.Equal("UniformToFill", (string)image.Attribute("Stretch"));
            Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "DecodedImageConverter" && (string)e.Attribute(X + "Key") == "Icon");

            // Arriving as the diamond flies out, zoomed from close up to its place, but never spun.
            var code = Code();
            Assert.Contains("Animate(board, IconZoom, ScaleTransform.ScaleXProperty, Jump(0, 3), Jump(SwapAt, 3), To(FlyEnd, 1, quintic));", code);
            Assert.Matches(@"if \(rare\)\s*\{\s*Spin\(board, SwapAt\);", code);
        }

        [Fact]
        public void It_moves_only_by_transforms_and_opacity_with_no_live_effects_for_the_TVs_sake()
        {
            var doc = Pill();
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName.EndsWith("Effect"));

            var code = Code();
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
            Assert.Contains("PushUp = -24, PushFrom = 58;", Code());
        }

        [Fact]
        public void The_shine_is_a_gradient_band_clipped_to_the_pill_under_the_text()
        {
            var doc = Pill();
            var shine = Named(doc, "Shine");

            Assert.Contains(shine.Descendants(), e => e.Name.LocalName == "LinearGradientBrush");
            Assert.True(shine.IsBefore(Named(doc, "Pages")));
            Assert.Contains(shine.Ancestors(), a => a.Elements().Any(e => e.Name.LocalName.EndsWith(".Clip")));
        }
    }
}
