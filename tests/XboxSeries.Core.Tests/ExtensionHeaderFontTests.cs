using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // Headers in Bahnschrift, body text in Segoe UI (#77), in the extension's own views: page titles, section and Group
    // headings, the Guide's and dialogs' titles and the nav labels reach XboxHeaderFont (or its SemiBold, where they were
    // semibold), and tile names, counts, rows, boxes and prompts stay on XboxFont. Each text is found by role (its text,
    // its name, or the text its own style gives it) and its font followed through its style and BasedOn chain.
    public class ExtensionHeaderFontTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        [Theory]
        // My games & apps: the page's heading, Manage Queue's and its sections, Play history's day headings, the nav labels.
        [InlineData("MyGamesControl.xaml", "Name=Header", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Manage Queue", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Recently installed", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Installing", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text={Binding Name}|FontSize=30", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Games", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Apps", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Play history", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Full library", "XboxHeaderFont")]
        [InlineData("MyGamesControl.xaml", "Text=Manage", "XboxHeaderFont")]
        // Home's Groups: each Group's heading, semibold as it was.
        [InlineData("GroupsControl.xaml", "Text={Binding Name}", "XboxHeaderFontSemibold")]
        // The Guide's tab headings and its flyouts' titles.
        [InlineData("OverlayWindow.xaml", "Sets=Now Playing", "XboxHeaderFont")]
        [InlineData("OverlayWindow.xaml", "Text={Binding Flyout.Title}", "XboxHeaderFont")]
        // Dialogs' titles: the Tile menu's and prompts' panel, the wide dialog.
        [InlineData("MenuPanel.xaml", "Text={Binding Title}", "XboxHeaderFont")]
        [InlineData("WideDialogPanel.xaml", "Text={Binding Title}", "XboxHeaderFont")]
        // Settings: its title, the category's heading and the rail, Settings' nav.
        [InlineData("SettingsControl.xaml", "Text=Settings", "XboxHeaderFont")]
        [InlineData("SettingsControl.xaml", "Name=Header", "XboxHeaderFont")]
        [InlineData("SettingsControl.xaml", "Text=Personalisation", "XboxHeaderFont")]
        [InlineData("SettingsControl.xaml", "Text=More Playnite options", "XboxHeaderFont")]
        // Start-up's title.
        [InlineData("StartupWindow.xaml", "Text=Who's playing today?", "XboxHeaderFont")]
        public void Each_header_is_in_the_header_font(string file, string selector, string font)
        {
            Assert.Equal(font, FontOf(file, selector));
        }

        [Theory]
        // Counts beside the nav labels, and the nav's second line.
        [InlineData("MyGamesControl.xaml", "Text={Binding GameCount}", "XboxFont")]
        [InlineData("MyGamesControl.xaml", "Text={Binding FullLibraryCount}", "XboxFont")]
        [InlineData("MyGamesControl.xaml", "Text=Queue|Foreground={StaticResource DimBrush}", "XboxFont")]
        // The page's count, Sort and Filter, rows and empty states.
        [InlineData("MyGamesControl.xaml", "Name=Count", "XboxFont")]
        [InlineData("MyGamesControl.xaml", "Text={Binding SortLabel}", "XboxFont")]
        [InlineData("MyGamesControl.xaml", "Text=Filter", "XboxFont")]
        [InlineData("MyGamesControl.xaml", "Text={Binding Tile.Title}|FontSize=28", "XboxFontSemibold")]
        [InlineData("MyGamesControl.xaml", "Text=Games you play show up here.", "XboxFont")]
        [InlineData("MyGamesControl.xaml", "Text=All storage", "XboxFont")]
        // Tile captions.
        [InlineData("PromoRow.xaml", "Text=Browse the store", "XboxFontSemibold")]
        [InlineData("PromoRow.xaml", "Text=Pre-order, play now", "XboxFont")]
        [InlineData("HomeTiles.xaml", "Text={TemplateBinding Tag}|FontSize=18", "XboxFont")]
        // The Guide's entries, dialogs' options and prompts' messages.
        [InlineData("OverlayWindow.xaml", "Name=Calling", "XboxFont")]
        [InlineData("MenuPanel.xaml", "Text={Binding Label}", "XboxFont")]
        [InlineData("MenuPanel.xaml", "Text={Binding Message}", "XboxFont")]
        [InlineData("WideDialogPanel.xaml", "Text={Binding Label}", "XboxFont")]
        // Settings' boxes and text, Start-up's gamertags, Toasts.
        [InlineData("SettingsBoxes.xaml", "Key=Text", "XboxFont")]
        [InlineData("SettingsControl.xaml", "Text=Client ID", "XboxFont")]
        [InlineData("StartupWindow.xaml", "Text={Binding Gamertag}", "XboxFont")]
        [InlineData("ToastWindow.xaml", "Name=Line", "XboxFont")]
        public void Body_text_stays_in_the_body_font(string file, string selector, string font)
        {
            Assert.Equal(font, FontOf(file, selector));
        }

        // The font key a TextBlock reaches: its own FontFamily, else its inline style's, else its keyed style's, each
        // style followed down its BasedOn. "Key=K" asks for the keyed style K itself.
        private static string FontOf(string file, string selector)
        {
            var doc = XDocument.Load(Path.Combine(ExtensionDir, file));

            if (selector.StartsWith("Key="))
            {
                return FontOfStyle(doc, StyleByKey(doc, selector.Substring(4)));
            }

            var element = Select(doc, selector);
            var own = ResourceKey((string)element.Attribute("FontFamily"));
            if (own != null)
            {
                return own;
            }

            var inline = element.Elements().FirstOrDefault(e => e.Name.LocalName == "TextBlock.Style")?.Elements().FirstOrDefault();
            if (inline != null)
            {
                return FontOfStyle(doc, inline);
            }

            var styleKey = ResourceKey((string)element.Attribute("Style"));
            return styleKey == null ? null : FontOfStyle(doc, StyleByKey(doc, styleKey));
        }

        // The one TextBlock matching every "|"-separated condition: "Name=N" its x:Name, "Sets=T" a Text setter in its
        // style, any other "Attr=V" an attribute of its own.
        private static XElement Select(XDocument doc, string selector)
        {
            var conditions = selector.Split('|').Select(c => c.Split(new[] { '=' }, 2)).ToList();
            var matches = doc.Descendants().Where(e => e.Name.LocalName == "TextBlock").Where(e => conditions.All(c =>
                c[0] == "Name" ? (string)e.Attribute(X + "Name") == c[1]
                : c[0] == "Sets" ? e.Descendants().Any(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == "Text"
                                                            && (string)s.Attribute("Value") == c[1])
                : (string)e.Attribute(c[0]) == c[1])).ToList();

            Assert.True(matches.Count == 1, $"{selector} matches {matches.Count} TextBlocks");
            return matches[0];
        }
    }
}
