# Research: what Windows and app updates do to the Boot shell's setup

Ticket: [What do Windows and app updates do to the Boot shell's setup?](https://github.com/snrgarb/xbox-series-theme/issues/111), part of the map [Boot this TV PC straight into Playnite at Xbox mode speed](https://github.com/snrgarb/xbox-series-theme/issues/96). It builds on [How does a per-user shell start Playnite first and explorer later, without admin?](https://github.com/snrgarb/xbox-series-theme/issues/99) ([findings](https://github.com/snrgarb/xbox-series-theme/blob/research/per-user-shell/docs/research/per-user-shell.md)) and feeds [How does the tool keep Playnite up, and when does it fall back to the desktop?](https://github.com/snrgarb/xbox-series-theme/issues/105), [When does explorer start, and what starts the startup apps?](https://github.com/snrgarb/xbox-series-theme/issues/104) and the map's "How the Boot shell is set up and removed".

**Question.** Does a Windows update (cumulative, feature or Insider flight) or an app's own update undo the Boot shell's setup on this PC? If so, what could the Boot shell check at sign-in to notice? The setup, per [Which route boots this PC into Playnite: Xbox mode or our own shell?](https://github.com/snrgarb/xbox-series-theme/issues/103):

- the HKCU Winlogon `Shell` value naming the Boot shell, with HKLM left as `explorer.exe`;
- startup apps switched off in Settings > Apps > Startup (HKCU `StartupApproved`);
- `Playnite.lnk` taken out of the Startup folder.

**Researched** on 2026-10-21. It is read-only: nothing on this PC was changed, installed, restarted or closed. The sources are:

- **Microsoft Learn**: the Policy CSPs, the CloudDesktop CSP, StartupTask, the 26H2 release notes and the upgrade docs.
- **Windows' own component manifests** on this PC. They were decompressed read-only with `msdelta`, using the servicing stack's dictionary.
- **Strings** in this build's `explorer.exe`, `winlogon.exe`, `userinit.exe` and `ShellAppRuntime.exe`, and in the Settings and setup DLLs.
- **Playnite's source**: `master` at [`3e8abb2`](https://github.com/JosefNemec/Playnite/tree/3e8abb241873e326e27c75b59dd0d2ea367698e3), and its last public installer script ([`e916d38`](https://github.com/JosefNemec/Playnite/blob/e916d3858319043ac9fe8ca484d1da58a2a565fe/build/InnoSetup.iss)).
- **This PC's Playnite log**, which recorded the 10.62 → 10.63 update on 2026-10-20.
- **Discord's own JavaScript** (`app.asar`, version 1.0.9264, on this PC).
- **Strings in Steam's and Spotify's binaries** on this PC.
- **Cairo Shell**, which sets itself as the shell through the same HKCU value.

**How sure each claim is.**
- **Confirmed**: from Microsoft docs or source code, or read on this PC.
- **Strong**: from binary strings, a single report, or one app version's code.
- **Inference**: reasoned, not seen.
- **Unknown**: needs a real update or sign-in. These are written up as tests in §8.

## Answer in short

| Item | Does it undo the setup? | How sure | What the Boot shell checks |
|---|---|---|---|
| **HKCU `Shell`**, monthly and preview cumulative updates, the 26H2 enablement package, and flights on the same servicing branch | No. These go through Windows' servicing stack, which writes machine-wide (HKLM) settings and files, not user hives. | Strong | Nothing extra. |
| **HKCU `Shell`**, a full Setup upgrade (a new build family, or Settings > Recovery > *Reinstall now*) | **Maybe.** No migration rule names the HKCU value, though one names the HKLM one. One Cairo Shell user's per-user shell stopped starting after the 2020 upgrade to Windows 10 2004. | Inference, one field report | The Boot shell can't notice that it never ran. A **sentinel** in HKCU Run, run by explorer, notices that the Boot shell is missing, puts the value back and starts Playnite (§6). |
| **HKLM `Shell`**, any update | No. The Winlogon component carries it across upgrades with the old value winning. | Confirmed (manifest) | Check that it still names `explorer.exe`. |
| **Steam, Spotify** updates | No. They rewrite their own Run value but never touch the switch. The switch is stored by value name and outlives the value. | Strong (binary strings), confirmed (switch survives) | The switch for each name is still off. |
| **Discord** updates | **No** on update: its update code re-adds autostart only while the switch is on. **Yes** if "Open Discord" is turned on in Discord, or Discord is reinstalled: both delete the switch. | Confirmed (Discord 1.0.9264 code) | As above, plus no Startup-folder shortcut to Discord except one the setup knows. |
| **Playnite** | **Yes, on every start, not just on updates.** "Start Playnite on computer startup" is on here, and Playnite re-creates `Playnite.lnk` each time it starts. Its updater doesn't touch the shortcut. | Confirmed (source, this PC) | Playnite's `StartOnBoot` is off and `Playnite.lnk` is gone (or switched off) before explorer starts. |
| **Windows**, for startup entries | No. Upgrades carry the switches over, and a packaged app can't re-enable a startup task the user turned off. | Confirmed (manifest, docs) | New entries since setup are only logged. They trail Start-up anyway. |
| **`ShellAppRuntime.exe`** | No. It is Windows' "Lightweight shell", started only by the `WindowsLogon/OverrideShellProgram` device policy (Windows 365 Boot). That policy beats every `Shell` value, but only MDM or an admin sets it. It isn't a feature flag, and explorer never refers to it. | Confirmed (docs, manifest), strong (strings) | Nothing useful: if it were set, the Boot shell wouldn't run. |
| **"Finish setting up your device" (SCOOBE)** | Doesn't undo the setup, but it is **one of explorer's logon tasks**. So it appears when the Boot shell starts explorer, over Playnite, after Start-up. Each user can switch it off without admin. It isn't switched off here. | Strong (strings), confirmed (setting) | `ScoobeSystemSettingEnabled` is `0`. |
| **Playnite's own updater** | No. It runs only when someone picks *Update*. It closes Playnite, runs the installer and restarts **Fullscreen**. For ~1.5 s no Playnite runs, and the installer waits until Playnite's mutex is free. | Confirmed (source, this PC's log), strong (installer script) | Not a sign-in check, but a watchdog rule (§5). |

## This PC's update state (read-only)

- **Build.** Windows 11 Pro, `DisplayVersion` 26H2, build 26300.9457. `LCUVer` is `10.0.26100.9457` and `BuildLabEx` is `26100.1.amd64fre.ge_release`, so it sits on the 26100 servicing branch.
- **26H2 is an enablement package.** Microsoft: "Devices running eligible versions of Windows 11 can move to version 26H2 through a small enablement package rather than a full operating system upgrade… versions 26H2, 25H2, and 24H2 share a common servicing branch" ([What's new in 26H2](https://learn.microsoft.com/windows/whats-new/whats-new-windows-11-version-26h2)).
- **No Setup upgrade has run since the 2026-09-29 install.** Windows Update history shows a critical update (KB5128942), "Windows 11, version 26H2" on 2026-10-06 and a preview cumulative update. There is no `C:\Windows.old` and no `HKLM\SYSTEM\Setup\Source OS (Updated on …)` key, which Setup leaves after an upgrade. So everything so far came through the servicing stack. **Confirmed.**
- **Insider flights.** `WindowsSelfHost\FIDs` holds flight records, but no channel is readable without admin. A flight that stays on the 26100 branch arrives like a cumulative update. A flight to a new build family installs through Setup, like any feature upgrade. **Inference.**

## 1. The `Shell` value

### What touches it

- **Cumulative updates and enablement packages don't.** The servicing stack installs component manifests, and their registry entries are machine-wide. The Winlogon component (`Microsoft-Windows-Winlogon` 10.0.26100.9444) writes only under `HKLM\…\Winlogon`, `Services\EventLog` and `Control\Session Manager`, and has no HKCU entry. The userinit component writes only `HKLM\…\Winlogon\Userinit`. **Confirmed (manifests).** No component writes a user's `HKCU\…\Winlogon\Shell`. **Strong.** Per-user changes reach existing users only through first-sign-in tasks, and none found here touches `Shell`.
- **A full Setup upgrade might.** In an upgrade, each component's `<migration>` rules say which settings carry over. On this build:
  - **HKLM `Shell` carries over.** Winlogon's System rules include `HKLM\Software\Microsoft\Windows NT\CurrentVersion\Winlogon [Shell]` and `[USERINIT]` with `MigXmlHelper.SourcePriority()`, so the old value wins. **Confirmed (manifest).** HKLM stays `explorer.exe` unless someone changes it.
  - **HKCU `Shell` isn't named.** Winlogon's User rules list only `Winlogon [EnableGoodbye]`, `Winlogon [RestartApps]`, `RunOnce [Application Restart #*]` and `Explorer\SavedApps\*`. No other manifest read here names `Shell` (Shell Launcher, explorer, shell32, profile service, userinit). **Confirmed (manifests).**
  - **Whether unnamed per-user values survive is not documented.** Microsoft says only that an upgrade keeps "applications, files, and settings" ([upgrade and migration considerations](https://learn.microsoft.com/windows/deployment/upgrade/windows-upgrade-and-migration-considerations)). Third-party apps' HKCU settings survive upgrades, so most of the user hive must carry over. **Inference.**
  - **One field report.** Cairo Shell sets itself as the shell through exactly this value. Both its 2020 code ([Shell.cs L821–L829 at `091de84`](https://github.com/cairoshell/cairoshell/blob/091de84ae854801034c12a89292253b205c4a3bc/Cairo%20Desktop/CairoDesktop.Interop/Shell.cs#L821-L829)) and today's ([ManagedShell EnvironmentHelper.cs L222–L229](https://github.com/cairoshell/ManagedShell/blob/855222035e927910ca53fd3215caef056337a048/src/ManagedShell.Common/Helpers/EnvironmentHelper.cs#L222-L229)) write `HKCU\…\Winlogon\Shell`. In June 2020 a user reported that after the Windows 10 **2004** upgrade, Cairo "does not start… when logging in as previously set up", and that switching the shell back to Explorer and then to Cairo again fixed it ([cairoshell#392](https://github.com/cairoshell/cairoshell/issues/392)). The report doesn't say whether the value was gone or merely ignored. **Strong that a full upgrade can break a per-user shell. Cause unknown.**
- **When this PC gets a full upgrade:** only when it moves to a new build family, or when someone picks Settings > System > Recovery > *Reinstall now*. That option reinstalls the current version and keeps files, apps and settings ([recovery framework](https://learn.microsoft.com/windows/configuration/windows-device-recovery-framework#scenarios-and-solutions)), so it is an in-place upgrade too. Monthly updates and the yearly enablement packages don't qualify.

### A shell setting that beats every `Shell` value

The `WindowsLogon/OverrideShellProgram` device policy "has the highest precedence over other ways of configuring the shell program". Its one option, *Apply Lightweight Shell*, starts `ShellAppRuntime.exe` ([Policy CSP – WindowsLogon](https://learn.microsoft.com/windows/client-management/mdm/policy-csp-windowslogon#overrideshellprogram)). The policy applies to Pro, but only MDM or an admin sets it, usually through Windows 365 Boot's `BootToCloudPCEnhanced` ([CloudDesktop CSP](https://learn.microsoft.com/windows/client-management/mdm/clouddesktop-csp#boottocloudpcenhanced-technical-reference)). On this PC it holds its default of `0`, and there is no current policy value. Updates ship it at `0`. See §3.

### What the Boot shell can check

- **HKLM `Shell` still contains `explorer.exe`.** Otherwise a later plain `explorer.exe` won't become the shell, and Game Pass launches break ([per-user shell §1](https://github.com/snrgarb/xbox-series-theme/blob/research/per-user-shell/docs/research/per-user-shell.md#1-becoming-the-shell)).
- **HKCU `Shell` names the Boot shell's current path.** This catches a moved build, but not a lost value: if the value is gone, the Boot shell never starts.
- **A lost value needs a check outside the Boot shell (§6).** Without it, the PC would boot to a plain desktop:
  - explorer becomes the shell and runs the startup apps;
  - Playnite doesn't start, because `Playnite.lnk` and the startup switches are off.

## 2. Startup entries

### How a switch-off is stored, read on this PC

- **The format.** Settings > Apps > Startup writes one 12-byte value per entry under `HKCU\…\Explorer\StartupApproved\Run` (or `…\StartupFolder`): a 4-byte state, then the time it was switched (a FILETIME). Today's switch-offs are all `01 00 00 00` + a timestamp:
  - Spotify, 15:45:57;
  - Steam, 15:46:02;
  - DeckyLoader, 15:46:03;
  - Discord, 21:11:58;
  - Battle.net, Firefox and Edge on 2026-10-06.
- **`01` means off.** In the 21:15 boot that followed, none of those apps started. Discord started from `DiscordMini.lnk` (`--start-minimized`), not from its Run value (`--start-inactive`). An HKLM Run entry with no switch value (`RtkAudUService64`) did start, under explorer, at 21:15:31. **Confirmed (this PC).** Task Manager is widely reported to write `03` for off and `02` for on. All values seen here fit "odd first byte means off" (HKLM `SecurityHealth` is `05` and doesn't run). **Inference.** The check should compare against the exact bytes the setup wrote, not decode them.
- **The switch is stored by value name and outlives the value.** `DeckyLoader`'s switch-off is still in `StartupApproved\Run`, although Decky and its Run value were purged later the same day. **Confirmed (this PC).** So an app that deletes and re-writes its Run value under the **same name** stays switched off. A **new name** starts switched on. Firefox's and Edge's value names contain a hash, so a reinstall to another path would give them a new name. **Inference.**

### App by app

| App | What its update does | Basis |
|---|---|---|
| **Steam** (11.12.30.07) | Writes `HKCU\…\Run\Steam` (`"…\steam.exe" -silent`). `steam.exe`, `steamclient64.dll`, `steamclient.dll`, `steamui.dll` and `steamwebhelper.exe` all contain `Software\Microsoft\Windows\CurrentVersion\Run`, and none contains `StartupApproved` (ASCII or UTF-16). So it can't turn the switch back on. | Strong (strings) |
| **Spotify** (1.3.5.3058, the desktop build in `%AppData%\Spotify`) | Writes `HKCU\…\Run\Spotify` with `--autostart --minimized`. `Spotify.dll` and `SpotifyLauncher.exe` hold that string next to `Windows.ApplicationModel.StartupTask` (its Store build's mechanism). No `StartupApproved` string. | Strong (strings) |
| **Discord** (1.0.9264) | The autostart module in `app.asar` has `update()`, which runs on every host update (`updater.on("host-updated", …)`) and on `--squirrel-updated`. It calls `install()` only if `isInstalled()`. `isInstalled()` is true when the Run value exists **and** the `StartupApproved\Run\Discord` value is missing or its first byte reads `00`. Here it is `01`, so an update does nothing. `install()`, run on fresh install (`--squirrel-install`) or when "Open Discord" is turned on, **deletes** `StartupApproved\Run\Discord` (`uninstallStartupApproval`) and re-writes the Run value. Discord's shortcut updater touches only `Discord.lnk` on the Desktop and in the Start menu, and only if they exist. It never writes to the Startup folder. So `DiscordMini.lnk` (created 2026-10-21 21:12:43) isn't Discord's: no Discord update will restore or remove it. It points its working folder at `app-1.0.9264`, which the next Discord update deletes. | Confirmed (Discord's code, this version) |
| **Playnite** (10.63) | **Re-creates `Playnite.lnk` on every start while its own setting is on.** At each start Playnite calls `SystemIntegration.SetBootupStateRegistration(AppSettings.StartOnBoot, …)` ([PlayniteApplication.cs L1088–L1095](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/App/PlayniteApplication.cs#L1088-L1095)). With `StartOnBoot` on, that writes `Startup\Playnite.lnk` → `Playnite.DesktopApp.exe --hidesplashscreen` if it is missing or different. With it off, it **deletes** the shortcut ([SystemIntegration.cs L45–L74](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/SystemIntegration.cs#L45-L74)). On this PC `%AppData%\Playnite\config.json` has `"StartOnBoot": true`. **So deleting the shortcut alone is undone by the very next Playnite start**, which under the Boot shell happens before explorer runs the Startup folder. The default is off ([PlayniteSettings.cs L1256](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/Settings/PlayniteSettings.cs#L1256)). The installer leaves the shortcut alone: `/UPDATE` skips creating shortcuts, and the 2021 script never had a Startup one ([InnoSetup.iss L49–L53, L100–L103](https://github.com/JosefNemec/Playnite/blob/e916d3858319043ac9fe8ca484d1da58a2a565fe/build/InnoSetup.iss#L49-L53)). On this PC `Playnite.lnk` still has its 2026-10-09 22:12:48 timestamp after the 2026-10-20 update. | Confirmed (source, this PC) |
| **Windows** | Upgrades carry the switches over: shell32's manifest includes `HKCU\…\Explorer\StartupApproved\* [*]` and the HKLM one in its migration rules. Packaged apps can't undo a user's switch-off: `RequestEnableAsync` "will not override their choice", and `DisabledByUser` "can only be re-enabled by the user" ([RequestEnableAsync](https://learn.microsoft.com/uwp/api/windows.applicationmodel.startuptask.requestenableasync), [StartupTaskState](https://learn.microsoft.com/uwp/api/windows.applicationmodel.startuptaskstate)). Packaged tasks here: Xbox app `DisabledByUser` (1), Teams `Enabled` (2), Terminal and LocalSend `Disabled` (0). Teams therefore starts with explorer, which is still after Start-up. | Confirmed (manifest, docs, this PC) |

**Why it matters which ones flip back.** Every entry explorer runs trails Start-up anyway, because the Boot shell starts explorer late. Three cases cost something:

- **`Playnite.lnk` running.** A second Playnite start makes the running Fullscreen **switch to Desktop mode** ([per-user shell §2](https://github.com/snrgarb/xbox-series-theme/blob/research/per-user-shell/docs/research/per-user-shell.md#2-startup-apps-when-explorer-starts-late)).
- **Steam, Discord or Spotify switched back on.** The Boot shell starts these itself, so explorer would start them a second time. A second Discord or Spotify start can bring its window forward over Playnite. **Inference.**
- **"Restart apps".** With *Automatically save my restartable apps and restart them when I sign back in* on (`HKCU\…\Winlogon\RestartApps`), Windows queues `RunOnce\Application Restart #N` entries at shutdown, and explorer runs them. Winlogon's manifest names both values. It is unset here.

## 3. `ShellAppRuntime.exe`

- **It is Windows' "Lightweight shell", not explorer split in two.** Its PDB path is `…embedded\sys\shelllauncher\customshellhost\shellappruntime\shellappruntime.cpp`, beside Shell Launcher v2's `CustomShellHost.exe`. It ships in its own component (`Microsoft-Windows-Shell-ShellAppRuntime` 10.0.26100.9444), whose only registry entry is `Winlogon\ShellPrograms\ShellAppRuntime.exe` with `AppResolverHostProcess = 1`, `HandlesShellDesktopSwitchEvent = 1` and `InitializationCompleteEvent = ""`. The Winlogon component adds `HKLM\…\Winlogon\ShellAppRuntime = ShellAppRuntime.exe`. Microsoft describes the Lightweight shell as having no user interface and "a limited set of features which could be consumed by applications" ([OverrideShellProgram](https://learn.microsoft.com/windows/client-management/mdm/policy-csp-windowslogon#overrideshellprogram)). It carries explorer's first-sign-in tasks too (the SCOOBE eligibility class, Spotlight first-logon strings). **Confirmed (manifests, docs), strong (strings).** This corrects the [per-user shell research](https://github.com/snrgarb/xbox-series-theme/blob/research/per-user-shell/docs/research/per-user-shell.md#3-whats-missing-before-explorer-runs-and-what-comes-back), which took it for the immersive shell split out of explorer and waiting behind a feature flag.
- **What turns it on:** the `OverrideShellProgram` device policy, set to `1`. Windows 365 Boot sets it (`HKLM\Software\Microsoft\PolicyManager\current\device\WindowsLogon\OverrideShellProgram = 1`, [Troubleshoot Windows 365 Boot](https://learn.microsoft.com/troubleshoot/windows-365/troubleshoot-windows-365-boot#physical-device-registry-key-configuration)). In the binaries:
  - `winlogon.exe` names `ShellAppRuntime.exe` only next to `OverrideShellProgram`, `WindowsLogon` and the CloudDesktop node values;
  - `userinit.exe` names it next to the CloudDesktop values and a wait on `Local\ShellStartupEvent`;
  - `explorer.exe` never names it.

  Here the policy is at its default `0` (`PolicyManager\default\WindowsLogon\OverrideShellProgram`), there is no `current` value, no Shell Launcher configuration, and neither `ShellAppRuntime` nor `CustomShellHost` is running. **Confirmed (this PC).**
- **What it changes:**
  - **If the policy were set**, Winlogon would start `ShellAppRuntime.exe` instead of *any* `Shell` value, the Boot shell's included, and explorer would never start. Only MDM or an admin can set it, and an update ships it at `0`. **Confirmed (docs), inference for "no update sets it".**
  - **Without the policy**, it plays no part in how userinit picks the per-user shell or how explorer decides to be the shell. **Strong.**
  - **It is no shortcut to Game Pass launches before explorer.** It hosts the app resolver only when it *is* the shell. Starting it by hand beside the Boot shell is untested and could claim the shell window, which would stop a later explorer from becoming the shell. **Inference.** Don't.

## 4. "Let's finish setting up your device" (SCOOBE)

- **Who shows it: explorer, as a logon task.**
  - `explorer.exe` holds `Windows.Internal.System.UserProfile.ScoobeEligibility.ModalUserSetupEligibilityManager` (implemented in `CloudExperienceHostRedirection.dll`).
  - It also holds logon-task names `LaunchUserOobePrep`, `LaunchBasicCXH`, `LaunchUserOobe` and `LogonTasks-s_LaunchUserOobeMethod`, and the setup pages it opens (`ms-cxh://OOBE/…`, `ms-cxh://NTH…`, the "Nth sign-in" privacy, Hello, Recall and Microsoft 365 pages).
  - `ShellAppRuntime.exe` has the same eligibility class.
  - `winlogon.exe` and `userinit.exe` have none of these.

  **Strong (strings).** Microsoft described the page when it shipped: "You may see this window showing after you upgrade… choose **Skip for now** if you want to go straight to your desktop" ([Windows 10 RS5 Insider notes](https://learn.microsoft.com/windows-insider/archive/new-in-rs5#post-upgrade-setup)).
- **So under the Boot shell it comes over Playnite.** It doesn't come before the Boot shell, and it doesn't come from Winlogon. It arrives when the Boot shell starts explorer, after Start-up's first frame, as a full-screen setup window that takes the foreground. On a controller-only TV it would sit on top of Who's playing today? or Home. **Inference** (that it takes the foreground). The eligibility logic records `UserSetupLastSkippedTime`, `IsUserSetupCurrentlyPostponed` and build-change fields, so it can come back after later updates, not only after the first one. **Strong (strings).**
- **Each user can switch it off without admin.** It is Settings > System > Notifications > Additional settings > *Suggest ways to get the most out of Windows and finish setting up this device*:
  - `SettingsHandlers_nt.dll` writes it to `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement\ScoobeSystemSettingEnabled`;
  - the eligibility data in `windowsudk.shellcommon.dll` reads that value.

  **Strong (strings)**, and the setting path is confirmed. This key doesn't exist on this PC, so SCOOBE is **on**. Unknown: whether the switch also stops the separate "Nth sign-in" pages (U5).
- **Worth switching off on the same page:**
  - *Show the Windows welcome experience after updates…* (`ContentDeliveryManager\SubscribedContent-310093Enabled`, not set here). It opens a "what's new" page after updates. It wasn't traced in the binaries. **Inference.**
  - *Get tips and suggestions…* (`SubscribedContent-338389Enabled = 1` here).
- **The first sign-in animation is a different screen.** The "Hi… we've updated your PC" screens belong to Winlogon (`EnableFirstLogonAnimation`, `FirstLogonAfterUpdateFlag` in `winlogon.exe`). They come *before* the Boot shell starts and delay it, but change nothing. The switch is the HKLM policy *Show first sign-in animation* (`EnableFirstLogonAnimation = 1` here, admin only; [Policy CSP](https://learn.microsoft.com/windows/client-management/mdm/policy-csp-windowslogon#enablefirstlogonanimation)). **Strong.**

## 5. Playnite's own updater

### What happens, step by step

1. **Only a person starts it.** Playnite checks for updates 5 s after start and every 4 h ([PlayniteApplication.cs L1452–L1476](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/App/PlayniteApplication.cs#L1452-L1476)). A found update only sets `UpdatesAvailable` and, when Playnite isn't the active window, asks for a Windows notification ([L1378–L1416](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/App/PlayniteApplication.cs#L1378-L1416)). Fullscreen shows no Windows notification ([FullscreenApplication.cs L224–L227](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite.FullscreenApp/FullscreenApplication.cs#L224-L227)). Installing happens only from the update window's *Install* ([UpdateViewModel.cs L99–L133](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/ViewModels/UpdateViewModel.cs#L99-L133)). This PC checks on every start (`CheckForProgramUpdates = 0`). **Confirmed.**
2. **Playnite closes itself.** It downloads to `%TEMP%\Playnite\update.exe`, checks the MD5 and calls `QuitAndStart` ([Updater.cs L169–L178](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/App/Updater.cs#L169-L178)). That:
   - saves settings and releases resources, **including the single-instance mutex `PlayniteInstaceMutex`**;
   - starts the installer;
   - shuts Playnite down ([PlayniteApplication.cs L1228–L1250, L1278–L1290](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/App/PlayniteApplication.cs#L1228-L1250)).

   The command line is `/SILENT /NOCANCEL /DIR="<install dir>" /UPDATE [/PORTABLE] [/FULLSCREEN]`, with `/FULLSCREEN` when the update was started from Fullscreen. It is elevated only if the install folder isn't writable. Here Playnite is a per-user install in `%LocalAppData%\Playnite` (`Playnite_is1`, `unins000.exe`), so no UAC prompt appears. **Confirmed.**
3. **The installer (Inno Setup) waits, installs and restarts Playnite.** Its script hasn't been public since 2021 ("Removed obsolete scripts"). The last public copy ([InnoSetup.iss](https://github.com/JosefNemec/Playnite/blob/e916d3858319043ac9fe8ca484d1da58a2a565fe/build/InnoSetup.iss)) does this:
   - with `/UPDATE`, **loops `while CheckForMutexes('PlayniteInstaceMutex') … Sleep(300)` until no Playnite holds the mutex** (L100–L110). It also has `AppMutex` and `CloseApplications=yes`;
   - with `/SILENT`, hides the wizard but **shows the progress window** ([Inno command line](https://jrsoftware.org/ishelp/topic_setupcmdline.htm));
   - then **starts `Playnite.FullscreenApp.exe` with no arguments** if `/FULLSCREEN` was passed, and `Playnite.DesktopApp.exe` otherwise (L55–L57, L153–L166).

   **Strong** (the script is from 2021, but this PC's 2026 update behaved the same way).
4. **This PC's 10.62 → 10.63 update, 2026-10-20, from Fullscreen** (`%AppData%\Playnite\playnite.log`):

   | Time | Event |
   |---|---|
   | 11:22:44.855 | "Installing new update", Playnite releases its resources and mutex. The package was `1062to1063.exe`, a 4.4 MB patch. |
   | 11:22:46.468 | `update.exe … /UPDATE /FULLSCREEN` started, not elevated |
   | 11:22:46.619 | "Shutting down Playnite" |
   | 11:22:48.141 | New Playnite starts with **no arguments** |
   | 11:22:52.040 | **Fullscreen** main window shown |

   So no Playnite process ran for **~1.5 s**, and Fullscreen was back **~7 s** after *Install*. The new Playnite is the installer's child, not the Boot shell's. It starts without `--hidesplashscreen`. **Confirmed (this PC).**
5. **Add-on and theme updates** restart Playnite without an installer: `Restart` → `QuitAndStart(Playnite.FullscreenApp.exe, "--masterinstance …")` ([FullscreenApplication.cs L217–L222](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite.FullscreenApp/FullscreenApplication.cs#L217-L222)). *Switch to Desktop mode* is the same call with `--startdesktop` (seen at 11:36:16 in the same log). **Confirmed.**

### Rules this sets for the watchdog

From [How does the tool keep Playnite up, and when does it fall back to the desktop?](https://github.com/snrgarb/xbox-series-theme/issues/105):

- **Don't relaunch while an update runs.** If Playnite is gone and `%TEMP%\Playnite\update.exe` is running, Playnite is updating. If the watchdog started Playnite then, the new Playnite would take the mutex, and the installer would wait for it indefinitely (or, through `AppMutex`, show a "close Playnite" box behind Fullscreen). The update would then never apply. **Inference from the script.** Wait for `update.exe` to exit, then for a Playnite window, with a timeout. Fall back to starting Fullscreen only after the timeout.
- **Use the mutex, not the child handle, to tell whether Playnite is up.** After an update, an add-on restart or a mode switch, Playnite is not the Boot shell's child. `PlayniteInstaceMutex` held by anyone means "Playnite is up or starting" ([PlayniteApplication.cs L38, L155](https://github.com/JosefNemec/Playnite/blob/3e8abb241873e326e27c75b59dd0d2ea367698e3/source/Playnite/App/PlayniteApplication.cs#L38)). After any exit, give a short grace period for a self-restart before relaunching.
- **Cover the gap.** During those 1.5–7 s the desktop (explorer, if it's up) and the installer's progress window show. To keep "no desktop flash", the Boot shell can put its black window back when it sees `update.exe` and remove it when Fullscreen's window returns.

## 6. The sign-in check

Every item below is a registry or file read, plus a few HKCU writes, so none needs admin. The setup records what it set (value names, the exact `StartupApproved` bytes, file names). The Boot shell compares against that record **while Playnite is starting and before it starts explorer**. That is the last moment a fix still affects what explorer runs. Whether it fixes things or only reports them is for "How the Boot shell is set up and removed" to decide. The list says which fixes are safe.

| # | Check | If it fails | Safe to fix silently? |
|---|---|---|---|
| C1 | HKLM `Winlogon\Shell` contains `explorer.exe` | Explorer won't become the shell, so Game Pass launches break | No (admin). Report. |
| C2 | HKCU `Winlogon\Shell` equals the Boot shell's path | The build moved | Yes (HKCU) |
| C3 | `%AppData%\Playnite\config.json` has `"StartOnBoot": false` | Playnite re-creates `Playnite.lnk` at this very start | Don't edit `config.json`: Playnite rewrites it on exit. Handle the shortcut instead (C4) and report. |
| C4 | No `Startup\Playnite.lnk`, or it is switched off in `StartupApproved\StartupFolder` | Explorer would start DesktopApp and flip Fullscreen to Desktop mode | Yes. After Start-up's first frame, when Playnite has finished its own start, delete the shortcut or write the switch-off bytes before starting explorer. |
| C5 | `StartupApproved\Run` has the recorded switch-off bytes for `Steam`, `Discord`, `Spotify`, and any Startup-folder shortcut to them is switched off or gone | A second copy starts with explorer and may take the foreground | Yes (HKCU), re-write the bytes |
| C6 | No new Run, RunOnce or Startup-folder entries, and no newly enabled packaged startup task (`State` 2 or 4), since the record | Something new starts with explorer (still after Start-up) | Log only |
| C7 | `HKCU\…\Winlogon\RestartApps` isn't `1`, and HKCU `RunOnce` has no `Application Restart #*` values | Explorer relaunches apps that were open at shutdown | Report |
| C8 | `UserProfileEngagement\ScoobeSystemSettingEnabled = 0` | SCOOBE can cover Playnite when explorer starts | Yes (HKCU), but it's a Settings choice. Set it at setup and report if it changes. |
| C9 | `CurrentBuild.UBR` against the last boot's, and whether a new `HKLM\SYSTEM\Setup\Source OS (Updated on …)` key exists | An update or a full upgrade happened since the last boot | Log it, so a regression can be tied to the update |

**The sentinel, for a lost `Shell` value.** The Boot shell can't check a value whose loss stops it from running. One fix:

- **Setup** adds an HKCU Run entry (for example `BootShellSentinel` → `"<Boot shell>.exe" --sentinel`).
- **Normally** explorer runs it after Start-up, the sentinel finds the Boot shell's own mutex and exits.
- **If the Boot shell didn't run this session**, explorer was the shell from the start. The sentinel then:
  1. writes the HKCU `Shell` value back;
  2. starts `Playnite.FullscreenApp.exe`;
  3. records what happened, so the next boot is right again.
- **Undo** removes the sentinel with the `Shell` value.

It costs nothing on a normal boot, because explorer's startup run happens after Start-up anyway. It can't help if `OverrideShellProgram` is set, because then explorer never runs. That policy is admin- or MDM-only (§3). **Inference.** Prototype it in U7.

## 7. What this means for the map

- **[How does the tool keep Playnite up, and when does it fall back to the desktop?](https://github.com/snrgarb/xbox-series-theme/issues/105)**
  - Playnite's update is user-started and comes back in **Fullscreen** on its own, after ~1.5 s without a Playnite process and ~7 s to the window.
  - The watchdog must not relaunch while `%TEMP%\Playnite\update.exe` runs or `PlayniteInstaceMutex` is held. A relaunch would leave the installer waiting on the mutex.
  - It should judge "Playnite is up" by the mutex or window, not by its own child, because after an update, an add-on restart or a mode switch, Playnite isn't its child.
  - It can cover the gap with its black window.
- **[When does explorer start, and what starts the startup apps?](https://github.com/snrgarb/xbox-series-theme/issues/104)**
  - **Playnite's own "Start Playnite on computer startup" must be off**, not just `Playnite.lnk` deleted, because Playnite re-creates the shortcut at every start. It is on now.
  - Switch-offs in Settings survive Steam, Spotify and Discord updates and Windows upgrades. Discord removes its switch-off only when "Open Discord" is turned on in Discord or Discord is reinstalled.
  - `DiscordMini.lnk` isn't Discord's. Its working folder `app-1.0.9264` disappears at the next Discord update. If the Boot shell starts Discord, use the root `%LocalAppData%\Discord\Discord.exe --processStart Discord.exe`.
  - Explorer will show SCOOBE when it starts after an update, so switch it off per user.
  - Teams' packaged startup task is on.
- **"How the Boot shell is set up and removed" (not yet specified)**
  - Setup should also turn off Playnite's *Start on boot* and SCOOBE, record the exact `StartupApproved` bytes it wrote, and add the sentinel.
  - Removal undoes all of that.
  - The sign-in check is §6.
- **[What is the Boot shell built in, where does it live in this repo, and what does it log?](https://github.com/snrgarb/xbox-series-theme/issues/110)**: the sentinel is a second entry point (`--sentinel`) of the same program. The log should record the build number (C9) and every check result.
- **[Prototype the per-user shell on the TV: black until Start-up, explorer behind, first Game Pass launch](https://github.com/snrgarb/xbox-series-theme/issues/107)**: U6 and U7 below need the prototype, so they fit there.
- **Correction to the per-user shell research:** `ShellAppRuntime` isn't a feature-flagged explorer split. It is the Lightweight shell behind a device policy, and no route to Store activation before explorer (§3).

## 8. Tests to run on the TV

Snapshot script for U1–U3: export, to a dated file:
- HKCU and HKLM `Winlogon` (`Shell`, `Userinit`, `RestartApps`);
- HKCU `Run` and `RunOnce`;
- HKCU and HKLM `StartupApproved\*`;
- the Startup folder listing;
- `UserProfileEngagement`;
- the packaged `StartupTask` states;
- `config.json`'s `StartOnBoot`;
- `CurrentBuild.UBR`;
- any `Setup\Source OS*` keys.

All of it is readable without admin.

- **U1, a monthly cumulative update.** Snapshot before and after its restart. Expect no difference apart from the build number.
- **U2, a full upgrade.** This is the next new-build-family upgrade, or *Reinstall now*, and only if one is wanted anyway. Snapshot before and after. Record:
  - whether HKCU `Shell` survives;
  - whether the Boot shell runs at the first sign-in after the upgrade (with the "Hi" screens);
  - whether the sentinel fired;
  - whether SCOOBE showed when explorer started.

  Expect HKLM `Shell` and `StartupApproved` to survive (manifests). HKCU `Shell` is the open question.
- **U3, app updates.** After Steam's, Spotify's and Discord's next client updates, compare snapshots. Expect `StartupApproved\Run\Steam|Spotify|Discord` to keep their `01 …` bytes and timestamps. Optionally, turn Steam's *Run Steam when my computer starts* off and on, and watch whether Steam re-writes the Run value and leaves the switch. Do the same for Spotify's *Open Spotify automatically*.
- **U4, Playnite's start-on-boot.** No sign-in needed: just restart Playnite.
  1. With *Start Playnite on computer startup* on, delete `Playnite.lnk`, restart Playnite, and expect it back.
  2. Turn the setting off in Playnite, restart Playnite, and expect the shortcut gone.
- **U5, SCOOBE and the Nth sign-in pages.** On the first boot after the next update that triggers them, note what explorer shows and whether it takes the foreground from Playnite. Run it first with `ScoobeSystemSettingEnabled` unset (as now), and after switching it off, check that nothing shows on later updates.
- **U6, a Playnite update under the Boot shell.** At the next Playnite release, update from Fullscreen with the watchdog running. Log, every 100 ms:
  - the Playnite processes;
  - `update.exe` start and exit;
  - the `PlayniteInstaceMutex` state;
  - the foreground window.

  Confirm that the watchdog didn't relaunch during the gap and that Fullscreen came back. Film what shows on screen during the gap.
- **U7, the sentinel.** In the prototype, with the sentinel's Run entry in place, delete HKCU `Shell` and sign out and in. Expect explorer as the shell, then the sentinel after explorer's startup delay. It should put `Shell` back and start Fullscreen, and the next sign-in should run the Boot shell. It needs the Boot shell from [Prototype the per-user shell on the TV: black until Start-up, explorer behind, first Game Pass launch](https://github.com/snrgarb/xbox-series-theme/issues/107) first.

## Confidence and gaps

- **Confirmed:**
  - Winlogon's migration rules carry HKLM `Shell` and `Userinit` with the old value winning. Shell32's carry `StartupApproved`. No rule read names HKCU `Shell`.
  - The `OverrideShellProgram` precedence and what sets it. The StartupTask rules.
  - Discord 1.0.9264's autostart logic.
  - Playnite's `SetBootupStateRegistration` at every start, its update and restart path, and `StartOnBoot = true` on this PC.
  - The 2026-10-20 update timeline. `01` meaning off on this PC.
  - That this PC has had no Setup upgrade since install.
- **Strong:**
  - Steam's and Spotify's binaries never refer to `StartupApproved`.
  - SCOOBE is an explorer logon task, and the per-user switch reads `ScoobeSystemSettingEnabled`.
  - `ShellAppRuntime` is started only by the policy.
  - The 2021 installer script still describes today's installer.
  - The Cairo report that a full upgrade broke an HKCU shell.
- **Inference:**
  - Whether HKCU `Shell` survives a full upgrade.
  - That a second Discord or Spotify start takes the foreground.
  - That SCOOBE takes the foreground over Playnite.
  - That a watchdog relaunch would stall the installer.
  - Insider flights on this branch behaving like cumulative updates.
- **Unknown until a TV test:** U2 (full upgrade), U5 (the SCOOBE switch covering the Nth sign-in pages), and how Steam and Spotify handle their own settings (U3).
