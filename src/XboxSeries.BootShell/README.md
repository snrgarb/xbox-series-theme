# Boot shell

This TV PC's own sign-in shell (see **Boot shell** in `CONTEXT.md`, and ADR 0002). Windows starts it in explorer's place. It:

1. starts Playnite's Fullscreen (`%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe --hidesplashscreen`);
2. puts a topmost, black, cursorless window over the screen, and sets `ShellDesktopSwitchEvent`, so Windows' Welcome screen gives way straight to black;
3. makes its two events, which Start-up sets (`BootShellEvents.cs`, linked from the extension), and takes the black down at FirstFrame, or after 60 s at most;
4. starts explorer at BootVideoEnded, or 20 s after FirstFrame without it, or at the 60 s limit, once per sign-in, and only while no shell is up;
5. starts Steam, Discord and Spotify straight after explorer;
6. keeps Fullscreen up until Windows ends the session (the watchdog, below), and starts explorer again if Windows hasn't brought it back 10 s after it went;
7. reads the pad itself (the pad, below), so the TV can always be driven back from the sofa;
8. holds the mutex `Local\XboxSeries.BootShell` until sign-out, which the sentinel looks for (the safety nets, below).

## The watchdog

Every 100 ms it looks for Playnite's processes in this session (`Playnite.FullscreenApp`, `Playnite.DesktopApp`, and the updater's `update.exe` under `%TEMP%\Playnite`), never its own child and never Playnite's mutex. When one exits, what's left says what it was:

- another Playnite or `update.exe` left: a restart, an update, a mode switch, or the crash window's Restart; it waits for them;
- nothing left and exit code 0: a quit the user meant; Playnite stays down;
- nothing left and any other code, or any exit before that start's first frame: a crash; it deletes `%APPDATA%\Playnite\safestart.flag` and relaunches Fullscreen at once.

Every exit code but 0 counts as a crash, the crash window's Restart (-1) too. The third crash within 5 minutes shows "Playnite closed 3 times in 5 minutes. Press the Guide button to try again." on the black for 5 s, then starts explorer, Steam, Discord and Spotify, each unless it's up, and relaunches nothing more on its own.

Each Fullscreen start, its own relaunch or one that follows Playnite quitting, gets the black and freshly reset events, until its first frame or for 60 s at most, a start that reaches the limit counting from then as past its first frame; not a `--safestartup` start. The black lifts at once when `playnite.log` says "Unhandled exception occured.", so the pad can reach Playnite's crash window.

## The pad

A thread of its own reads every pad every 30 ms through `XInputGetStateEx` (xinput1_4's ordinal 100), which has the Guide button as `0x0400`; a slot with no pad is tried once a second. Game Bar's controller button is off on this PC (`UseNexusForGameBarEnabled` 0).

- **The Guide button**, while no Fullscreen runs (Playnite closed on purpose, fallen back, or in Desktop mode): starts `Playnite.FullscreenApp.exe --hidesplashscreen` behind the black, after deleting `safestart.flag`, and starts the crash count again. From Desktop mode, Playnite's own second-instance handling switches it back to Fullscreen. While Fullscreen runs the button is Playnite's, and while `update.exe` runs it starts nothing.
- **View+Menu held for 5 s**, at any time, under the black at sign-in, over Fullscreen, over a game or with Playnite hung: `WM_CLOSE` to the window titled "Playnite" of each Fullscreen and Desktop-mode process, ending any still up 10 s later; the black and its line down; explorer, Steam, Discord and Spotify, each unless it's up. Playnite stays down, as the hold isn't a crash. It's also the button held at boot.

There's no automatic hang detection: a stalled Playnite is left to the hold.

It's not part of the Bundle: `dotnet build` builds it with the rest, but it is never packed into the .pext.

## Its log

One plain-text file per sign-in, `%LOCALAPPDATA%\XboxSeries.BootShell\logs\<yyyyMMdd-HHmmss>.log`. Each line is stamped with the wall clock, ms since boot (`boot+`) and ms since the Boot shell's process was created (`h+`). Files past the newest 30 are deleted at sign-in. It has a line for every Playnite process that appears or exits, with its exit code and how it was judged, the crash count, each relaunch, the black and the fallback, each Guide press, each View+Menu hold and what followed.

## The safety nets

Two nets for a sign-in the Boot shell doesn't carry, both written by `--install` and taken off by `--uninstall`.

- **The sentinel**, the HKCU Run entry `XboxSeriesBootShellSentinel`: `"<installed exe>" --sentinel`. A full Windows upgrade may drop the HKCU `Shell` value, and the Boot shell can't notice a sign-in it never ran. Explorer's startup run starts the sentinel after Start-up; it finds the Boot shell's mutex and exits. With no Boot shell in the session, explorer was the shell from the start: it writes `Shell` back, naming its own exe, and starts `Playnite.FullscreenApp.exe --hidesplashscreen` unless Fullscreen runs. The next sign-in is the Boot shell's again. Its lines go to `logs\sentinel.log`.
- **The safety net**, the logon task `XboxSeries.BootShell.SafetyNet`: 90 s after the user signs in, at normal rights, `conhost.exe --headless powershell.exe -Command "…"` starts explorer if none runs in the session and writes a line to `logs\safety-net.log`; nothing else. It runs no Boot shell exe, so it covers a Boot shell that crashed or hung before explorer, and a missing or broken exe. Explorer's startup run then brings Orca up, and, unless a hung Boot shell still holds its mutex, the sentinel starts Fullscreen. By 90 s explorer is always up when the Boot shell works.

Registering the task needs no admin: a logon task for the signed-in user registers from a Medium terminal (`schtasks /Create /XML`, tried on this PC 2026-10-23). Windows hands it back rewritten (the delay as `PT1M30S`, the user as a SID), so setup compares only its command, arguments and delay.

## Publishing

`dotnet build` and the tests need nothing more than the .NET 10 SDK. `dotnet publish` compiles one NativeAOT exe, which needs Visual Studio Build Tools with the *Desktop development with C++* workload, installed once:

```powershell
winget install --id Microsoft.VisualStudio.BuildTools --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

The Build Tools' `vcvarsall.bat` calls `vswhere.exe` by name, and the installer doesn't put its folder on PATH, so add it in the shell you publish from:

```powershell
$env:PATH += ";${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
dotnet publish src/XboxSeries.BootShell -c Release -r win-x64
```

The exe is `src\XboxSeries.BootShell\bin\Release\net10.0-windows\win-x64\publish\XboxSeries.BootShell.exe`. It runs with no .NET runtime installed.

## Setting it up and taking it off

From a terminal on this PC, not elevated (from an elevated one, `--shutdown` never reaches your Playnite), in the publish folder. The exe is a Windows app, so pipe it through `Out-Host` for the terminal to show its lines and wait for it:

```powershell
cd src\XboxSeries.BootShell\bin\Release\net10.0-windows\win-x64\publish
.\XboxSeries.BootShell.exe --install | Out-Host
.\XboxSeries.BootShell.exe --uninstall | Out-Host
```

Each prints a line per setting and exits 1 if it stopped short. Each also writes its lines to `logs\setup-<yyyyMMdd-HHmmss>.log`. Both take effect from the next sign-in.

### `--install`

It copies the exe to `%LOCALAPPDATA%\XboxSeries.BootShell\XboxSeries.BootShell.exe`, beside `logs\`, `setup-record.json` and `backup\`. Then it changes, in this order:

- the safety net's logon task (the safety nets, above);
- the Startup-apps switch (`HKCU\…\Explorer\StartupApproved\Run`) off for Steam, Discord, Spotify, AMDNoiseSuppression and RobloxPlayerBeta, each that has an HKCU Run entry and isn't off already;
- Teams' packaged startup task (`TeamsTfwStartupTask` `State`) off, if Teams is installed;
- `Playnite.lnk` and `DiscordMini.lnk` moved from the Startup folder (asked of Windows, as it's redirected here) into `backup\`;
- SCOOBE off (`UserProfileEngagement\ScoobeSystemSettingEnabled` 0);
- Playnite's settings, with Playnite sent `--shutdown` first if it runs (a running Boot shell leaves it down): in `config.json`, `StartOnBoot` off; *Extra Metadata tools*, UniPlaySong and Battle.net in `DisabledPlugins`; auto-backup on, once a week; the Bundle's extension folder in `DevelExtenions`, selected, so it loads first; in `fullscreenConfig.json`, `MainMenuShowMinimize` off;
- the sentinel's HKCU Run entry, just before `Shell`, as the sentinel would write `Shell` itself;
- last, `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell`, naming the installed exe, so a setup that stops halfway never boots into the Boot shell.

Before it changes a value, it records the value as it was in `setup-record.json`, saving the record each time, so a setup stopped halfway can still be undone. Running `--install` again updates in place: it renames the installed exe aside to `XboxSeries.BootShell.<stamp>.old` (Windows allows renaming a running exe), copies the new one in and changes whatever doesn't hold. A value already in the record keeps its first before-value, so undo still goes back to the PC before the first setup. The new build runs from the next sign-in, which deletes the old files.

### `--uninstall`

It puts back each recorded value, last change first: `Shell`, then the sentinel's Run entry, Playnite's settings with Playnite closed first, the shortcuts, SCOOBE, Teams' task, the startup switches' old bytes, and last the safety net's task, deleted. With `StartOnBoot` and `Playnite.lnk` back, Playnite starts at the first sign-in after undo. A value that changed after setup (it no longer holds what setup wrote) is left alone and said. The exe and logs stay; the record is renamed `setup-record.<stamp>.json`, so a later `--install` records afresh. A running Boot shell runs on until sign-out.

### Before the first `--install` on a PC set up by hand

Undo the hand setup first, so the record keeps the PC as it was without the Boot shell: delete the `Shell` value; move `Playnite.lnk` and `DiscordMini.lnk` from `%LOCALAPPDATA%\XboxSeries.BootShell\backup\` back into the Startup folder; switch Steam, Discord and Spotify back on in Settings > Apps > Startup (Roblox stays off, before setup and after undo); and switch Playnite's *Start Playnite on computer startup* back on. Otherwise undo would put the hand setup back, and Playnite wouldn't start after it.
