# This PC boots into Playnite through its own per-user shell, not Xbox mode

Windows' Xbox mode looks made for booting a TV PC straight into a game launcher, but it can't be the route on this PC (#98, #103):

- **Handheld-only settings.** On a desktop, Windows offers *Choose home app* and *Enter Xbox mode on startup* only to gaming handhelds. Getting them means faking the handheld device form (`DeviceForm` 46) and turning on ViVe feature flags.
- **The Guide button.** In Xbox mode Windows owns the Guide button. A short press opens Game Bar, and an app gets a Guide press only through View+Menu, so the pad's Guide button would stop opening the **Guide**.

So the **Boot shell** is our own per-user shell. The HKCU Winlogon `Shell` value names it, and HKLM keeps `explorer.exe`. Setting it needs no admin, and a plain `explorer.exe` the Boot shell starts later still becomes the full shell, which Game Pass games need to launch (#99, #100). *Enable Xbox mode* is turned off on this PC, so nothing can switch it into a mode where Windows takes the Guide button.

## Considered options

- **Xbox mode with a small packaged launcher for Playnite as its home app**: the home-app package itself works in Developer Mode, but it needs the handheld fake and gives the Guide button to Windows.
- **The machine-wide (HKLM) `Shell` value**: needs admin to set, and explorer becomes the full shell only while HKLM names `explorer.exe`, so Game Pass games would stop launching.
