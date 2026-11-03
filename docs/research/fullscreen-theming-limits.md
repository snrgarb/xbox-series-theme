# Limits of Playnite 10 Fullscreen theming

Research for [#6](https://github.com/snrgarb/xbox-series-theme/issues/6) (part of #1). Researched 2026-10-13.

**Baseline:** Playnite `master` @ [`ecee014`](https://github.com/JosefNemec/Playnite/tree/ecee01452a25db1fb2674a69f883d298d4c7be8a), which is the tagged **10.62** release (2026-09-29). Fullscreen Theme API version **2.10.0**. All source links below are pinned to that commit. `PN` = `https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source`.

## TL;DR

- A Fullscreen theme is **only a set of WPF `ResourceDictionary` overrides**. It can't add C# code-behind, view models, or windows. It can replace the `ControlTemplate` of every *themeable* view. The **main view (`Views/Main.xaml`) is fully re-templatable**: you can lay the whole screen out however you like.
- The hard limits are in the **compiled code**, not in the XAML:
  1. **`PART_ListGameItems` always uses Playnite's `FullscreenTilePanel` grid and `GameListItem` template.** `Main.OnApplyTemplate` overwrites `ItemsPanel` and `ItemTemplate`, so you can't make the main list a free-form row or carousel. You can only set rows, columns, orientation and spacing through user settings, plus the item's visuals.
  2. **The child windows** (main menu, game menu, settings, notifications, message box, item pickers, text input, and so on) are compiled XAML. Their structure is fixed, and you can restyle them only through the keyed styles and templates they reference.
  3. **The settings section pages** are compiled `UserControl`s, so you can only restyle their controls.
  4. **Behaviour is fixed:** controller and keyboard bindings, which commands exist (`ActivateSelectedCommand` acts only on `SelectedGame`), and when details, filters and status show (code-driven `Visibility`).
- **Every `PART_` element is optional.** Code looks each one up with `Template.FindName(...) as T` and null-checks it. If you leave one out, you lose its behaviour (bindings and commands) but nothing crashes. The common trick is to keep a `PART_` but hide it (`Visibility="Collapsed"`, or `Width="0" Height="0" Opacity="0"`).
- **The data you can bind to is broad.** The `DataContext` is `FullscreenAppViewModel`: the full filtered/sorted game collection, `SelectedGame` (with playtime, last activity, cover/background/icon, install state and every metadata field), filter presets, the clock, battery, progress, and `{Settings ...}` for every user setting. But **there is no built-in "recently played" collection**. You have to derive it in XAML (for example a `CollectionViewSource` sorted by `LastActivity`) or get it from a plugin.
- **Prior art confirms the ceiling.** The most dashboard-like themes either (a) **hide `PART_ListGameItems` and draw their own lists** bound to `GamesView.CollectionView` and `SelectedGame` (Aniki-ReMake), or (b) **ship a companion C# plugin** that injects custom controls, such as a carousel, or exposes extra data like recent games and commands (Aniki-ReMake with *Aniki Helper*, and *Xbox 360 NXE Dashboard* with its *NXE Game List* extension). Pure-XAML Xbox Series themes (XBOXSERIESish) stay within the tile grid and use `SelectedIndex` DataTrigger hacks.

**What this means for this project:** an Xbox Series X|S home screen made only of XAML is feasible for the shell: top bar, background art, a pinned or recent row, and a "My games & apps" grid. The parts that need either a theme-built secondary `ListBox` (with caveats) or a companion plugin are a true "recent games" row with launch-in-place, multiple independent horizontal rows, and anything that needs new commands. Guide-style menus and the settings screen can only be restyled, not re-architected.

---

## 1. How a theme is loaded, and what that implies

`ThemeManager.ApplyTheme` ([PN/Playnite/Themes.cs#L112-L208](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Themes.cs#L112-L208)) works like this:

- It builds the list of *acceptable* XAML files from the default theme's merged dictionaries, which are the ones listed in [`App.xaml`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/App.xaml).
- For each acceptable file, it loads `Default/<file>` and then `<theme>/<file>` if that exists. Theme keys therefore override default keys, and anything the theme omits falls back to the default.
- **A file at any other path is ignored** at the top level, and so is a file that isn't a `ResourceDictionary`. You can still pull in extra dictionaries from inside an acceptable file with `<ResourceDictionary Source="{ThemeFile 'Views/Extra/Foo.xaml'}"/>`. Aniki-ReMake does this for about 30 extra view files ([Main.xaml L5-L38](https://github.com/Mike-Aniki/Aniki-ReMake/blob/main/Source/Views/Main.xaml#L5-L38)).
- Version gate: the theme's `ThemeApiVersion` must have the **same major version (2)** and be ≤ 2.10.0 ([Themes.cs#L101-L110](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Themes.cs#L101-L110)).
- Non-XAML hooks: `cursor.cur`/`cursor.ani` ([Themes.cs#L148-L164](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Themes.cs#L148-L164)); `audio/navigation.*`, `audio/activation.*` and `audio/background.*` ([FullscreenApplication.cs#L400-L428](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/FullscreenApplication.cs#L400-L428)); `DescriptionView.html` for the HTML game description ([GameDetails.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/GameDetails.cs)); and theme localization files ([Themes.cs#L210-L218](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Themes.cs#L210-L218)).
- Button prompt glyph sets (`Images/ButtonPrompts/{Xbox,PlayStation}`) are resolved from the **default** theme's directory ([Themes.cs#L54-L91](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Themes.cs#L54-L91)). The theme can still override the `ButtonPrompt*` resource keys.
- `GlobalResources.xaml`, `HiddenStyles.xaml` and `ControlGalleryView.xaml` live outside `Themes/Fullscreen/Default/`, so they are **not** overridable.

Docs: [Introduction to themes](https://api.playnite.link/docs/tutorials/themes/introduction.html) (the folder roles: DefaultControls, DerivedStyles, CustomControls, Views, Constants, Media) and [Manual editing](https://api.playnite.link/docs/tutorials/themes/manualEditing.html) ("copy original theme file… keep the directory structure"). Neither page documents `PART_` rules. Those come only from source.

## 2. Inventory: the Fullscreen theme template files

The overridable set is exactly the files under [`Themes/Fullscreen/Default/`](https://github.com/JosefNemec/Playnite/tree/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Themes/Fullscreen/Default) referenced from `App.xaml`, plus `theme.yaml`, `DescriptionView.html`, images and audio:

| Group | Files | What they govern |
|---|---|---|
| Root | `Constants.xaml`, `Media.xaml` | Brushes, colours, font sizes, `ChildWindowStyle`, `MainWindowStyle`, icons and glyphs |
| Button prompts | `Images/ButtonPrompts/Xbox/Xbox.xaml` (and PlayStation) | `ButtonPromptA/B/X/Y/LB/RS/Start…` resources |
| DefaultControls | `Button`, `CheckBox`, `ComboBox`, `ProgressBar`, `ScrollViewer`, `Slider`, `TextBox`, `ToggleButton`, `ToolTip` | Implicit styles for the standard controls used everywhere, including in compiled windows and settings pages |
| CustomControls | `FilterDbItemtSelection`, `FilterEnumListSelection`, `FilterStringListSelection`, `FilterPresetSelector` | Filter sub-pickers and the filter-preset strip (tabs) |
| DerivedStyles | `ListGameItemStyle` (the `ListBoxItem` container), `ListGameItemTemplate` (the `GameListItem` tile), `ButtonTopMenu`, `ButtonBottomMenu` | The game tile and the top/bottom bar buttons |
| Views | `Main`, `GameDetails`, `GameStatus`, `FiltersView`, `FiltersAdditional`, `GameMenu`, `MainMenu`, `HelpMenu`, `NotificationsMenu`, `SettingsMenus`, `MessageBox`, `TextInput`, `ItemSelection`, `CustomMenus` | See §3 and §4 |

The Toolbox generates a new theme by copying the default theme ([Tools/Playnite.Toolbox/Themes.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Tools/Playnite.Toolbox/Themes.cs)). Stale files that the current default no longer has are silently ignored. For example, XBOXSERIESish still ships `Views/ActionSelection.xaml`.

## 3. Views you can re-template: `Control` subclasses with `PART_`s

These are custom `Control`s whose whole `ControlTemplate` comes from the theme. Their code-behind only looks up named parts. The list below comes from the `[TemplatePart]` attributes in [`Controls/`](https://github.com/JosefNemec/Playnite/tree/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls).

### 3.1 `Main` (Views/Main.xaml): the library or home screen

[`Controls/Views/Main.cs`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/Main.cs). It is hosted in `MainWindow` inside a `Viewbox` ([MainWindow.xaml](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Windows/MainWindow.xaml)), so you design at a fixed canvas and it scales. `DataContext` = `FullscreenAppViewModel`.

| PART | Type | What code does to it |
|---|---|---|
| `PART_ViewHost` | FrameworkElement | Gets global input bindings: Back → main menu, LB/RB → previous/next filter preset, Y → search, RS → filters, LS → preset picker, plus F-keys ([L236-L254](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/Main.cs#L236-L254)) |
| `PART_MainHost` | FrameworkElement | Width/Height bound to the viewport |
| `PART_ListGameItems` | ListBox | **ItemsPanel is forced to `FullscreenTilePanel`** (Rows/Columns/HorizontalLayout/ItemSpacing/SmoothScrolling from settings), **ItemTemplate is forced to `GameListItem` styled by `ListGameItemTemplate`**, the container style is `ListGameItemStyle`, `ItemsSource = GamesView.CollectionView`, `SelectedItem ⇄ SelectedGame`, and A/X/Start/Enter bindings ([L366-L407](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/Main.cs#L366-L407)) |
| `PART_ImageBackground` | FadeImage | Source = `SelectedGame.DisplayBackgroundImageObject`; blur and darkening from settings |
| `PART_ButtonPlay` / `PART_ButtonInstall` / `PART_ButtonDetails` / `PART_ButtonGameOptions` / `PART_ButtonSearch` / `PART_ButtonFilter` | ButtonEx | Command, visibility and input-hint glyph wired up |
| `PART_ButtonMainMenu` / `PART_ButtonNotifications` / `PART_ButtonProgramUpdate` | ButtonBase | Commands, plus update visibility |
| `PART_TextClock`, `PART_TextBatteryPercentage`, `PART_ElemBatteryStatus` | TextBlock / FE | Text and visibility from `CurrentTime`, `PowerStatus` and settings |
| `PART_TextProgressTooltip`, `PART_ElemProgressIndicator` | TextBlock / FE | Library-update progress |
| `PART_ElemSearchActive`, `PART_ElemExtraFilterActive` | FE | Visibility indicators |
| `PART_ElemFilters`, `PART_ElemFiltersAdditional`, `PART_ContentFilterItems` | `Filters`, `FiltersAdditional`, ContentControl | Filter panels. Visibility is code-driven and the sub-filter content is injected |
| `PART_ElemGameDetails` | `GameDetails` | Visibility = `GameDetailsVisible`; DataContext = `SelectedGame` |
| `PART_ElemGameStatus` | `GameStatus` | The "launching/running" overlay |
| `PART_FilterPresetSelector` | `FilterPresetSelector` | The filter-preset strip, built in code from `SortedFilterFullscreenPresets`, with each item → `ApplyFilterPresetCommand` |

Plugin elements: `ControlTemplateTools.InitializePluginControls` runs on the Main template, on `GameDetails` and on each `GameListItem` ([Main.cs#L523-L529](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/Main.cs#L523-L529); [GameListItem.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/GameListItem.cs)). Any `ContentControl x:Name="<Source>_<Element>"` gets a plugin's custom control. See the docs page [Extension integration](https://api.playnite.link/docs/tutorials/themes/extensionIntegration.html).

### 3.2 Other re-templatable controls

| Control (file) | PARTs |
|---|---|
| `GameDetails` (Views/GameDetails.xaml) | `PART_ViewHost` (B/Back → close, LB/RB → previous/next game), `PART_ButtonContext` (Play/Install, with content and command bound), `PART_ButtonOptions`, `PART_ImageCover`, `PART_ImageBackground`, `PART_HtmlDescription` (rendered via `DescriptionView.html`), `PART_ScrollHtmlDescription` ([GameDetails.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/GameDetails.cs)) |
| `GameStatus` (Views/GameStatus.xaml) | `PART_ViewHost`, `PART_PanelActionButtons`, `PART_TextStatus`, `PART_ImageCover` |
| `GameListItem` (DerivedStyles/ListGameItemTemplate.xaml) | `PART_ImageCover` (source = `FullscreenListItemCoverObject` with a default-cover fallback). Everything else in the tile is free, since DataContext = `GamesCollectionViewEntry` |
| `Filters` (Views/FiltersView.xaml) | `PART_ItemsHost` |
| `FiltersAdditional` | `PART_MenuHost`, `PART_ButtonBack`, `PART_ItemsHost` |
| `FilterDbItemtSelection` / `FilterEnumListSelection` / `FilterStringListSelection` | `PART_MenuHost`, `PART_ButtonBack`, `PART_ButtonClear`, `PART_ItemsHost` |
| `FilterPresetSelector` | `PART_ItemsFilterPresets` |

**`PART_` contract:** every lookup is `Template.FindName("PART_x", this) as T` followed by a null check (for example [Main.cs#L235-L519](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/Main.cs#L235-L519)). So:

- Omitting a part is safe, but you lose what code wires into it. If you omit `PART_ViewHost`, you lose the global controller shortcuts. If you omit `PART_ListGameItems`, nothing sets `SelectedGame` and the A/X bindings disappear.
- The **type matters**. Something that isn't a `ButtonEx` named `PART_ButtonPlay` fails the `as` cast and is treated as missing.
- Code **overwrites** some properties on parts (`ItemsPanel`, `ItemTemplate`, `ItemContainerStyle`, `Visibility`, `Command`, `DataContext`, `Source`). Values you set on those properties in XAML are lost. A `Style` trigger can't beat a local value or binding that code has set.

## 4. Views you can only restyle (compiled windows and pages)

These are `Window`s and `UserControl`s compiled into `Playnite.FullscreenApp.dll` ([Windows/](https://github.com/JosefNemec/Playnite/tree/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Windows), [Controls/SettingsSections/](https://github.com/JosefNemec/Playnite/tree/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/SettingsSections)). Their element tree is fixed. The theme controls only the resource keys they reference:

| Window | Theme hooks (resource keys) |
|---|---|
| MainMenuWindow (Guide-like menu) | `ChildWindowStyle`, `MainMenuBorder`, `MainMenuButton`, `MainMenuHeaderTemplate`, and one `ContentTemplate` per item (`MainMenuSettingsButtonTemplate`, `MainMenuShutdowButtonTemplate`, …) |
| GameMenuWindow | `GameMenuBorder`, `GameMenuButton`, `GameMenuHeaderTemplate` |
| SettingsWindow | `SettingsMenuBorder`, `SettingsMenuButton`, `SettingsMenuHeaderTemplate`, `SettingsMenu*ButtonTemplate`, `SettingsSectionBorder` |
| NotificationsWindow | `NotificationsMenuBorder`, `NotificationsMenuItemButton`, `NotificationsMenuDismissButton`, `NotificationsMenuHeaderText` |
| MessageBoxWindow | `MessageBoxBorder`, `MessageBoxButton`, `MessageBoxCaptionText`, `MessageBoxMessageText` |
| Single/MultiItemSelectionWindow | `ItemSelectionBorder`, `ItemSelectionButton`, `ItemSelectionCheckBox`, `ItemSelectionHeaderText`, footer styles |
| TextInputWindow (on-screen keyboard) | `TextInputKeysBorder`, `TextInputOptionsBorder`, `TextInputButton`, `TextInputTextbox`, `TextInputCheckbox` |
| HelpMenu / Extensions / GameClients / SoftwareTools windows | `*Border` and `*Button` keys |
| Progress, RandomGameSelect, Update, AddonsUpdate, Crash, License windows | Generic keys only (`ChildWindowStyle`, `MessageBoxButton`, `TextBlockBaseStyle`, brushes) |

Implications:

- **Layout inside these windows is mostly fixed.** For example, MainMenuWindow is a 1920×1080 `Grid` with the menu in a right-hand `Auto` column, holding a vertical `StackPanel` of `ButtonEx` in a hard-coded order ([MainMenuWindow.xaml](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Windows/MainMenuWindow.xaml)). Because `MainMenuBorder` is a `Style` on the host `Border`, it can set margins, alignment, size, background, `RenderTransform`, and even attached `Grid.Column`/`Grid.ColumnSpan` setters. That allows a *left-docked Xbox Guide-style* panel, but not new items or a reordered list. Item visibility follows user settings (`MainMenuShow*`).
- `ChildWindowStyle` (in `Constants.xaml`/`Media.xaml`) controls the overlay window chrome, background and dimming for all child windows.
- **The Settings pages** (General, Visuals, Audio, Layout, Menus, Input) are compiled `UserControl`s. Only the implicit DefaultControls styles (CheckBox, Slider, ComboBox, …) reach them.

## 5. Data available to bindings

`DataContext` in `Main` = [`FullscreenAppViewModel`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs), which extends [`MainViewModelBase`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/ViewModels/MainViewModelBase.cs).

- **Game collections:** `GamesView.CollectionView` (`ListCollectionView`, the currently filtered and sorted library) and `GamesView.Items` (`RangeObservableCollection<GamesCollectionViewEntry>`) ([GamesCollectionView.cs#L19-L31](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/GamesCollectionView.cs#L19-L31)). `Database` (`IGameDatabaseMain`, raw SDK `Game` objects) is also exposed.
- **Selection:** `SelectedGame` (`GamesCollectionViewEntry`, settable), `SelectedGameDetails`, `GameDetailsVisible`, `GameListVisible`, `GameStatusVisible`, `FilterPanelVisible`, `IsSearchActive`, `IsExtraFilterActive`.
- **Per-game fields** ([GamesCollectionViewEntry.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/GamesCollectionViewEntry.cs)): `DisplayName`, `Playtime`, `PlayCount`, `LastActivity`, `RecentActivity`, `Added`, `IsInstalled`/`IsRunning`/`IsLaunching`, `CoverImageObject`, `DisplayBackgroundImageObject`, `IconObject`, `FullscreenListItemCoverObject`, `Platforms`, `Genres`, `Developers`, `Publishers`, `ReleaseYear`, `Description`, `CompletionStatus`, `UserScore`/`CriticScore`/`CommunityScore`, `InstallSize`, `Source`, `Favorite`, `Tags`, `Features`, `Series`, and grouping helpers (`PlaytimeCategory`, `LastActivitySegment`, …).
- **Filter presets:** `SortedFilterFullscreenPresets`, `ActiveFilterPreset`, `ApplyFilterPresetCommand` (parameter = preset). These are the natural mechanism for Xbox-style "pivots" such as *Recently played / Installed / Full library*, because a preset includes a sort order.
- **System:** `CurrentTime.Time`, `PowerStatus.{PercentCharge,Charge,IsCharging}`, `ProgressActive`/`ProgressStatus`, `UpdatesAvailable`.
- **Commands** ([FullscreenAppViewModel_Commands.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel_Commands.cs)): `ActivateSelectedCommand`, `ToggleGameDetailsCommand`, `OpenGameMenuCommand`, `OpenMainMenuCommand`, `OpenSearchCommand`, `ToggleFiltersCommand`, `Next/PrevFilterViewCommand`, `SelectNext/PrevGameCommand`, `SelectRandomGameCommand`, `SelectFilterPresetCommand`, `OpenNotificationsMenuCommand`, `SwitchToDesktopCommand`, `MinimizeCommand`, `ClearFiltersCommand`. **No command takes a game as a parameter.** Launching always acts on `SelectedGame`.
- **Markup extensions** ([Markup/Markups.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Markup/Markups.cs)): `{Settings …}`/`{SettingsBinding …}` (any `PlayniteSettings` path, such as `Fullscreen.Rows`, `Fullscreen.ShowClock` or `CoverAspectRatio`), `{ThemeFile …}`/`{ThemeFileBinding …}` (theme assets), `{PluginSettings Plugin=…, Path=…}`, `{PluginStatus …}`, `{PluginConverter …}`, `{Api …}` and `{MainViewModel …}`.
- **User layout settings that the theme can't override** but can read: `Fullscreen.Rows`, `Columns`, `HorizontalLayout`, `ShowGameTitles`, `EnableMainBackgroundImage`, blur and darken amounts, `CoverAspectRatio`, `FontSize`, `ButtonPrompts` ([FullscreenSettings.cs](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Settings/FullscreenSettings.cs)). Prior-art themes respond by documenting "required settings". DashX says "Rows: 3, 1:1 square covers" in its [README](https://github.com/saVantCZ/DashX).

**"Recent games" specifically:** nothing provides it out of the box. There are three options:

1. **Make "Recently played" a filter preset** sorted by last activity, and show it in the main tile list. This is fully supported, but it occupies the one main list.
2. **Add a theme-built secondary `ListBox`**: a `CollectionViewSource` over `GamesView.Items` sorted by `LastActivity` descending, with `SelectedItem="{Binding SelectedGame}"`, so that the existing Play/Details commands act on it. Aniki-ReMake proves the binding pattern works for a second list (`PART_ListGameItemsDetailed`, `ItemsSource="{Binding GamesView.CollectionView}" SelectedItem="{Binding SelectedGame}"`, [Main.xaml L10365](https://github.com/Mike-Aniki/Aniki-ReMake/blob/main/Source/Views/Main.xaml#L10365)). It has caveats:
   - **Not tested here:** whether a sorted CVS over `Items` plays well with `SelectedGame` when the game is filtered out of `CollectionView`. `LastValidSelectedGameIndex` uses `CollectionView.IndexOf` ([FullscreenAppViewModel.cs#L156-L181](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs#L156-L181)).
   - You'd have to add your own A/X `InputBindings` on that list in XAML.
   - XAML alone can't cap the list at N items.
3. **Use a companion plugin** that exposes a list through `PluginSettings`. This is what Aniki Helper does (`HubMostPlayedPlaytime`, `HubRecentAdded*`, `HubLibraryRecommendedGames`, …).

## 6. Prior art: Fullscreen themes that depart from the default layout

Source: the official add-on index [`PlayniteAddonDatabase/addons/themes_fullscreen`](https://github.com/JosefNemec/PlayniteAddonDatabase/tree/master/addons/themes_fullscreen), which lists 39 themes.

| Theme | Approach | Notes |
|---|---|---|
| [Aniki-ReMake](https://github.com/Mike-Aniki/Aniki-ReMake) (Mike Aniki, ~159★, active Sept 2026) | XAML **and** a companion plugin, [Aniki Helper](https://github.com/Mike-Aniki/AnikiHelper) | The most console-dashboard-like theme. Its `Views/Main.xaml` is about 16k lines and has a "Welcome Hub" with multiple rows fed by `{PluginSettings Plugin=AnikiHelper, Path=Hub…Items}`. It keeps `PART_ListGameItems`, adds `PART_ListGameItemsDetailed` bound to `GamesView.CollectionView` and `SelectedGame`, and loads about 30 extra dictionaries via `{ThemeFile}`. It binds into many other plugins: PlayniteAchievements, UniPlaySong, ExtraMetadataLoader, NowPlaying, and others. Its README says the plugin enables features "themes alone cannot provide" |
| [Xbox 360 NXE Dashboard](https://github.com/Neon-Silicon/Xbox_360_Playnite_Theme) (Neon Silicon) | XAML **and** a required extension | `Main.xaml` **hides `PART_ListGameItems`** (`Width="0" Height="0" Opacity="0"`) and collapses the Play/Details/Search buttons. In their place it puts `<ContentControl x:Name="NXEGameList_GameCarousel">`, a C# carousel the plugin registers via `AddCustomElementSupport`. This is a clear example of the tile-panel limit being worked around with code |
| [XBOXSERIESish](https://github.com/davidkgriggs/XBOXSERIESish) (DavidKGriggs, ~27★, last pushed 2022, API 2.1.0) | Pure XAML | An Xbox Series look inside the stock tile grid. It uses `DataTrigger`s on `PART_ListGameItems.SelectedIndex` combined with `{Settings Fullscreen.Columns}` to fake a "first tile is big" layout. This is fragile, and it hard-depends on the user's column setting |
| [DashX](https://github.com/saVantCZ/DashX) / [XBOXreborn](https://github.com/saVantCZ/XBOXreborn) (saVantCZ) | XAML plus required plugins | Inspired by the Xbox Series dashboard. The repos publish only packed releases, with no XAML source. DashX's README requires the *Theme Options* plugin, ExtraMetadata, SuccessStory and SteamScreenshots, and specific Rows and cover settings |
| [ReskinXBOXSX](https://github.com/TheKersalMassive/ReskinXBOXSX) / ReXBOXSX_Ext | Reskin | Packed only. Described as a reskin of the default layout |
| Others in the index (PS5ish, PS5Like, PS4Like, NintendoSwitch-ish, Solaris, AllyNite, …) | Mostly XAML | Console-styled, but they generally keep the single tile list as the core |

Observed pattern: **pure-XAML themes reskin the tile grid. Themes with real dashboard layouts (several rows, carousels, a hub) ship or require a plugin.**

## 7. Conclusions for the Xbox Series X|S theme

You can do these in XAML only, within the limits:

- A full custom layout of the home screen: top bar with clock, battery and notifications; a hero background (`PART_ImageBackground` or your own `Image` bound to `SelectedGame.DisplayBackgroundImageObject`); selected-game title and metadata; and a bottom hint bar.
- Xbox-style tile visuals. The `GameListItem` template is free apart from `PART_ImageCover`, so you can add a focus ring, scale-on-focus animations, titles, and install/running badges.
- A single horizontal tile row: user settings `Rows=1`, `HorizontalLayout=true`. Users must set this themselves; the theme can only detect it and adapt.
- Filter presets styled as the Xbox pivot tabs, with LB/RB already bound to previous/next preset.
- Restyled Guide (main menu), game menu, settings, notifications and dialogs. Their structure stays fixed, but they can be repositioned and fully reskinned.

These need a secondary theme-built list (feasible but must be tested) or a companion plugin:

- A distinct "Jump back in" recent row alongside a full-library grid.
- Several independent horizontal rows with independent focus.
- Pinned items, "apps" tiles, or any action Playnite has no command for.
- Capping row length, and any computed data such as "most played".

You can't do these at all:

- Change the main list's panel or virtualization (it is always `FullscreenTilePanel`).
- Add or reorder items in compiled menus.
- Change the settings pages' content.
- Change controller button mappings, or add new global shortcuts beyond the XAML `InputBindings` you attach to your own elements.
- Ship code inside a theme.

**Recommended next step:** decide early whether this project accepts a companion plugin. If it doesn't, base the home screen on one main tile row (filter-preset pivots) plus a prototype of a CVS-backed "recent" `ListBox`, and validate its selection, focus and launch behaviour on a real install before committing to the design.
