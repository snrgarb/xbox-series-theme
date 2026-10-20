using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of the Idle screen's cards. The card isn't run here (the real DimWindow and IdleCardControl were
    // hosted off-screen with each kind of card set on an IdleState, a game's art, a cover standing in for it, an
    // achievement's icon and none, a tip and a long title, at 1663 and 1920 wide, and drawn to pictures; and the dim
    // driven from Dimmed to Showing things, from one card to the next and back to Awake); these checks guard what it is:
    // the Guide's menus' panel, centred over the dim, never taking focus or the mouse, crossfading as Home's top bar does
    // and going with the dim on wake.
    public class IdleCardXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XElement Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file)).Root;

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        private static XElement Named(XElement root, string name) =>
            root.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        [Fact]
        public void The_card_is_the_Guide_menus_panel_with_its_title_and_detail_fonts()
        {
            var card = Load("IdleCardControl.xaml");
            var menu = Load("MenuPanel.xaml");
            var menuPanel = menu.Elements().Single(e => e.Name.LocalName == "Border");
            var panel = Named(card, "Panel");
            Assert.Equal((string)menuPanel.Attribute("Background"), (string)panel.Attribute("Background"));
            Assert.Equal((string)menuPanel.Attribute("CornerRadius"), (string)panel.Attribute("CornerRadius"));

            var shadow = panel.Descendants().First(e => e.Name.LocalName == "DropShadowEffect");
            var menuShadow = menuPanel.Descendants().First(e => e.Name.LocalName == "DropShadowEffect");
            Assert.Equal(menuShadow.Attributes().Select(a => a.ToString()), shadow.Attributes().Select(a => a.ToString()));

            var title = Named(card, "Title");
            Assert.Equal("{DynamicResource XboxHeaderFont}", (string)title.Attribute("FontFamily"));
            Assert.Equal("33", (string)title.Attribute("FontSize"));
            Assert.Equal("CharacterEllipsis", (string)title.Attribute("TextTrimming"));
            var detail = Named(card, "Detail");
            Assert.Equal("{DynamicResource XboxFont}", (string)detail.Attribute("FontFamily"));
            Assert.Equal("#B3FFFFFF", (string)detail.Attribute("Foreground"));
            Assert.Equal("CharacterEllipsis", (string)detail.Attribute("TextTrimming"));
        }

        [Fact]
        public void The_card_asks_only_for_the_extensions_own_or_the_shared_resources_never_a_StaticResource_of_the_theme()
        {
            var xaml = File.ReadAllText(Path.Combine(ExtensionDir, "IdleCardControl.xaml"));
            Assert.DoesNotContain("StaticResource", xaml);
            var keys = Regex.Matches(xaml, @"\{DynamicResource (\w+)\}").Cast<Match>().Select(m => m.Groups[1].Value).Distinct();
            Assert.All(keys, key => Assert.Contains(key, SharedKeys));
        }

        [Fact]
        public void The_card_never_takes_focus_or_the_mouse_and_sits_centred_unseen_until_shown()
        {
            var card = Load("IdleCardControl.xaml");
            Assert.Equal("False", (string)card.Attribute("Focusable"));
            Assert.Equal("False", (string)card.Attribute("IsHitTestVisible"));
            Assert.Equal("Center", (string)card.Attribute("HorizontalAlignment"));
            Assert.Equal("Center", (string)card.Attribute("VerticalAlignment"));
            Assert.Equal("0", (string)card.Attribute("Opacity"));

            var dim = Load("DimWindow.xaml");
            var cards = Named(dim, "Cards");
            Assert.Equal("0", (string)cards.Attribute("Opacity"));
            Assert.Equal(new[] { "CardA", "CardB" }, cards.Elements().Select(e => (string)e.Attribute(X + "Name")));
            Assert.All(cards.Elements(), e => Assert.Equal("IdleCardControl", e.Name.LocalName));

            // Over the shade, never under it.
            var all = dim.Descendants().ToList();
            Assert.True(all.IndexOf(Named(dim, "Shade")) < all.IndexOf(cards));
        }

        [Fact]
        public void The_cards_are_laid_out_1080_tall_at_the_screens_aspect_and_scaled_as_the_Overlay_is()
        {
            var dim = Load("DimWindow.xaml");
            var root = Named(dim, "Root");
            Assert.Equal("Viewbox", root.Parent.Name.LocalName);
            Assert.Equal("Uniform", (string)root.Parent.Attribute("Stretch"));
            Assert.Equal("1080", (string)root.Attribute("Height"));
            Assert.Contains("Root.Width = Root.Height * ActualWidth / ActualHeight;", Code("DimWindow.xaml.cs"));
            Assert.Contains("Root.Width = Root.Height * ActualWidth / ActualHeight;", Code("OverlayWindow.xaml.cs"));
        }

        [Fact]
        public void A_card_crossfades_to_the_next_in_three_tenths_as_Homes_top_bar_and_goes_with_the_dim_on_wake()
        {
            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("CardCrossfade = TimeSpan.FromMilliseconds(300)", code);
            Assert.Contains("e.PropertyName == nameof(IdleState.Card)", code);
            Assert.Contains("front.BeginAnimation(OpacityProperty, new DoubleAnimation(1, CardCrossfade))", code);
            Assert.Contains("back.BeginAnimation(OpacityProperty, new DoubleAnimation(0, CardCrossfade))", code);

            // Home's top bar: the gamertag and the real name, a 0.3 s linear crossfade.
            var main = File.ReadAllText(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Views", "Main.xaml"));
            Assert.Contains("<LinearDoubleKeyFrame KeyTime=\"0:0:7.5\" Value=\"0\" />", main);
            Assert.Contains("<DiscreteDoubleKeyFrame KeyTime=\"0:0:7.2\" Value=\"1\" />", main);

            // On wake the cards fade with the shade, in its quick fade out; dimming again starts with none.
            var fade = Regex.Match(code, @"private void Fade\(double to, Duration duration, Action done\)[\s\S]*?\n        \}").Value;
            Assert.Contains("Shade.BeginAnimation(OpacityProperty, fade);", fade);
            Assert.Contains("Cards.BeginAnimation(OpacityProperty, new DoubleAnimation(0, duration)", fade);
            Assert.Contains("Unseen(front);", code);
            Assert.Contains("Unseen(back);", code);
        }

        [Fact]
        public void Art_falls_back_as_Homes_hero_does_and_every_image_is_decoded_at_the_cards_size()
        {
            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("new HeroLayer(ArtDecodeWidth)", code);
            Assert.Contains("art.Show(card.Art)", code);
            Assert.Contains("DecodedImageConverter.Load(card.Art.Image, CoverDecodeWidth)", code);
            Assert.Contains("DecodedImageConverter.Load(card.Icon, IconDecodeWidth)", code);

            // The card's 640 wide art, its 288 cover and 120 icon at up to 4K, twice 1080p: no more than that.
            int Const(string name) => int.Parse(Regex.Match(code, name + @" = (\d+);").Groups[1].Value);
            Assert.Equal(1280, Const("ArtDecodeWidth"));
            Assert.InRange(Const("CoverDecodeWidth"), 288, 576);
            Assert.InRange(Const("IconDecodeWidth"), 120, 240);

            var card = Load("IdleCardControl.xaml");
            Assert.Equal("640", (string)Named(card, "Panel").Attribute("Width"));
            Assert.Equal("360", (string)Named(card, "Art").Attribute("Height"));
            Assert.Equal("288", (string)Named(card, "CoverImage").Attribute("Height"));
            Assert.Equal("120", (string)Named(card, "Icon").Attribute("Width"));
            Assert.Equal("120", (string)Named(card, "Icon").Attribute("Height"));
            Assert.Contains("isCover ? 480 : artDecodeWidth", Code("HeroLayer.cs"));
        }

        [Fact]
        public void A_tip_is_text_alone_wrapping_and_a_missing_detail_collapses()
        {
            var code = Code("IdleCardControl.xaml.cs");
            Assert.Contains("var hasIcon = card.Art == null && (card.Icon != null || card.Kind == IdleCardKind.RecentUnlock);", code);
            Assert.Contains("Title.TextWrapping = isTip ? TextWrapping.Wrap : TextWrapping.NoWrap;", code);
            Assert.Contains("Detail.Visibility = string.IsNullOrEmpty(card.Description) ? Visibility.Collapsed : Visibility.Visible;", code);
        }

        [Fact]
        public void Each_card_fades_as_a_picture_drawn_at_the_screens_resolution_with_its_shadow_and_blur_inside()
        {
            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("new BitmapCache(pixelsPerUnit) { SnapsToDevicePixels = true }", code);
            Assert.Contains("foreach (var card in new UIElement[] { CardA, CardB })", code);

            // The cache sits on the card itself, so the panel's shadow and a cover's blur, on elements inside it, are in
            // the picture rather than redrawn over it every frame.
            var card = Load("IdleCardControl.xaml");
            Assert.Null(card.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Effect")));
            Assert.NotNull(Named(card, "Panel").Elements().SingleOrDefault(e => e.Name.LocalName == "Border.Effect"));
        }
    }
}
