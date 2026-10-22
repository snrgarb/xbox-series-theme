# Boot shell

This TV PC's own sign-in shell (see **Boot shell** in `CONTEXT.md`, and ADR 0002). Windows starts it in explorer's place. It:

1. starts Playnite's Fullscreen (`%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe --hidesplashscreen`);
2. puts a topmost, black, cursorless window over the screen, and sets `ShellDesktopSwitchEvent`, so Windows' Welcome screen gives way straight to black;
3. makes its two events, which Start-up sets (`BootShellEvents.cs`, linked from the extension), and takes the black down at FirstFrame, or after 60 s at most;
4. starts explorer at BootVideoEnded, or 20 s after FirstFrame without it, or at the 60 s limit, once per sign-in, and only while no shell is up;
5. starts Steam, Discord and Spotify straight after explorer;
6. keeps Fullscreen up until Windows ends the session (the watchdog, below), and starts explorer again if Windows hasn't brought it back 10 s after it went;
7. reads the pad itself (the pad, below), so the TV can always be driven back from the sofa.

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

## Trying it on the TV

Until `--install` comes:

- set `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell` to the published exe's full path, and delete the value to undo;
- switch Steam's, Discord's and Spotify's entries off in Settings > Apps > Startup, and any other app you don't want trailing Start-up;
- switch off Playnite's own *Start Playnite on computer startup* (Settings > General in Desktop mode): while it's on, Playnite rewrites `Startup\Playnite.lnk` at every start, and that shortcut's Desktop app tells the running Fullscreen to switch to Desktop mode as explorer's startup run reaches it;
- keep a safety net like the prototype's `safety-net.vbs` registered (on the `prototype/boot-shell` branch), so a Boot shell that dies before explorer still lets Orca come up.
