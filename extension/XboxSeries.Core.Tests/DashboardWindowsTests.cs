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
        private static readonly string DashboardDir = Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard");
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "extension", "XboxSeries.Extension");

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
            { "Views/GameStatus.xaml", new[] { "PART_ViewHost", "PART_PanelActionButtons", "PART_ImageCover", "PART_TextStatus" } },
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
                    && e.Descendants().Any(g => ((string)g.Attribute("Style") ?? "").StartsWith("{DynamicResource XboxFocusGlow")
                                                && (string)g.Attribute("Style") != "{DynamicResource XboxFocusGlowCache}"))
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
        [InlineData("Views/MainMenu.xaml", "MainMenuBorder")]
        [InlineData("Views/HelpMenu.xaml", "HelpMenuBorder")]
        [InlineData("Views/NotificationsMenu.xaml", "NotificationsMenuBorder")]
        // The menus of game clients, software tools and extensions' commands draw theirs inside (A_console_menus_panel_is_450_wide).
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

        // The console's menus, which Playnite's game menu and its menus of game clients, software tools and extensions'
        // commands are drawn as (and the extension's MenuPanel.xaml): a 450 wide panel, rows 72 apart, the focused row's
        // 72 tall green ring standing 8 past the panel on either side round a fill the panel's full width. Playnite's
        // windows put the rows in a ScrollViewer, whose content presenter clips to itself, so the theme widens the scroll
        // area past the panel and insets the rows back: the ring and its glow are never clipped.
        private const int RingOverhang = 8;
        // How far the glow's blur reaches past the panel at the sides (the ring's 8, the band's 3.5 and the blur's 17,
        // with room to spare), and past the ring above and below, as rendered: it has faded out by 19.
        private const int GlowRoomSides = 33;
        private const int GlowRoomEnds = 20;
        // The first ink of a row's 22 icon and label, from either of its edges: a row scrolled out of the list shows
        // this much of itself in the band above or below before any of its icon or label shows.
        private const int RowInkInset = 22;

        // Each menu: its file, its rows' style and its panel's style.
        public static IEnumerable<object[]> ConsoleMenus => new[]
        {
            new object[] { "Views/GameMenu.xaml", "GameMenuButton", "GameMenuBorder" },
            new object[] { "Views/CustomMenus.xaml", "GameClientMenuButton", "GameClientsBorder" },
            new object[] { "Views/CustomMenus.xaml", "SoftwareToolMenuButton", "SoftwareToolsBorder" },
            new object[] { "Views/CustomMenus.xaml", "ExtensionMenuButton", "ExtensionsBorder" },
        };

        [Theory]
        [MemberData(nameof(ConsoleMenus))]
        public void A_console_menus_focus_ring_stands_8_past_the_panel_on_both_sides(string file, string rowKey, string borderKey)
        {
            var row = Thickness(Setter(Resolved(file, rowKey), "Margin"));
            var ring = Thickness((string)RowPart(file, rowKey, "FocusRing").Attribute("Margin") ?? "0");
            var panel = PanelInset(file, borderKey);

            Assert.Equal(RingOverhang, panel[0] - row[0] - ring[0]);
            Assert.Equal(RingOverhang, panel[2] - row[2] - ring[2]);
        }

        [Theory]
        [MemberData(nameof(ConsoleMenus))]
        public void A_console_menus_focus_ring_is_the_rows_full_72_with_its_glow(string file, string rowKey, string borderKey)
        {
            var style = Resolved(file, rowKey);
            var row = Thickness(Setter(style, "Margin") ?? "0");
            var ring = RowPart(file, rowKey, "FocusRing");
            var ringMargin = Thickness((string)ring.Attribute("Margin") ?? "0");

            Assert.Equal("72", Setter(style, "Height"));
            Assert.Equal(0, row[1] + row[3]);
            Assert.Equal(0, ringMargin[1] + ringMargin[3]);
            Assert.Equal("4", (string)ring.Attribute("BorderThickness"));
            Assert.Equal("7", (string)ring.Attribute("CornerRadius"));
            Assert.Equal("Hidden", (string)ring.Attribute("Visibility"));
            var cache = ring.Elements().Single();
            Assert.Equal("{DynamicResource XboxFocusGlowCache}", (string)cache.Attribute("Style"));
            Assert.Equal("{DynamicResource XboxFocusGlow}", (string)cache.Elements().Single().Attribute("Style"));
            Assert.True(ShownOnFocus(file, rowKey, "FocusRing"), $"{rowKey}: no ring on focus");
        }

        [Theory]
        [MemberData(nameof(ConsoleMenus))]
        public void A_console_menus_focused_row_is_filled_lighter_across_the_panels_full_width(string file, string rowKey, string borderKey)
        {
            var fill = RowPart(file, rowKey, "Fill");

            Assert.Equal("{DynamicResource XboxRowFocusBrush}", (string)fill.Attribute("Background"));
            // Its sides are the panel's, the ring's overhang in from the row's; its top and bottom 4 inside the ring's 4: 56 tall.
            Assert.Equal(new[] { RingOverhang, 8, RingOverhang, 8 }, Thickness((string)fill.Attribute("Margin")));
            Assert.Equal("Hidden", (string)fill.Attribute("Visibility"));
            Assert.True(ShownOnFocus(file, rowKey, "Fill"), $"{rowKey}: no fill on focus");
        }

        [Fact]
        public void The_focused_rows_fill_is_a_named_grey()
        {
            Assert.Equal("#FF404040", ColorOf("XboxRowFocusBrush"));
        }

        [Theory]
        [MemberData(nameof(ConsoleMenus))]
        public void A_console_menus_icons_centre_44_in_and_its_labels_start_88_in(string file, string rowKey, string borderKey)
        {
            var padding = Thickness(Setter(Resolved(file, rowKey), "Padding"));
            foreach (var icon in Icons(file, rowKey))
            {
                var width = int.Parse((string)icon.Attribute("Width"));
                var margin = Thickness((string)icon.Attribute("Margin"));

                // From the panel's edge, which is the ring's overhang in from the row's.
                Assert.Equal(44 * 2, 2 * (padding[0] - RingOverhang) + width);
                Assert.Equal(88, padding[0] - RingOverhang + width + margin[2]);
            }
        }

        [Theory]
        [MemberData(nameof(ConsoleMenus))]
        public void A_console_menus_labels_are_22(string file, string rowKey, string borderKey)
        {
            Assert.Contains(RowTemplate(file, rowKey).Descendants(),
                e => (string)e.Attribute("FontSize") == "22" || (string)e.Attribute("TextElement.FontSize") == "22");
        }

        [Fact]
        public void The_game_menus_icons_are_its_own_22_glyphs_not_a_shadow_of_the_shared_icon_style()
        {
            var doc = XDocument.Load(Path.Combine(DashboardDir, "Views", "GameMenu.xaml"));
            var own = StyleOf("Views/GameMenu.xaml", "GameMenuButton").Descendants()
                .SingleOrDefault(e => (string)e.Attribute(X + "Key") == "GameMenuIcon");

            Assert.DoesNotContain(doc.Descendants(), e => (string)e.Attribute(X + "Key") == "XboxMenuIcon");
            Assert.NotNull(own);
            Assert.Equal("22", Setter(own, "FontSize"));
            var uses = doc.Descendants().Where(e => ((string)e.Attribute("Style") ?? "").Contains("MenuIcon")).ToList();
            Assert.NotEmpty(uses);
            Assert.All(uses, e => Assert.Equal("{DynamicResource GameMenuIcon}", (string)e.Attribute("Style")));
        }

        [Theory]
        [MemberData(nameof(ConsoleMenus))]
        public void A_console_menus_panel_is_450_wide_with_corners_of_6(string file, string rowKey, string borderKey)
        {
            var drawn = DrawnPanel(file, borderKey);

            Assert.Equal(450, PanelWidth(file, rowKey, borderKey));
            Assert.Equal("6", drawn != null ? (string)drawn.Attribute("CornerRadius") : Setter(StyleOf(file, borderKey), "CornerRadius"));
            Assert.Equal("{DynamicResource XboxPanelBrush}", drawn != null ? (string)drawn.Attribute("Background") : Setter(StyleOf(file, borderKey), "Background"));
        }

        [Theory]
        [MemberData(nameof(ConsoleMenus))]
        public void A_console_menus_scroll_area_holds_all_of_the_glow_and_none_of_a_row_scrolled_out(string file, string rowKey, string borderKey)
        {
            Assert.DoesNotContain(XDocument.Load(Path.Combine(DashboardDir, file)).Descendants(), e => (string)e.Attribute("ClipToBounds") == "True"
                || ((string)e.Attribute("Property") == "ClipToBounds" && (string)e.Attribute("Value") == "True"));

            var panel = PanelInset(file, borderKey);
            var list = ItemsPanel(file, borderKey);
            var rows = Thickness((string)list.Attribute("Margin") ?? Setter(list, "Margin"));

            Assert.True(panel[0] >= GlowRoomSides && panel[2] >= GlowRoomSides, "room for the glow beside the ring");
            // The rows start and end where the panel's rows do: the scroll area's band above and below given back.
            Assert.Equal(panel[1], rows[1]);
            Assert.Equal(panel[3], rows[3]);
            // Room for the glow above the first row and below the last, but too little for a scrolled-out row's icon or
            // label to show over the title or past the panel's foot.
            Assert.InRange(rows[1], GlowRoomEnds, RowInkInset);
            Assert.InRange(rows[3], GlowRoomEnds, RowInkInset);
            Assert.Contains(ScrollViewerStyle(file, borderKey).Descendants(), e => e.Name.LocalName == "ScrollContentPresenter"
                && (string)e.Attribute(X + "Name") == "PART_ScrollContentPresenter");
        }

        [Theory]
        // Playnite's 648 high middle of the screen, less the game's name over the rows: 7 rows.
        [InlineData("Views/GameMenu.xaml", "GameMenuBorder", 504)]
        // Its 810 high middle, with no title: 11 rows.
        [InlineData("Views/CustomMenus.xaml", "GameClientsBorder", 792)]
        [InlineData("Views/CustomMenus.xaml", "SoftwareToolsBorder", 792)]
        [InlineData("Views/CustomMenus.xaml", "ExtensionsBorder", 792)]
        public void A_long_console_menu_scrolls_by_whole_rows_inside_the_panel(string file, string borderKey, int height)
        {
            var list = Resolved(file, borderKey).Elements().Single(e => e.Name.LocalName == "Style.Resources")
                .Elements().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "ItemsControlEx");

            Assert.Equal(height.ToString(), Setter(list, "MaxHeight"));
        }

        [Fact]
        public void The_game_menus_title_is_the_games_name_on_one_trimmed_line()
        {
            var title = StyleOf("Views/GameMenu.xaml", "GameMenuHeaderText");

            Assert.Equal("{DynamicResource XboxHeaderFont}", Setter(title, "FontFamily"));
            Assert.Equal("33", Setter(title, "FontSize"));
            Assert.Equal("28,36,28,24", Setter(title, "Margin"));
            Assert.Equal("NoWrap", Setter(title, "TextWrapping"));
            Assert.Equal("CharacterEllipsis", Setter(title, "TextTrimming"));
            // A long name is trimmed to the panel rather than widening it: the panel less the title's 28 each side.
            Assert.Equal("394", Setter(title, "MaxWidth"));
        }

        // A menu's panel's edges in from the scroll area's: left, top, right, bottom. The game menu's panel is its Border,
        // the scroll area reaching past it; Playnite fixes the other menus' Border at 500 wide, so their 450 panel is drawn
        // inside the scroll area, behind the rows.
        private static int[] PanelInset(string file, string borderKey)
        {
            var drawn = DrawnPanel(file, borderKey);
            return drawn != null ? Thickness((string)drawn.Attribute("Margin"))
                : Thickness(Setter(ScrollViewerStyle(file, borderKey), "Margin")).Select(m => -m).ToArray();
        }

        private static XElement DrawnPanel(string file, string borderKey) =>
            ScrollViewerStyle(file, borderKey).Descendants().SingleOrDefault(e => (string)e.Attribute(X + "Name") == "Panel");

        private static int PanelWidth(string file, string rowKey, string borderKey)
        {
            var scroll = Thickness(Setter(ScrollViewerStyle(file, borderKey), "Margin"));
            var drawn = DrawnPanel(file, borderKey);
            if (drawn != null)
            {
                // Playnite's Border is 500 wide; the scroll area reaches past it and the panel is inset in that.
                var inset = Thickness((string)drawn.Attribute("Margin"));
                return 500 - scroll[0] - scroll[2] - inset[0] - inset[2];
            }
            // The Border is as wide as its rows: the row's least width and its margins, less the scroll area's reach.
            var style = Resolved(file, rowKey);
            var row = Thickness(Setter(style, "Margin"));
            return int.Parse(Setter(style, "MinWidth")) + row[0] + row[2] + scroll[0] + scroll[2];
        }

        private static XElement ScrollViewerStyle(string file, string borderKey) =>
            Resolved(file, borderKey).Elements().Single(e => e.Name.LocalName == "Style.Resources")
                .Elements().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "ScrollViewer");

        // What insets the rows: the game menu's own StackPanel, or, where Playnite sets the rows' panel itself, an implicit
        // style on the presenter in Playnite's list template.
        private static XElement ItemsPanel(string file, string borderKey)
        {
            var resources = Resolved(file, borderKey).Elements().Single(e => e.Name.LocalName == "Style.Resources");
            return resources.Descendants().SingleOrDefault(e => e.Name.LocalName == "Style" && (string)e.Attribute("TargetType") == "ItemsPresenter")
                ?? resources.Descendants().Single(e => e.Name.LocalName == "StackPanel");
        }

        // The icons of a menu's rows, each with its Width and Margin: the game menu's icon style, or each content
        // template's 24 wide icon.
        private static IEnumerable<XElement> Icons(string file, string rowKey)
        {
            var style = StyleOf(file, rowKey);
            var own = style.Descendants().SingleOrDefault(e => (string)e.Attribute(X + "Key") == "GameMenuIcon");
            if (own != null)
            {
                return new[] { new XElement("Icon", new XAttribute("Width", Setter(own, "Width")), new XAttribute("Margin", Setter(own, "Margin"))) };
            }
            var icons = style.Descendants().Where(e => (string)e.Attribute("Width") == "24" && e.Attribute("Margin") != null).ToList();
            Assert.NotEmpty(icons);
            return icons;
        }

        private static bool ShownOnFocus(string file, string rowKey, string part) =>
            RowTemplate(file, rowKey).Descendants().Any(s => s.Name.LocalName == "Setter"
                && (string)s.Attribute("TargetName") == part && (string)s.Attribute("Value") == "Visible"
                && (string)s.Parent.Attribute("Property") == "IsFocused");

        // A style with the setters of the style it's based on in the same file under its own.
        private static XElement Resolved(string file, string key)
        {
            var style = StyleOf(file, key);
            var basedOn = (string)style.Attribute("BasedOn") ?? "";
            if (!basedOn.StartsWith("{StaticResource Xbox"))
            {
                return style;
            }
            var parent = Resolved(file, basedOn.Substring("{StaticResource ".Length).TrimEnd('}'));
            var own = style.Elements().Where(s => s.Name.LocalName == "Setter").Select(s => (string)s.Attribute("Property")).ToList();
            var resources = style.Elements().Any(e => e.Name.LocalName == "Style.Resources")
                ? Enumerable.Empty<XElement>() : parent.Elements().Where(e => e.Name.LocalName == "Style.Resources");
            return new XElement(style.Name, style.Attributes(),
                parent.Elements().Where(s => s.Name.LocalName == "Setter" && !own.Contains((string)s.Attribute("Property"))),
                resources, style.Elements());
        }

        private static XElement RowTemplate(string file, string rowKey) =>
            Resolved(file, rowKey).Elements().Single(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == "Template")
                .Descendants().Single(e => e.Name.LocalName == "ControlTemplate");

        private static XElement RowPart(string file, string rowKey, string name) =>
            RowTemplate(file, rowKey).Descendants().Single(e => (string)e.Attribute(X + "Name") == name);
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
