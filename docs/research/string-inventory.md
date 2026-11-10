# String inventory: what the Bundle shows the user

Every string a user can see in the **Bundle** (the **Dashboard** theme under `theme/XboxSeries.Dashboard` and the extension under `extension/XboxSeries.Extension` and `extension/XboxSeries.Core`), grouped by how it is built. Taken from `main` at 7e05668 (theme 0.2.2, extension 0.2.2). The Boot shell (`bootshell/`) is out of scope. This is groundwork for translation decisions: it records what exists and does not propose a design.

## How it was gathered

- XAML: `Text=`, `Content=`, `Header=`, `Title=`, `Tag=` holding labels, `ToolTip`, `AutomationProperties.*`, `<Run>`, `Setter Property="Text"`, `StringFormat` and `{DynamicResource LOC…}`.
- C#: every string literal in `XboxSeries.Core` and `XboxSeries.Extension`, traced to where it ends up (Guide items, Dialog options, Toasts, Idle cards, Settings labels, Console keyboard titles, status lines), and the formatting helpers (sizes, speeds, percentages, relative times, clock).
- Images and video: `shared/Images`, the extension's `Videos/` and `DiscordPortalQr.png`.
- Icon-only text (Segoe MDL2 code points such as `&#xE713;`) is left out: it is a glyph, not words. Bindings that only pass user or library data through (game names, Gamertags, filter preset names, Discord names) are left out unless the Bundle formats or wraps them.
- One row can hold a set of sibling strings from the same spot (written `a / b / c`); a row's count is one.

## Classes

| Class | Meaning |
|---|---|
| 1 fixed | a literal shown as is |
| 2 value in | a literal with a value put in, such as `Uninstall {game}?` |
| 3 counted | carries a number, date, time, size or percentage, so plural and culture formatting matter |
| 4 assembled | a sentence built from pieces around a controller glyph or icon, or across several TextBlocks or Runs |
| 5 image/video | text drawn into an image or video |
| 6 Playnite LOC | a string Playnite already translates (`{DynamicResource LOC…}`, `ResourceProvider.GetString("LOC…")`, Playnite's converters) |

## Summary

Counts are rows of the full list below.

| Area | 1 fixed | 2 value in | 3 counted | 4 assembled | 5 image/video | 6 Playnite LOC | Total |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dashboard: Home (theme-only parts and top bar) | 3 | 0 | 1 | 0 | 0 | 2 | 6 |
| Dashboard: Game list tiles | 2 | 0 | 0 | 0 | 0 | 0 | 2 |
| Dashboard: Game hub | 18 | 0 | 2 | 2 | 0 | 4 | 26 |
| Dashboard: Main menu and Help menu | 1 | 0 | 0 | 0 | 0 | 23 | 24 |
| Dashboard: Playnite's Settings menus (theme-only) | 4 | 0 | 0 | 0 | 0 | 8 | 12 |
| Dashboard: Filters | 0 | 0 | 0 | 0 | 0 | 4 | 4 |
| Home (extension) | 14 | 0 | 2 | 2 | 0 | 0 | 18 |
| My games & apps | 26 | 2 | 9 | 2 | 0 | 0 | 39 |
| Search | 4 | 0 | 0 | 0 | 0 | 0 | 4 |
| Tile menu and dialogs | 33 | 5 | 0 | 0 | 0 | 0 | 38 |
| Guide | 43 | 4 | 4 | 2 | 0 | 0 | 53 |
| Toasts | 6 | 3 | 4 | 2 | 0 | 0 | 15 |
| Idle screen | 13 | 1 | 5 | 6 | 0 | 0 | 25 |
| Settings (extension) | 65 | 6 | 2 | 0 | 0 | 1 | 74 |
| Start-up and Launch screen | 2 | 0 | 0 | 0 | 0 | 0 | 2 |
| Status area | 0 | 0 | 2 | 0 | 0 | 0 | 2 |
| Console keyboard | 4 | 0 | 0 | 0 | 0 | 0 | 4 |
| Error dialogs (Playnite message boxes) | 0 | 1 | 0 | 0 | 0 | 2 | 3 |
| Window titles (not normally on screen) | 5 | 0 | 0 | 0 | 0 | 0 | 5 |
| Add-on manifests (Playnite's add-on lists) | 3 | 0 | 0 | 0 | 0 | 0 | 3 |
| Images and video | 0 | 0 | 0 | 0 | 3 | 0 | 3 |
| **All** | **246** | **22** | **31** | **16** | **3** | **44** | **362** |

The Bundle has no translation layer of its own today: there is no `.resx`, no `Localization` folder and no `XmlLanguage`/`LanguageProperty` override (the one `CurrentUICulture` use only measures text in `AchievementToast.xaml.cs:88`). Only class 6 changes with Playnite's language.

### Accessibility, tooltips and other surfaces

- **Accessibility text**: none. No `AutomationProperties.Name`/`HelpText` anywhere in the Bundle, and no `ToolTip` text (the theme styles the `ToolTip` control in `theme/XboxSeries.Dashboard/DefaultControls/ToolTip.xaml`, but sets none). Icon-only controls (Now Playing's Previous, Play/Pause, Next; the Guide's tab icons; the Settings box icons) have no accessible name beyond the Guide item titles in Core.
- **Settings UI**: the extension's Settings page and its panes are the largest single area (74 rows); the theme-only `SettingsMenus.xaml` restyles Playnite's own settings, which stay Playnite-translated, and adds four literals of its own.
- **Toasts and notifications**: 15 rows. Playnite's own notifications (and other add-ons') reach the Toast as the text they were posted with; the Bundle adds only the "Press [Guide] to view" prompt around them.
- **Error text a user can see**: the Discord status line in Settings (class 2 rows quoting Discord's own English error text and raw HTTP bodies), and two Playnite error message boxes (`PlayniteHost.cs`) whose captions and bodies are Playnite LOC strings, one with an English exception message appended.
- **Log-only text (not user-visible)**: 286 call sites. 207 `logger.Info/Warn/Error` calls, 21 `StartupTiming`/`HeroTiming` lines, 15 `WarnOnce`, 11 `Guarded.Run` descriptions, 6 media-session `Refresh` reasons, 2 `SettingsLog` calls, 1 `Unsubscribe` description, plus 23 Steam trial `report`/`watch.Note` lines that only reach a Toast from the Debug menu. Left out of the list.
- **Debug menu** (`DebugPane.xaml.cs`, `SteamDownloads.Trials.cs`): about 60 English strings (page titles, box labels, fake notification texts, Steam trial reports). Shown only in Debug builds, so left out of the list beyond its Settings entry.

## Full list

Paths are from the repo root. Line numbers point at the string, or at the first of a set.

### Dashboard: Home (theme-only parts and top bar)

| Where | String | Class | Note |
|---|---|---:|---|
| `theme/XboxSeries.Dashboard/Views/Main.xaml:216` | G | 1 | Score badge letter, drawn in a white disc |
| `theme/XboxSeries.Dashboard/Views/Main.xaml:219` | {Score:N0} | 3 | Score total; WPF binding StringFormat, culture from the element's Language (en-US unless Playnite overrides it) |
| `theme/XboxSeries.Dashboard/Views/Main.xaml:490` | Install the extension for the full Home | 1 | shown only without the extension |
| `theme/XboxSeries.Dashboard/Views/Main.xaml:492` | Xbox Series extension: github.com/snrgarb/xbox-series-theme | 1 | contains a URL |
| `theme/XboxSeries.Dashboard/Views/Main.xaml:665` | LOCGameListExtraFiltered | 6 |  |
| `theme/XboxSeries.Dashboard/Views/Main.xaml:676` | LOCNoGamesFound | 6 |  |

### Dashboard: Game list tiles

| Where | String | Class | Note |
|---|---|---:|---|
| `theme/XboxSeries.Dashboard/DerivedStyles/ListGameItemTemplate.xaml:68` | Installing... | 1 | same text as the extension's Queue state (C/ExtensionCore.Queue.cs:15) |
| `theme/XboxSeries.Dashboard/DerivedStyles/ListGameItemStyle.xaml:89` | Installing... | 1 | duplicate of the above |

### Dashboard: Game hub

| Where | String | Class | Note |
|---|---|---:|---|
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:56` | • | 4 | separator between header items, shown per item by triggers |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:129` | {Playnite main action label, upper-cased} | 6 | Playnite's own Play/Install text through StringToUpperCaseConverter |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:144` | {main action label, upper-cased} | 6 | also wears the extension's own "Install" (E/InstallActionControl.xaml.cs:26) |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:245` | Info | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:257` | Genres | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:270` | Developer | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:283` | Series | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:296` | Critic score | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:309` | Community score | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:322` | Your score | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:472` | Running | 1 | Content of a badge; same word in the Guide (E/OverlayWindow.xaml:299) |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:534` | GAME DETAILS | 1 | written in capitals in source |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:537` | GAME HUB | 1 | written in capitals in source |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:781` | Your game stats | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:792` | Time played | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:793` | {Playtime} | 6 | Playnite's PlayTimeToStringConverter |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:806` | Times played | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:807` | {PlayCount} | 3 | bare number, no format |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:819` | Last played | 1 | same text as the extension's sort option (C/MyGamesState.cs:345) |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:820` | {LastActivity} | 6 | Playnite's DateTimeToLastPlayedConverter |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:833` | Completion | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:850` | Achievements | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:853` | {Percent:0}% | 3 | PlayniteAchievements percent, WPF binding culture |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:857` | {Unlocked} / {Total} | 4 | three TextBlocks: number, " / ", number |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:880` | About | 1 |  |
| `theme/XboxSeries.Dashboard/Views/GameDetails.xaml:925` | Description | 1 |  |

### Dashboard: Main menu and Help menu

| Where | String | Class | Note |
|---|---|---:|---|
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:24` | Playnite | 1 | product name heading |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:30` | LOCSettingsLabel | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:37` | LOCMenuReloadLibrary | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:44` | LOCMenuCancelLibraryUpdate | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:51` | LOCExitPlaynite | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:58` | LOCBackToDesktopMode | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:65` | LOCMenuSelectRandomGame | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:72` | LOCMenuShutdownSystem | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:79` | LOCMenuSuspendSystem | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:86` | LOCMenuHibernateSystem | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:93` | LOCMenuRestartSystem | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:100` | LOCMenuHelpTitle | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:107` | LOCMenuPatreonSupport | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:114` | LOCMenuKofiSupport | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:121` | LOCMinimize | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:128` | LOCMenuLockSystem | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:135` | LOCMenuLogoutUser | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:142` | LOCMenuOpenClient | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:149` | LOCMenuTools | 6 |  |
| `theme/XboxSeries.Dashboard/Views/MainMenu.xaml:156` | LOCExtensions | 6 |  |
| `theme/XboxSeries.Dashboard/Views/HelpMenu.xaml:23` | LOCMenuHelpTitle | 6 |  |
| `theme/XboxSeries.Dashboard/Views/HelpMenu.xaml:29` | LOCCrashRestartPlaynite | 6 |  |
| `theme/XboxSeries.Dashboard/Views/HelpMenu.xaml:36` | LOCCrashRestartSafe | 6 |  |
| `theme/XboxSeries.Dashboard/Views/HelpMenu.xaml:43` | LOCMenuIssues | 6 |  |

### Dashboard: Playnite's Settings menus (theme-only)

| Where | String | Class | Note |
|---|---|---:|---|
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:132` | LOCSettingsLabel | 6 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:149` | LOCSettingsLabel | 6 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:163` | Choose a category to change Playnite's own options. | 1 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:170` | Get the Xbox Series extension for the full Settings: github.com/snrgarb/xbox-series-theme | 1 | contains a URL |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:243` | Off | 1 | toggle state beside Playnite's own (translated) setting name |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:261` | On | 1 | toggle state, set by trigger |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:284` | LOCSettingsFSSectionGeneral | 6 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:288` | LOCSettingsFSSectionVisuals | 6 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:292` | LOCSettingsFSSectionAudio | 6 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:296` | LOCSettingsFSSectionLayout | 6 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:300` | LOCSettingsFSSectionMenus | 6 |  |
| `theme/XboxSeries.Dashboard/Views/SettingsMenus.xaml:304` | LOCSettingsFSSection | 6 |  |

### Dashboard: Filters

| Where | String | Class | Note |
|---|---|---:|---|
| `theme/XboxSeries.Dashboard/CustomControls/FilterEnumListSelection.xaml:27` | LOCClearLabel | 6 |  |
| `theme/XboxSeries.Dashboard/CustomControls/FilterStringListSelection.xaml:27` | LOCClearLabel | 6 |  |
| `theme/XboxSeries.Dashboard/CustomControls/FilterDbItemtSelection.xaml:27` | LOCClearLabel | 6 |  |
| `theme/XboxSeries.Dashboard/Views/FiltersAdditional.xaml:20` | LOCFilters | 6 |  |

### Home (extension)

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/PromoRow.xaml:98` | Discord | 1 |  |
| `extension/XboxSeries.Extension/PromoRow.xaml:114` | Playnite settings | 1 | also E/PlaynitePane.xaml:51, E/SettingsControl.xaml:280, E/SettingsControl.xaml.cs:300 |
| `extension/XboxSeries.Extension/PromoRow.xaml:134` | Achievements | 1 | same word as the Game hub's (T/Views/GameDetails.xaml:850) |
| `extension/XboxSeries.Extension/PromoRow.xaml:135` | {Score:N0} Score | 3 | unit word inside StringFormat; WPF binding culture |
| `extension/XboxSeries.Core/ExtensionCore.Achievements.cs:25` | Needs PlayniteAchievements | 1 | Achievements tile note; names another add-on |
| `extension/XboxSeries.Core/ExtensionCore.Achievements.cs:28` | Nothing recorded yet | 1 | Achievements tile note |
| `extension/XboxSeries.Extension/PromoRow.xaml:172` | Downloads | 1 |  |
| `extension/XboxSeries.Extension/PromoRow.xaml:176` | {QueueCount} in queue | 3 | no singular/plural split |
| `extension/XboxSeries.Extension/PromoRow.xaml:179` | Nothing queued | 1 |  |
| `extension/XboxSeries.Extension/HomeTiles.xaml:162` | CONTINUE | 1 | written in capitals in source |
| `extension/XboxSeries.Extension/RecentRowControl.xaml:54` | Play history | 1 | label carried in Tag |
| `extension/XboxSeries.Extension/GroupsControl.xaml:81` | See all | 1 | label carried in Tag |
| `extension/XboxSeries.Core/ExtensionCore.cs:1374` | Favourites | 1 | fallback Group name beside user-named filter presets |
| `extension/XboxSeries.Core/ExtensionCore.Hints.cs:19` | More options | 4 | hint text beside the Menu (Start) glyph in the hint bar |
| `extension/XboxSeries.Core/ExtensionCore.Hints.cs:22` | Search | 4 | hint text beside the Y glyph |
| `extension/XboxSeries.Extension/HintBarControl.xaml:41` | Y | 1 | button letter drawn in the glyph ring |
| `extension/XboxSeries.Core/ExtensionCore.Queue.cs:103` | Install | 1 | Game hub Install label, shown upper-cased by the theme |
| `extension/XboxSeries.Extension/InstallActionControl.xaml.cs:26` | Install | 1 | duplicate label constant |

### My games & apps

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/MyGamesControl.xaml:400` | Filter by name | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:475` | Games | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:484` | Apps | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:491` | Play history | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:498` | Full library | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:514` | Manage | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:519` | Queue | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:564` | {Amount}, {Percent} | 4 | two Runs; ", " lives in the second Run's StringFormat |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:579` | All storage | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:609` | {GameCount} games | 3 | no singular ("1 games") |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:649` | Filter | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:653` | {FullLibraryCount} games | 3 | no singular |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:728` | {LastPlayed:d} | 3 | short date, ConverterCulture=CurrentCulture (the only binding that sets it) |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:730` |   •  {Platform} | 4 | separator glued into the StringFormat, follows the date TextBlock |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:778` | Software tools you add in Playnite show up here. | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:781` | Games you play show up here. | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:789` | Looking for more? | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:792` | SEE FULL LIBRARY | 1 | written in capitals in source |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:805` | Queue | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:823` | Manage Queue | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:841` | Recently installed | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:864` | Installing | 1 | differs from "Installing..." elsewhere |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:900` | Network: {Amount} | 2 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml:936` | When games and apps are installing, you'll see them here | 1 |  |
| `extension/XboxSeries.Extension/MyGamesControl.xaml.cs:344` | Apps / Play history / Full library / Manage / Games | 1 | page header, duplicates the nav labels above |
| `extension/XboxSeries.Core/MyGamesState.cs:139` | Sort / Sorted by {sort} | 2 | concatenation |
| `extension/XboxSeries.Core/MyGamesState.cs:265` | {used:0.0}% / {size} free | 3 | CurrentCulture number, unit suffix concatenated |
| `extension/XboxSeries.Core/MyGamesState.cs:279` | {n} TB / GB / MB | 3 | binary units labelled GB; CurrentCulture number; 1 or 2 decimals |
| `extension/XboxSeries.Core/MyGamesState.cs:345` | Last played | 1 | sort option |
| `extension/XboxSeries.Core/MyGamesState.cs:346` | Recently added | 1 | sort option; also a Search heading |
| `extension/XboxSeries.Core/MyGamesState.cs:347` | Playtime | 1 | sort option |
| `extension/XboxSeries.Core/MyGamesState.cs:348` | Release date | 1 | sort option |
| `extension/XboxSeries.Core/ExtensionCore.Queue.cs:15` | Installing... | 1 | Queue state |
| `extension/XboxSeries.Core/ExtensionCore.Queue.cs:16` | Queued | 1 | Queue state |
| `extension/XboxSeries.Core/ExtensionCore.Queue.cs:434` | {size} of {size} | 3 | two sizes joined by " of " |
| `extension/XboxSeries.Core/ExtensionCore.Queue.cs:473` | {percent:0.0}% | 3 | CurrentCulture, "%" appended |
| `extension/XboxSeries.Core/ExtensionCore.Queue.cs:488` | {n} Kbps / {n} Mbps | 3 | CurrentCulture, decimal units |
| `extension/XboxSeries.Core/ExtensionCore.cs:1864` | Today / Yesterday / This week / Last week / This month / Last month / This year / Older | 3 | Play history buckets by calendar date |
| `extension/XboxSeries.Core/ExtensionCore.FullLibrary.cs:83` | Filter by name | 1 | Console keyboard title |

### Search

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/SearchControl.xaml:223` | Search for games and apps | 1 |  |
| `extension/XboxSeries.Extension/SearchControl.xaml:242` | Recently added | 1 |  |
| `extension/XboxSeries.Extension/SearchControl.xaml:247` | Games | 1 |  |
| `extension/XboxSeries.Extension/SearchControl.xaml:252` | Apps | 1 |  |

### Tile menu and dialogs

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:79` | Quit | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:80` | Install | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:81` | Play game | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:83` | Remove from Favourites | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:84` | Add to Favourites | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:89` | Manage game | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:99` | Uninstall | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:103` | Customise games & apps / Customise Home | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:105` | More options | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:120` | Uninstall {game}? | 2 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:124` | Uninstall | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:125` | Cancel | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:147` | Unhide this game | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:148` | Hide this game | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:152` | Disable HDR | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:153` | Enable HDR | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:156` | Extensions | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:157` | Remove | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:160` | More options | 1 | flyout title |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:180` | Remove {game}? | 2 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:184` | Remove | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.TileMenu.cs:185` | Cancel | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:669` | Quit {running} and start {game}? | 2 | two values; either may be the fallback "the game" |
| `extension/XboxSeries.Core/ExtensionCore.cs:673` | Quit and start | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:682` | Cancel | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:1027` | What do you want to do? | 1 | power dialog |
| `extension/XboxSeries.Core/ExtensionCore.cs:1029` | Close | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:1030` | Turn off PC | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:1031` | Restart PC | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:1032` | Sleep | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:1033` | Exit Playnite | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2312` | Quit | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2343` | Force quit {game}? | 2 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2344` | It hasn't closed. Anything unsaved will be lost. | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2347` | Force quit | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2348` | Cancel | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2440` | the game | 2 | lower-case fallback name spliced into the sentences above |
| `extension/XboxSeries.Extension/WideDialogPanel.xaml:103` | CLOSE | 1 | written in capitals in source |

### Guide

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Core/ExtensionCore.cs:2475` | Home | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2476` | My games & apps | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:2482` | Notifications | 1 | Utility |
| `extension/XboxSeries.Core/ExtensionCore.cs:2483` | Audio | 1 | Utility |
| `extension/XboxSeries.Core/ExtensionCore.cs:2484` | Search | 1 | Utility |
| `extension/XboxSeries.Core/ExtensionCore.cs:1131` | Notifications / Audio | 1 | Flyout title |
| `extension/XboxSeries.Core/ExtensionCore.cs:1160` | Switch profile | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.cs:1165` | Settings | 1 | the theme shows Playnite's LOCSettingsLabel for the same idea |
| `extension/XboxSeries.Core/ExtensionCore.cs:1166` | Power | 1 |  |
| `extension/XboxSeries.Core/GuideState.cs:76` | Hold for power options | 4 | hint text beside the Guide glyph |
| `extension/XboxSeries.Core/GuideState.cs:78` | More options | 4 | hint text beside the Menu glyph |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:230` | {Position} / {Duration} | 3 | MediaTimeConverter m:ss or h:mm:ss, InvariantCulture (E/MediaTimeConverter.cs:28) |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:299` | Running | 1 |  |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:373` | Empty | 1 |  |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:448` | {Score:N0} G | 3 | "G" glued into the StringFormat; WPF binding culture |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:453` | {Unlocked:N0} achievements | 3 | plural form |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:456` | 1 achievement | 3 | singular form chosen by a DataTrigger on value 1 |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:1154` | Discord | 1 | Guide tab heading, also the trigger value "Discord" |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:1158` | Profile & system | 1 | Guide tab heading |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:1285` | No new notifications | 1 |  |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:1294` | No servers | 1 |  |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:1302` | No voice channels | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.AudioDevices.cs:25` | Output | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.AudioDevices.cs:26` | Input | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.AudioDevices.cs:33` | None | 1 | no device |
| `extension/XboxSeries.Core/ExtensionCore.AudioDevices.cs:125` | Audio | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:365` | Volume | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:366` | Mute | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:369` | Mute in Discord | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:370` | Deafen in Discord | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.NowPlaying.cs:73` | No music app is running | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.NowPlayingControls.cs:27` | Previous | 1 | item title behind an icon-only control |
| `extension/XboxSeries.Core/ExtensionCore.NowPlayingControls.cs:28` | Play/Pause | 1 | item title behind an icon-only control |
| `extension/XboxSeries.Core/ExtensionCore.NowPlayingControls.cs:29` | Next | 1 | item title behind an icon-only control |
| `extension/XboxSeries.Core/MediaSession.cs:17` | Spotify, Edge, Chrome, Firefox, Brave, Opera, Vivaldi, Media Player, Films & TV, Windows Media Player, VLC, Apple Music, iTunes, Amazon Music, TIDAL, Deezer, MusicBee, AIMP, Discord | 1 | 23 known app names (lines 17-39); "Media Player" and "Films & TV" are Windows' own localised app names |
| `extension/XboxSeries.Core/MediaSession.cs:141` | {Exe name, first letter capitalised} | 2 | fallback app name, InvariantCulture upper-case |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:305` | Reconnect Discord in settings / Connecting to Discord / Discord isn't running | 1 | why the Discord tab is empty (constants in ExtensionCore.Discord*.cs) |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:315` | Servers | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordServers.cs:88` | Servers | 1 | loading flyout title |
| `extension/XboxSeries.Core/ExtensionCore.DiscordServers.cs:98` | Servers | 1 | flyout title |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:326` | You're not in a voice channel | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:340` | Mute | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:341` | Deafen | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordCall.cs:342` | Leave | 1 |  |
| `extension/XboxSeries.Core/DiscordCall.cs:64` | Call | 1 | fallback call title |
| `extension/XboxSeries.Core/DiscordCall.cs:114` | {name}, {name}, ... | 2 | call title built with string.Join(", ") of the others' names |
| `extension/XboxSeries.Core/ExtensionCore.DiscordShortcuts.cs:138` | Call | 1 | fallback Shortcut title |
| `extension/XboxSeries.Core/ExtensionCore.DiscordShortcuts.cs:159` | Remove shortcut | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordRing.cs:217` | Someone | 1 | fallback caller name spliced into "... is calling" |
| `extension/XboxSeries.Core/ExtensionCore.DiscordRing.cs:303` | Answer / {caller} is calling | 2 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordRing.cs:323` | Leave {call} and answer? | 2 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordRing.cs:327` | Leave and answer | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.DiscordRing.cs:332` | Cancel | 1 |  |

### Toasts

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/ToastWindow.xaml:192` | Press [Guide] to view / to start | 4 | "Press" TextBlock, Guide logo Path, then "to view" (205) or "to start" (208) by Toast kind |
| `extension/XboxSeries.Extension/AchievementToast.xaml:244` | Press [Guide] to view | 4 | "Press" (244), Guide logo Path, "to view" (249) |
| `extension/XboxSeries.Extension/AchievementToast.xaml:196` | {Text} - {Rarity:0.#}% | 3 | two Runs; " - " and "%" inside the second's StringFormat |
| `extension/XboxSeries.Extension/AchievementToast.xaml:219` | G | 1 | Score badge letter |
| `extension/XboxSeries.Extension/AchievementToast.xaml:222` | {Score} - | 3 | dash glued after the Score, then the achievement name in the next TextBlock |
| `extension/XboxSeries.Core/ExtensionCore.Achievements.cs:16` | Achievement unlocked | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.Achievements.cs:19` | {n} achievements unlocked | 3 | leading-space constant concatenated to the count (n > 1 only) |
| `extension/XboxSeries.Core/ExtensionCore.Achievements.cs:22` | Rare Achievement unlocked | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.Uninstall.cs:14` | {game} was uninstalled | 2 | leading-space constant concatenated after the name |
| `extension/XboxSeries.Core/ExtensionCore.LibraryUpdate.cs:22` | Library updated | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.LibraryUpdate.cs:215` | 1 game added / {n} games added | 3 | hand-rolled English plural |
| `extension/XboxSeries.Core/ExtensionCore.Queue.cs:19` | Ready to play | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.Startup.cs:267` | Hi {first name}! | 2 | concatenation |
| `extension/XboxSeries.Core/ExtensionCore.DiscordRing.cs:270` | {caller} is calling | 2 | concatenation |
| `extension/XboxSeries.Core/Notification.cs:18` | screenshot | 1 | not shown: English keyword matched against Playnite's (possibly translated) notification text to pick the Toast kind |

### Idle screen

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:26` | Press {Guide} anywhere, even in a game, to open the Guide. | 4 | {Button} tokens drawn as glyphs by E/ButtonTokens.cs |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:27` | Press {Start} on a game tile to play it, favourite it or manage it. | 4 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:28` | Press {Y} on Home to search your games and apps. | 4 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:29` | Customise Home from a tile's menu, or Settings › Theme settings › Personalisation. | 1 | breadcrumb repeats other labels |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:30` | Flag a filter preset in Playnite to show it as a Group on Home. | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:31` | On the keyboard, {X} is backspace, {Y} is space, {Start} is Enter. | 4 | three tokens in one sentence |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:32` | See what's downloading in My games & apps › Manage › Queue. | 1 | breadcrumb repeats other labels |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:33` | Your Score adds up achievements across every platform. | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:34` | Connect Discord in Settings to see your calls in the Guide. | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:35` | Rejoin a recent Discord call in one press from the Discord tab. | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:36` | See what's playing in the Guide's Audio, under Now Playing. | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:37` | Pick a boot video, original Xbox to Series X, in Settings › Theme settings › Personalisation. | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:41` | See game | 4 | prompt beside the Y glyph |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:123` | Tip / Got it | 4 | card header; prompt beside a glyph |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:152` | {game} - {achievement} | 2 | concatenation |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:160` | Close achievement | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:187` | Unlocked {when} | 3 | "Unlocked " + relative time |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:188` | Achievement unlocked | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:206` | {playtime} played | 3 | "12 h played" |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:208` | Played {when} | 3 | "Played " + relative time |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:209` | Recently played | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:216` | {n} h / {n} min | 3 | integer division, no culture |
| `extension/XboxSeries.Core/ExtensionCore.IdleCards.cs:232` | today / yesterday / {n} days ago / a week ago / {n} weeks ago / a month ago / {n} months ago / a year ago / {n} years ago | 3 | lower-case so it reads after "Played "/"Unlocked " |
| `extension/XboxSeries.Extension/IdleCardControl.xaml:116` | X / Y / B / A | 1 | face-button letters drawn in the controller picture (116-134) |
| `extension/XboxSeries.Extension/IdleCardControl.xaml:178` | G | 1 | Score badge letter |

### Settings (extension)

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/SettingsControl.xaml:274` | Settings | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:277` | Theme settings | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:280` | Playnite settings | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:285` | More Playnite options | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:291` | Debug menu | 1 | debug builds only |
| `extension/XboxSeries.Extension/SettingsControl.xaml:336` | Dim after | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:351` | Show things after | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:374` | Now Playing source | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:375` | The app the Audio flyout's Now Playing follows while it plays or is paused, else whatever Windows shows. | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:397` | Connect your own Discord application, so the Guide can show you on Discord. | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:435` | Client ID | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:440` | Type | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:443` | Paste | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:453` | Client Secret | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:457` | Not set | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:460` | Saved | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:469` | Type | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:472` | Paste | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:480` | Connect | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml:484` | Disconnect | 1 |  |
| `extension/XboxSeries.Extension/SettingsControl.xaml.cs:296` | Idle / Guide / Discord / Theme settings / Playnite settings / {category} | 1 | pane header; Debug falls back to the enum name |
| `extension/XboxSeries.Core/IdleState.cs:87` | Never | 1 | dropdown label; also the saved value (C/ExtensionCore.Idle.cs:58) |
| `extension/XboxSeries.Core/IdleState.cs:90` | {n} min / {n} h | 3 | interpolated "{x:0}" (CurrentCulture) |
| `extension/XboxSeries.Core/SettingsState.cs:141` | Any | 1 | Now Playing source choice; also the saved value |
| `extension/XboxSeries.Core/SettingsState.cs:144` | Spotify | 1 | default source; saved value and label |
| `extension/XboxSeries.Extension/ThemeSettingsPane.xaml:21` | Personalisation | 1 |  |
| `extension/XboxSeries.Extension/ThemeSettingsPane.xaml:24` | Idle | 1 |  |
| `extension/XboxSeries.Extension/ThemeSettingsPane.xaml:27` | Guide | 1 |  |
| `extension/XboxSeries.Extension/ThemeSettingsPane.xaml:30` | Discord | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:139` | Boot video | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:142` | Profiles | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:145` | Who's playing today? | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:155` | Background delay | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:177` | Waits a moment on a game before Home's background shows its art. Turn it off to change the background as you move along your recent games. | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:189` | Reduce motion | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:208` | Shows pages, rows and tiles in place and holds the background still. Turn it on if the Dashboard feels slow on this PC. | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:216` | Plays as Playnite starts in Fullscreen. Any button skips it. | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:224` | Choose who's playing as Playnite starts in Fullscreen and wakes from sleep. | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:255` | Add profile | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:273` | Gamertag | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:276` | Real name | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:279` | Picture | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:282` | Remove profile | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml:289` | Add images to the Gamerpics folder to choose from more. | 1 | "Gamerpics" is also a folder name on disk |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml.cs:31` | Off, Xbox (2001), Xbox 360 (2005), Xbox 360 Slim (2010), Xbox 360 Special Edition (2012), Xbox One (2013), Xbox One X (2017), Xbox Series S (2020), Xbox Series X (2020) | 1 | 9 boot video choices (lines 31-39) |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml.cs:85` | Boot video / Who's playing today? / Profiles / Picture / Personalisation | 1 | pane titles, duplicate the XAML labels |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml.cs:192` | Ask every time | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml.cs:193` | Sign in the last profile | 1 |  |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml.cs:409` | Add images to the Gamerpics folder to choose from. / Picture | 1 | Playnite message box text and caption |
| `extension/XboxSeries.Extension/PersonalisationPane.xaml.cs:415` | Remove {gamertag}? / Remove profile | 2 | Playnite Yes/No message box (buttons from Playnite) |
| `extension/XboxSeries.Extension/PlaynitePane.xaml:39` | Download metadata | 1 |  |
| `extension/XboxSeries.Extension/PlaynitePane.xaml:42` | Cancel | 1 |  |
| `extension/XboxSeries.Extension/PlaynitePane.xaml:51` | Playnite settings | 1 |  |
| `extension/XboxSeries.Extension/PlaynitePane.xaml:62` | Update after downloads | 1 |  |
| `extension/XboxSeries.Extension/PlaynitePane.xaml:81` | Updates every library and downloads missing metadata for new games after a download finishes. | 1 |  |
| `extension/XboxSeries.Extension/PlayniteLibraryTasks.cs:337` | LOCProgressMetadata | 6 | Playnite's progress text |
| `extension/XboxSeries.Extension/PlayniteLibraryTasks.cs:347` | {LOCProgressMetadata} [{i}/{total}] | 3 | translated text plus bracketed counter |
| `extension/XboxSeries.Core/ExtensionCore.TextEntry.cs:63` | Gamertag | 1 | Console keyboard title (also 73) |
| `extension/XboxSeries.Core/ExtensionCore.TextEntry.cs:83` | Real name | 1 | Console keyboard title |
| `extension/XboxSeries.Core/ExtensionCore.TextEntry.cs:92` | Client ID | 1 | Console keyboard title |
| `extension/XboxSeries.Core/ExtensionCore.TextEntry.cs:104` | Client Secret | 1 | Console keyboard title |
| `extension/XboxSeries.Core/DiscordSettingsState.cs:15` | Not connected | 1 | also C/ExtensionCore.Discord.cs:29 |
| `extension/XboxSeries.Core/DiscordSettingsState.cs:28` | Create an application at discord.com/developers/applications. | 1 | Connect checklist step |
| `extension/XboxSeries.Core/DiscordSettingsState.cs:29` | Under OAuth2, add the redirect http://127.0.0.1. | 1 | names Discord's own UI |
| `extension/XboxSeries.Core/DiscordSettingsState.cs:30` | Under App Testers, add yourself. | 1 | names Discord's own UI |
| `extension/XboxSeries.Core/DiscordSettingsState.cs:31` | Paste its Client ID and Client Secret here, then select Connect. | 1 | repeats the field and button labels |
| `extension/XboxSeries.Core/ExtensionCore.Discord.cs:30` | Connecting... | 1 | status |
| `extension/XboxSeries.Core/ExtensionCore.DiscordTokens.cs:125` | Approve the request in Discord | 1 | status |
| `extension/XboxSeries.Core/ExtensionCore.DiscordTokens.cs:165` | Couldn't connect to Discord: {message} | 2 | message is Discord's own English text |
| `extension/XboxSeries.Core/ExtensionCore.Discord.cs:208` | Discord closed the connection / Discord closed the connection: {message} | 2 |  |
| `extension/XboxSeries.Core/ExtensionCore.Discord.cs:318` | The token request failed: {body} | 2 | raw HTTP body |
| `extension/XboxSeries.Core/ExtensionCore.Discord.cs:326` | Couldn't connect to Discord: {error} | 2 | error from Discord, or "HTTP {status}" |
| `extension/XboxSeries.Core/ExtensionCore.Discord.cs:332` | Couldn't connect to Discord: no access token came back | 1 |  |
| `extension/XboxSeries.Core/ExtensionCore.Discord.cs:361` | Connected as {user} | 2 |  |

### Start-up and Launch screen

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/StartupWindow.xaml:75` | Who's playing today? | 1 | also a Settings label |
| `extension/XboxSeries.Extension/StartupWindow.xaml:188` | Signing you in | 1 |  |

### Status area

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/StatusArea.xaml:19` | {Battery.Percent}% | 3 | controller battery |
| `extension/XboxSeries.Core/ExtensionCore.cs:429` | {time:t} | 3 | clock, short time pattern of CurrentCulture |

### Console keyboard

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Core/ConsoleKeyboard.cs:228` | abc / #+= | 1 | symbol-toggle key label |
| `extension/XboxSeries.Core/ConsoleKeyboard.cs:310` | QWERTYUIOP / ASDFGHJKL" / ZXCVBNM;:! | 1 | fixed US-style layout; key text lower-cased with ToLowerInvariant (233) |
| `extension/XboxSeries.Extension/ConsoleKeyboardControl.xaml:139` | LT / X / LB / RB / LS / Y | 1 | controller button names drawn as key hints (139-201) |
| `extension/XboxSeries.Extension/ButtonGlyphs.cs:58` | LB / RB | 1 | letters drawn into the bumper glyphs |

### Error dialogs (Playnite message boxes)

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/PlayniteHost.cs:184` | LOCAppStartupError + "\n\n" + {exception message} / LOCStartupError | 6 | translated text plus an English exception message |
| `extension/XboxSeries.Extension/PlayniteHost.cs:161` | {app} has no path to start; only Playnite can run a script tool. | 2 | exception message that reaches the dialog above |
| `extension/XboxSeries.Extension/PlayniteHost.cs:312` | LOCGameRemoveRunningError / LOCGameError | 6 |  |

### Window titles (not normally on screen)

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/StartupWindow.xaml:6` | Xbox Series Start-up | 1 |  |
| `extension/XboxSeries.Extension/DimWindow.xaml:5` | Xbox Series Idle dim | 1 |  |
| `extension/XboxSeries.Extension/LaunchScreenWindow.xaml:4` | Xbox Series Launch screen | 1 |  |
| `extension/XboxSeries.Extension/ToastWindow.xaml:6` | Xbox Series Toast | 1 |  |
| `extension/XboxSeries.Extension/OverlayWindow.xaml:5` | Xbox Series Guide | 1 |  |

### Add-on manifests (Playnite's add-on lists)

| Where | String | Class | Note |
|---|---|---:|---|
| `theme/XboxSeries.Dashboard/theme.yaml:2` | Xbox Series | 1 | theme Name |
| `extension/XboxSeries.Extension/extension.yaml:2` | Xbox Series | 1 | extension Name |
| `theme/XboxSeries.Dashboard/theme.yaml:8` | GitHub | 1 | link name (also extension.yaml:8) |

### Images and video

| Where | String | Class | Note |
|---|---|---:|---|
| `extension/XboxSeries.Extension/Videos/*.mp4` | Boot videos (8 files) | 5 | console boot animations; they show Xbox logos/wordmarks, not translatable prose (not inspected frame by frame: no ffmpeg here) |
| `shared/Images/source-xbox-shadow.png` | (no text) | 5 | Xbox sphere logo only; source-steam-shadow.png likewise a logo |
| `extension/XboxSeries.Extension/DiscordPortalQr.png` | (no readable text) | 5 | QR code encoding the Discord developer portal URL |

## Awkward cases

Concrete cases any later decision has to handle.

### Word order around icons and glyphs

- **Toast prompt**: `Press` + Guide logo (a `Path`) + `to view` / `to start`, as three siblings in a horizontal StackPanel (`ToastWindow.xaml:192-208`; `AchievementToast.xaml:244-249`). A language that puts the verb last, or the button mid-phrase differently, cannot be expressed by translating the two halves.
- **Idle tips with `{Button}` tokens** (`ExtensionCore.IdleCards.cs:26-31`): the tokens are swapped for glyphs by `ButtonTokens.cs`, so the sentence is already one string with placeholders. This is the one pattern in the Bundle that survives reordering, but the token names (`{Guide}`, `{Start}`, `{Y}`, `{X}`) are identifiers inside prose: a translator must leave them alone.
- **Hint bar and Idle card prompts** (`More options`, `Search`, `Hold for power options`, `See game`, `Got it`): glyph always first, text after, fixed by layout (`HintBarControl.xaml`, `IdleCardControl.xaml:215-217`).
- **Score badge**: the letter `G` drawn in a disc next to the number in four places (`Main.xaml:216`, `AchievementToast.xaml:219`, `IdleCardControl.xaml:178`, and glued into `{0:N0} G` at `OverlayWindow.xaml:448`). The Promo row writes the word instead: `{0:N0} Score` (`PromoRow.xaml:135`).
- **Controller button names as text**: `LT`, `LB`, `RB`, `LS`, `X`, `Y` on Console keyboard key hints (`ConsoleKeyboardControl.xaml:139-201`), `LB`/`RB` drawn into glyphs (`ButtonGlyphs.cs:58`), `X Y B A` in the Idle card's controller (`IdleCardControl.xaml:116-134`). These are hardware labels, not words.

### Plurals

- Hand-rolled English plural: `1 game added` / `{n} games added` (`ExtensionCore.LibraryUpdate.cs:215`); `1 achievement` / `{n} achievements` via a DataTrigger on the value 1 (`OverlayWindow.xaml:453-456`); `a week ago` / `{n} weeks ago` and likewise for months and years (`ExtensionCore.IdleCards.cs:232-259`).
- No plural at all: `{0} games` (`MyGamesControl.xaml:609`, `653`) reads "1 games"; `{0} in queue` (`PromoRow.xaml:176`); `{n} achievements unlocked` (`ExtensionCore.Achievements.cs:19`) is only used for n > 1, which hides the problem in English only.
- Unit abbreviations that some languages inflect or reorder: `min`, `h`, `{n} h played`, `{n} min` (`IdleState.cs:90`, `ExtensionCore.IdleCards.cs:216`).

### Culture-dependent formatting

- C# formatting uses `CultureInfo.CurrentCulture` (the Windows regional format, not Playnite's language): sizes (`MyGamesState.cs:279`), storage percent (`MyGamesState.cs:265`), download percent (`ExtensionCore.Queue.cs:473`), speed (`ExtensionCore.Queue.cs:488`), clock `ToString("t")` (`ExtensionCore.cs:429`), Settings dropdown minutes and hours via `$"{x:0}"` (`IdleState.cs:90`).
- XAML `StringFormat` uses the element's `Language`, which WPF defaults to en-US; the Bundle never sets `Language`/`XmlLanguage`, and whether Playnite overrides it for Fullscreen was not checked. Affected: `{0:N0}` Score (`Main.xaml:219`, `PromoRow.xaml:135`, `OverlayWindow.xaml:448`), `{0:N0} achievements` (`OverlayWindow.xaml:453`), `{0:0}%` (`GameDetails.xaml:853`), `{0:0.#}%` rarity (`AchievementToast.xaml:196`), battery `{0}%` (`StatusArea.xaml:19`). Only `MyGamesControl.xaml:728` (`StringFormat=d`) sets `ConverterCulture=CurrentCulture`. So in one screen a number can be grouped one way by C# and another by XAML.
- Media time is forced to InvariantCulture (`MediaTimeConverter.cs:28`), and Idle playtime uses integer division with no culture (`ExtensionCore.IdleCards.cs:216`).
- `%` is always appended after the number with no space; French and others put a space before it.
- Sizes are binary (GiB) but labelled `GB`/`MB`/`TB`, speed is decimal `Kbps`/`Mbps`, as the console does.
- Relative time (`today`, `{n} days ago`) and Play history buckets (`Today` … `Older`, `ExtensionCore.cs:1864`) are calendar arithmetic on `clock.Now`, English only; the theme's Game hub uses Playnite's own `DateTimeToLastPlayedConverter` for the same idea, so the two will disagree once Playnite is in another language.
- Search compares with `CultureInfo.CurrentCulture.CompareInfo` (`ExtensionCore.Search.cs:291`): matching is culture-aware already.

### Strings built by concatenation

- Prefix + value: `"Hi " + name + "!"` (`ExtensionCore.Startup.cs:267`), `"Played " + when`, `"Unlocked " + when` (`ExtensionCore.IdleCards.cs:187`, `208`), `"Sorted by " + name` (`MyGamesState.cs:139`), `"Connected as " + user` (`ExtensionCore.Discord.cs:361`), `"Couldn't connect to Discord: " + message`.
- Value + suffix constant with a leading space: `name + " was uninstalled"` (`ExtensionCore.Uninstall.cs:14`), `count + " achievements unlocked"` (`ExtensionCore.Achievements.cs:19`), `caller + " is calling"` (`ExtensionCore.DiscordRing.cs:270`, `303`), `played + " played"`.
- Relative-time pieces are lower-case so they read after `Played `/`Unlocked ` (`today`, `yesterday`): reusing them alone would need a capitalised form.
- Two values in one sentence: `Quit {running} and start {game}?` (`ExtensionCore.cs:669`); either may be the fallback `the game` (`ExtensionCore.cs:2440`), which carries its own article and lower-case. `Someone` (`ExtensionCore.DiscordRing.cs:217`) and `Call` play the same fallback role.
- Lists: a Call's title is `string.Join(", ", names)` (`DiscordCall.cs:114`); `{size} of {size}` (`ExtensionCore.Queue.cs:434`); `{game} - {achievement}` (`ExtensionCore.IdleCards.cs:152`).
- Split across TextBlocks or Runs: `{Unlocked} / {Total}` (`GameDetails.xaml:857`), `{Amount}` + `, {Percent}` (`MyGamesControl.xaml:564`), `{Text}` + ` - {Rarity}%` (`AchievementToast.xaml:196`), `{Score} -` then the name (`AchievementToast.xaml:222`), `{LastPlayed:d}` then `  •  {Platform}` (`MyGamesControl.xaml:728-730`), and the Game hub's `•` separators (`GameDetails.xaml:56`).
- A Playnite LOC string with English glued on: `LOCAppStartupError + "\n\n" + exception message` (`PlayniteHost.cs:184`), whose message can be the Bundle's own English (`PlayniteHost.cs:161`); `LOCProgressMetadata + " [i/total]"` (`PlayniteLibraryTasks.cs:347`).
- External English passed through as is: Discord's `message`, `error_description` and raw HTTP bodies in the Discord status line (`ExtensionCore.Discord.cs:208`, `318`, `326`).

### Strings shared between the Dashboard and the extension

The theme and the extension each carry their own copy; nothing ties them together.

- `Installing...`: theme tiles (`ListGameItemTemplate.xaml:68`, `ListGameItemStyle.xaml:89`) and the Queue state (`ExtensionCore.Queue.cs:15`), while Manage Queue's heading says `Installing` (`MyGamesControl.xaml:864`).
- `Running`: Game hub badge (`GameDetails.xaml:472`) and the Guide (`OverlayWindow.xaml:299`).
- `Achievements`, `Last played`: Game hub labels and the extension's Promo row / sort option.
- `Install`: the extension's own label (`ExtensionCore.Queue.cs:103`, `InstallActionControl.xaml.cs:26`, `ExtensionCore.TileMenu.cs:80`) sits in the Game hub beside Playnite's translated Play/Install, both upper-cased by the theme's `StringToUpperCaseConverter`.
- `Settings`: the theme uses Playnite's `LOCSettingsLabel`; the extension's Guide item and Settings page heading are the literal `Settings`. Likewise the theme menus use `LOCExtensions` while the Tile menu has a literal `Extensions`.
- The theme-only "install the extension" lines (`Main.xaml:490-492`, `SettingsMenus.xaml:170`) name the extension and its URL.
- Within the extension, the same label is duplicated across XAML and C#: `Playnite settings` (4 places), `Games`/`Apps`/`Play history` (nav in XAML and header in `MyGamesControl.xaml.cs:344`), the Personalisation pane titles (`PersonalisationPane.xaml` and `.xaml.cs:85-90`), the Console keyboard titles and the field labels (`Gamertag`, `Real name`, `Client ID`, `Client Secret`), `Who's playing today?` (Start-up and Settings).
- Idle tips spell out breadcrumbs of other labels (`Settings › Theme settings › Personalisation`, `My games & apps › Manage › Queue`) and the Discord checklist names `Client ID`, `Client Secret`, `Connect`; translating one side alone breaks them.

### Text in images and video

- The eight boot videos (`extension/XboxSeries.Extension/Videos/*.mp4`) are console boot animations showing Xbox logos and wordmarks; brand marks, not prose. Not inspected frame by frame (no ffmpeg in this environment).
- `shared/Images/source-*-shadow.png` are logos with no text; `DiscordPortalQr.png` is a QR code of the portal URL, which the page also prints in words (`DiscordSettingsState.cs:28`).
- No other images in the Bundle carry text. Glyphs come from the Segoe MDL2/Fluent icon font, and one emoji, the trophy `🏆` (`OverlayWindow.xaml:156`, `447`), drawn from Segoe UI Symbol, not dependent on language.

### Keys and identifiers that look like text

- Saved values that are also labels: `Never` (`IdleState.cs:87`, saved as `NeverSaved`, `ExtensionCore.Idle.cs:58`), `Any` and `Spotify` (`SettingsState.cs:141`, `144`) for the Now Playing source. Translating the label must not change what is saved.
- Media app names are both display names and matching keys (`MediaSession.cs:17-39`); `Media Player` and `Films & TV` are Windows' own app names, which Windows itself localises. Unknown apps get their exe name with its first letter capitalised (`MediaSession.cs:141`).
- `Notification.cs:18` picks the screenshot Toast by finding the English word `screenshot` in a notification's id or text; translated Playnite notifications will not match.
- `Tag` doubles as label and state: `Tag="Play history"` (`RecentRowControl.xaml:54`), `Tag="See all"` (`GroupsControl.xaml:81`) are shown, while `Tag="Selected"`/`"Open"`, `"Shape"`/`"Lit"`/`"Glint …"` are state markers. The Guide's heading triggers on the tab names `Discord`/`ProfileAndSystem` (`OverlayWindow.xaml:1153-1158`), which are enum values, not shown text.
- Idle tip tokens `{Guide}`, `{Start}`, `{X}`, `{Y}` (above); `Gamerpics` is both a word in a Settings hint and a folder on disk (`PersonalisationPane.xaml:289`, `WindowsUserAccount.cs:37`).
- The Debug pane heading falls back to `category.ToString()` (`SettingsControl.xaml.cs:301`), an enum name.
- Window titles (`Xbox Series Guide`, `Xbox Series Toast`, …) are seen only in task switchers and tools that list windows, and are also how other code could find the windows.

### Casing transforms

- `StringToUpperCaseConverter` (Playnite's) upper-cases the Game hub's main action, Playnite's translated Play/Install and the extension's `Install` (`GameDetails.xaml:129`, `144`). Culture of that upper-casing is Playnite's; Turkish dotted i and German ß are the classic traps.
- Literals already written in capitals: `GAME HUB`, `GAME DETAILS` (`GameDetails.xaml:534-537`), `CONTINUE` (`HomeTiles.xaml:162`), `SEE FULL LIBRARY` (`MyGamesControl.xaml:792`), `CLOSE` (`WideDialogPanel.xaml:103`). Capitals are a style here, held in the text.
- Console keyboard keys are upper-case strings lowered with `ToLowerInvariant` (`ConsoleKeyboard.cs:233`); its rows are a fixed US-style QWERTY (`ConsoleKeyboard.cs:310-313`), not a per-language layout.
- Case is also carried by meaning: `Rare Achievement unlocked` capitalises Achievement, `Achievement unlocked` does not (`ExtensionCore.Achievements.cs:16`, `22`).

### Truncation and fixed widths

- Fixed-width boxes that English fills: the Game hub's main action is 380 wide (`GameDetails.xaml:124`); Game menu items cap at `MaxWidth` 394 with ellipsis (`GameMenu.xaml:125-126`); the source name under it at 180 (`GameDetails.xaml:741`); the Guide's Profile, Settings boxes and Tile menu rows are fixed-size cards.
- `TextTrimming="CharacterEllipsis"` is widespread (Constants.xaml base style, ComboBox, CheckBox, ToggleButton, Settings rows, Game menu, Toasts, Achievement Toast, App tiles, Groups), so longer translations will cut off silently rather than wrap.
- Wrapped text with a height cap: tile titles at `MaxHeight` 56 (`ListGameItemStyle.xaml:85`) and 52 (`AppTiles.xaml:44`).
- The Achievement Toast measures its text with `FormattedText` to size its pill and page the description (`AchievementToast.xaml.cs:88`); longer strings change its timing (`AchievementShownFor`).
- The Console keyboard's key grid and the Idle card prompt are laid out for short English words.

## Tests that assert English user-visible strings

`extension/XboxSeries.Core.Tests` asserts the English text in many places, so translating any of it will touch tests. Counted as `Assert.*` statements that quote one of the strings above (whole, or a fixed fragment of a built one), or a counted English form such as `3 achievements unlocked`, `2 days ago`, `5.2 GB` or `1.9%`. Matching was by script, so treat the figures as close, not exact.

| Test file | Assertions |
|---|---:|
| `ProfileAndSystemTests.cs` | 21 |
| `TileMenuTests.cs` | 21 |
| `DiscordReconnectTests.cs` | 20 |
| `IdleCardsTests.cs` | 20 |
| `QueueTests.cs` | 17 |
| `TileMenuFlyoutTests.cs` | 16 |
| `AchievementsTests.cs` | 15 |
| `DiscordRingTests.cs` | 15 |
| `DiscordTests.cs` | 15 |
| `LandingTabTests.cs` | 15 |
| `QuitFlowTests.cs` | 13 |
| `DiscordTokenTests.cs` | 12 |
| `MoreOptionsHintTests.cs` | 12 |
| `GuideUtilityTests.cs` | 7 |
| `SilentInstallTests.cs` | 7 |
| `AchievementToastXamlTests.cs` | 6 |
| `DebugMenuTests.cs` | 6 |
| `TextEntryTests.cs` | 6 |
| `YSearchHintTests.cs` | 6 |
| `DashboardBoundaryTests.cs` | 5 |
| `FullLibraryTests.cs` | 5 |
| `ToastTests.cs` | 5 |
| `AudioDeviceTests.cs` | 4 |
| `LibraryUpdateTests.cs` | 4 |
| `StartupTests.cs` | 4 |
| `DiscordShortcutsTests.cs` | 3 |
| `GroupTests.cs` | 3 |
| `IdleCardXamlTests.cs` | 3 |
| `KeptRowsTests.cs` | 3 |
| `PlayniteSettingsXamlTests.cs` | 3 |
| `QueuedToastTests.cs` | 3 |
| `SearchTests.cs` | 3 |
| `UninstallTests.cs` | 3 |
| `ButtonTokensTests.cs` | 2 |
| `DiscordServersTests.cs` | 2 |
| `DiscordSettingsXamlTests.cs` | 2 |
| `IdleSettingsTests.cs` | 2 |
| `IdleSettingsXamlTests.cs` | 2 |
| `SearchXamlTests.cs` | 2 |
| `ThemeSettingsXamlTests.cs` | 2 |
| `BackgroundDelayXamlTests.cs` | 1 |
| `DashboardHeaderFontTests.cs` | 1 |
| `DiscordCallTests.cs` | 1 |
| `GroupsXamlTests.cs` | 1 |
| `GuideFreezeTests.cs` | 1 |
| `HomePromoRowTests.cs` | 1 |
| `HomeTests.cs` | 1 |
| `LaunchScreenTests.cs` | 1 |
| `NowPlayingTests.cs` | 1 |
| `ReadyToPlayToastTests.cs` | 1 |
| `ReduceMotionXamlTests.cs` | 1 |
| `SoundTests.cs` | 1 |
| **52 files** | **327** |
