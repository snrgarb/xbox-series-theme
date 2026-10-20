using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of the Idle screen's cards, the console's idle panel. The card isn't run here (the real DimWindow
    // and IdleCardControl were hosted off-screen with each kind of card set on an IdleState, a close achievement, an
    // unlock with its icon over art and without art, a game played with background art and with a cover, a long title
    // and two tips, at 1920 and 1663 wide, drawn to pictures and measured against the console's captures; and the dim
    // driven from Dimmed to Showing things, flying the panel in, from one card to the next, out with none, in again and
    // back to Awake); these checks guard what it is: a full-height panel over the right half, flown in from the right
    // with the time at the bottom left, its parts where the captures have them, crossfading as Home's top bar does and
    // going with the dim on wake.
    public class IdleCardXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XElement Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file)).Root;

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        private static XElement Named(XElement root, string name) =>
            root.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        [Fact]
        public void The_panel_is_the_consoles_dark_grey_for_a_game_and_slate_blue_for_a_tip()
        {
            var card = Load("IdleCardControl.xaml");
            Assert.Equal("#FF303030", (string)Named(card, "Panel").Attribute("Background"));
            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("GamePanel = Frozen(Color.FromRgb(0x30, 0x30, 0x30))", code);
            Assert.Contains("TipPanel = Frozen(Color.FromRgb(0x64, 0x71, 0x84))", code);
            Assert.Contains("Panel.Background = isTip ? TipPanel : GamePanel;", code);
        }

        [Fact]
        public void The_text_is_where_the_consoles_captures_have_it_in_their_sizes()
        {
            var card = Load("IdleCardControl.xaml");

            // 72 in from the panel's edge and 75 down; the top line 46 tall, the bar 10 tall at 150, teal on grey.
            var text = Named(card, "TopLine").Parent;
            Assert.Equal("72,75,78,0", (string)text.Attribute("Margin"));
            Assert.Equal("46", (string)Named(card, "TopLine").Attribute("Height"));
            var bar = Named(card, "Bar");
            Assert.Equal("10", (string)bar.Attribute("Height"));
            Assert.Equal("0,29,0,0", (string)bar.Attribute("Margin"));
            Assert.Equal("#FF838383", (string)bar.Attribute("Background"));
            Assert.Equal("#FF008080", (string)bar.Elements().Single(e => e.Name.LocalName == "Rectangle").Attribute("Fill"));

            // The title 64 bold on 76 lines, three at most; the label and the description 46, the description on 60 lines.
            var title = Named(card, "Title");
            Assert.Equal("{DynamicResource XboxFont}", (string)title.Attribute("FontFamily"));
            Assert.Equal("Bold", (string)title.Attribute("FontWeight"));
            Assert.Equal("64", (string)title.Attribute("FontSize"));
            Assert.Equal("76", (string)title.Attribute("LineHeight"));
            Assert.Equal("228", (string)title.Attribute("MaxHeight"));
            Assert.Equal("Wrap", (string)title.Attribute("TextWrapping"));
            Assert.Equal("CharacterEllipsis", (string)title.Attribute("TextTrimming"));
            Assert.Equal("Bold", (string)Named(card, "Label").Attribute("FontWeight"));
            Assert.Equal("46", (string)Named(card, "Label").Attribute("FontSize"));
            var description = Named(card, "Description");
            Assert.Null(description.Attribute("FontWeight"));
            Assert.Equal("46", (string)description.Attribute("FontSize"));
            Assert.Equal("60", (string)description.Attribute("LineHeight"));
            Assert.Equal("Wrap", (string)description.Attribute("TextWrapping"));

            // The header light, the percentage regular, the Score bold, all 46.
            Assert.Equal("350", (string)Named(card, "Header").Attribute("FontWeight"));
            Assert.Equal("46", (string)Named(card, "Header").Attribute("FontSize"));
            Assert.Null(Named(card, "Percent").Attribute("FontWeight"));
            Assert.Equal("Bold", (string)Named(card, "Score").Attribute("FontWeight"));

            // A tip's text 46 on 60 lines, read whole.
            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("Title.FontSize = isTip ? 46 : 64;", code);
            Assert.Contains("Title.LineHeight = isTip ? 60 : 76;", code);
        }

        [Fact]
        public void The_Y_prompt_is_the_Ys_72_icon_at_the_bottom_left()
        {
            var card = Load("IdleCardControl.xaml");
            var prompt = Named(card, "Prompt");
            Assert.Equal("Bottom", (string)prompt.Attribute("VerticalAlignment"));
            Assert.Equal("72,0,78,62", (string)prompt.Attribute("Margin"));
            var glyph = prompt.Elements().First();
            Assert.Equal("PromptGlyph", (string)glyph.Attribute(X + "Name"));
            Assert.Equal("72", (string)glyph.Attribute("Width"));
            Assert.Equal("72", (string)glyph.Attribute("Height"));
            Assert.Contains("PromptGlyph.Child = ButtonGlyphs.Create(ControllerButton.Y, 72, Brushes.White);", Code("IdleCardControl.xaml.cs"));
            Assert.Equal("Bold", (string)Named(card, "PromptText").Attribute("FontWeight"));
            Assert.Equal("46", (string)Named(card, "PromptText").Attribute("FontSize"));
        }

        [Fact]
        public void Whatever_the_card_leaves_null_isnt_shown()
        {
            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("ScoreRow.Visibility = Seen(hasScore);", code);
            Assert.Contains("Rare.Visibility = Seen(hasScore && card.IsRare);", code);
            Assert.Contains("Percent.Visibility = Seen(hasProgress);", code);
            Assert.Contains("Bar.Visibility = Seen(hasProgress);", code);
            Assert.Contains("Header.Visibility = Seen(!string.IsNullOrEmpty(card.Header) && !(hasScore && hasProgress));", code);
            Assert.Contains("Label.Visibility = Seen(!string.IsNullOrEmpty(card.Label));", code);
            Assert.Contains("Description.Visibility = Seen(!string.IsNullOrEmpty(card.Description));", code);
            Assert.Contains("Art.Visibility = Seen(card.Art != null);", code);
            Assert.Contains("Picture.Visibility = Seen(PictureImage.Source != null);", code);
            Assert.Contains("Prompt.Visibility = Seen(!string.IsNullOrEmpty(card.Prompt));", code);
        }

        [Fact]
        public void The_art_rises_out_of_the_panel_in_its_lower_half_with_an_icon_or_a_cover_over_it_and_a_tip_has_the_controller()
        {
            var card = Load("IdleCardControl.xaml");
            var art = Named(card, "Art");
            Assert.Equal("540", (string)art.Attribute("Height"));
            Assert.Equal("Bottom", (string)art.Attribute("VerticalAlignment"));
            var stops = art.Element(art.Name.Namespace + "Grid.OpacityMask").Descendants()
                .Where(e => e.Name.LocalName == "GradientStop").Select(e => (string)e.Attribute("Color")).ToList();
            Assert.Equal("#00000000", stops.First());
            Assert.Equal("#FF000000", stops.Last());

            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("new HeroLayer(ArtDecodeWidth)", code);
            Assert.Contains("art.Show(card.Art)", code);
            Assert.Contains("var picture = card.Icon ?? (card.Art?.IsCover == true ? card.Art.Image : null);", code);
            Assert.Contains("Controller.Visibility = Seen(isTip && card.Art == null);", code);

            // The controller's outline, traced from the tip's capture, 442 by 312 at 506 down, centred, in white lines.
            var controller = Named(card, "Controller");
            Assert.Equal("442", (string)controller.Attribute("Width"));
            Assert.Equal("312", (string)controller.Attribute("Height"));
            Assert.Equal("0,506,4,0", (string)controller.Attribute("Margin"));
            Assert.Equal("Center", (string)controller.Attribute("HorizontalAlignment"));
            var body = Named(card, "Body");
            Assert.Equal("White", (string)body.Attribute("Stroke"));
            Assert.Equal("3.2", (string)body.Attribute("StrokeThickness"));
            Assert.Null(body.Attribute("Fill"));
            Assert.All(controller.Descendants().Where(e => e.Name.LocalName == "Path" && (string)e.Attribute("Tag") == "Shape"),
                path =>
                {
                    Assert.Equal("White", (string)path.Attribute("Stroke"));
                    Assert.Equal("3.2", (string)path.Attribute("StrokeThickness"));
                    Assert.Null(path.Attribute("Fill"));
                });
        }

        [Fact]
        public void Every_button_of_the_pad_is_on_the_controller_by_its_name_and_a_tips_buttons_are_filled_white()
        {
            var card = Load("IdleCardControl.xaml");
            var buttons = Named(card, "Controller").Elements().Where(e => e.Name.LocalName == "Canvas").ToList();

            // One element for each button, the D-pad's four as one, each but the Guide with a part filled when lit.
            var expected = System.Enum.GetValues(typeof(ControllerButton)).Cast<ControllerButton>()
                .Select(b => b >= ControllerButton.DPadUp ? "DPad" : b.ToString()).Distinct().OrderBy(n => n);
            Assert.Equal(expected, buttons.Select(b => (string)b.Attribute(X + "Name")).OrderBy(n => n));
            Assert.All(buttons.Where(b => (string)b.Attribute(X + "Name") != "Guide"), button =>
                Assert.Single(button.Elements(), e => (string)e.Attribute("Tag") == "Shape"));

            // A, B, X and Y have their letters, in the panel's colour, seen only when lit; the Guide its sphere outlined
            // until then, and a white disc with the sphere in the panel's colour when lit.
            foreach (var letter in new[] { "A", "B", "X", "Y" })
            {
                var text = Named(card, letter).Elements().Single(e => e.Name.LocalName == "TextBlock");
                Assert.Equal(letter, (string)text.Attribute("Text"));
                Assert.Equal("Lit", (string)text.Attribute("Tag"));
                Assert.Equal("Collapsed", (string)text.Attribute("Visibility"));
                Assert.Equal("{Binding Background, ElementName=Panel}", (string)text.Attribute("Foreground"));
            }

            var guide = Named(card, "Guide").Elements().ToList();
            Assert.All(guide.Where(e => (string)e.Attribute("Tag") == "Unlit"), e => Assert.Equal("White", (string)e.Attribute("Stroke")));
            Assert.All(guide.Where(e => (string)e.Attribute("Tag") == "Lit"), e => Assert.Equal("Collapsed", (string)e.Attribute("Visibility")));
            Assert.Contains("kenney.nl/assets/input-prompts, CC0 1.0", File.ReadAllText(Path.Combine(ExtensionDir, "IdleCardControl.xaml")));

            // Only a tip's buttons, from the core; a game's card none.
            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("Highlight(isTip ? card.Buttons : new ControllerButton[0]);", code);
            Assert.Contains("button >= ControllerButton.DPadUp ? \"DPad\" : button.ToString();", code);
            Assert.Contains("((Shape)part).Fill = on ? Brushes.White : null;", code);
        }

        [Fact]
        public void A_tips_buttons_are_drawn_as_their_icons_in_its_words_and_a_games_title_is_words_alone()
        {
            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("isTip ? ButtonTokens.Parse(card.Title) : new[] { new ButtonTokens.Part(card.Title, null) }", code);
            Assert.Contains("ButtonGlyphs.Inline(part.Button.Value, Title.FontSize, Brushes.White)", code);

            // The icons built in code with only the shared font key, so nothing of the theme's.
            var glyphs = Code("ButtonGlyphs.cs");
            Assert.DoesNotContain("FindResource", glyphs);
            var keys = Regex.Matches(glyphs, @"SetResourceReference\([^,]+, ""(\w+)""\)").Cast<Match>().ToList();
            Assert.NotEmpty(keys);
            Assert.All(keys, m => Assert.Contains(m.Groups[1].Value, SharedKeys));
        }

        [Fact]
        public void An_achievements_icon_has_the_art_under_it_darkened_by_half_again_and_no_other_card_does()
        {
            var card = Load("IdleCardControl.xaml");
            var shade = Named(card, "IconShade");
            Assert.Equal("Art", (string)shade.Parent.Attribute(X + "Name"));
            Assert.Equal("#80000000", (string)shade.Attribute("Fill"));
            Assert.Equal("Collapsed", (string)shade.Attribute("Visibility"));
            Assert.Contains("IconShade.Visibility = Seen(card.Icon != null);", Code("IdleCardControl.xaml.cs"));
        }

        [Fact]
        public void Every_image_is_decoded_at_the_panels_size()
        {
            var code = Code("IdleCardControl.xaml.cs");
            int Const(string name) => int.Parse(Regex.Match(code, name + @" = (\d+);").Groups[1].Value);

            // The panel's 962 wide art at up to 4K, twice 1080p, and the 320 picture likewise: no more than that.
            Assert.Equal(1920, Const("ArtDecodeWidth"));
            Assert.Equal(640, Const("PictureDecodeWidth"));
            Assert.Contains("DecodedImageConverter.Load(picture, PictureDecodeWidth)", code);
            Assert.Equal("320", (string)Named(Load("IdleCardControl.xaml"), "Picture").Attribute("Height"));
            Assert.Contains("isCover ? 480 : artDecodeWidth", Code("HeroLayer.cs"));
        }

        [Fact]
        public void The_card_asks_only_for_the_extensions_own_or_the_shared_resources_never_a_StaticResource_of_the_theme()
        {
            foreach (var file in new[] { "IdleCardControl.xaml", "DimWindow.xaml" })
            {
                var xaml = File.ReadAllText(Path.Combine(ExtensionDir, file));
                Assert.DoesNotContain("StaticResource", xaml);
                var keys = Regex.Matches(xaml, @"\{DynamicResource (\w+)\}").Cast<Match>().Select(m => m.Groups[1].Value).Distinct();
                Assert.All(keys, key => Assert.Contains(key, SharedKeys));
            }
        }

        [Fact]
        public void The_card_never_takes_focus_or_the_mouse_and_is_unseen_until_shown()
        {
            var card = Load("IdleCardControl.xaml");
            Assert.Equal("False", (string)card.Attribute("Focusable"));
            Assert.Equal("False", (string)card.Attribute("IsHitTestVisible"));
            Assert.Equal("0", (string)card.Attribute("Opacity"));

            var dim = Load("DimWindow.xaml");
            var cards = Named(dim, "Cards");
            Assert.Equal("0", (string)cards.Attribute("Opacity"));
            var flyout = Named(dim, "Flyout");
            Assert.Equal(new[] { "CardA", "CardB" }, flyout.Elements().Where(e => e.Name.LocalName == "IdleCardControl")
                .Select(e => (string)e.Attribute(X + "Name")));

            // Over the shade, never under it.
            var all = dim.Descendants().ToList();
            Assert.True(all.IndexOf(Named(dim, "Shade")) < all.IndexOf(cards));
        }

        [Fact]
        public void The_panel_covers_the_right_half_the_whole_height_laid_out_1080_tall_and_scaled_as_the_Overlay_is()
        {
            var dim = Load("DimWindow.xaml");
            var root = Named(dim, "Root");
            Assert.Equal("Viewbox", root.Parent.Name.LocalName);
            Assert.Equal("Uniform", (string)root.Parent.Attribute("Stretch"));
            Assert.Equal("1080", (string)root.Attribute("Height"));
            Assert.Equal("Right", (string)Named(dim, "Flyout").Attribute("HorizontalAlignment"));
            Assert.Null(Named(dim, "Flyout").Attribute("Height"));

            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("Root.Width = Root.Height * ActualWidth / ActualHeight;", code);
            Assert.Contains("Root.Width = Root.Height * ActualWidth / ActualHeight;", Code("OverlayWindow.xaml.cs"));

            // From 958 at 1920 wide, as the console's.
            Assert.Contains("Flyout.Width = Math.Round(Root.Width / 2) + 2;", code);
        }

        [Fact]
        public void The_panel_flies_in_from_the_right_with_the_first_card_and_out_with_none()
        {
            var dim = Load("DimWindow.xaml");
            Assert.Equal("FlyoutSlide", (string)Named(dim, "Flyout").Descendants().Single(e => e.Name.LocalName == "TranslateTransform").Attribute(X + "Name"));

            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("FlyInDuration = TimeSpan.FromMilliseconds(500)", code);
            Assert.Contains("FlyOutDuration = TimeSpan.FromMilliseconds(300)", code);
            Assert.Contains("FlyoutSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, FlyInDuration) { EasingFunction = ease });", code);
            Assert.Contains("new DoubleAnimation(Flyout.Width, FlyOutDuration)", code);
            Assert.Contains("FlyoutSlide.X = Flyout.Width;", code);

            // Dimming again starts out off the screen, with neither card, nor the time.
            var dimmed = Regex.Match(code, @"if \(idle\.IsDimmed\)\s*\{[\s\S]*?ShowOverForeground\(\);").Value;
            Assert.Contains("Unseen(front);", dimmed);
            Assert.Contains("Unseen(back);", dimmed);
            Assert.Contains("Unseen(Clock);", dimmed);
            Assert.Contains("FlownOut();", dimmed);
        }

        [Fact]
        public void A_card_crossfades_slowly_to_the_next_in_eight_tenths_and_goes_with_the_dim_on_wake()
        {
            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("CardCrossfade = TimeSpan.FromMilliseconds(800)", code);
            Assert.Contains("e.PropertyName == nameof(IdleState.Card)", code);
            Assert.Contains("front.BeginAnimation(OpacityProperty, new DoubleAnimation(1, CardCrossfade))", code);
            Assert.Contains("back.BeginAnimation(OpacityProperty, new DoubleAnimation(0, CardCrossfade))", code);

            // On wake the cards and the time fade with the shade, in its quick fade out.
            var fade = Regex.Match(code, @"private void Fade\(double to, Duration duration, Action done\)[\s\S]*?\n        \}").Value;
            Assert.Contains("Shade.BeginAnimation(OpacityProperty, fade);", fade);
            Assert.Contains("Cards.BeginAnimation(OpacityProperty, new DoubleAnimation(0, duration)", fade);
            Assert.Equal("Cards", Named(Load("DimWindow.xaml"), "Clock").Parent.Attribute(X + "Name").Value);
        }

        [Fact]
        public void The_time_shows_at_the_bottom_left_large_and_light_grey_with_the_cards()
        {
            var dim = Load("DimWindow.xaml");
            var clock = Named(dim, "Clock");
            Assert.Equal("{Binding Time}", (string)clock.Attribute("Text"));
            Assert.Equal("0", (string)clock.Attribute("Opacity"));
            Assert.Equal("Left", (string)clock.Attribute("HorizontalAlignment"));
            Assert.Equal("Bottom", (string)clock.Attribute("VerticalAlignment"));
            Assert.Equal("Bold", (string)clock.Attribute("FontWeight"));
            Assert.Equal("64", (string)clock.Attribute("FontSize"));
            Assert.Equal("#FFCCCCCC", (string)clock.Attribute("Foreground"));

            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("Clock.DataContext = status;", code);
            Assert.Contains("Clock.BeginAnimation(OpacityProperty, new DoubleAnimation(1, FlyInDuration));", code);
            Assert.Contains("Clock.BeginAnimation(OpacityProperty, new DoubleAnimation(0, FlyOutDuration));", code);
        }

        [Fact]
        public void Each_card_slides_and_fades_as_a_picture_drawn_at_the_screens_resolution_with_its_art_shadow_and_blur_inside()
        {
            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("new BitmapCache(pixelsPerUnit) { SnapsToDevicePixels = true }", code);
            Assert.Contains("foreach (var card in new UIElement[] { CardA, CardB })", code);

            // The cache sits on the card itself, so the art's fade, the picture's shadow and a cover's blur, on elements
            // inside it, are in the picture rather than redrawn every frame; the slide moves the panel holding them.
            var card = Load("IdleCardControl.xaml");
            Assert.Null(card.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Effect")));
            Assert.Null(card.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".RenderTransform")));
            Assert.NotNull(Named(card, "Art").Elements().SingleOrDefault(e => e.Name.LocalName == "Grid.OpacityMask"));
        }
    }
}
