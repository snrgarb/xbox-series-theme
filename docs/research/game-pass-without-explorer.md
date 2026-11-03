# What Game Pass games need from explorer to launch from Playnite

Research for [#100](https://github.com/snrgarb/xbox-series-theme/issues/100), part of the map [#96](https://github.com/snrgarb/xbox-series-theme/issues/96). It covers this TV PC only: Windows 11 Pro Insider 26300.9457, Playnite 10.63, and Playnite's Xbox library plugin 2.17.

Sources were read on 21 October 2026. Each claim is tagged with its source. **[this PC]** marks something read on this PC, read-only (Playnite's logs, package manifests, the event log and the registry). **[reported]** marks community reports, not Microsoft documentation. **[inference]** marks reasoning from those sources that nobody has tested yet. Nothing was launched, and the shell was not changed.

## Answer

**The need comes from activation itself, not only from Playnite's launch method.** A Game Pass game is an MSIX package. It has to be *app-launched*, which means activated through its package, to get its identity and for Gaming Services to run its launch flow. That activation goes through the shell's activation manager (`twinui.appcore.dll`). Windows has an error for when the shell side isn't there: `0x80270257`, "The app manager is required to activate applications, but is not running." Every report of a custom shell without explorer says Store/UWP launches fail. Shell Launcher v2 had to add a stand-in shell host (`CustomShellHost.exe`) so that a custom shell could launch them, and Shell Launcher isn't available on Pro.

- **How Playnite launches them (confirmed).** The Xbox plugin runs `explorer.exe shell:AppsFolder\<PackageFamilyName>!<AppId>`, for example `explorer.exe shell:AppsFolder\ROBLOXCorporation.RobloxGDK_55nm5eh3cm0pr!Game`. It does not call `IApplicationActivationManager` itself.
- **Can they launch with no explorer shell running? Almost certainly not, by any route.** A direct `ActivateApplication`, `start shell:AppsFolder\…`, a protocol link into the package, and the Xbox app all go through the same activation. Running the game's exe directly skips app-launch, which the GDK says breaks the Gaming Runtime and cloud services. [inference from the reports in §3; the user's own report agrees; a TV test would confirm it for GDK games on 26300]
- **What Playnite's `explorer.exe` does when no shell is running.** Explorer becomes the full shell (desktop and taskbar) only when no shell window exists *and* the Winlogon `Shell` value it checks is `explorer.exe`. Otherwise it only opens a folder window. On #99's planned set-up (HKCU `Shell` = the tool, HKLM left at `explorer.exe`), Playnite's own `explorer.exe shell:AppsFolder\…` would probably **become the shell**. Whether it then also launches the game, and how long that takes, needs a test on the TV.
- **Launch-time dependencies outside explorer.** Gaming Services is an Automatic service, so it runs whatever the shell. On every launch it runs the PC Bootstrapper (`GamingServicesUI.exe`), which shows a splash, updates the game, syncs cloud saves and keeps one instance. Sign-in is silent through the OS (XUser), and the Xbox app doesn't need to be open.
- **Xbox mode.** Microsoft documents that Xbox mode suppresses "the taskbar, some startup apps, and other services". It doesn't say explorer is off. Xbox mode starts a packaged home app at sign-in and launches Game Pass games, so whatever it runs as the shell keeps package activation working. Whether that is explorer.exe in Task Manager isn't published; #98 can check.

**Cheapest way to have it when explorer is held back:** start explorer as the shell **late**, after Start-up's first frame, so the stopwatch never sees it. Add a guard on Game Pass launches that starts explorer (or waits for it) if it isn't up yet. "Launch them differently" has no route on Pro that avoids the shell, unless #98's route ends up running under Xbox mode. See [§6](#6-options-when-explorer-is-held-back).

## 1. How Playnite starts a Game Pass game

**The play controller.** `XboxPlayController.Play` looks the game up in `Programs.GetUWPApps()` by package family name. It then calls `ProcessStarter.StartProcess(prg.Path, prg.Arguments)` and watches the install directory's processes to tell when the game starts and stops. [PlayniteExtensions `XboxLibrary/XboxGameController.cs`](https://github.com/JosefNemec/PlayniteExtensions/blob/master/source/Libraries/XboxLibrary/XboxGameController.cs)

**The command line.** `GetUWPApps()` builds every packaged app's launch as `Path = "explorer.exe"`, `Arguments = $"shell:AppsFolder\\{package.Id.FamilyName}!{appId}"`. The `appId` is the first `<Application>` in the package's `AppxManifest.xml`. [Playnite `source/Playnite/Common/Programs2.cs`, lines 299–411](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Common/Programs2.cs) `StartProcess` wraps `Process.Start(new ProcessStartInfo(path) { Arguments = … })`, which is a ShellExecute on .NET Framework. [Playnite `source/Playnite/Common/ProcessStarter.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Common/ProcessStarter.cs) Josef Nemec puts it the same way: "UWP/MS Store/GamePass games are launched by running explorer.exe process with an argument to run specific UWP app/game." [Playnite #4016](https://github.com/JosefNemec/Playnite/issues/4016)

**This PC runs exactly that.** [this PC]
- Playnite is 10.63, and the installed Xbox plugin is `XboxLibrary_Builtin` 2.17, the same version as upstream master.
- `Playnite.dll` contains the string `shell:AppsFolder\`.
- `%APPDATA%\Playnite\extensions.log` logs each launch, for example:
  `21-10 20:16:51.929|DEBUG|XboxLibrary#ProcessStarter:Starting process: explorer.exe, shell:AppsFolder\38985CA0.MWIIIGame_5bkah9njm3e9g!codShip, , False`
  (`playnite.log` only logs `GamesEditor:Starting …`, plus the plugin id `7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287`.)

**The games on this PC are GDK games.** All three installed Game Pass or Store games have the same entry point, `<Application … Executable="GameLaunchHelper.exe" EntryPoint="Windows.FullTrustApplication">`. They are `38985CA0.MWIIIGame` (`!codShip`), `Microsoft.MinecraftUWP` (`!Game`) and `ROBLOXCorporation.RobloxGDK` (`!Game`). All three live under `C:\Program Files\WindowsApps`. None declares an app execution alias. Roblox declares the protocols `ms-xbl-681f8096`, `ms-xbl-multiplayer`, `roblox` and `roblox-uwp-migration`. [this PC, `Get-AppxPackage` and the manifests, read-only]

**Extensions get a hook before the launch.** Playnite raises `OnGameStarting` synchronously to every plugin, and checks `CancelStartup`, *before* it calls the Xbox controller's `Play`. [Playnite `GamesEditor.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/GamesEditor.cs) (`controllers.InvokeOnStarting` → `controller.Play`), [`Plugins/ExtensionFactory.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Plugins/ExtensionFactory.cs) (`Controllers_Starting` loops `plugin.Plugin.OnGameStarting(args)`) So the Bundle's extension, or a script, can make sure explorer is up before Playnite runs `explorer.exe shell:AppsFolder\…`, without replacing the Xbox plugin. [inference]

## 2. What a launch goes through

1. **Activation.** `shell:AppsFolder\<AUMID>` names the app in the shell's Apps folder, and invoking it activates the package. The documented API for that is `IApplicationActivationManager::ActivateApplication`, which "activates the specified Windows Store app for the generic launch contract (Windows.Launch) in the current session". [MS: ActivateApplication](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateapplication) Microsoft's own tooling docs launch a packaged app with `start shell:AppsFolder\<AUMID>` or its execution alias. [MS: winapp CLI, Debugging with Package Identity](https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/debugging) The COM class `Application Activation Manager` `{45BA127D-10A8-46EA-8AB7-56EA9078943C}` is implemented in `C:\WINDOWS\System32\twinui.appcore.dll`, a shell component. [this PC, registry, read-only]
2. **Who does the activation.** Each Playnite launch shows up in `Microsoft-Windows-TWinUI/Operational` as event 1621 from the provider `Microsoft-Windows-Immersive-Shell`: "Activation of app 38985CA0.MWIIIGame_5bkah9njm3e9g!codShip attempted … The operation completed successfully". It lands under half a second after Playnite's `Starting process: explorer.exe …` line (0.24 s at 20:16:51.929). Within one boot, each launch logs from a **different process id** (17056 at 19:54:52 and 8072 at 20:16:52 on 21 October, with no boot in between). So the activation runs in a per-launch process, the `explorer.exe` that Playnite starts or a COM host it uses, not in the long-running shell. [this PC, event log, read-only; which process the PID was is an inference]
3. **Gaming Services' launch flow.** The package's entry point is `GameLaunchHelper.exe`. "Whenever an installed MSIXVC packaged PC game is launched (via the Xbox app, Start menu, Desktop shortcut, or command line tools), the Gaming Runtime Services (GRTS) first invokes the PC Bootstrapper" (`GamingServicesUI.exe`). The PC Bootstrapper:
   - shows the splash screen;
   - updates the game to the latest version;
   - syncs cloud saves;
   - keeps a single instance, bringing a running one to the front;
   - starts the game, then exits once the game's window shows.

   [MS: PC Bootstrapper](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/overviews/gr-pc-bootstrapper) `GamingServices` and `GamingServicesNet` are Automatic services and are running on this PC. `XblAuthManager` (Xbox Live Auth Manager) and `TokenBroker` (Web Account Manager) are manual-start and running. `ClipSVC` (licences) is manual-start. [this PC, `Get-Service`] None of these is started by explorer. [inference: they are services]
4. **Sign-in and licence.** On Windows, "XUser uses the operating system's built-in services for authentication, UI, and credential storage". [MS: XUser cross-platform](https://learn.microsoft.com/gaming/gdk/docs/features/common/user/xuser-crossplatform) With the simplified user model, a game adds the default user silently, and the PC Bootstrapper steps in to sign someone in if nobody is. [MS: XUserAddAsync](https://learn.microsoft.com/gaming/gdk/docs/reference/system/xuser/functions/xuseraddasync) The Xbox app doesn't have to be running: the PC Bootstrapper list above includes Start menu, desktop shortcut and command-line launches. If sign-in ever needs UI, that UI is itself an app to activate, so it needs the same activation path. [inference]

**Why not run the game's exe directly.** "Through an action called app-launch, a game process is created and the game is provided with access to its persistent contextual information from the app repository. If you don't app-launch the game, a process is created to run the game, but its context will be unavailable. This prevents the game from properly using Gaming Runtime and gaming cloud services." [MS: MicrosoftGame.config overview, "Launching a Win32 game"](https://learn.microsoft.com/gaming/gdk/docs/features/common/game-config/microsoftgameconfig-overview) A direct exe launch would also skip the PC Bootstrapper's update and cloud-save steps. On this PC the exes sit in the locked-down `WindowsApps` store. [MS: How packaged desktop apps run](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-behind-the-scenes); [this PC]

## 3. Where the need for explorer comes from

The evidence points at **activation**, not at Playnite's use of `explorer.exe` as a launcher:

| Evidence | What it shows | Kind |
| --- | --- | --- |
| Windows' message for `0x80270257` (`E_APPLICATION_MANAGER_NOT_RUNNING`, FACILITY_SHELL): "The app manager is required to activate applications, but is not running." | Activation needs a shell-side "app manager" | this PC (system message table); [hresult.info](https://www.hresult.info/FACILITY_SHELL/0x80270257) for the name |
| A custom WinForms shell on Windows 8 Embedded: `IApplicationActivationManager` returned `0x80270257`. "Metro style apps run well when explorer.exe is used as a shell", and "Starting explorer.exe manually doesn't change anything" (the registry still named the custom shell, so explorer didn't become the shell) | The *direct API* fails too, so it's not just the launch method | reported, 2013 ([Stack Overflow 14850591, mirror](https://windows-hexerror.linestarve.com/q/so14850591-run-metro-app-from-custom-shell)) |
| LaunchBox BigBox as the Windows shell: every Microsoft Store game fails with "Class not registered", while Steam, Origin and Uplay games work. The workaround was a `.bat` that starts explorer.exe, launches the game, then kills explorer | Store launches fail under a custom shell on Windows 10 | reported, 2018–2024 ([LaunchBox forum](https://forums.launchbox-app.com/topic/48259-bigbox-and-windows-store-w10-games-issue/)) |
| "When explorer.exe is not exists, windows cannot start UWP application". Open UWP apps freeze while explorer is gone and recover when it returns | UWP apps lean on explorer while they run, not only at launch | reported, community answer ([MS Q&A 679864](https://learn.microsoft.com/en-us/answers/questions/679864/start-uwp(universal-windows-platform)-application)) |
| Shell Launcher v1 (`Eshell.exe`) "can only launch a Windows desktop application". v2 replaces explorer with `CustomShellHost.exe`, so "you can use a custom Windows desktop application that can then launch UWP apps, such as Settings" | Microsoft needed a stand-in shell host to let a custom shell launch packaged apps | [MS: Shell Launcher overview](https://learn.microsoft.com/windows/configuration/shell-launcher/) |
| Shell Launcher's editions are "Enterprise / Enterprise LTSC, Education, IoT Enterprise / IoT Enterprise LTSC" | Not available on this PC's **Pro** | [MS: Shell Launcher overview](https://learn.microsoft.com/windows/configuration/shell-launcher/) |
| The user's report while charting #96: Game Pass games need explorer.exe | Holds for GDK (full-trust) games on this PC | the map |

**Caveat.** Most of these reports are about UWP or older Store apps. The GDK games here are full-trust Win32 packages (`Windows.FullTrustApplication`). They go through the same `ActivateApplication` path, and the user's own report matches, but nobody has captured the exact failure (error code, or silent nothing) for a GDK game on 26300. **[needs a test on the TV: T1]**

**So no launch route avoids it.** `IApplicationActivationManager` directly, `start shell:AppsFolder\…`, a package protocol (`ms-xbl-681f8096:`, `roblox:`), the Xbox app's Play button and a Start-menu tile are all package activations. [inference] An app execution alias is a different path (CreateProcess on an alias), but none of the three games declares one, so it doesn't help here. [this PC]

## 4. What `explorer.exe shell:AppsFolder\…` does when no shell is running

**How explorer decides to be the shell.** When explorer.exe starts, it becomes the shell (desktop and taskbar) only if no shell window exists *and* the Winlogon `Shell` value names explorer.exe. Otherwise "the process will only create a file explorer window and it will NOT launch the taskbar or desktop". The ReactOS report names the HKLM value and says HKCU "may" also be checked. [ReactOS CORE-19887](https://jira.reactos.org/browse/CORE-19887) This matches the following:
- **Shell Launcher (HKLM-level custom shell).** "launching explorer.exe only opens a file explorer window, not the full desktop shell". [MS Q&A 5576492](https://learn.microsoft.com/en-us/answers/questions/5576492/how-can-we-enable-full-windows-desktop-access-from) (reported)
- **PlayniteShell.** Its `Desktop.bat` writes `explorer.exe` back to the HKLM `Shell` value, runs `start explorer.exe`, then writes its own shell back, which needs admin. [Blueforcer/PlayniteShell `Desktop.bat`](https://github.com/Blueforcer/PlayniteShell/blob/main/Desktop.bat)
- **WindowsHandheldMod.** It sets only the **HKCU** `Shell` value, and its Playnite script ends with `start explorer.exe`, which brings up the normal desktop with HKLM left at `explorer.exe`. [CaffeinatedDragonWare/WindowsHandheldMod `playnite_startup.bat`](https://github.com/CaffeinatedDragonWare/WindowsHandheldMod/blob/main/playnite_startup.bat) (reported by use)

How explorer decides is #99's question. What matters here:

- **On #99's planned set-up** (HKCU `Shell` = the tool, HKLM = `explorer.exe`), the `explorer.exe shell:AppsFolder\…` that Playnite starts would probably **become the full shell**. It's the first explorer of the session, and nothing else has marked itself as the shell window. [inference]
- **Not known yet.** Does that first explorer also act on its `shell:AppsFolder\…` argument, or only start the shell and drop it? How long after it starts does activation stop failing with `0x80270257`? Does the taskbar come up over Playnite or the game's splash? Does it run the Run keys and Startup folder at that moment (#104)? **[needs a test on the TV: T2, T3]**
- **Under an HKLM custom shell** it would only open a folder window, and the 2013 report says activation still fails then.

**When the shell is already running** (today), Playnite's `explorer.exe` does the activation itself in its own short-lived process (the per-launch PIDs in §2). That process can linger, a Windows behaviour Playnite has met before. [Playnite #4248](https://github.com/JosefNemec/Playnite/issues/4248) The 26H1 Release Preview notes say Windows now "improves the reliability of relevant explorer.exe processes so they stop after closing File Explorer windows". [MS: Release Preview Build 28000.2176](https://learn.microsoft.com/windows-insider/release-notes/release-preview-26h1/build-28000-2173)

## 5. How Xbox mode runs Game Pass games without the desktop

What Microsoft documents about the full screen experience (Xbox mode):
- "Many standard Windows experiences are suppressed, such as the taskbar, some startup apps, and other services."
- "The default app automatically launches when the device is powered on from a shutdown state."
- "When FSE is active, the taskbar is hidden."

[MS: Game launchers and handhelds](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers) Xbox mode lets you "browse and launch games" from "the full games catalog from Xbox Game Pass". [Xbox Wire, 30 Apr 2026](https://news.xbox.com/en-us/2026/04/30/xbox-mode-pc-windows-11/) Nothing from Microsoft says explorer.exe is off in Xbox mode. Secondary write-ups disagree: some say the "explorer.exe desktop shell" is bypassed, others that desktop pieces are deferred. [reported]

- **The home app is a package.** AnyFSE gets picked as Xbox mode's home app by installing an MSIX with the custom capability `Microsoft.appCategory.gamingHome`. [ashpynov/AnyFSE README](https://github.com/ashpynov/AnyFSE) Windows activates that package at sign-in, and the Xbox app launches Game Pass games, so whatever Xbox mode runs as its shell keeps the activation machinery from §3 alive. It drops the desktop and taskbar, not activation. [inference]
- **Not published.** Whether that shell is explorer.exe in a reduced mode or something else. If #98's route boots into Playnite through Xbox mode (an AnyFSE-style home app), Game Pass launches should just work there. **[check under #98: T4]**

## 6. Options when explorer is held back

| Option | Game Pass launches work? | Cost on the stopwatch | Cost elsewhere | Status |
| --- | --- | --- | --- | --- |
| **A. Start explorer on a Game Pass launch** (on demand) | Yes, once explorer is up as the shell | None | The first Game Pass launch of a session waits for explorer to become the shell (time unmeasured). The taskbar and desktop appear during that launch. Startup apps may fire then, unless #104 has the tool run them. | Mechanism is sound [inference]. Wait time and side effects: **T2, T3** |
| A′. Leave it to Playnite's own `explorer.exe shell:AppsFolder\…` | Maybe: it probably becomes the shell, but may drop the argument or hit the app-manager race | None | Same as A, with no control over the wait or the taskbar | **T2** |
| **B. Launch them differently** (direct `ActivateApplication`, protocol, Xbox app, exe) | No on Pro: every activation route needs the shell side, and a direct exe loses app-launch | n/a | n/a | Exception: under Xbox mode (§5), if #98 picks it |
| **C. Run explorer every boot, after Start-up's first frame** | Yes, once it's up | None, if it starts after the first frame and doesn't disturb the Boot video | Explorer's RAM and CPU all session (out of scope for this map). It needs #99's black screen and Playnite staying in front. | Prior art does this (WindowsHandheldMod: 10 s after Playnite) |

**Recommendation.** Combine **C with A's guard**:

- Start explorer as the shell late, for example when Start-up's Boot video ends, so it never lands on the stopwatch.
- On a Game Pass launch, make sure explorer is up as the shell, and wait until activation works before Playnite's launch goes ahead. Two places can do this:
  - the Bundle's `OnGameStarting` handler for games from the Xbox plugin (`7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287`), which runs before the plugin's `Play`;
  - the tool itself.

  The guard covers a Game Pass game launched before explorer is up. On its own, the guard is option A, which works if the user wants explorer fully on demand. The Run-keys and Startup-folder question for a late explorer is #104's.

What "explorer is ready" means is still open: a `Shell_TrayWnd` window, `GetShellWindow()` returning a window, or an `ActivateApplication` that stops returning `0x80270257`. T3 measures it.

## 7. Tests to run on the TV

These need a keyboard or a remote session, because Task Manager and a console aren't usable with the pad. Ending explorer.exe and restarting it from Task Manager (File → Run new task → `explorer`) doesn't change the shell setting.

- **T1: Activation with no shell.** End `explorer.exe`, then from a console run `start "" "shell:AppsFolder\ROBLOXCorporation.RobloxGDK_55nm5eh3cm0pr!Game"` (or call `ActivateApplication` from a few lines of PowerShell or C#). Expected: no game. Note any error (`0x80270257`, "Class not registered", or nothing), and check `Microsoft-Windows-TWinUI/Operational` for events 1621 and 5961. This confirms §3 for a GDK game on 26300.
- **T2: Playnite's own launch with no shell.** End `explorer.exe`, then start Roblox from Playnite. Does the explorer.exe that Playnite starts become the shell (taskbar appears)? Does the game then start, and after how long? Does anything flash over Playnite?
- **T3: Readiness time.** With no shell, start `explorer.exe` and time three things: until `Shell_TrayWnd` exists, until an activation (T1's command) succeeds, and until the game's splash. This sizes the wait in option A or the guard.
- **T4: Xbox mode (with #98).** In Xbox mode, is `explorer.exe` in Task Manager → Details, and with which command line? Does a Game Pass game launch from the Xbox app?

## Sources

- Playnite: [`XboxLibrary/XboxGameController.cs`](https://github.com/JosefNemec/PlayniteExtensions/blob/master/source/Libraries/XboxLibrary/XboxGameController.cs), [`Common/Programs2.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Common/Programs2.cs), [`Common/ProcessStarter.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Common/ProcessStarter.cs), [`GamesEditor.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/GamesEditor.cs), [`Plugins/ExtensionFactory.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Plugins/ExtensionFactory.cs), issues [#4016](https://github.com/JosefNemec/Playnite/issues/4016) and [#4248](https://github.com/JosefNemec/Playnite/issues/4248)
- Microsoft:
  - [IApplicationActivationManager](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-iapplicationactivationmanager)
  - [ActivateApplication](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateapplication)
  - [PC Bootstrapper](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/overviews/gr-pc-bootstrapper)
  - [MicrosoftGame.config overview](https://learn.microsoft.com/gaming/gdk/docs/features/common/game-config/microsoftgameconfig-overview)
  - [Launching on PC](https://learn.microsoft.com/gaming/gdk/docs/tools/tools-pc/launching-on-pc)
  - [XUser cross-platform](https://learn.microsoft.com/gaming/gdk/docs/features/common/user/xuser-crossplatform)
  - [XUserAddAsync](https://learn.microsoft.com/gaming/gdk/docs/reference/system/xuser/functions/xuseraddasync)
  - [Game launchers and handhelds](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers)
  - [Shell Launcher overview](https://learn.microsoft.com/windows/configuration/shell-launcher/)
  - [How packaged desktop apps run](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)
  - [winapp CLI debugging](https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/debugging)
  - [Xbox Wire: Xbox mode on Windows 11 PCs](https://news.xbox.com/en-us/2026/04/30/xbox-mode-pc-windows-11/)
- Reports: [SO 14850591 (mirror)](https://windows-hexerror.linestarve.com/q/so14850591-run-metro-app-from-custom-shell), [LaunchBox forum 48259](https://forums.launchbox-app.com/topic/48259-bigbox-and-windows-store-w10-games-issue/), [MS Q&A 679864](https://learn.microsoft.com/en-us/answers/questions/679864/start-uwp(universal-windows-platform)-application), [MS Q&A 5576492](https://learn.microsoft.com/en-us/answers/questions/5576492/how-can-we-enable-full-windows-desktop-access-from), [ReactOS CORE-19887](https://jira.reactos.org/browse/CORE-19887)
- Prior art: [PlayniteShell](https://github.com/Blueforcer/PlayniteShell), [WindowsHandheldMod](https://github.com/CaffeinatedDragonWare/WindowsHandheldMod), [AnyFSE](https://github.com/ashpynov/AnyFSE)
- This PC (read-only):
  - `%APPDATA%\Playnite\playnite.log` and `extensions.log`
  - `Playnite.dll` 10.63
  - `Get-AppxPackage` and the AppxManifests
  - `Microsoft-Windows-TWinUI/Operational`
  - `HKCR\CLSID\{45BA127D-…}`
  - `Get-Service`
  - Windows' message text for `0x80270254`–`0x8027025B`
