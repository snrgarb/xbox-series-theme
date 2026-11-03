# Playnite 10 add-on model for a theme + extension Bundle

Research for [#4](https://github.com/snrgarb/xbox-series-theme/issues/4) (part of #1, blocks #11).

**Question:** what does Playnite 10's add-on model allow for a **Bundle** made of a Fullscreen theme plus a Generic extension?

**Sources and versions checked (2026-10-13):**

- Playnite source at `master` [`ecee014`](https://github.com/JosefNemec/Playnite/tree/ecee01452a25db1fb2674a69f883d298d4c7be8a), which is the 10.62 release (2026-09-29). SDK `6.18.0`, Fullscreen theme API `2.10.0`.
- Playnite docs repo [`Playnite/Docs@41d6529`](https://github.com/Playnite/Docs/tree/41d6529c7b96335211777031e5a7b36b86cd982a). This is the source of api.playnite.link. It was last pushed in May 2025, and its SDK changelog stops at 6.11.0, so where the docs and the code disagree, the code wins.
- Add-on database [`JosefNemec/PlayniteAddonDatabase@bf5d564`](https://github.com/JosefNemec/PlayniteAddonDatabase/tree/bf5d5647647da13ddfd717714ff1aa5f7cd792a6).
- Prior art: [Neon-Silicon/Xbox_360_Playnite_Theme](https://github.com/Neon-Silicon/Xbox_360_Playnite_Theme) (a theme plus a required extension) and [jonosellier/PlayniteGameOverlay](https://github.com/jonosellier/PlayniteGameOverlay) (an in-game overlay that closes the game and refocuses Playnite).


---

## TL;DR

1. **There is no single-unit Bundle and no dependency declaration.** A theme (`.pthm`) and an extension (`.pext`) are always two packages, two add-on database entries and two installs. No manifest (theme, extension, add-on DB or installer) has a field for dependencies. A theme can only *detect* whether the extension is present, using `{PluginStatus}`, and degrade or show a "please install" hint. Extension submissions to the add-on DB are currently on hold, which makes this worse.
2. **Yes, extensions can inject controls into Fullscreen themes.** This is officially supported. The extension registers named elements with `AddCustomElementSupport` and returns them from `GetGameViewControl`. The theme places `<ContentControl x:Name="<SourceName>_<ElementName>"/>` in `Main.xaml`, `GameDetails.xaml` or the game list item template. Settings bind with `{PluginSettings}`, converters with `{PluginConverter}`, and presence with `{PluginStatus}`. The theme should not reference extension assembly types directly in XAML, because the theme loads before plugins.
3. **The SDK gives an extension a narrow control surface. Everything else is your own code.**
   - SDK-supported: select a game, apply a filter preset, toggle list/details, read the active view, game start/stop events (`StartedProcessId`, `Game.IsRunning`), controller button events (also while a game has focus), `CreateWindow`, and main/game menu items.
   - Not in the SDK: open the main menu, settings or search; close a game (Playnite never kills games); shutdown, sleep or restart (these exist only as internal main-menu commands).
   - Bringing Playnite to the front is possible from an extension, either with `GetCurrentAppWindow()` plus activation on the UI thread, or by re-launching `Playnite.FullscreenApp.exe` or `playnite://playnite/restore`. Playnite also restores itself automatically when a tracked game exits in Fullscreen mode.

---

## 1. Shipping and installing as one unit; theme→extension dependency

### Packages are single-manifest, single-type

- The package type is decided by file extension: `.pthm` is a theme and `.pext` is an extension ([`PlaynitePaths.cs` L16-17](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Settings/PlaynitePaths.cs#L16-L17)).
- `InstallPackedFile<T>` reads exactly one manifest from the zip root. That is `theme.yaml` for themes and `extension.yaml` for extensions. It unpacks the whole zip into a single `Themes/<Mode>/<Id>` or `Extensions/<Id>` folder ([`ExtensionInstaller.cs` L118-238](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Plugins/ExtensionInstaller.cs#L118-L238)). No package format can contain both.
- Theme packages are rejected if they contain `Playnite.dll`, `Playnite.Common.dll` or `Playnite.SDK.dll`, or theme project files ([same file, L365-410](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Plugins/ExtensionInstaller.cs#L365-L410)). The docs also say themes can't ship 3rd-party assemblies or fonts inside a `.pthm`; those only load if they are "manually placed in application folder" ([Docs `themes/various.md`](https://github.com/Playnite/Docs/blob/41d6529c7b96335211777031e5a7b36b86cd982a/docs/tutorials/themes/various.md#using-custom-controls-and-3rd-party-assemblies)).

### No dependency fields anywhere

| Manifest | Fields | Source |
|---|---|---|
| `theme.yaml` (`ThemeManifest`) | Id, Name, Author, Version, Links, ThemeApiVersion, Mode, ForceLTRLayout | [`ThemeManifest.cs`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Manifests/ThemeManifest.cs), [`ExtensionManifest.cs` (BaseExtensionManifest)](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Manifests/ExtensionManifest.cs) |
| `extension.yaml` (`ExtensionManifest`) | base fields + Module, Icon, Type | same |
| Add-on DB entry (`AddonManifestBase`) | AddonId, Type, Name, Author, ShortDescription, Description, InstallerManifestUrl, SourceUrl, Tags, Links, Screenshots, IconUrl, UserAgreement, CommunityNote (+ FaqTopics in the DB README) | [`AddonManifestBase.cs`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Manifests/AddonManifestBase.cs), [DB README](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/bf5d5647647da13ddfd717714ff1aa5f7cd792a6/README.md#addon-manifest) |
| Installer manifest packages | Version, PackageUrl, RequiredApiVersion, ReleaseDate, Changelog | same files |

`RequiredApiVersion` gates only on the Playnite SDK or theme API version. It cannot reference another add-on.

### Add-on DB status matters for a Bundle

The add-on DB README currently says:

> New plugin submissions are currently on hold… There's new addon database planned for Playnite 11 release… which will be backported to Playnite 10 as well. Theme submissions are still allowed since themes currently can't ship with custom assemblies.
> — [DB README](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/bf5d5647647da13ddfd717714ff1aa5f7cd792a6/README.md)

So the theme half of a Bundle can be listed in the in-app browser now. The extension half may not get accepted until the new DB ships, unless the author counts as a "long standing trusted member". Without a DB entry the extension gets **no in-app install and no auto-update**.

### What you *can* do to approximate "one unit"

- **Two DB entries that cross-reference each other.** This is what the Xbox 360 NXE prior art does. Its README says "Make sure to install both the theme and the extension, as both are required". The extension's DB `ShortDescription` says "*Required by Xbox 360 NXE Dashboard Theme*" ([DB entry](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/bf5d5647647da13ddfd717714ff1aa5f7cd792a6/addons/generic/Xbox_360_NXE_Game_List_Extension_Manifest.yaml), [repo](https://github.com/Neon-Silicon/Xbox_360_Playnite_Theme)). One GitHub repo holds both sources and both installer manifests.
- **One-click install links.** `playnite://playnite/installaddon/<AddonId>` installs a DB-listed add-on, with the usual confirmation prompt ([DB README "Installation URI"](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/bf5d5647647da13ddfd717714ff1aa5f7cd792a6/README.md#installation-uri); handler at [`PlayniteApplication.cs` L1197-1204, L1534+](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/App/PlayniteApplication.cs#L1197-L1204)). Once the extension is installed, it can open that URI (or the `.pext`/`.pthm` file path, via `--installext`) to offer installing its partner theme. Playnite then shows its normal install prompt and asks to restart.
- **Theme-side detection.** `{PluginStatus Plugin=<AddonId>, Status=Installed}` resolves to `bool`, or to `Visibility` on Visibility properties, depending on whether the extension is loaded ([Docs `themes/extensionIntegration.md`](https://github.com/Playnite/Docs/blob/41d6529c7b96335211777031e5a7b36b86cd982a/docs/tutorials/themes/extensionIntegration.md); [`PluginStatus.cs`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Extensions/Markup/PluginStatus.cs), [`ExtensionsStatusBinder` in `ExtensionFactory.cs` L21-39](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Plugins/ExtensionFactory.cs#L21-L39)). Use this to show a "companion extension missing" banner and fall back to stock controls.
- *(Unofficial)* An extension could write theme files directly into `<ConfigurationPath>/Themes/Fullscreen/<Id>` on first run. That bypasses the installer and add-on updates. Not recommended.

---

## 2. Extension-supplied elements in Fullscreen themes

**Supported, in Fullscreen too.**

Extension side ([Docs `extensions/customUiIntegration.md`](https://github.com/Playnite/Docs/blob/41d6529c7b96335211777031e5a7b36b86cd982a/docs/tutorials/extensions/customUiIntegration.md)):

```csharp
// in the GenericPlugin constructor
AddCustomElementSupport(new AddCustomElementSupportArgs {
    SourceName = "XboxSeries", ElementList = new List<string> { "Guide", "Clock" } });
AddSettingsSupport(new AddSettingsSupportArgs { SourceName = "XboxSeries", SettingsRoot = "Settings" });
AddConvertersSupport(new AddConvertersSupportArgs { SourceName = "XboxSeries", Converters = new List<IValueConverter> { ... } });

public override Control GetGameViewControl(GetGameViewControlArgs args)
    => args.Name == "Clock" ? new ClockControl() : null;   // args.Mode tells Desktop vs Fullscreen
```

Theme side ([Docs `themes/extensionIntegration.md`](https://github.com/Playnite/Docs/blob/41d6529c7b96335211777031e5a7b36b86cd982a/docs/tutorials/themes/extensionIntegration.md)):

```xml
<ContentControl x:Name="XboxSeries_Clock" />
<TextBlock Text="{PluginSettings Plugin=XboxSeries, Path=SomeOption}" />
<TextBlock Text="{Binding X, Converter={PluginConverter Plugin=XboxSeries, Converter=MyConverter}, FallbackValue=...}" />
<Grid Visibility="{PluginStatus Plugin=<AddonId>, Status=Installed}" />
```

How it works in Fullscreen (source):

- `ControlTemplateTools.InitializePluginControls` looks up `{SourceName}_{ElementName}` as a `ContentControl` in a control template. It calls `GetGameViewControl(Name, Mode)`, binds the game to `PluginUserControl.GameContext` (or to `DataContext` for a plain `Control`), sets the host `Focusable = false` and assigns `Content` ([`ControlTemplateTools.cs`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/ControlTemplateTools.cs)).
- Fullscreen calls it from three templates only:
  - **Main view** ([`Views/Main.cs` L523](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/Main.cs#L523)): the game context is the selected game.
  - **GameDetails** ([`Views/GameDetails.cs` L190](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/Views/GameDetails.cs#L190)): the game context is the selected game.
  - **GameListItem** ([`GameListItem.cs` L86](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Controls/GameListItem.cs#L86)): one control per tile, and the game context is that tile's game.

  Elements placed in other views, such as the main menu, settings or filters, are **not** populated.
- `GetGameViewControl` runs each time a template initializes, so a new control instance is created for every list item. Keep per-tile controls light.
- `{PluginSettings}` resolves late. If extensions haven't loaded when the XAML is parsed, it re-binds on `ExtensionsLoaded` ([`PluginSettings.cs`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Extensions/Markup/PluginSettings.cs)). Settings should implement `INotifyPropertyChanged` for live updates.
- **Load order:** the theme is applied in `PlayniteApplication` startup ([`PlayniteApplication.cs` L351](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/App/PlayniteApplication.cs#L351)) *before* Fullscreen loads plugins in `OpenMainViewAsync` ([`FullscreenApplication.cs` L175-177](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/FullscreenApplication.cs#L175-L177)). Theme XAML should therefore not use `xmlns:...;assembly=<ExtensionAssembly>` types directly. Inject them through the custom-element mechanism, or have the extension merge `ResourceDictionary`s into `Application.Current.Resources` at runtime (inferred, not documented).
- **Controller focus:** the host `ContentControl` is non-focusable. Gamepad navigation into an injected control is up to the extension. The NXE prior art drives its carousel from `OnControllerButtonStateChanged` instead of WPF focus ([`NXEGameListPlugin.cs`](https://github.com/Neon-Silicon/Xbox_360_Playnite_Theme/blob/main/Source/Xbox_360_NXE_Game_List_Extension/NXEGameListPlugin.cs)).
- The theme itself can bind to the main window's DataContext, `FullscreenAppViewModel`, which has public commands such as `OpenMainMenuCommand`, `ToggleGameDetailsCommand`, `OpenSearchCommand` and `ActivateSelectedCommand` ([`FullscreenAppViewModel_Commands.cs` L18-43](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel_Commands.cs#L18-L43)). This is how theme buttons navigate without extension help.

---

## 3. What an extension can do to the running Fullscreen app and game

### Navigate views — partial

`IMainViewAPI` in Fullscreen ([`Playnite.FullscreenApp/Api/MainViewAPI.cs`](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/Api/MainViewAPI.cs)):

| Works | Throws `NotSupportedInFullscreenException` |
|---|---|
| `SelectGame`, `ApplyFilterPreset`, `GetActiveFilterPreset`, `GetCurrentFilterSettings`, `GetSorted*Presets`, `SortOrder(Direction)`, `FilteredGames`, `SelectedGames`, `ActiveFullscreenView` (List/Details), `ToggleFullscreenView()`, `UIDispatcher` | `SelectGames`, `OpenSearch`, `OpenEditDialog`, `SwitchToLibraryView`, `OpenPluginSettings` |

- `OnFullscreenViewChanged(args.NewView)` reports list↔details switches ([`Plugin.cs` L327](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/PlayniteSDK/Plugins/Plugin.cs#L327)).
- The SDK has no call to open the main menu, settings, notifications or the game menu. An extension can:
  - add its own entries via `GetMainMenuItems` (the Fullscreen "Extensions" menu) and `GetGameMenuItems`;
  - open its own windows with `Dialogs.CreateWindow`, which works in Fullscreen since SDK 6.12;
  - *unofficially* reach the internal `FullscreenAppViewModel` through `Dialogs.GetCurrentAppWindow().DataContext` and its commands, using reflection or `dynamic`. That is not SDK and can break on any Playnite update.

  The NXE prior art does a similar hack: it raises `Click` on `PART_ButtonDetails` found on `MainWindow`.

### Bring Playnite to the front — yes

- **Automatic:** in Fullscreen mode Playnite *always* restores itself when a tracked game stops ([`GamesEditor.cs` L1443-1460](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/GamesEditor.cs#L1443-L1460)). Playnite also has a built-in setting to focus on the Guide button: `GuideButtonFocus`, plus `GuideButtonFocusOnlyWhenNoGameRunning` ([`FullscreenAppViewModel.cs` L424-434](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs#L424-L434)).
- **From an extension (in-process):** call `PlayniteApi.MainView.UIDispatcher.Invoke(() => { var w = PlayniteApi.Dialogs.GetCurrentAppWindow(); /* Show, WindowState=Normal, Activate, Topmost toggle */ })`. This mirrors Playnite's own `WindowUtils.RestoreWindow` ([`WindowFactory.cs` L189-230](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Windows/WindowFactory.cs#L189-L230)).
- **Via IPC:** start `playnite://playnite/restore`, which the Fullscreen VM handles by closing any child window and calling `RestoreWindow()` ([`FullscreenAppViewModel.cs` L1150-1170](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs#L1150-L1170)). Alternatively, re-launch `Playnite.FullscreenApp.exe`; the running instance receives `CmdlineCommand.Focus` and calls `Restore()` ([`PlayniteApplication.cs` L808-810](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/App/PlayniteApplication.cs#L808-L810)). PlayniteGameOverlay uses the re-launch approach (`ShowPlaynite()` in [`PlayniteGameOverlay.cs`](https://github.com/jonosellier/PlayniteGameOverlay/blob/master/PlayniteGameOverlay.cs)).
- **Controller input while a game has focus:** `OnControllerButtonStateChanged` still fires for plugins when Playnite is inactive. Only Playnite's own WPF input processing is turned off (`StandardProcessingEnabled = IsActive`) ([`GameController.cs` L514-534](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Input/GameController.cs#L514-L534), [`FullscreenApplication.cs` L166-169](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/FullscreenApplication.cs#L166-L169)). An extension can therefore implement a Guide-button overlay. Caveat: Windows Game Bar may intercept the Guide button. This is general Windows behaviour and was not verified here.

### Detect the current game — yes

- Events: `OnGameStarting` (can set `CancelStartup`), `OnGameStarted` (`Game`, `SourceAction`, `StartedProcessId`), `OnGameStopped` (`ElapsedSeconds`, `ManuallyStopped`), `OnGameStartupCancelled` ([`ApplicationEvents.cs` L132-215](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/PlayniteSDK/Events/ApplicationEvents.cs#L132-L215)).
- Polling: `Database.Games.Where(g => g.IsRunning || g.IsLaunching)`.
- `StartedProcessId` is set only "if startup source sets it" ([SDK changelog 6.3.0](https://github.com/Playnite/Docs/blob/41d6529c7b96335211777031e5a7b36b86cd982a/docs/changelog.md)). For library-plugin launches through Steam, Epic and similar it is often a launcher PID or absent. PlayniteGameOverlay therefore walks the process tree and falls back to exe-path and window-title matching ([`PlayniteGameOverlay.cs`](https://github.com/jonosellier/PlayniteGameOverlay/blob/master/PlayniteGameOverlay.cs), `GetProcessTree`/`FindBestMatch`).

### Close the current game — only with your own code

- Playnite has **no API to terminate a game**. Its "stop tracking" action, `GamesEditor.CancelGameMonitoring`, only detaches the controllers and records playtime. It does not touch the process ([`GamesEditor.cs` L1275-1310](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/GamesEditor.cs#L1275-L1310)), and it isn't in the SDK.
- Extensions do it themselves: `Process.CloseMainWindow()`, a timeout, then `Kill()`. That is PlayniteGameOverlay's `CloseGame` with its CloseAndEnd / CloseWindow / EndTask behaviours ([`GameStateManager.cs`](https://github.com/jonosellier/PlayniteGameOverlay/blob/master/GameStateManager.cs)). When the tracked process exits, Playnite fires `OnGameStopped` and auto-restores Fullscreen.

### Shutdown / sleep / restart via Playnite — not via the SDK

- Shutdown, Sleep, Hibernate, Restart, Lock and Logout exist only as `MainMenuViewModel` commands inside `Playnite.FullscreenApp`. They show a confirmation dialog, then call internal `Computer.*` helpers: `QuitAndExecute(Computer.Shutdown)`, `Powrprof.SetSuspendState`, and `shutdown.exe -r -t 0` ([`MainMenuViewModel.cs` L28-33, L119-201](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite.FullscreenApp/ViewModels/MainMenuViewModel.cs#L28-L201); [`Computer.cs` L82-83, L271-299](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/Common/Computer.cs#L82-L299)). The buttons are defined in app XAML (`Windows/MainMenuWindow.xaml`), so themes can restyle them but not wire new ones elsewhere.
- An extension has to call the Win32 APIs itself: `ExitWindowsEx`/`shutdown.exe`, or `SetSuspendState` for sleep. Before shutdown it should let Playnite save, for example by running `Playnite.FullscreenApp.exe --shutdown` (the `shutdown` CLI option quits Playnite, [`CmdLineOptions.cs` L45](https://github.com/JosefNemec/Playnite/blob/ecee01452a25db1fb2674a69f883d298d4c7be8a/source/Playnite/App/CmdLineOptions.cs#L45)), or by triggering the internal command through reflection (unsupported).

---

## Implications for the Bundle design (for #11)

- Plan for **two artifacts**: a `.pthm` theme and a `.pext` Generic extension, in one repo with two installer manifests and two add-on DB entries. The theme must work, degraded, without the extension, with `{PluginStatus}` gating the extension-backed UI.
- Put Xbox-specific widgets such as the Guide overlay, power menu, clock and "quit game" in the extension. They reach the theme either as `ContentControl` slots in Main, GameDetails or the list item, or as separate `CreateWindow` windows for anything outside those three views.
- Treat game-close, power actions and opening Playnite's own menus as **extension-implemented features on top of Win32**, not Playnite APIs.
- Risk to watch: the extension's availability in the add-on browser is blocked until the Playnite 11 add-on DB is backported. Until then it is distributed as a GitHub `.pext` or a `playnite://` link.
