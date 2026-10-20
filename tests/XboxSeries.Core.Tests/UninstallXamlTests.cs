using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of uninstalling through the client (#79): the "X was uninstalled" Toast's look, and the game menu's
    // Uninstall row the extension takes over. The XAML and the seam aren't run here (the seam was tried on a harness
    // hosting Playnite's own row template); these checks guard what the Toast shows and how the row is found.
    public class UninstallXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XElement Keyed(XDocument doc, string key) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Key") == key);

        // The glyph a ToastImage.<Kind> draws, with its font, size and colour.
        private static (string Text, string Font, string Size, string Colour) Glyph(string kind)
        {
            var text = Keyed(Load("ToastImages.xaml"), "ToastImage." + kind).Elements().Single();
            return ((string)text.Attribute("Text"), (string)text.Attribute("FontFamily"), (string)text.Attribute("FontSize"),
                (string)text.Attribute("Foreground"));
        }

        // The ContentTemplate the tile's kind trigger sets for a kind, or null for none.
        private static string TileTemplateFor(string kind)
        {
            var trigger = Load("ToastWindow.xaml").Descendants().SingleOrDefault(e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Binding") == "{Binding Kind}"
                && (string)e.Attribute("Value") == "{x:Static core:ToastKind." + kind + "}"
                && e.Elements().Any(s => (string)s.Attribute("Property") == "ContentTemplate"));
            return (string)trigger?.Elements().Single().Attribute("Value");
        }

        [Fact]
        public void An_Uninstalled_Toast_has_its_tile_in_place_from_the_start_as_Ready_to_play_has()
        {
            Assert.Equal("False", Keyed(Load("ToastImages.xaml"), "ToastSlide.Uninstalled").Value);
        }

        [Fact]
        public void Without_a_cover_its_tile_shows_the_bin_the_game_menu_Uninstall_shows_drawn_as_Ready_to_play_draws_its_mark()
        {
            var bin = Glyph("Uninstalled");
            var installed = Glyph("Installed");

            Assert.Equal("", bin.Text);
            Assert.Equal((installed.Font, installed.Size, installed.Colour), (bin.Font, bin.Size, bin.Colour));

            // The theme's game menu row it matches.
            var menu = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Views", "GameMenu.xaml"));
            Assert.Contains(Keyed(menu, "GameMenuUninstallButtonTemplate").Descendants(),
                e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == bin.Text);
        }

        [Fact]
        public void The_tile_picks_the_bin_for_an_Uninstalled_Toast()
        {
            Assert.Equal("{StaticResource ToastImage.Uninstalled}", TileTemplateFor("Uninstalled"));
        }

        [Fact]
        public void Every_kind_has_a_tile_image_a_slide_and_but_for_the_default_a_trigger_picking_its_image()
        {
            var images = Load("ToastImages.xaml");
            // But for the achievements', which aren't the banner at all but a control of their own, the pill, blue with its
            // diamond for a rare one and green with the achievement's icon for any other (AchievementToast.xaml,
            // AchievementToastXamlTests), so they have no tile to slide or picture.
            var pill = new[] { nameof(ToastKind.AchievementUnlocked), nameof(ToastKind.RareAchievementUnlocked) };
            foreach (var kind in Enum.GetNames(typeof(ToastKind)).Except(pill))
            {
                Assert.Single(images.Descendants(), e => (string)e.Attribute(X + "Key") == "ToastImage." + kind);
                Assert.Single(images.Descendants(), e => (string)e.Attribute(X + "Key") == "ToastSlide." + kind);
                if (kind != nameof(ToastKind.Notification))
                {
                    Assert.Equal("{StaticResource ToastImage." + kind + "}", TileTemplateFor(kind));
                }
            }
        }

        [Fact]
        public void The_game_cover_shows_bare_over_the_tile_as_for_any_kind_but_the_greeting()
        {
            // The Toast's own image, the cover, is hidden for the greeting alone.
            var cover = Load("ToastWindow.xaml").Descendants()
                .Single(e => e.Name.LocalName == "Image" && ((string)e.Attribute("Source"))?.StartsWith("{Binding Image") == true)
                .Parent;
            var hidden = cover.Descendants().Where(e => e.Name.LocalName == "DataTrigger").Select(e => (string)e.Attribute("Value"));

            Assert.Equal(new[] { "{x:Static core:ToastKind.Greeting}" }, hidden);
        }

        [Fact]
        public void Its_prompt_reads_to_view_as_only_Ready_to_play_reads_to_start()
        {
            var said = Load("ToastWindow.xaml").Descendants()
                .Single(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "Text" && (string)e.Attribute("Value") == "to view")
                .Parent;
            var otherwise = said.Descendants().Where(e => e.Name.LocalName == "DataTrigger").ToList();

            Assert.Equal("{x:Static core:ToastKind.Installed}", (string)Assert.Single(otherwise).Attribute("Value"));
        }

        [Fact]
        public void The_game_menu_Uninstall_row_is_found_by_the_template_the_theme_gives_it_not_by_its_localised_title()
        {
            var seam = File.ReadAllText(Path.Combine(ExtensionDir, "GameMenuUninstall.cs"));
            var key = Regex.Match(seam, "UninstallTemplateKey = \"(\\w+)\"").Groups[1].Value;
            var menu = XDocument.Load(Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Views", "GameMenu.xaml"));

            Assert.Single(menu.Descendants(), e => e.Name.LocalName == "DataTemplate" && (string)e.Attribute(X + "Key") == key);
            Assert.DoesNotContain("\"Title\"", seam);
        }

        [Fact]
        public void The_game_menu_Uninstall_is_taken_over_in_Fullscreen_alone()
        {
            var plugin = File.ReadAllText(Path.Combine(ExtensionDir, "XboxSeriesPlugin.cs"));
            var fullscreen = plugin.IndexOf("if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)", StringComparison.Ordinal);
            var desktop = plugin.IndexOf("else", fullscreen, StringComparison.Ordinal);
            var register = plugin.IndexOf("GameMenuUninstall.Register(core)", StringComparison.Ordinal);

            Assert.True(fullscreen > 0 && fullscreen < register && register < desktop);
        }
    }
}
