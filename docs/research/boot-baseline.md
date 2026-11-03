# Boot baseline on the stock desktop form

For [What is this PC's boot baseline once XFSET and OmniConsole are gone?](https://github.com/snrgarb/xbox-series-theme/issues/109), on the map [Boot this TV PC straight into Playnite at Xbox mode speed](https://github.com/snrgarb/xbox-series-theme/issues/96). Measured on 2026-10-22.

## TL;DR

- **The PC is back on the stock desktop form.** XFSET 0.9.4 and OmniConsole 4.6.2.0 are gone, and so are `DeviceForm`, XFSET's three feature overrides, both of its tasks, OmniConsole's two packages and the `CN=8bit2qubit` certificate.
- **The new baseline is about 37.4 s** (36.9–38.4 s) from POST's start to Start-up's first frame, read from the logs. It was 35.9 s on 2026-10-21 with XFSET in. Start-up's first *visible* frame comes about 2.7 s later than that, because the Series X Boot video opens with 2.85 s of black.
- **Kernel to sign-in is 1.8 s shorter** (10.1 s against 11.9 s). That is the 1.8 s that appeared on 2026-10-15, and it went when XFSET came off. Defender's platform hasn't changed since 10-15.
- **Sign-in to Playnite is 2.5–4 s longer** (13.9–15.3 s against 11.4 s), and on one Restart it took 87 s. This is explorer's startup-app run, which the Boot shell replaces.
- **The device-and-driver phase is CPU-bound**, not stuck waiting on a device. The traced boot spent about 2 s of it in the kernel and about 2 s in Code Integrity. The 1.5 s Winlogon wait is services starting on every core, and it didn't change.

## The baseline

These come from [`boot-baseline/measure-boot.ps1`](boot-baseline/measure-boot.ps1), which needs no admin. The phases are the map's.

| Boot (2026-10-22) | POST | Kernel → sign-in | Sign-in → Playnite | Playnite → Start-up shown | → first frame | Total |
|---|---|---|---|---|---|---|
| 10-21 21:15 Restart, XFSET in (the old baseline) | 4.62 | 11.93 | 11.40 | 7.74 | 0.19 | **35.9** |
| 11:29 Restart | 4.62 | 10.06 | 15.33 | 8.21 | 0.18 | **38.4** |
| 11:32 Restart | 4.70 | 10.14 | 14.32 | 8.09 | 0.17 | **37.4** |
| 11:34 Restart | 4.70 | 10.11 | *87.14* | 8.00 | 0.17 | *110.1* |
| 12:09 power-on (Wake-on-LAN after Turn off) | 4.79 | 10.15 | 13.92 | 7.83 | 0.17 | **36.9** |

All times are in seconds.

- **POST** is `FwPOSTTime`.
- **Kernel → sign-in** runs from `Kernel-General 12` to autologon (`Winlogon/Operational 1`). The kernel start is stamped to the whole second, so this phase is ±0.5 s; the other phases are exact.
- **Sign-in → Playnite** runs to DesktopApp's first `playnite.log` line.
- **Playnite → Start-up shown** runs to the Bundle's `[Start-up timing] Start-up shown`.
- **First frame** is `Start-up window's first frame`.

Some boots are left out:

- **11:09** was the boot-traced Restart with XFSET still in. Its total of 46.6 s includes the trace's own cost and the daily auto-backup.
- **11:22** is also left out. XFSET's uninstall restarted the PC itself while a Moonlight session was streaming.
- **The 11:34 outlier** is explorer, not Playnite. Explorer started its whole startup-app run (Run keys, the Startup folder, RunOnce) at +92.7 s, against +15–16 s on the other boots (`Shell-Core/Operational` 9705–9708).

The markers inside kernel → sign-in, in seconds from kernel start:

| | Boot volume | Wininit | Autologon |
|---|---|---|---|
| 10-21, XFSET in | 1.66–1.70 | 9.73–9.90 | 11.93–12.14 |
| 10-22, stock | 1.67–1.72 | 7.94–7.99 | 10.06–10.15 |

## The first *visible* frame

The [stopwatch](https://github.com/snrgarb/xbox-series-theme/issues/106#issuecomment-6328181613) stops at Start-up's first frame that shows something. The Boot video's own black opening sits between the log's first frame and that.

| Boot video file | Black at the start (ffmpeg `blackdetect`, `pix_th=0.10`) |
|---|---|
| `XboxSeriesX.mp4` (the one this PC plays) | 2.85 s |
| `XboxSeriesS.mp4` | 3.37 s |
| `Xbox.mp4` | 0.53 s, then black again from 0.68 to 1.10 s |

The power-on film agrees. Start-up's black window darkens the screen at 32.8 s into the film, and the Series X logo first shows at about 35.4 s.

## The power-on film (M3)

- **It was filmed on a phone**, from a Turn off to the Boot video. The PC was woken by Wake-on-LAN from Home Assistant, so no button press is on film. The phone started recording about 4 s after firmware start, going by the two clocks.
- **POST never shows on the TV.** The PC reaches the TV through an HDMI switch, and the TV shows "No signal" until about 3.4 s into the film. The first picture is Windows' own boot screen: the board's logo with Windows' spinner, about 6.9–8.7 s in. The TV re-syncs again at 18.2 s.
- **So no film through this switch can time power-on to POST.** It can time the visible end, as above.

## What fills kernel → sign-in (M1, M2)

**M1, Windows' own boot events** (`Diagnostics-Performance/Operational` 100–110):

- **The 1.8 s from 10-15 was boot-start driver initialisation.** `BootDriverInitTime` went from 0.1 s to 1.8 s on the 10-15 and 10-16 boots.
- **The 1.5 s Winlogon wait is Windows' own `UserLogonWaitDuration`.** It has been a steady 1.4–1.5 s on every autologon boot since 2026-09-29 21:44.
- **Windows has written no boot event (100) since 2026-10-16 22:03.** So M1 can't time any boot since, the 10-21 baseline included.
- **The slow-start events (101) are all after sign-in.** Defender's `MsMpEng` took 45–160 s, AMD's User Experience Program 37 s, and Playnite's FullscreenApp 17.5 s.

**M2, one WPR boot trace** (`wpr -boottrace -addboot GeneralProfile -filemode`, the 11:09 Restart with XFSET still in):

- **The trace has no PnP device starts, Winlogon notifications, service starts or shell marker.** So xperf's `boot`, `pnp`, `winlogon`, `services` and `drvdelay` views are all empty. GeneralProfile has CPU samples, disk I/O and process lifetimes, and nothing finer.
- **The device-and-driver phase is CPU-bound.** Session 0's `smss` ran from 3.0 s to the start of its `csrss` at 8.57 s, 5.6 s while the kernel starts drivers and devices. CPU samples over that window total 4.8 s:

  | Module | CPU |
  |---|---|
  | `ntoskrnl.exe` | 2.0 s |
  | `CI.dll` (Code Integrity, checking driver and image signatures) | 2.0 s |
  | `Ntfs.sys` | 0.16 s |
  | Everything else | small; `WdFilter.sys` is 12 ms |

  So the phase is serial kernel and signature work, not waiting on a slow device. Smart App Control is off, and the vulnerable-driver blocklist is on.
- **The Winlogon wait is contention.** From LogonUI's start (11.37 s) to autologon's `userinit` (13.95 s), the 16 cores are about half busy:

  | Process | CPU |
  |---|---|
  | The kernel | 2.9 s |
  | DWM | 1.3 s |
  | The Task Scheduler host | 1.2 s (the one that ran XFSET's `PhysPanelCS`) |
  | Other service hosts | 0.5–1.0 s each |
  | `services.exe` | 0.7 s |
  | LogonUI | 0.6 s |
  | AMD's `atieclxx` | 0.6 s |
  | Defender | 0.5 s |

  It didn't change once XFSET came off: autologon still comes about 2.1 s after Wininit.
- **XFSET's own processes started after the kernel phase.** `PhysPanelCS` started at 11.57 s, after `services.exe`. Its 1.8 s cost was in the device phase, probably from `DeviceForm=46` or its feature flags, but this trace can't show which.
- **A trace that names devices and Winlogon's gate needs more than GeneralProfile.** It needs the `Kernel-PnP`, `Winlogon`, `Services` and `Shell-Core` providers (or `xbootmgr -trace boot`), and symbols to break down the kernel and Code Integrity samples.

## Settings > Gaming on the stock form

Gaming lists XBOX mode, Game Bar, Captures and Game Mode. The XBOX mode page has three switches:

| Switch | Now |
|---|---|
| Enable XBOX mode | Off |
| Show XBOX mode in Task View | On |
| Show accessibility control hints in Task View | Off |

There is no home-app choice and no start-at-sign-in option, as [the Xbox mode research](https://github.com/snrgarb/xbox-series-theme/issues/98#issuecomment-6314298168) found.

## Gotchas met on the way

- **XFSET's silent uninstall hangs.** `msiexec /x {FBD2321F-2851-4E21-9A79-63A209EC3AD7} /qn` runs XFSET's own `/silentdisable` as SYSTEM in session 0. That shows a hidden dialog, "Do you want to remove the log files (including archived logs) created by the application?", and waits on it forever. Once it's answered, the tool restarts the PC itself, logged as "Application: Maintenance (Planned)".
- **This PC idle-sleeps after 60 s on AC.** A restart scheduled more than a minute ahead can land after a long sleep instead. Unattended reboots need sleep set to never while they run.
- **The 2026-10-22 PC clock and the phone's agree to about a second.** That's good enough to place a film's events at the phase level, not finer.
