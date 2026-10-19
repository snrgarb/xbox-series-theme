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
        public void Playnites_other_windows_panels_are_the_extensions_panel_grey()
        {
            // Update, add-on updates, random game, licence and crash windows draw their panel in ControlBackgroundDarkBrush, Default's navy.
            Assert.Equal(ColorOf("XboxPanelBrush"), ColorOf("ControlBackgroundDarkBrush"));
        }

        [Fact]
        public void The_progress_window_shows_no_Playnite_logo()
        {
            var media = XDocument.Load(Path.Combine(DashboardDir, "Media.xaml"));
            var header = media.Root.Elements().SingleOrDefault(e => (string)e.Attribute(X + "Key") == "ProgressHeaderStyle");

            Assert.NotNull(header);
            Assert.DoesNotContain(header.Descendants(), e => ((string)e.Attribute("Source") ?? "").Contains("applogo"));
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

        // The game menu, drawn as the console's tile menu: a 450 wide panel whose focused row's green ring stands 8 past
        // the panel on either side. Playnite's window puts the rows in a ScrollViewer, whose content presenter clips to
        // itself, so the theme widens the scroll area past the panel (GameMenuBorder's ScrollViewer) and insets the rows
        // back by the same less the overhang.
        private const int RingOverhang = 8;
        private const int GlowRoom = 20;

        [Fact]
        public void The_game_menus_focus_ring_stands_past_the_panel_on_both_sides()
        {
            var scroll = Thickness(Setter(GameMenuScrollViewer(), "Margin"));
            var row = Thickness(Setter(StyleOf("Views/GameMenu.xaml", "GameMenuButton"), "Margin"));
            var ring = Thickness((string)GameMenuRowPart("FocusRing").Attribute("Margin") ?? "0");

            // Left and right: how far the ring's outer edge lies outside the panel's edge.
            Assert.Equal(RingOverhang, -scroll[0] - row[0] - ring[0]);
            Assert.Equal(RingOverhang, -scroll[2] - row[2] - ring[2]);
        }

        [Fact]
        public void The_game_menus_focused_row_is_filled_lighter_across_the_panels_full_width()
        {
            var fill = GameMenuRowPart("Fill");
            var margin = Thickness((string)fill.Attribute("Margin"));

            Assert.Equal("#FF404040", (string)fill.Attribute("Background"));
            // Its sides are the panel's: the ring's overhang in from the row's edges, the ring 4 and a 4 gap inside them.
            Assert.Equal(RingOverhang, margin[0]);
            Assert.Equal(RingOverhang, margin[2]);
            Assert.Equal("4", (string)GameMenuRowPart("FocusRing").Attribute("BorderThickness"));
            Assert.Equal("Hidden", (string)fill.Attribute("Visibility"));
            Assert.Contains(GameMenuRowTemplate().Descendants(), s => s.Name.LocalName == "Setter"
                && (string)s.Attribute("TargetName") == "Fill" && (string)s.Attribute("Value") == "Visible"
                && (string)s.Parent.Attribute("Property") == "IsFocused");
        }

        [Fact]
        public void The_game_menus_title_is_the_games_name_on_one_trimmed_line()
        {
            var title = StyleOf("Views/GameMenu.xaml", "GameMenuHeaderText");

            Assert.Equal("NoWrap", Setter(title, "TextWrapping"));
            Assert.Equal("CharacterEllipsis", Setter(title, "TextTrimming"));
            // A long name is trimmed to the panel rather than widening it: the panel less the title's 28 each side.
            Assert.Equal("394", Setter(title, "MaxWidth"));
            Assert.Equal(28, Thickness(Setter(title, "Margin"))[0]);
        }

        [Fact]
        public void The_game_menus_rows_stand_72_apart()
        {
            var row = StyleOf("Views/GameMenu.xaml", "GameMenuButton");
            var margin = Thickness(Setter(row, "Margin"));

            Assert.Equal(72, int.Parse(Setter(row, "Height")) + margin[1] + margin[3]);
        }

        [Fact]
        public void Nothing_in_the_game_menu_clips_the_focused_rows_ring_or_glow()
        {
            var doc = XDocument.Load(Path.Combine(DashboardDir, "Views", "GameMenu.xaml"));
            Assert.DoesNotContain(doc.Descendants(), e => (string)e.Attribute("ClipToBounds") == "True"
                || ((string)e.Attribute("Property") == "ClipToBounds" && (string)e.Attribute("Value") == "True"));

            // The scroll area reaches past the ring on every side by the glow's room, and the rows' panel gives back the
            // top and bottom, so the rows sit where Playnite lays them while their ring and glow stay inside the clip.
            var scroll = Thickness(Setter(GameMenuScrollViewer(), "Margin"));
            var row = Thickness(Setter(StyleOf("Views/GameMenu.xaml", "GameMenuButton"), "Margin"));
            var panel = GameMenuItemsPanel();
            var inset = Thickness((string)panel.Attribute("Margin"));

            Assert.True(row[0] >= GlowRoom && row[2] >= GlowRoom, "room for the glow beside the ring");
            Assert.Equal(-scroll[1], inset[1]);
            Assert.Equal(-scroll[3], inset[3]);
            Assert.True(inset[1] >= GlowRoom && inset[3] >= GlowRoom, "room for the glow above and below the end rows");
            Assert.Contains(GameMenuScrollViewer().Descendants(), e => e.Name.LocalName == "ScrollContentPresenter");
        }

        [Fact]
        public void A_long_game_menu_scrolls_by_whole_rows_inside_the_panel()
        {
            // The scroll area reaches past the panel, so a row cut by its end would show over the dimmed screen. Its list
            // is as tall as whole rows, as many as fit in Playnite's 648 high middle of the screen under the title.
            var list = StyleOf("Views/GameMenu.xaml", "GameMenuBorder").Elements().Single(e => e.Name.LocalName == "Style.Resources")
                .Elements().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "ItemsControlEx");

            Assert.Equal("504", Setter(list, "MaxHeight"));
        }

        private static XElement GameMenuScrollViewer() =>
            StyleOf("Views/GameMenu.xaml", "GameMenuBorder").Elements().Single(e => e.Name.LocalName == "Style.Resources")
                .Elements().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "ScrollViewer");

        private static XElement GameMenuItemsPanel() =>
            StyleOf("Views/GameMenu.xaml", "GameMenuBorder").Elements().Single(e => e.Name.LocalName == "Style.Resources")
                .Elements().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "ItemsControlEx")
                .Descendants().Single(e => e.Name.LocalName == "StackPanel");

        private static XElement GameMenuRowTemplate() =>
            StyleOf("Views/GameMenu.xaml", "GameMenuButton").Descendants().Single(e => e.Name.LocalName == "ControlTemplate");

        private static XElement GameMenuRowPart(string name) =>
            GameMenuRowTemplate().Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        // A Thickness as left, top, right, bottom, from one, two or four values.
        private static int[] Thickness(string value)
        {
            var parts = value.Split(',').Select(int.Parse).ToArray();
            return parts.Length == 1 ? new[] { parts[0], parts[0], parts[0], parts[0] }
                : parts.Length == 2 ? new[] { parts[0], parts[1], parts[0], parts[1] } : parts;
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
