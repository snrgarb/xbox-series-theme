using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Playnite's other windows (#41): its keyboard, message boxes, item selections, the game menu, the main, help,
    // notifications and custom menus, the filter panels and the game status view. Their layouts are Playnite's own and
    // fixed; the Dashboard restyles them through the theme files Playnite loads each after its Default namesake. The XAML
    // isn't run here; these checks guard that every override names only what Playnite asks for, keeps the parts Playnite
    // drives, and draws as the Dashboard's menus and prompts do.
    public class DashboardWindowsTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard");
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        // The keys each of Playnite's Default fullscreen theme files defines (Playnite 10's, as installed), an implicit
        // style by its target type. Playnite looks up only these, so an override's other keys would never be used.
        private static readonly Dictionary<string, string[]> DefaultKeys = new Dictionary<string, string[]>
        {
            { "Views/TextInput.xaml", new[] { "TextInputButton", "TextInputCheckbox", "TextInputTextbox", "TextInputOptionsBorder", "TextInputKeysBorder" } },
            { "Views/MessageBox.xaml", new[] { "MessageBoxBorder", "MessageBoxCaptionText", "MessageBoxMessageText", "MessageBoxButton" } },
            { "Views/ItemSelection.xaml", new[] { "ItemSelectionButton", "ItemSelectionCheckBox", "ItemSelectionBorder", "ItemSelectionFooterBorder", "ItemSelectionFooterButton", "ItemSelectionHeaderText" } },
            { "Views/GameMenu.xaml", new[] { "GameMenuButton", "GameMenuBorder", "GameMenuHeaderText", "GameMenuHeaderTemplate", "GameMenuPlayButtonTemplate", "GameMenuInstallButtonTemplate", "GameMenuFavoriesButtonTemplate", "GameMenuVisibilityButtonTemplate", "GameMenuHdrButtonTemplate", "GameMenuRemoveButtonTemplate", "GameMenuUninstallButtonTemplate", "GameMenuCustomActionButtonTemplate", "GameMenuCompletionStatusTemplate", "GameMenuSetFieldsTemplate", "GameMenuExtensionsTemplate" } },
            { "Views/CustomMenus.xaml", new[] { "GameClientMenuButton", "GameClientsBorder", "SoftwareToolMenuButton", "SoftwareToolsBorder", "ExtensionMenuButton", "ExtensionsBorder" } },
            { "Views/MainMenu.xaml", new[] { "MainMenuBorder", "MainMenuButton", "MainMenuHeaderTemplate", "MainMenuSettingsButtonTemplate", "MainMenuLibUpdateButtonTemplate", "MainMenuCancelLibUpdateButtonTemplate", "MainMenuExitPlayniteButtonTemplate", "MainMenuSwithDesktopButtonTemplate", "MainMenuRandomGameButtonTemplate", "MainMenuShutdowButtonTemplate", "MainMenuSuspendButtonTemplate", "MainMenuHibernateButtonTemplate", "MainMenuRestartButtonTemplate", "MainMenuHelpButtonTemplate", "MainMenuPatreonButtonTemplate", "MainMenuKofiButtonTemplate", "MainMenuMinimizeButtonTemplate", "MainMenuLockSystemButtonTemplate", "MainMenuLogoutUserButtonTemplate", "MainMenuClientsButtonTemplate", "MainMenuToolsButtonTemplate", "MainMenuExtensionsButtonTemplate" } },
            { "Views/HelpMenu.xaml", new[] { "HelpMenuBorder", "HelpMenuButton", "HelpMenuHeaderTemplate", "HelpMenuRestartPlayniteButtonTemplate", "HelpMenuSafeModeButtonTemplate", "HelpMenuFeedbackButtonTemplate" } },
            { "Views/NotificationsMenu.xaml", new[] { "NotificationsMenuBorder", "NotificationsMenuItemButton", "NotificationsMenuDismissButton", "NotificationsMenuHeaderText" } },
            { "Views/GameStatus.xaml", new[] { "ButtonGameStatusAction", "GameStatus" } },
            { "Views/FiltersView.xaml", new[] { "FilterPanelNagivationButton", "FilterItemtSelectionStyle", "FilterPanelCheckBoxEx", "FilterPanelButtonEx", "FilterPanelComboBoxEx", "FilterPanelText", "FilterPanelFilterPresetActionButton", "FilterPanelAddPresetTemplate", "FilterPanelRenamePresetTemplate", "FilterPanelRemovePresetTemplate", "Filters" } },
            { "Views/FiltersAdditional.xaml", new[] { "FiltersAdditional" } },
            { "CustomControls/FilterDbItemtSelection.xaml", new[] { "FilterDbItemtSelection" } },
            { "CustomControls/FilterEnumListSelection.xaml", new[] { "FilterEnumListSelection" } },
            { "CustomControls/FilterStringListSelection.xaml", new[] { "FilterStringListSelection" } },
            { "CustomControls/FilterPresetSelector.xaml", new[] { "ItemFilterQuickPreset", "FilterPresetSelector" } },
            { "DefaultControls/Button.xaml", new[] { "Button", "ButtonEx", "LoadMoreButton" } },
            { "DefaultControls/TextBox.xaml", new[] { "TextBox" } },
            { "DefaultControls/ScrollViewer.xaml", new[] { "ScrollBarTrackButton", "Thumb", "ScrollBar", "ScrollViewer", "ScrollViewerEx" } },
            { "DefaultControls/ProgressBar.xaml", new[] { "ProgressBar", "ProgressWindowProgressBarStyle" } },
            { "DefaultControls/ToolTip.xaml", new[] { "ToolTip" } },
        };

        // The named parts Playnite's code finds in a template it is handed (Playnite.FullscreenApp's controls).
        private static readonly Dictionary<string, string[]> DrivenParts = new Dictionary<string, string[]>
        {
            { "Views/FiltersView.xaml", new[] { "PART_ItemsHost" } },
            { "Views/FiltersAdditional.xaml", new[] { "PART_MenuHost", "PART_ButtonBack", "PART_ItemsHost" } },
            { "CustomControls/FilterDbItemtSelection.xaml", new[] { "PART_MenuHost", "PART_ButtonBack", "PART_ButtonClear", "PART_ItemsHost" } },
            { "CustomControls/FilterEnumListSelection.xaml", new[] { "PART_MenuHost", "PART_ButtonBack", "PART_ButtonClear", "PART_ItemsHost" } },
            { "CustomControls/FilterStringListSelection.xaml", new[] { "PART_MenuHost", "PART_ButtonBack", "PART_ButtonClear", "PART_ItemsHost" } },
            { "CustomControls/FilterPresetSelector.xaml", new[] { "PART_ItemsFilterPresets" } },
            { "Views/GameStatus.xaml", new[] { "PART_ViewHost", "PART_ImageCover", "PART_TextStatus", "PART_PanelActionButtons" } },
            { "DefaultControls/TextBox.xaml", new[] { "PART_ContentHost" } },
            { "DefaultControls/ScrollViewer.xaml", new[] { "PART_Track", "PART_ScrollContentPresenter", "PART_VerticalScrollBar", "PART_HorizontalScrollBar" } },
        };

        [Theory]
        [InlineData("Views/TextInput.xaml")]
        [InlineData("Views/MessageBox.xaml")]
        [InlineData("Views/ItemSelection.xaml")]
        [InlineData("Views/GameMenu.xaml")]
        [InlineData("Views/CustomMenus.xaml")]
        [InlineData("Views/MainMenu.xaml")]
        [InlineData("Views/HelpMenu.xaml")]
        [InlineData("Views/NotificationsMenu.xaml")]
        [InlineData("Views/GameStatus.xaml")]
        [InlineData("Views/FiltersView.xaml")]
        [InlineData("Views/FiltersAdditional.xaml")]
        [InlineData("CustomControls/FilterDbItemtSelection.xaml")]
        [InlineData("CustomControls/FilterEnumListSelection.xaml")]
        [InlineData("CustomControls/FilterStringListSelection.xaml")]
        [InlineData("DefaultControls/Button.xaml")]
        [InlineData("DefaultControls/TextBox.xaml")]
        [InlineData("DefaultControls/ScrollViewer.xaml")]
        [InlineData("DefaultControls/ProgressBar.xaml")]
        [InlineData("DefaultControls/ToolTip.xaml")]
        public void Each_of_Playnites_windows_files_has_the_Dashboards_override(string file)
        {
            Assert.True(File.Exists(Path.Combine(DashboardDir, file)), $"{file} isn't restyled");
        }

        [Fact]
        public void Every_override_defines_only_keys_its_Default_namesake_does_or_the_Dashboards_own()
        {
            var faults = new List<string>();
            foreach (var file in DefaultKeys.Keys.Where(f => File.Exists(Path.Combine(DashboardDir, f))))
            {
                foreach (var key in KeysOf(XDocument.Load(Path.Combine(DashboardDir, file))))
                {
                    if (!DefaultKeys[file].Contains(key) && !key.StartsWith("Xbox"))
                    {
                        faults.Add($"{file}: {key}");
                    }
                }
            }

            Assert.Empty(faults);
        }

        [Fact]
        public void Every_override_restyles_all_of_its_Default_namesake()
        {
            // A key left to Default keeps Default's orange look in that corner of the window.
            var faults = new List<string>();
            foreach (var file in DefaultKeys.Keys.Where(f => File.Exists(Path.Combine(DashboardDir, f))))
            {
                var keys = KeysOf(XDocument.Load(Path.Combine(DashboardDir, file))).ToList();
                faults.AddRange(DefaultKeys[file].Where(k => !keys.Contains(k)).Select(k => $"{file}: {k}"));
            }

            Assert.Empty(faults);
        }

        [Fact]
        public void Every_override_keeps_the_parts_Playnite_drives()
        {
            var faults = new List<string>();
            foreach (var pair in DrivenParts.Where(p => File.Exists(Path.Combine(DashboardDir, p.Key))))
            {
                var names = XDocument.Load(Path.Combine(DashboardDir, pair.Key)).Descendants()
                    .Select(e => (string)e.Attribute(X + "Name") ?? (string)e.Attribute("Name")).Where(n => n != null).ToList();
                faults.AddRange(pair.Value.Where(part => !names.Contains(part)).Select(part => $"{pair.Key}: {part}"));
            }

            Assert.Empty(faults);
        }

        [Fact]
        public void Every_button_and_text_box_template_shows_focus_with_the_Dashboards_green_ring_and_glow()
        {
            // As the extension's menus: a green ring round what has focus, carrying the shared glow band. Pad users
            // have no mouse, so a template that drew nothing on focus would leave them lost.
            string[] focusable = { "Button", "ButtonEx", "TextBox", "{x:Type ButtonEx}", "{x:Type Button}", "{x:Type TextBox}" };
            var templates = OverrideFiles()
                .SelectMany(f => XDocument.Load(f).Descendants()
                    .Where(e => e.Name.LocalName == "ControlTemplate" && focusable.Contains((string)e.Attribute("TargetType")))
                    .Select(t => (file: Relative(f), template: t)))
                .ToList();

            Assert.NotEmpty(templates);
            Assert.All(templates, t =>
            {
                var rings = t.template.Descendants().Where(e => e.Name.LocalName == "Border"
                    && (string)e.Attribute("BorderBrush") == "{DynamicResource GlyphLightBrush}"
                    && e.Descendants().Any(g => (string)g.Attribute("Style") == "{DynamicResource XboxFocusGlow}"))
                    .Select(e => (string)e.Attribute(X + "Name")).ToList();
                var shown = t.template.Descendants().Where(e => e.Name.LocalName == "Trigger"
                        && new[] { "IsFocused", "IsKeyboardFocused" }.Contains((string)e.Attribute("Property"))
                        && (string)e.Attribute("Value") == "True")
                    .SelectMany(e => e.Elements().Where(s => s.Name.LocalName == "Setter"
                        && (string)s.Attribute("Property") == "Visibility" && (string)s.Attribute("Value") == "Visible"))
                    .Select(s => (string)s.Attribute("TargetName"));

                Assert.True(rings.Intersect(shown).Any(), $"{t.file}: a {(string)t.template.Attribute("TargetType")} template shows no ring on focus");
            });
        }

        [Theory]
        [InlineData("Views/MessageBox.xaml", "MessageBoxBorder")]
        [InlineData("Views/ItemSelection.xaml", "ItemSelectionBorder")]
        [InlineData("Views/GameMenu.xaml", "GameMenuBorder")]
        [InlineData("Views/CustomMenus.xaml", "GameClientsBorder")]
        [InlineData("Views/CustomMenus.xaml", "SoftwareToolsBorder")]
        [InlineData("Views/CustomMenus.xaml", "ExtensionsBorder")]
        [InlineData("Views/MainMenu.xaml", "MainMenuBorder")]
        [InlineData("Views/HelpMenu.xaml", "HelpMenuBorder")]
        [InlineData("Views/NotificationsMenu.xaml", "NotificationsMenuBorder")]
        public void Each_menu_and_prompt_is_the_extensions_dark_panel(string file, string key)
        {
            var style = StyleOf(file, key);

            Assert.Equal("{DynamicResource XboxPanelBrush}", Setter(style, "Background"));
        }

        [Fact]
        public void The_panel_is_the_colour_of_the_extensions_menus()
        {
            // The extension's MenuPanel draws the Guide's and the tile menu's menus; Playnite's must match it.
            var menu = XDocument.Load(Path.Combine(ExtensionDir, "MenuPanel.xaml"));
            var panel = menu.Root.Elements().Single(e => e.Name.LocalName == "Border");

            Assert.Equal((string)panel.Attribute("Background"), ColorOf("XboxPanelBrush"));
        }

        [Fact]
        public void Playnites_windows_dim_the_screen_as_the_tile_menu_does()
        {
            // Playnite dims its main window by OverlayBrush while one of its windows is open.
            var tileMenu = XDocument.Load(Path.Combine(ExtensionDir, "TileMenuControl.xaml"));
            var dim = tileMenu.Descendants().Single(e => (string)e.Attribute(X + "Name") == "Dim");

            Assert.Equal((string)dim.Attribute("Background"), ColorOf("OverlayBrush"));
        }

        [Fact]
        public void Playnites_text_is_the_Dashboards_white_body_text()
        {
            // Every text in Playnite's windows is set in TextBlockBaseStyle, directly or through its buttons.
            var constants = XDocument.Load(Path.Combine(DashboardDir, "Constants.xaml"));
            var style = constants.Descendants().Single(e => (string)e.Attribute(X + "Key") == "TextBlockBaseStyle");

            Assert.Equal("{DynamicResource XboxFont}", Setter(style, "FontFamily"));
            Assert.Equal("{DynamicResource XboxTextBrush}", Setter(style, "Foreground"));
        }

        [Fact]
        public void Every_Dashboard_file_defines_each_key_once()
        {
            // Loose XAML refuses a dictionary with a key twice, which fails the whole theme.
            var twice = Directory.GetFiles(DashboardDir, "*.xaml", SearchOption.AllDirectories)
                .SelectMany(f => KeysOf(XDocument.Load(f)).GroupBy(k => k).Where(g => g.Count() > 1).Select(g => $"{Relative(f)}: {g.Key}"));

            Assert.Empty(twice);
        }

        // The keys a theme file defines at its top level: x:Key, else an implicit style's target type.
        private static IEnumerable<string> KeysOf(XDocument doc) =>
            doc.Root.Elements().Select(e =>
            {
                var key = (string)e.Attribute(X + "Key");
                if (key != null)
                {
                    return key;
                }
                var target = (string)e.Attribute("TargetType") ?? "";
                return target.StartsWith("{x:Type ") ? target.Substring("{x:Type ".Length).TrimEnd('}') : target;
            });

        private static IEnumerable<string> OverrideFiles() =>
            DefaultKeys.Keys.Select(f => Path.Combine(DashboardDir, f)).Where(File.Exists);

        private static XElement StyleOf(string file, string key) =>
            XDocument.Load(Path.Combine(DashboardDir, file)).Root.Elements()
                .Single(e => e.Name.LocalName == "Style" && (string)e.Attribute(X + "Key") == key);

        private static string Setter(XElement style, string property) =>
            (string)style.Elements().SingleOrDefault(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == property)?.Attribute("Value");

        private static string ColorOf(string brushKey)
        {
            var constants = XDocument.Load(Path.Combine(DashboardDir, "Constants.xaml"));
            var brush = constants.Root.Elements().SingleOrDefault(e => (string)e.Attribute(X + "Key") == brushKey);
            return (string)brush?.Attribute("Color");
        }

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
