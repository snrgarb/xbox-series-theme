using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of achievements (#81): the Achievement unlocked Toast's look, and Profile & system's achievements
    // section and recent unlocks. The XAML isn't run here (it was laid out on a harness hosting the real windows); these
    // checks guard what the Toast and the tab show and where they get it.
    public class AchievementsXamlTests
    {
        private const string Trophy = "\U0001F3C6";
        private const string ImageConverter = "Converter={StaticResource Image}";
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XDocument Load(string file) => XDocument.Load(Path.Combine(ExtensionDir, file));

        private static XElement Keyed(XDocument doc, string key) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Key") == key);

        private static XElement Named(XContainer root, string name) =>
            root.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        private static XElement GuideEntry() => Keyed(Load("OverlayWindow.xaml"), "GuideEntry");

        private static XElement KindTrigger(XElement template, string kind) =>
            template.Elements().Single(e => e.Name.LocalName == "DataTemplate.Triggers").Elements()
                .Single(t => (string)t.Attribute("Binding") == "{Binding Kind}" && (string)t.Attribute("Value") == kind);

        private static string Set(XElement trigger, string target, string property) =>
            (string)trigger.Elements().SingleOrDefault(s => (string)s.Attribute("TargetName") == target
                && (string)s.Attribute("Property") == property)?.Attribute("Value");

        // The values a style's DataTriggers on a binding set Visibility to, by the value they match.
        private static string HiddenWhen(XElement element, string binding) =>
            (string)element.Descendants().Where(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == binding)
                .SingleOrDefault(t => t.Elements().Any(s => (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Collapsed"))
                ?.Attribute("Value");

        [Fact]
        public void An_Achievement_unlocked_Toast_has_its_tile_in_place_from_the_start_as_the_other_Toasts_with_an_image_have()
        {
            Assert.Equal("False", Keyed(Load("ToastImages.xaml"), "ToastSlide.AchievementUnlocked").Value);
        }

        [Fact]
        public void Without_an_icon_its_tile_shows_the_trophy_on_the_tiles_green_drawn_as_the_other_kinds_draw_their_glyphs()
        {
            var images = Load("ToastImages.xaml");
            var trophy = Keyed(images, "ToastImage.AchievementUnlocked").Elements().Single();
            var bin = Keyed(images, "ToastImage.Uninstalled").Elements().Single();

            Assert.Equal(Trophy, (string)trophy.Attribute("Text"));
            Assert.Equal("{DynamicResource XboxFont}", (string)trophy.Attribute("FontFamily"));
            Assert.Equal(((string)bin.Attribute("FontSize"), (string)bin.Attribute("Foreground")),
                ((string)trophy.Attribute("FontSize"), (string)trophy.Attribute("Foreground")));
        }

        [Fact]
        public void Its_name_and_Score_are_a_second_line_under_the_cycling_one_moving_with_it_and_gone_for_several_unlocks()
        {
            var toast = Load("ToastWindow.xaml");
            var line = Named(toast, "Line");
            var achievement = Named(toast, "AchievementLine");

            // Under the line and the prompt, which fade in its place, and not fading with them.
            var cycling = line.Parent;
            Assert.Same(cycling.Parent, achievement.Parent);
            Assert.True(cycling.IsBefore(achievement));
            Assert.Equal("StackPanel", achievement.Parent.Name.LocalName);
            Assert.Contains(achievement.Ancestors(), a => a.Descendants().Any(e => (string)e.Attribute(X + "Name") == "TextShift")
                && a.Elements().Any(e => e.Name.LocalName == "Grid.RenderTransform"));

            // The trophy, "<Score> G - " and the name, in the line's font and size.
            var texts = achievement.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToList();
            Assert.Equal(new[] { Trophy, "{Binding Score, StringFormat={}{0} G -}", "{Binding Detail}" },
                texts.Select(t => (string)t.Attribute("Text")));
            Assert.All(texts, t => Assert.Equal(((string)line.Attribute("FontSize"), (string)line.Attribute("FontFamily")),
                ((string)t.Attribute("FontSize"), (string)t.Attribute("FontFamily"))));
            Assert.Equal("CharacterEllipsis", (string)texts[2].Attribute("TextTrimming"));

            // Gone without a name, as for "<n> achievements unlocked" and every other kind; the Score gone without one.
            Assert.Equal("{x:Null}", HiddenWhen(achievement, "{Binding Detail}"));
            Assert.Equal("{x:Null}", HiddenWhen(texts[1], "{Binding Score}"));
        }

        [Fact]
        public void Its_prompt_reads_to_view_as_the_Guide_button_opens_the_Guide_at_Profile_and_system()
        {
            var toast = Load("ToastWindow.xaml");
            Assert.DoesNotContain(toast.Descendants(), e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Value") == "{x:Static core:ToastKind.AchievementUnlocked}"
                && e.Elements().Any(s => (string)s.Attribute("Property") == "Text"));
        }

        [Fact]
        public void A_recent_achievement_is_its_icon_on_a_rounded_square_its_name_and_its_game_under_it()
        {
            var entry = GuideEntry();
            var trigger = KindTrigger(entry, "RecentAchievement");

            // The icon, decoded once at twice its size as the other pictures are, over the trophy for none.
            Assert.Equal("{Binding Image, " + ImageConverter + ", ConverterParameter=120}", Set(trigger, "AchievementIcon", "Tag"));
            Assert.Equal("Visible", Set(trigger, "AchievementIcon", "Visibility"));
            Assert.Equal("Collapsed", Set(trigger, "Glyph", "Visibility"));
            var icon = Named(entry, "AchievementIcon");
            Assert.Equal("Collapsed", (string)icon.Attribute("Visibility"));
            Assert.Contains(icon.Descendants(), e => e.Name.LocalName == "TextBlock" && (string)e.Attribute("Text") == Trophy);
            Assert.Contains(icon.Descendants(), e => e.Name.LocalName == "ImageBrush"
                && (string)e.Attribute("ImageSource") == "{Binding Tag, ElementName=AchievementIcon}");

            // The game's name under its own, as the caller's under Answer, each on one line.
            Assert.Equal("Visible", Set(trigger, "Calling", "Visibility"));
            Assert.Equal("{Binding Detail}", (string)Named(entry, "Calling").Attribute("Text"));
            Assert.Equal("NoWrap", Set(trigger, "Title", "TextWrapping"));

            // A row as the servers' and the recent games' are, its text where the profile card's is.
            Assert.Equal("96", Set(trigger, "Entry", "Height"));
            Assert.Equal("96,0,16,0", Set(trigger, "Text", "Margin"));
        }

        [Fact]
        public void A_recent_achievement_takes_focus_and_its_glow_as_any_entry_does()
        {
            var trigger = KindTrigger(GuideEntry(), "RecentAchievement");

            Assert.DoesNotContain(trigger.Elements(), s => new[] { "Ring", "Fill" }.Contains((string)s.Attribute("TargetName")));
            Assert.DoesNotContain(trigger.Elements(), s => (string)s.Attribute("Property") == "IsHitTestVisible");
        }

        [Fact]
        public void Under_the_profile_card_the_Profiles_Score_and_unlocked_count_gone_with_no_achievements()
        {
            var entry = GuideEntry();
            var section = Named(entry, "AchievementsSection");

            // Under the card, outside its focus ring, shown only for the profile card.
            var card = Named(entry, "Entry");
            Assert.Same(card.Parent, section.Parent);
            Assert.True(card.IsBefore(section));
            Assert.Equal("Collapsed", (string)section.Attribute("Visibility"));
            Assert.Equal("Visible", Set(KindTrigger(entry, "Profile"), "AchievementsSection", "Visibility"));

            // From the Guide's totals, and gone altogether while there are none.
            const string totals = "DataContext.Achievements, RelativeSource={RelativeSource AncestorType=local:OverlayWindow}";
            Assert.Equal("{x:Null}", HiddenWhen(section, "{Binding " + totals + "}"));
            var bindings = section.Descendants()
                .Select(e => (string)e.Attribute("Text") ?? ((string)e.Attribute("Property") == "Text" ? (string)e.Attribute("Value") : null))
                .Where(t => t != null).ToList();
            Assert.Contains("{Binding " + totals.Replace("Achievements,", "Achievements.Score,") + ", StringFormat={}{0:N0} G}", bindings);
            Assert.Contains("{Binding " + totals.Replace("Achievements,", "Achievements.Unlocked,") + ", StringFormat={}{0:N0} achievements}", bindings);
            Assert.Contains(Trophy, bindings);
        }
    }
}
