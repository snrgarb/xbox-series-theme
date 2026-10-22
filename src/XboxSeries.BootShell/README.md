# Boot shell

This TV PC's own sign-in shell (see **Boot shell** in `CONTEXT.md`, and ADR 0002). Windows starts it in explorer's place. It:

1. starts Playnite's Fullscreen (`%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe --hidesplashscreen`);
2. puts a topmost, black, cursorless window over the screen, and sets `ShellDesktopSwitchEvent`, so Windows' Welcome screen gives way straight to black;
3. makes its two events, which Start-up sets (`BootShellEvents.cs`, linked from the extension), and takes the black down at FirstFrame, or after 60 s at most;
4. starts explorer at BootVideoEnded, or 20 s after FirstFrame without it, or at the 60 s limit, once per sign-in, and only while no shell is up;
5. starts Steam, Discord and Spotify straight after explorer, then stays up until sign-out.

It's not part of the Bundle: `dotnet build` builds it with the rest, but it is never packed into the .pext.

## Its log

One plain-text file per sign-in, `%LOCALAPPDATA%\XboxSeries.BootShell\logs\<yyyyMMdd-HHmmss>.log`. Each line is stamped with the wall clock, ms since boot (`boot+`) and ms since the Boot shell's process was created (`h+`). Files past the newest 30 are deleted at sign-in.

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
