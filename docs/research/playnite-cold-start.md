# Where Playnite's cold start to Start-up goes, and what cuts it

Research for [#101](https://github.com/snrgarb/xbox-series-theme/issues/101) (part of map #96). Researched 2026-10-21.

**Sources.**

- **Playnite**: 10.63, tag [`f6424bb`](https://github.com/JosefNemec/Playnite/tree/f6424bba5676e78380f6e52a4784bd8a6f754d42). `PN` = `https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source`.
- **The Bundle**: `origin/main` @ [`d8f620a`](https://github.com/snrgarb/xbox-series-theme/tree/d8f620a). `XS` = `https://github.com/snrgarb/xbox-series-theme/blob/d8f620a/src/XboxSeries.Extension`.
- **This PC's logs**, read only:
  - `%APPDATA%\Playnite\playnite.log`: 992 process starts and 497 Fullscreen starts since 2026-10-06.
  - `extensions.log`: 123 `[Start-up timing] Start-up shown` blocks since 2026-10-19.
  - The System log: 32 boots (Kernel-General 12) and 30 sign-ins (Winlogon 7001).
  - The running Playnite's modules, the native image cache and the NGEN logs.
- **Docs**: [Playnite's command-line arguments](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html); Microsoft's [Ngen.exe](https://learn.microsoft.com/dotnet/framework/tools/ngen-exe-native-image-generator) and [ProfileOptimization](https://learn.microsoft.com/dotnet/api/system.runtime.profileoptimization); the .NET blog's [multicore JIT post](https://devblogs.microsoft.com/dotnet/an-easy-solution-for-improving-app-launch-performance/).

Playnite wasn't started, closed or restarted for this research, and nothing on the PC was changed.

## TL;DR

- **Where the time goes.** Tonight's 7.9 s runs from DesktopApp's first log line to Start-up's first frame. It is one long single-threaded chain on Playnite's UI thread:

  | Phase | Time |
  |---|---|
  | DesktopApp's hop to FullscreenApp | 1.2 s |
  | Playnite's own start-up: WPF, the WCF pipe, settings, the theme | 1.4 s |
  | CEF, SDL and audio | 1.2 s |
  | 16 extensions' constructors | 2.9 s |
  | A PowerShell script extension | 0.5 s |
  | The main window | 0.3 s |
  | The Bundle beginning Start-up, at Home's first element | 0.1 s |
  | Start-up's own first frame, on its thread | 0.2 s |

  The library database opens *after* Start-up shows, so it isn't on the path. Neither is the library update, which `--nolibupdate` skips.
- **Cold against warm.** The same chain takes about **3.8 s** when the PC is warm. The extra ~3–4 s on a cold boot is file I/O and contention with everything else starting after sign-in (Discord's six processes started 33–36 s into tonight's boot), not JIT. JIT costs the same cold or warm.
- **The cuts, by size.**
  1. **The Bundle begins Start-up in its extension's constructor** instead of at Home's first element: **0.9–1.5 s** cold. If the extension also **loaded first** rather than last, the cut grows to **3.5–4.2 s**. It loads last because Playnite loads extensions in folder-name order, and its folder sorts last.
  2. **The tool launches `Playnite.FullscreenApp.exe --hidesplashscreen` directly**, with Playnite's own "Start on boot" turned off: **~1 s** (the hop measures 1.2–1.35 s).
  3. **Extension choices**:
     - Drop the *Extra Metadata tools* PowerShell script extension: **0.5–0.9 s**.
     - Drop UniPlaySong: **0.75 s**. It is also #97's trigger.
     - Drop unused metadata providers: up to **0.3 s**.
  4. **Auto-backup "once a day" → weekly or off.** The day's first start backs up and then restarts Playnite. That costs **~1.3–2.2 s** once a day, on the cold boot when that's the day's first start.
  5. **NGEN.**
     - Playnite runs **32-bit**, and this PC has no x86 native images for System.ServiceModel (Playnite's pipe), WinForms, System.Drawing or PowerShell's System.Management.Automation.
     - Playnite's own assemblies are already helped by multicore JIT.
     - NGEN needs **admin every time**: once, then again after every Playnite update or Bundle redeploy.
     - Expected gain: a few hundred ms, unmeasured. Worth a trial, not a spec commitment.
- **`--nolibupdate`** saves nothing before the first frame. It does remove ~11 s of background importing that runs during Start-up, including Battle.net logging in through a hidden CEF browser. It also clears Playnite's "crashed while starting" flag ~10–20 s sooner (§5).

---

## 1. Phases, from many starts

The phases come from log lines Playnite and the Bundle already write, matched per start:

- Each process's `ProgramEntry:App arguments` line.
- DesktopApp's `ProcessStarter: Starting process … Playnite.FullscreenApp.exe` line.
- `PlayniteApplication: Application started from`.
- Each `ExtensionFactory: Loaded plugin`.
- `Loaded script extension`.
- `WindowFactory: Show window …FullscreenApp.Windows.MainWindowFactory`.
- The Bundle's `[Start-up timing] Start-up shown at 0 ms` and `Start-up window's first frame`.

A start counts as **cold** when it is the first Playnite process after a boot and starts within 5 minutes of the Winlogon sign-in (event 7001). Three cold starts carry the Bundle's timing lines, all from today on Playnite 10.63:

| # | Phase (what runs) | 15:48 | 21:06 | **21:15** | Warm median (n≈55) |
|---|---|---|---|---|---|
| 1 | DesktopApp, from its first log line until it spawns FullscreenApp: WPF `Application`, the WCF pipe, settings, then the relaunch | 0.86 | 0.64 | **0.67** | 0.43 (warm hops, n=9) |
| 2 | FullscreenApp's process start → its first log line (CLR, NLog) | 0.50 | 0.62 | **0.56** | 0.26 (warm hops, n=9) |
| 3 | FullscreenApp's first log line → "Application started": instance check, WPF `Application`, the WCF pipe, settings, extension install queue, theme load | 1.54 | 1.77 | **1.40** | 0.88 |
| 4 | "Application started" → first "Loaded plugin": `Cef.Initialize`, registry writes, `InstantiateApp`, SDL, audio, manifest scan, then Battle.net's constructor | 1.67 | 1.37 | **1.24** | 0.60 |
| 5 | The other 15 extensions' load and constructors | 2.94 | 3.05 | **2.94** | 1.30 |
| 6 | `ExtraMetadataTools.psm1`, the PowerShell runspace | 0.81 | 0.86 | **0.54** | 0.64 |
| 7 | → main window shown: third-party tools scan, window made | 0.32 | 0.50 | **0.29** | 0.10 |
| 8 | Main window → Start-up shown: WPF applies the Dashboard's Main template, which asks the Bundle for `XboxSeries_Hero`, and the Bundle begins Start-up there | 0.12 | 0.13 | **0.10** | 0.09 |
| 9 | Start-up shown → Start-up window's first frame, on its own thread | 0.27 | 0.24 | **0.19** | 0.18 |
| | **Total, DesktopApp's first line → first frame** | 9.01 | 9.18 | **7.93** | 3.81 (direct, phases 3–9) |

All times are in seconds.

Notes:

- **Before phase 1.** Explorer's launch of the Startup-folder shortcut and DesktopApp's own CLR start come before DesktopApp's first log line, and nothing logs them. FullscreenApp's process (start time 21:15:38.839) took 0.23 s to reach its first log line with the framework already warm. DesktopApp's equivalent on a cold boot is likely ≥0.25 s and belongs to #104's explorer-to-Playnite gap.
- **The ticket's 7.7 s** is to "Start-up shown" (21:15:45.573). The first frame is 0.19 s later.
- **Where the warm figures come from.**
  - Phases 3–9 are direct FullscreenApp starts since 2026-10-19 (the user's dev restarts), with the 10th–90th percentile within ±0.2 s except phase 9 (0.16–0.40).
  - Phases 1–2 come from the 9 warm hops on record (2026-10-11 to 13).
- **History: the PS5-Experience theme.** The 12 earlier cold hops (2026-10-06 to 13) ran it, before the Dashboard. Phase 3 took **3.7–4.4 s** cold and ~3.5 s warm, against ~0.9 s warm with the Dashboard, and total starts ran 8.2–11.5 s. Theme loading lives in phase 3 (`ThemeManager.ApplyTheme`, [PN/Playnite/App/PlayniteApplication.cs#L345-L354](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L345-L354)). The Dashboard is cheap there.

### What each phase is, in source

- **The hop (phases 1–2).** Playnite's own "Start on boot" always writes `Startup\Playnite.lnk` → `Playnite.DesktopApp.exe --hidesplashscreen`, and rewrites it on every start while the setting is on ([PN/Playnite/SystemIntegration.cs#L45-L74](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/SystemIntegration.cs#L45-L74), called from [PlayniteApplication.cs#L1088-L1095](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L1088-L1095)). DesktopApp's constructor does all of this before it notices `StartInFullscreen` and relaunches FullscreenApp:
  - creates the WPF `Application` ([#L149-L155](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L149-L155))
  - opens the WCF named-pipe service ([#L157-L171](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L157-L171); `ServiceHost` + `NetNamedPipeBinding` in [PN/Playnite/PipeServer.cs#L84-L94](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/PipeServer.cs#L84-L94))
  - loads settings ([#L173-L174](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L173-L174))

  The relaunch itself is at [#L227-L248](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L227-L248). FullscreenApp then repeats the same work for itself.
- **Phase 3.** `PlayniteApplication`'s constructor runs these steps, then WPF raises `Startup`, which logs "Application started from" ([#L618-L620](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L618-L620)):
  - the single-instance check, skipped with `--masterinstance` ([#L121-L129](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L121-L129))
  - the safe-start flag ([#L131-L147](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L131-L147))
  - the WPF `Application`, the pipe and settings
  - `WaitForOtherInstacesToExit`, which sleeps 500 ms per loop while another Playnite process lives, and the extension install queue ([#L266-L287](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L266-L287))
  - App.xaml's default theme and the custom theme ([#L289-L354](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L289-L354))
- **Phase 4.** `FullscreenApplication.Startup` ([PN/Playnite.FullscreenApp/FullscreenApplication.cs#L81-L111](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite.FullscreenApp/FullscreenApplication.cs#L81-L111)) runs these steps:
  - `ConfigureApplication` ([PlayniteApplication.cs#L1047-L1116](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L1047-L1116)): a synchronous `Cef.Initialize` ([PN/Playnite/CefTools.cs#L50](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/CefTools.cs#L50)), whose four `Playnite.BrowserProcess` children started 40.73–41.07 tonight with "Application started" at 40.47, followed by the start-on-boot shortcut, the `playnite://` URI and file-type registrations
  - `InstantiateApp`, then `InitSDL`, `SetupInputs` and `InitializeAudio`
  - `OpenMainViewAsync`, which loads the plugins synchronously ([#L173-L195](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite.FullscreenApp/FullscreenApplication.cs#L173-L195))

  So the first "Loaded plugin" gap, Battle.net's, is mostly Playnite's own work. The `--safestartup` run on 2026-10-13 20:47 (warm) shows the same 0.68 s before its first plugin.
- **Phase 5.**
  - **What each gap measures.** `ExtensionFactory.LoadPlugins` loads each manifest's assembly, scans its types, constructs the plugin, then logs "Loaded plugin" ([PN/Playnite/Plugins/ExtensionFactory.cs#L390-L494](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Plugins/ExtensionFactory.cs#L390-L494)). The gap before each line is that extension's load, type scan and constructor.
  - **Load order.** The order is whatever `DirectoryInfo.GetDirectories` returns for `%APPDATA%\Playnite\Extensions` ([GetInstalledManifests #L231-L293](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Plugins/ExtensionFactory.cs#L231-L293), [SafeFileEnumerator.cs#L136](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Common/SafeFileEnumerator.cs#L136)). On NTFS that is the folder names' sorted order. Each folder is named after the extension's Id ([ExtensionInstaller.cs#L205](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Plugins/ExtensionInstaller.cs#L205)), and the Bundle's `XboxSeries_eb759ff1-…` sorts **last**.
- **Phase 6.** `LoadScripts` walks the manifests again, then makes a PowerShell runspace for each script extension ([#L322-L388](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Plugins/ExtensionFactory.cs#L322-L388)). The only one here is *Extra Metadata tools*. The safe start, which had none, went from its last plugin to the main window in 0.13 s.
- **Phases 7–9.** `FullscreenAppViewModel.OpenView` calls `Window.Show` first ([PN/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs#L867-L887](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs#L867-L887)). Applying the Dashboard's Main template asks the Bundle for `XboxSeries_Hero`. `GetGameViewControl` calls `BeginStartup` ([XS/XboxSeriesPlugin.cs#L108-L133](https://github.com/snrgarb/xbox-series-theme/blob/d8f620a/src/XboxSeries.Extension/XboxSeriesPlugin.cs#L108-L133), [#L368-L386](https://github.com/snrgarb/xbox-series-theme/blob/d8f620a/src/XboxSeries.Extension/XboxSeriesPlugin.cs#L368-L386)), which starts the `StartupThread` ([XS/StartupThread.cs#L64-L93](https://github.com/snrgarb/xbox-series-theme/blob/d8f620a/src/XboxSeries.Extension/StartupThread.cs#L64-L93)). The first frame is the `StartupWindow`'s `ContentRendered` ([XS/StartupWindow.xaml.cs#L96](https://github.com/snrgarb/xbox-series-theme/blob/d8f620a/src/XboxSeries.Extension/StartupWindow.xaml.cs#L96)).

### What runs after the first frame (not on the stopwatch)

Everything below runs on Playnite's UI thread behind Start-up, which has its own thread. It decides when Home is ready, not when Start-up shows.

- **`InitializeView`** ([FullscreenAppViewModel.cs#L971-L1048](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs#L971-L1048)) runs these steps:
  - opens the **library database**: "Opening db" 0.39–0.51 s after the main window, `DatabaseLoadCollections` 231–248 ms cold and 133 ms warm
  - builds the games collection view: 0.86 s tonight
  - raises every extension's `OnApplicationStarted`. The Bundle's began **1.30–1.50 s** after Start-up showed (cold; 0.91 s warm) and took 115–160 ms.
- **The UI thread's first idle** came at **2.08–2.47 s** after Start-up showed (cold; 1.67 s warm).
- **The startup library update** (`ProcessStartupLibUpdate`, [PN/Playnite/ViewModels/MainViewModelBase.cs#L526-L551](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/ViewModels/MainViewModelBase.cs#L526-L551)) starts after all of that. Tonight it ran 21:15:47.0–57.8:
  - Battle.net: 2.3 s. It found 0 games and failed its linked-account import after opening a CEF browser to battle.net's login (`cef.log`, a new `Playnite.BrowserProcess` at 21:15:48.4).
  - Steam: 1.2 s.
  - Xbox: 7.2 s.
  - Then a metadata `ProgressWindow` opened at 21:15:58.8.

## 2. Cold against warm, and what JIT has to do with it

- **The gap.** FullscreenApp's first log line to Start-up's first frame takes **6.7–7.9 s cold** against **3.8 s warm**. Every phase grows on a cold boot. The biggest growths are phase 5 (+1.7 s), phase 4 (+0.7–1.1 s) and phase 3 (+0.5–0.9 s).
- **Why it isn't JIT.**
  - JIT is the same CPU work cold or warm, and Playnite already uses multicore JIT on both. Both executables call `ProfileOptimization.SetProfileRoot(%APPDATA%\Playnite\JITProfiles)` and `StartProfile("desktop" | "fullscreen")` ([PN/Playnite.FullscreenApp/ProgramEntry.cs#L46-L48](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite.FullscreenApp/ProgramEntry.cs#L46-L48)). On this PC the `fullscreen` profile is 72 KB and was rewritten today.
  - So the cold excess is reading files for the first time and sharing the CPU and disk with sign-in. Tonight, explorer and its hosts started at 21:15:26.9 and the Run-key apps at 31.3–32.9. Discord's six processes started 32.9–36.0, then Playnite at 37.8.
- **The PC.** A Ryzen 7 5800X3D (8 cores, 16 threads) with NVMe SSDs, and SysMain (the prefetcher) running.
- **Bundle-era samples.** There are only three cold starts with the Bundle, so treat the cold columns as ±0.5 s.

## 3. Cuts the tool can make in how it launches Playnite

| Cut | Saves | Notes |
|---|---|---|
| **Launch `Playnite.FullscreenApp.exe --hidesplashscreen` directly**, not DesktopApp | **~1 s cold** (phases 1–2: 1.23–1.35 s; ~0.7 s warm) | Some of DesktopApp's first-read file I/O moves into FullscreenApp's own cold start, so expect a little less than the full 1.2–1.35 s. See the requirements below the table. |
| **Start Playnite before the other startup apps** (the map's decision) | Part of the 3–4 s cold excess; unmeasured | Tonight Discord started 5 s before Playnite and was still spawning while Playnite started. Measure with the tool's own boot timeline. |
| **`--nolibupdate`** | **0 s** to the first frame | Removes ~11 s of importing that runs during the Boot video and Who's playing today?. Removes Battle.net's hidden CEF login and the metadata `ProgressWindow`. Clears the "crashed while starting" flag at once (§5). The cost is that games installed outside Playnite appear only after a manual update, or the Bundle's Update after downloads. Playnite's setting to check for library updates on start-up does the same without a flag. |
| **NGEN**, see §6 | A few hundred ms, unmeasured | Needs admin each time. A trial, not a spec item. |
| Raise Playnite's priority, or pre-read its files | Unknown | Untested ideas. SysMain already prefetches launched apps, so pre-reading files probably adds little. |

Requirements for launching FullscreenApp directly:

- **Turn off Playnite's own "Start on boot".** While it's on, Playnite re-creates `Startup\Playnite.lnk` → DesktopApp on every start, and that would launch a second Playnite.
- **`--startfullscreen` is not needed.** It only matters for DesktopApp, or for switching a running Desktop instance.
- **Don't pass `--masterinstance`.** It skips the single-instance check ([#L121-L129](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L121-L129)). Without it, a second launch while Playnite runs only pipes "Focus" or "SwitchMode" to the running one and exits ([#L962-L1045](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L962-L1045)), which is useful to #105.

Playnite's docs list `--hidesplashscreen`, `--nolibupdate`, `--startfullscreen` and `--safestartup`. They don't list `--masterinstance` or `--startclosedtotray`, which exist only in [CmdLineOptions.cs](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/CmdLineOptions.cs).

## 4. Cuts from Playnite settings and extension choices

Each extension's own load and constructor, from the gaps between "Loaded plugin" lines. Cold is the median of the three Bundle-era cold starts; warm is the median of 95 warm starts since 2026-10-20. All times in ms.

| Extension | Cold | Warm | Keep? |
|---|---|---|---|
| Battle.net library (first, so its line also carries phase 4's CEF, SDL and audio) | 1373 | 572 | Its own constructor is small. 0 Battle.net games, and its linked-account import fails every start. **Disable**: no gain before the first frame, but no CEF login during Start-up. |
| PlayniteAchievements | **1124** | 641 | The costliest. The Bundle's achievement Toasts and Score need it. Keep. |
| UniPlaySong | **757** | 286 | Its folder is 113 MB, and it is #97's trigger. **Disable** unless its music is wanted. |
| *Extra Metadata tools* (PowerShell script) | **807** (535–857) | 635 | Only adds menu commands. **Disable.** |
| Steam library | 427 | 71 | Needed. |
| Xbox Series (the Bundle) | 332 | 71 | §5 |
| Metadata providers: Extra Metadata Loader 40, SteamGridDB 77, Xbox Metadata 75, Universal Steam 53, Wikipedia 47, IGDB 23, GOG 13, IGN 7, Nintendo and Universal PSN 0 | ~335 in all | ~110 | Used only when downloading metadata, including the Bundle's Download metadata box. Disable the ones not wanted. |
| Xbox library | 22 | 8 | Needed for Game Pass. |

Settings:

- **Auto-backup, "once a day".**
  - **How it works.** `AutoBackupEnabled` is on with `OnceADay`. The first start on a new calendar date writes a backup action and runs the backup instead of starting normally ([PlayniteApplication.cs#L178-L185](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L178-L185), [#L628-L673](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L628-L673), [PN/Playnite/Settings/PlayniteSettings.cs#L2782-L2798](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Settings/PlayniteSettings.cs#L2782-L2798)). Then it **restarts Playnite**.
  - **How often.** The log shows it on 12 of the 13 days from 2026-10-09 to today. One of those was a cold boot: on 2026-10-11 at 12:53 the first DesktopApp spent 2.2 s before relaunching DesktopApp, which then hopped to FullscreenApp. Today at 10:07, launched as FullscreenApp, it cost 1.3 s warm.
  - **Why it matters here.** Once the TV PC's first Playnite start each day is its cold boot, it lands there every day.
  - **The cut.** Set it to once a week, or off, and back up some other way. That saves **1.3–2.2 s** on the day's first boot.
- **The startup library update**: see `--nolibupdate` in §3.
- **The PS5-Experience theme and its PS5Core extension** (disabled, 158 MB) are still installed. Playnite reads only their manifests, so uninstalling them is tidiness, not speed.

## 5. What the Bundle would have to change

| Change | Saves | Notes |
|---|---|---|
| **Begin Start-up in the extension's constructor**, not at Home's first element | **0.93–1.49 s cold** (phases 6–8 after the extension's line); ~0.83 s warm | See the details below the table. Worth its own issue. |
| **…and load the extension first** | **3.5–4.2 s cold in all** (another 2.6–2.7 s); ~2.1 s warm | See the details below the table. Needs grilling. |
| **Slim the extension's constructor** | Up to ~0.3 s cold | 320–357 ms cold against 42–71 ms warm. It makes every adapter at once: Win32 windows, the sounds, Steam downloads, an `HttpClient` for gamerpics, the PlayniteAchievements source, the core with WinRT pad input, Windows audio, the Discord pipe and OAuth, DPAPI and media sessions ([XS/XboxSeriesPlugin.cs#L66-L106](https://github.com/snrgarb/xbox-series-theme/blob/d8f620a/src/XboxSeries.Extension/XboxSeriesPlugin.cs#L66-L106)). Defer what Start-up doesn't need. Measure which parts cost first. |
| **A sooner first frame on the Start-up thread** | ~0.1 s | 0.19–0.27 s now cover the theme-resource copy, a new STA thread, the window's XAML and the Boot video's source. A bare black frame could come first. |
| **#97** (UniPlaySong kills the Start-up thread) | n/a | Already filed. Dropping UniPlaySong (§4) also removes the trigger. |

Beginning Start-up in the constructor:

- **Why it works.** Start-up already runs on its own thread and reads the pad itself. The theme's resources are loaded by then, because the theme loads in phase 3.
- **What it needs.**
  - `ShowStartup` must cope with no Playnite main window at all. Today it hooks `SourceInitialized` only when `Application.Current.MainWindow` exists ([#L411-L442](https://github.com/snrgarb/xbox-series-theme/blob/d8f620a/src/XboxSeries.Extension/XboxSeriesPlugin.cs#L411-L442)).
  - Playnite's window must still come up under the topmost Start-up window, without stealing it.

Loading the extension first:

- **How.** Playnite loads extensions in folder order, and the folder is named after the extension's Id. An Id that sorts before `BattlenetLibrary_Builtin` would put Start-up right after phase 4.
- **The risks.**
  - .NET doesn't promise `GetDirectories`' order.
  - A new Id is a breaking change: a new install folder and new disabled-list entries.
  - The Boot video would then play while the UI thread loads 15 extensions, so it must be tried on the TV for stutter.

The Dashboard theme itself is cheap. Switching to it from PS5-Experience cut ~2.6 s from every start (§1), so there's nothing to cut there.

## 6. NGEN, in detail

- **Playnite is 32-bit.** `Playnite.FullscreenApp.exe` is PE32/x86 (machine `0x14C`) and runs under WOW64. Any NGEN must use `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\ngen.exe`.
- **What the running Playnite uses natively.** It loads x86 native images only for these Windows-maintained framework assemblies:
  - mscorlib, System, System.Core and System.Xml(.Linq)
  - WPF: PresentationCore, PresentationFramework(.Aero2), WindowsBase and System.Xaml
  - System.Configuration and System.Runtime.Serialization
  - three small PowerShell helpers
- **What this PC's x86 native image cache lacks.** `C:\Windows\assembly\NativeImages_v4.0.30319_32` has 56 entries. It has nothing for these, so they are JIT-compiled on every start:
  - **System.ServiceModel**: Playnite's WCF pipe, in both DesktopApp and FullscreenApp.
  - **System.Windows.Forms** and **System.Drawing**: Playnite reads the screens through WinForms' `Screen.AllScreens` ([PN/Playnite/Common/Computer.cs#L231](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Common/Computer.cs#L231)).
  - **System.Management.Automation**: the PowerShell script extension.

  Windows' NGEN task ran today at 17:42 and logged "All compilation targets are up to date", so nothing will fill them in by itself.
- **Admin.** Ngen.exe writes the machine-wide native image cache and is run "as an administrator" ([EF6's NGen guide](https://learn.microsoft.com/ef/ef6/fundamentals/performance/ngen#how-to-use-ngenexe)). The map rules out admin *at boot*, so this would be a one-time setup step, repeated as described next.
- **When the images stop counting.** A native image is invalid once its assembly, or any assembly it references, is recompiled ([Ngen.exe: invalid images](https://learn.microsoft.com/dotnet/framework/tools/ngen-exe-native-image-generator#native-images-and-jit-compilation)). In practice:
  - **Playnite's own assemblies** (Playnite, Playnite.SDK, Playnite.Common, the two executables, LiteDB and the other libraries next to them) go stale at every Playnite update. 10.62 → 10.63 landed on 2026-10-19/20. Each update means another admin `ngen update`.
  - **.NET Framework updates** run `ngen update` themselves, so framework images survive. Images of Playnite's assemblies are regenerated then too, if they were installed as roots.
  - **Extensions.** Every Bundle redeploy (many a day in development) and every extension update makes their images stale. Playnite also loads extensions by path, `Assembly.Load(AssemblyName.GetAssemblyName(path))` ([#L461-L465](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/Plugins/ExtensionFactory.cs#L461-L465)). Whether the runtime uses native images for assemblies bound that way is **unverified**. Check with Fuslogvw's *Native Images* log, or look for `.ni.dll` among Playnite's modules, after a trial.
- **Overlap with multicore JIT.** Microsoft pitches multicore JIT "for applications that do not use Ngen" and reports 16–35 % start-up gains for WPF apps. Playnite already has those gains, so NGEN's extra win on Playnite's own code is smaller.
- **What it can cut.** JIT costs the same cold or warm (§2), so NGEN can only cut inside the ~3.8 s warm chain, never the 3–4 s cold excess.
- **The likeliest wins**, by guess:
  - NGEN'ing the four missing x86 framework assemblies: once, survives Playnite updates.
  - Then Playnite's own assemblies: after every update.

  Plausibly 0.2–0.8 s in all, but **unmeasured**. A trial on the TV PC, read off the same log lines, would settle it.

## 7. Notes for other tickets on the map

- **#104 (startup apps).**
  - Startup-folder items don't appear in Shell-Core's Run-key events (9705–9708). Tonight the Run keys ran 21:15:31.3–32.9 and DesktopApp first logged at 37.8.
  - Playnite's "Start on boot" must be off once the tool launches it.
- **#105 (keep Playnite up, fall back).** Two things bear on a relaunch:
  - **The safe-start prompt.** Release builds create a "safe startup" flag file as they start and delete it only after the startup library update finishes ([FullscreenApplication.cs#L191-L194](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite.FullscreenApp/FullscreenApplication.cs#L191-L194)). That is ~10–20 s after Start-up with the update on, and at once with `--nolibupdate`. If Playnite dies before then, the next start shows a **blocking MessageBox** ("Playnite closed unexpectedly while starting… start in safe mode?", [#L131-L147](https://github.com/JosefNemec/Playnite/blob/f6424bba5676e78380f6e52a4784bd8a6f754d42/source/Playnite/App/PlayniteApplication.cs#L131-L147)), which the pad may not be able to answer.
  - **Second launches.** Without `--masterinstance`, a second launch just hands its arguments to the running Playnite and exits.
- **#106 (which cuts).** Sections 3–6 above are the menu.
- **A new Bundle issue is worth filing**: "Begin Start-up from the extension's constructor". Whether the extension should also load first is a decision for #106.
