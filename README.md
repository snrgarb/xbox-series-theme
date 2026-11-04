# Xbox Series Theme

The Xbox Series Theme makes Playnite 10 look and work like the Xbox Series X|S dashboard. Use it on a TV PC with an Xbox controller.

## Downloads

| Component | Version | Released | Download |
|---|---|---|---|
| Dashboard theme | 0.1.0 | 2026-11-04 | [XboxSeries_Dashboard_0.1.0.pthm](https://github.com/snrgarb/xbox-series-theme/releases/download/theme-v0.1.0/XboxSeries_Dashboard_0.1.0.pthm) |
| Extension | 0.1.0 | 2026-11-04 | [XboxSeries_0.1.0.pext](https://github.com/snrgarb/xbox-series-theme/releases/download/extension-v0.1.0/XboxSeries_0.1.0.pext) |
| Boot shell | 1.0.0 | 2026-11-03 | [XboxSeries.BootShell.exe](https://github.com/snrgarb/xbox-series-theme/releases/download/bootshell-v1.0.0/XboxSeries.BootShell.exe) |

All three are early-access releases. For older versions, see [Releases](https://github.com/snrgarb/xbox-series-theme/releases).

The theme and the extension are the Bundle. Install the two together, from the same release. The Boot shell is optional.

## What it does

### The Dashboard theme

The theme changes the Playnite Fullscreen screens to look like the console:

- **Home**: your recently played games, your Groups, and tiles for Discord, Playnite settings, Achievements and Downloads.
- **Game hub**: one page for each game, with its details and actions.
- **Tile menu**: one menu for each game. Play, install, add to Favourites, manage or uninstall the game.

### The extension

The extension adds the console's functions. It works only in Fullscreen mode with the Dashboard theme. In Desktop mode or with a different theme, it does nothing.

- **Start-up**: a boot video, then *Who's playing today?*, then sign-in.
- **Profiles**: one profile for each person who plays on the PC.
- **The Guide**: press the Guide button to open the Guide over Playnite or over a game. Hold the Guide button to open Power.
- **Discord**: see your call in the Guide. Mute, deafen, leave and join channels.
- **Now Playing**: see and control the music that plays, for example on Spotify.
- **Toasts**: green banners for notifications, calls, downloads, achievements and library updates. A Toast does not stop your game.
- **Launch screen**: the game's art covers launchers and update windows while the game starts.
- **Idle screen**: when nobody uses the PC, the screen dims. Then it shows your games, achievements and tips.
- **My games & apps**: your games, apps, Groups, play history and full library.
- **Downloads**: the Queue shows the games that Playnite and Steam install.
- **Achievements**: Toasts and totals for your achievements. This needs the PlayniteAchievements add-on.
- **Search**: find games and apps with an on-screen keyboard for the controller.
- **Settings**: the options for the theme and for Playnite.

### The Boot shell

The Boot shell is for the TV PC of this project. It replaces the Windows desktop at sign-in:

- It shows a black screen and starts Playnite in Fullscreen mode.
- It starts Playnite again after a crash. After three crashes in five minutes, it opens the Windows desktop.
- When Playnite is closed, press the Guide button to start Playnite again.
- Hold View and Menu for 5 seconds to close Playnite and open the Windows desktop.

## Install

### The Bundle

1. Install [Playnite 10](https://playnite.link).
2. Download the theme and the extension.
3. Drag the `.pthm` file onto the Playnite window.
4. Drag the `.pext` file onto the Playnite window.
5. Restart Playnite.
6. Start Playnite in Fullscreen mode.

If the Dashboard does not show, select it in Fullscreen mode's Settings, in Appearance.

### The Boot shell

**CAUTION:** The Boot shell changes the Windows shell, the startup apps and the Playnite settings of your user account. Read [the Boot shell README](bootshell/XboxSeries.BootShell/README.md) before you install it.

1. Download `XboxSeries.BootShell.exe`.
2. Open PowerShell. Do not run it as administrator.
3. Go to the folder that has the exe.
4. Type `.\XboxSeries.BootShell.exe --install | Out-Host`.
5. Sign out of Windows, then sign in.

To remove the Boot shell, type `.\XboxSeries.BootShell.exe --uninstall | Out-Host`. Then sign out and sign in. This puts back all the settings that `--install` changed.
