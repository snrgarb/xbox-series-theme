# Can Xbox mode boot this PC into Playnite and leave the Guide button to Playnite?

Research for [#98](https://github.com/snrgarb/xbox-series-theme/issues/98), part of the map [#96](https://github.com/snrgarb/xbox-series-theme/issues/96). It feeds the route decision in [#103](https://github.com/snrgarb/xbox-series-theme/issues/103).

Sources were read on 21 October 2026. This PC's registry, packages and feature state were read without changing anything; nothing was turned on. Settings › Gaming › Xbox mode was not opened, because it would have shown on the TV; its values were read from the registry the page writes. Each claim is tagged with its source; **[inference]** marks reasoning from those sources that nobody has tested yet.

## Answer

**Not viable on this PC under the map's constraints.** Two separate facts each rule it out:

1. **A desktop can't pick a home app or start Xbox mode at sign-in.** Microsoft gives *Choose home app* and *Enter Xbox mode on startup* to gaming handhelds only. Other PCs get *Enable Xbox mode*, which you enter by hand from Game Bar, Task View, the Xbox app or Win+F11. [MS Support KB5070297] Community tools unlock the handheld edition on desktops only by setting `DeviceForm` to 46 ("Gaming handheld"). XFSET also turns on three feature IDs with ViVe. [XFSET source; AnyFSE source] Both are forbidden by the map.
2. **In Xbox mode the Guide button belongs to Windows.** *"When FSE is active, Windows has exclusive access to the **Guide** button on controllers."* A short press opens Game Bar. Apps get a Guide press only when the player presses **View+Menu**, or presses View in the long-press Task Switcher. [MS Learn: Game launchers and handhelds] So Playnite's `OnControllerButtonStateChanged` would no longer see a plain Guide press, and the pad's Guide button would stop opening the Bundle's Guide.

**On the boot savings.** By Microsoft's design, a home app other than the Xbox app gets the same Xbox mode start: OEMs can set "third party launchers" as the home app. [MS Learn: GamingHomeApp] Xbox mode holds back the startup apps you choose (*Start when exiting to desktop*). Microsoft never says it holds back explorer: it says the *taskbar* is suppressed. The evidence says explorer still starts during an Xbox mode boot (see [section 5](#5-would-playnite-as-the-home-app-get-the-boot-savings)). So Xbox mode wouldn't give the map's "explorer held back until the desktop" even if it were allowed.

**This PC is already outside the map's constraints.** On 29 September 2026, the *Xbox FullScreen Experience Tool* (XFSET) 0.9.4 was run here. It set `DeviceForm` = 46 and turned on feature IDs 52580392, 50902630 and 59765208 at User priority. It also installed two boot-triggered SYSTEM tasks. OmniConsole 4.6.2.0 was installed too. All of this is still in place (see [section 0](#0-this-pc-today-read-only)). The map forbids each piece, and the boot tasks run inside the 36 s baseline.

## 0. This PC today (read-only)

| What | Value | How read |
|---|---|---|
| Build | 26300.9457, 26H2, Pro | `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` |
| Device form | **46 (Gaming handheld)** in `HKLM\...\CurrentVersion\OEM\DeviceForm`; `RtlGetDeviceFamilyInfoEnum` also returns form 46, family 3 (Desktop) | registry; ntdll call |
| Feature IDs 52580392, 50902630, 59765208 | **Enabled at priority 8 (User)**, in both the Boot and Runtime stores | `RtlQueryFeatureConfiguration` (query only) |
| Xbox mode | Supported, not active | `IsGamingFullScreenExperienceSupported` / `IsGamingFullScreenExperienceActive` |
| Home app and startup | `GamingHomeApp` = "" (None), `StartupToGamingHome` = 0, `ShowOnDesktopSwitcher` = 1 | `HKCU\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration` |
| Earlier Xbox mode use | `SystemDialogResults\EnterGamingPostureConfirmation_NoReboot` = 1, `ExitGamingPostureConfirmation` = 1 | same key |
| Game Bar on Guide | `UseNexusForGameBarEnabled` = 0, `GamepadNexusChordEnabled` = 0 | `HKCU\Software\Microsoft\GameBar` |
| Developer Mode | On (`AllowDevelopmentWithoutDevLicense` = 1) | `HKLM\...\AppModelUnlock` |
| XFSET 0.9.4 | Installed 29 Sep 2026 (MSI `{FBD2321F-…}`); `DeviceForm.bak` = `DELETE_ON_RESTORE`, so there was no `DeviceForm` before | Uninstall key; `C:\Program Files\8bit2qubit\Xbox FullScreen Experience Tool\` |
| XFSET boot tasks | `XFSET-SetPanelDimensions` and `XFSET-SimulateTouchService`: `schtasks /query` says *Access is denied* for both, but *cannot find* for a made-up name, so both exist | `schtasks` |
| Home-app packages | `Microsoft.GamingApp` 2610.1001.16.0 (Store) and OmniConsole 4.6.2.0 (Developer-signed, `CN=8bit2qubit`), plus its second package `4fa8e044-…`; the `CN=8bit2qubit` certificate is in `LocalMachine\TrustedPeople` | `Get-AppxPackage`, manifests, cert store |

XFSET's own log shows what it did here. The build was 26200.9457 at the time:

```
[2026-09-29 17:00:53] Registry key successfully set to 46 (0x2E).
[2026-09-29 17:00:53] Enabling Feature IDs: 52580392, 50902630, 59765208...
[2026-09-29 17:00:53] Detected Native Build, applying native support configuration. (Screen Size: 0.00", Override Status: False)
[2026-09-29 17:00:53] Non-handheld environment detected: Creating Registry maintenance task...
[2026-09-29 17:00:53] Scheduled task 'XFSET-SetPanelDimensions' has been created.
```

An earlier entry that day: `Scheduled task 'XFSET-SimulateTouchService' has been created.`

In XFSET's source, both tasks have a `BootTrigger`, run as SYSTEM with `HighestAvailable`, and are created by [`TaskSchedulerManager.cs`](https://github.com/8bit2qubit/XboxFullScreenExperienceTool/blob/main/XboxFullScreenExperienceTool/Helpers/TaskSchedulerManager.cs):

- **`XFSET-SetPanelDimensions`** runs `PhysPanelCS.exe reg`, which writes `DeviceForm` = 0x2E again at every boot (`PhysPanelCPP.cpp`: *"Set OEM DeviceForm registry key to 0x2e only. Requires SYSTEM privileges."*).
- **`XFSET-SimulateTouchService`** runs `touchservice`, which spreads touch injection into every user session.

**This PC's Settings page therefore shows the handheld edition today**, with *Choose home app* and *Enter Xbox mode on startup* **[inference from the device form; the page wasn't opened]**. That is not what a stock desktop sees.

## 1. Who gets "Choose home app" and "Enter Xbox mode on startup"

**Microsoft splits the setup by device type.** Microsoft's support article *Windows Gaming: Full screen experience* (KB5070297, first published 20 Oct 2025) sets out two cases. [MS Support KB5070297](https://support.microsoft.com/en-us/topic/windows-gaming-full-screen-experience-67fb8d12-5467-4a95-8adf-0a10789576ab)

- **On gaming handhelds:** *"Under **Choose home app**, select a supported gaming home app installed on your device"*, then optionally *"Turn on **Enter Xbox mode on startup** to start Xbox mode automatically after you sign in."* It adds: *"On gaming handhelds, Xbox mode starts automatically at sign-in when Enter Xbox mode on startup is turned on."*
- **On other PC form factors:** *"Turn on **Enable Xbox mode**."* and *"Entry points will be available in **Game Bar** and the **Xbox app**."* There is also an optional *Show Xbox mode in Task View*. The page says nothing about a home-app choice or starting at sign-in for these PCs.

**The general PC rollout doesn't change this.** Xbox Wire's rollout post (30 Apr 2026) brings Xbox mode to *"Windows 11 PCs, including laptops, desktops, and tablets"*. For how to use it, it links to that same KB5070297 article, and it says nothing about home apps or starting on boot. [Xbox Wire, 30 Apr 2026](https://news.xbox.com/en-us/2026/04/30/xbox-mode-pc-windows-11/); [Windows Experience Blog, 30 Apr 2026](https://blogs.windows.com/gaming/2026/04/30/windows-11-pc-gamers-xbox-mode-rolls-out-and-rog-xbox-ally-updates/)

The Insider notes for 26300.8155 only rename the feature to Xbox mode and add a first-run experience. [Windows Insider Blog, 3 Apr 2026](https://blogs.windows.com/windows-insider/2026/04/03/announcing-windows-11-insider-preview-build-26300-8155-dev-channel/) The PC preview for Insiders (26220.7271) let users *"turn off the experience entirely"* under Gaming › Full screen experience. It named no home-app or startup setting for PCs. [Windows Insider Blog, 21 Nov 2025](https://blogs.windows.com/windows-insider/2025/11/21/full-screen-experience-expands-to-more-windows-11-pc-form-factors-for-windows-insiders/)

**What "gaming handheld" means.** It is device form 46: *"A portable gaming device, battery powered with the main input method being gamepad."* [MS Learn: Microsoft-Windows-Deployment › DeviceForm](https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-deployment-deviceform) The GDK detects it as `DEVICEFAMILYDEVICEFORM_GAMING_HANDHELD 0x0000002E` through `RtlGetDeviceFamilyInfoEnum`. [MS Learn: Handheld guidance](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-guidance) *"The Original Equipment Manufacturer (OEM) adds this flag if they want the device to be identified as a Windows gaming handheld."* [MS Learn: Handheld performance](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-performance)

**How the community tools unlock it.** Both do it by faking the device form:

- **XFSET** calls Microsoft's PC rollout the *"Limited PC edition"*, with no *Choose home app* and no auto-launch on startup. The tool's purpose is *"to enable the **Full Handheld** edition on any PC"*. [XFSET README](https://github.com/8bit2qubit/XboxFullScreenExperienceTool) In code, enabling sets `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\OEM\DeviceForm` = 0x2E. It also turns on feature IDs `{52580392, 50902630}` on legacy builds, or `{52580392, 50902630, 59765208}` on native builds for non-handhelds, through ViVe at User priority. [XFSET `MainForm.cs`](https://github.com/8bit2qubit/XboxFullScreenExperienceTool/blob/main/XboxFullScreenExperienceTool/MainForm.cs)
- **AnyFSE** only sets `DeviceForm` = 46 (`EnableGamingHandheld`). Its installer tells the user: *"Custom Home app selection is disabled. AnyFSE requires the gaming handheld device mode to be enabled."* [AnyFSE `GamingExperience.cpp`, `localization/en_US.json`](https://github.com/ashpynov/AnyFSE) Its README: *"AnyFSE is not implement enabling FSE mode support in windows. It is require either supported Handheld device … or enabling this mode on other devices using 'Enabler' tool"*.
- **OmniConsole** *"requires … the **Full Handheld edition** of XBOX Mode (FSE)"*, and sends desktops to XFSET first. [OmniConsole README](https://github.com/8bit2qubit/OmniConsole)

**What XFSET's "native" builds are.** "Native" (26300.7674+ among others) only means XFSET no longer has to override the screen size: *"Native builds … no longer require any screen dimensions override on Desktop PCs and Laptops."* [XFSET `Docs/screen-dimensions-override.md`] On those builds XFSET still sets `DeviceForm` = 46, turns on all three feature IDs, and, on a non-handheld, installs the boot task that re-sets `DeviceForm` (`IsNativeSupportBuild`, `GetRequiredFeatureIds`, and the log above). **Native doesn't mean stock Windows does it.** It means the fake no longer needs a panel-size override.

## 2. The unattend settings, and setting them after install

**Unattend.** `Microsoft-Windows-Gaming-Configuration` has `StartupToGamingHome` (boolean: *"Determine whether the gaming full screen experience launches on startup"*) and `GamingHomeApp` (an AUMID). `GamingHomeApp` *"allows OEMs to declare third party launchers or default to first party applications like the Game Pass client when booting into the full screen experience."* [MS Learn: StartupToGamingHome](https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-gaming-configuration-startuptogaminghome), [GamingHomeApp](https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-gaming-configuration-gaminghomeapp)

Both settings are valid only in the **offlineServicing, specialize and oobeSystem** passes, which run while an image is serviced or while Windows Setup runs. So they're no way for a user to change an installed PC. Applying them would mean reimaging or a sysprep round trip.

**At runtime.** Windows keeps the same two names per user in `HKCU\Software\Microsoft\Windows\CurrentVersion\GamingConfiguration`, which this PC has. AnyFSE reads and writes them there (`GamingHomeAppRegKey`, `StartupToGamingHomeRegValue` in [`Constants.hpp`](https://github.com/ashpynov/AnyFSE/blob/main/src/App/Constants.hpp)).

- **Writable without admin.** They're per-user values. [registry location]
- **Undocumented.** Microsoft documents only the unattend form and the Settings page.
- **Not known to work on a stock desktop.** No source says Windows honours `StartupToGamingHome` = 1 when the device form isn't 46. XFSET's author says the limited PC edition doesn't support auto-launch. This can't be tried here without changing settings, and this PC's device form is faked anyway. **[untested]**

Even if those values did work, the Guide-button blocker in section 3 would remain.

## 3. The Guide button in Xbox mode

This is the deciding fact, and it holds whatever the device form:

> When FSE is active, Windows has exclusive access to the **Guide** button on controllers. When a player short-presses the **Guide** button in the Full Screen Experience, it brings up Xbox Game Bar. … To ensure compatibility with existing experiences that use the **Guide** button, a player can press **View+Menu** to send a **Guide** button to the focused application. The controller driver sends both **View**, **Menu**, and **Guide** button presses as part of the input payload. A long press of the **Guide** button opens an optimized **Task Switcher** where players can press **View** on the selected app to send a **Guide** button press.
>
> [MS Learn: Game launchers and handhelds › Game overlays and guide button](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers)

The support article agrees: the Xbox button opens Game Bar, a long press opens Task View, and Game Bar's **Home** button returns to the home app. [MS Support KB5070297]

**What it means for the Bundle.** Playnite reads the pads through SDL, which polls `XInputGetStateEx` and tests the Guide bit `0x0400`. Playnite forwards that to plugins through `OnControllerButtonStateChanged`. [this repo: guide-button-capture.md](https://github.com/snrgarb/xbox-series-theme/blob/research/guide-button-capture/docs/research/guide-button-capture.md) That research had already flagged Xbox mode as the case where detection breaks. While Xbox mode is active:

- **A plain Guide press** opens Game Bar. Playnite never sees it, so the Bundle's Guide doesn't open.
- **View+Menu** reaches Playnite as View, Menu *and* Guide together. The Bundle would see a Guide press, but only through a chord, not the Guide button.

**`UseNexusForGameBarEnabled` = 0 doesn't help, as far as anyone documents.** On the desktop that setting stops Guide opening Game Bar. No source says Xbox mode honours it, and Microsoft's wording ("exclusive access") has no exception. **[untested; turning Xbox mode on to check it is outside this ticket]**

**Leaving Xbox mode right after boot doesn't rescue the route.** A tool could boot into Xbox mode for the boot path, then call `SetGamingFullScreenExperience(FALSE)` (as AnyFSE's `ExitFSEMode` does) to get the Guide button back. Leaving Xbox mode brings the desktop up, though, which risks the desktop flash, and booting into Xbox mode at all still needs the handheld fake. **[inference]**

## 4. A home-app package of our own, under Developer Mode

This part would work under the map. It just doesn't matter, given sections 1 and 3.

**What makes a package a home app.** It needs a `windows.gamingApp` app extension and the custom capability `Microsoft.appCategory.gamingHome_8wekyb3d8bbwe`. Microsoft's own Xbox app on this PC declares exactly that: `<uap3:AppExtension Name="windows.gamingApp" Id="XboxPcApp" DisplayName="XBOX" …/>` and `<uap4:CustomCapability Name="Microsoft.appCategory.gamingHome_8wekyb3d8bbwe" />` (Microsoft.GamingApp 2610.1001.16.0 manifest). AnyFSE's [`AppxManifest.xml`](https://github.com/ashpynov/AnyFSE/blob/main/AppxManifest.xml) and OmniConsole's [`Package.appxmanifest`](https://github.com/8bit2qubit/OmniConsole/blob/main/OmniConsole/Package.appxmanifest) declare the same, with `runFullTrust`.

**How Developer Mode lets our own package claim it.** Both projects ship a `CustomCapability.SCCD` with `<AuthorizedEntities AllowAny="true"/>` and `<Catalog>FFFF</Catalog>`. Microsoft documents `FFFF` as the development catalog that works *"on a desktop PC in Developer Mode"*. [MS Learn: HSA steps for app developers](https://learn.microsoft.com/windows-hardware/drivers/devapps/hardware-support-app--hsa--steps-for-app-developers) Microsoft also says: *"If you only want to run your application locally in developer mode, then you don't need the custom capability [approval]."* [MS Learn: App capability declarations › Custom capabilities](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations#custom-capabilities)

**Installing it.** OmniConsole's installer turns on Developer Mode and puts its certificate in `LocalMachine\TrustedPeople`, which is a one-time admin step, not a boot-time one. [OmniConsole README] A loose-file layout registered with `Add-AppxPackage -Register` is the other development route. [MS Learn: Loose file registration](https://learn.microsoft.com/windows/uwp/debug-test-perf/loose-file-registration)

**Playnite needs a launcher in front of it.** Playnite isn't packaged, so the home app would be a small packaged launcher that starts `Playnite.FullscreenApp.exe`, the way AnyFSE does. AnyFSE has Playnite-specific handling for Desktop↔Fullscreen restarts and `--hidesplashscreen`. [AnyFSE README; issues #91, #12]

## 5. Would Playnite as the home app get the boot savings?

**Startup apps: yes, by Microsoft's design.**

- **The suppression belongs to Xbox mode, not to the Xbox app.** When OEMs configure the default full-screen app, *"Many standard Windows experiences are suppressed, such as the taskbar, some startup apps, and other services."* The default app *"automatically launches when the device is powered on from a shutdown state."* [MS Learn: Game launchers and handhelds](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers) `GamingHomeApp` is meant for third-party launchers. [MS Learn: GamingHomeApp]
- **Startup apps are chosen per app**, in Settings › Apps › Startup: *Start at log in* (runs in Xbox mode), *Start when exiting to desktop*, or *Off*. Also *"When this setting is on, Windows limits background processes to improve performance while gaming."* On handhelds with start-on-sign-in, Windows offers a restart to get the optimisations after you've used the desktop. [MS Support KB5070297] This is an OS feature that doesn't depend on the home app. This PC has the `GamingConfiguration\Startup\Run` and `\StartupFolder` keys for it, both empty.
- **One user report contradicts this for a third-party home app.** In February 2026 a user reported that with AnyFSE as the home app, Xbox mode never booted in "optimized mode": *"the desktop and full pc is running at the same time as fse at startup."* The author's only answer was that a new version was *"totally redesigned"*. [AnyFSE #80](https://github.com/ashpynov/AnyFSE/issues/80)

**Explorer: not shown to be held back.**

- **Microsoft only promises to hide the taskbar.** It never says explorer.exe is held back.
- **Xbox mode uses explorer's own UI.** It relies on Task View (long-press Guide, Win+Tab), which is shell UI. **[inference: Task View is part of the explorer shell]**
- **AnyFSE's author saw explorer start during an Xbox mode boot.** He found the home app *"may be executed earlier even explorer starts, but even Fullscreen API required that it presents"*. He fixed it by waiting for the taskbar or Xbox mode before continuing. [AnyFSE #9](https://github.com/ashpynov/AnyFSE/issues/9) His code still listens for `TaskbarCreated` (`MainWindow.cpp`).
- **So explorer starts later, behind the home app, with its taskbar hidden.** It doesn't stay off until the desktop. **[inference from community evidence]** Microsoft publishes no boot times for Xbox mode.

## What this changes elsewhere on the map

- **#103 (route):** The Xbox mode route fails twice: on the Guide button, whatever the device form, and on the handheld-only home app and start-on-sign-in. That leaves the per-user shell.
- **#102 (POST and kernel to sign-in) and the baseline:** The 36 s baseline was measured with XFSET's two SYSTEM boot tasks in place. `XFSET-SimulateTouchService` injects touch into every session, and `XFSET-SetPanelDimensions` re-writes `DeviceForm` each boot. Both are "admin changes at boot", which the map forbids.
  - **What uninstalling XFSET does**, per its README: it *"reverts all system changes made when enabling Xbox Mode and resets the Feature IDs to their default Microsoft-managed state"*. So it should remove the tasks, the flags and `DeviceForm`. **[not run]**
  - **Order matters.** OmniConsole's README warns to set the home app to Xbox or None *before* uninstalling it, or Task View can break. The home app is already None here.
  - **Leftovers to clean up:** the two OmniConsole packages and the `CN=8bit2qubit` certificate in `LocalMachine\TrustedPeople`.
  - **Re-measure after the clean-up.** The baseline may move once XFSET is gone.
- **#99 (per-user shell):** Until XFSET is removed, Windows sees this desktop as a gaming handheld (form 46). The shell work should be measured on a stock device form. Xbox mode also uses explorer's Task View, so it gives no evidence that explorer can be skipped.
- **#104 (startup apps):** Windows' own *Start when exiting to desktop* only takes effect in Xbox mode, so our own tool has to hold back and order the startup apps itself.
- **#100 (Game Pass):** Nothing new. Xbox mode keeps explorer, so it shows nothing about launching Game Pass without it.
- **Optional:** if Xbox mode should be off entirely, the Games policy `DisableGamingFullScreenExperience` = 1 does it, on Pro. That's a one-time admin policy, not a boot change. [MS Learn: Policy CSP – Games](https://learn.microsoft.com/windows/client-management/mdm/policy-csp-games#disablegamingfullscreenexperience)

## Not verified

- **Start-on-sign-in on a stock desktop:** whether `HKCU\…\GamingConfiguration\StartupToGamingHome` = 1, with an AUMID in `GamingHomeApp`, starts Xbox mode at sign-in when the device form isn't 46. Untested, and officially handheld-only.
- **The Game Bar setting in Xbox mode:** whether `UseNexusForGameBarEnabled` = 0 changes Guide handling there. Microsoft's wording says it can't.
- **Explorer's start time:** when explorer.exe starts in an Xbox mode boot compared with a desktop boot. Measuring it needs Xbox mode turned on, which is outside this ticket.
- **The stock Settings page:** what this PC shows on stock Windows after XFSET is removed. That depends on the controlled rollout and the market (XFSET logged region AU).

## Sources

- **Microsoft:**
  - [Windows Gaming: Full screen experience (KB5070297)](https://support.microsoft.com/en-us/topic/windows-gaming-full-screen-experience-67fb8d12-5467-4a95-8adf-0a10789576ab), also at [the servicing URL](https://support.microsoft.com/en-US/servicing/os/windows/docs/2025/10/windows-gaming-full-screen-experience)
  - [GDK: Game launchers and handhelds](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers), [Handheld guidance](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-guidance) and [Handheld performance](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-performance)
  - Unattend: [Microsoft-Windows-Gaming-Configuration](https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-gaming-configuration), [StartupToGamingHome](https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-gaming-configuration-startuptogaminghome), [GamingHomeApp](https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-gaming-configuration-gaminghomeapp) and [DeviceForm](https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/microsoft-windows-deployment-deviceform)
  - Packaging: [App capability declarations](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations#custom-capabilities), [HSA: steps for app developers](https://learn.microsoft.com/windows-hardware/drivers/devapps/hardware-support-app--hsa--steps-for-app-developers) and [Loose file registration](https://learn.microsoft.com/windows/uwp/debug-test-perf/loose-file-registration)
  - [Policy CSP – Games](https://learn.microsoft.com/windows/client-management/mdm/policy-csp-games#disablegamingfullscreenexperience)
  - Announcements: [Xbox Wire, 30 Apr 2026](https://news.xbox.com/en-us/2026/04/30/xbox-mode-pc-windows-11/), [Windows Experience Blog, 30 Apr 2026](https://blogs.windows.com/gaming/2026/04/30/windows-11-pc-gamers-xbox-mode-rolls-out-and-rog-xbox-ally-updates/), [Windows Insider Blog, 21 Nov 2025](https://blogs.windows.com/windows-insider/2025/11/21/full-screen-experience-expands-to-more-windows-11-pc-form-factors-for-windows-insiders/) and [Windows Insider Blog, 3 Apr 2026 (26300.8155)](https://blogs.windows.com/windows-insider/2026/04/03/announcing-windows-11-insider-preview-build-26300-8155-dev-channel/)
- **Community source, read at these commits:**
  - [8bit2qubit/XboxFullScreenExperienceTool](https://github.com/8bit2qubit/XboxFullScreenExperienceTool) @ `1170d77` (v0.9.4), with its release notes for v0.9.0–v0.9.4
  - [ashpynov/AnyFSE](https://github.com/ashpynov/AnyFSE) @ `6de7302`, with issues [#9](https://github.com/ashpynov/AnyFSE/issues/9) and [#80](https://github.com/ashpynov/AnyFSE/issues/80)
  - [8bit2qubit/OmniConsole](https://github.com/8bit2qubit/OmniConsole) @ `6dcf97a` (v4.6.2.0)
- **This repo:** [Guide button capture research](https://github.com/snrgarb/xbox-series-theme/blob/research/guide-button-capture/docs/research/guide-button-capture.md) (#2)
- **This PC, read-only, 21 Oct 2026:**
  - the registry (`OEM`, `GamingConfiguration`, `GameBar`, `AppModelUnlock`, Uninstall)
  - `RtlQueryFeatureConfiguration`, `RtlGetDeviceFamilyInfoEnum`, `IsGamingFullScreenExperienceSupported` and `IsGamingFullScreenExperienceActive`
  - `Get-AppxPackage` and the package manifests, the `LocalMachine\TrustedPeople` store, and `schtasks /query`
  - XFSET's install folder and its log
