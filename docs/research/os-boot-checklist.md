# POST and kernel-to-sign-in on the TV PC: where the 16 s go, and the checklist

Research for [#102](https://github.com/snrgarb/xbox-series-theme/issues/102) (part of #96).

**Question.** Why do this PC's firmware POST (4.6 s) and Windows' kernel start to sign-in (~12 s) take as long as they do, and which one-time changes cut them?

**Date researched:** 2026-10-21, read-only and without admin. Nothing was changed, rebooted or uninstalled.

**Sources:** this PC's System, Winlogon/Operational, Kernel-Boot/Operational, Kernel-PnP (Configuration, Device Management, Driver Watchdog), User Profile Service and Shell-Core logs, which cover every boot since Windows was installed on 2026-09-29. Also its registry, WMI, `powercfg` and PnP device properties. Primary references: Microsoft Learn (WPR, the Windows Performance Toolkit and the On/Off assessments, fast startup, autologon, Wake on LAN), Microsoft's archived boot-and-logon optimisation series (Mark Renoden, 2015), Gigabyte's B550I AORUS PRO AX manual (revisions 1101 and 1303), and EDK2's definition of the ACPI firmware boot performance record.

---

## TL;DR

1. **Where the 16.6 s go** on the 21:15 restart:

   | Phase | Time |
   |---|---|
   | POST | 4.6 s |
   | Boot manager and winload | 0.5 s |
   | Kernel start to boot volume | 1.7 s |
   | **Devices and drivers starting, until Wininit** | **8.0 s** |
   | Services, LSASS and Winlogon starting | 0.7 s |
   | **Winlogon waiting before autologon** | **1.5 s** |
   | Autologon, then profile load | 0.02 s + 0.05 s |

   Group Policy, the profile and autologon cost almost nothing.
2. **The kernel phase grew with hardware and drivers, not with Windows.** On install day, Windows got from the boot volume to Wininit in 2.0 s. Today it takes 8.0 s. Microsoft says this phase "lasts until all PnP devices have been started", and that graphics drivers are the usual culprit. The logs tie part of the growth to specific devices:
   - The Xbox Wireless Adapter's driver took 4.7 s to start on every boot until the adapter was unplugged on 2026-10-09.
   - A ghost PS/2 keyboard has failed on every boot since a UEFI change on 2026-10-06. It took 10.3 s on the first of them.
   - Another 1.8 s appeared between 2026-10-13 and 10-15, and nothing in the readable logs names its cause.
3. **POST has already been cut**, from 8.3–8.9 s to 4.6 s, by a UEFI change on 2026-10-06. A power-on after Shut down posts in the same 4.7 s as a restart, so the restart numbers stand for cold boots. What's left in UEFI is small. It has to be measured one setting at a time, and `FwPOSTTime` makes that easy without admin.
4. **Fast Startup is the one change that could remove most of the 12 s** (an estimated 7–9 s). It resumes the kernel, drivers and services from the hiberfile instead of starting them again. Users are still logged off, so autologon still runs and Playnite still cold-starts. But it isn't a full cold boot, and Restart still does a full one, so the map has to decide whether its stopwatch counts it. Windows ships with Fast Startup on, so an Xbox mode PC that boots fast from Shut down is probably using it.
5. **Measure first, with admin.** Read Diagnostics-Performance events 100–110 (no reboot needed) and capture one WPR boot trace. Together they name the slow drivers and devices inside the 8 s and explain the 1.5 s Winlogon wait. The logs readable without admin can't do either.

---

## This PC

| | |
|---|---|
| Board | Gigabyte B550I AORUS PRO AX, PCB rev. 1.3, Mini-ITX (`Win32_BaseBoard`). Rev. 1.3 is the one with the onboard Realtek Wi-Fi 6E RTL8852CE. |
| Firmware | AMI, BIOS **FHa**, released 2026-04-14 (`Win32_BIOS`) |
| CPU, RAM | Ryzen 7 5800X3D; 2 × 16 GB Corsair CMK32GX4M2D3600C18 running at 3600 (XMP) |
| GPU | RX 9070 XT, AMD driver 32.0.32015.7029, installed 2026-10-21 16:39 |
| Disks | Crucial P3 Plus 1 TB and P3 2 TB, both NVMe. **No SATA drives.** |
| OS | Windows 11 Pro 26300 (26H2 Insider), installed 2026-09-29 09:20 |
| Sign-in | Autologon of local account `garbonsole\gabe`. The password is an LSA secret, not `DefaultPassword` (`AutoAdminLogon=1`). |
| Fast Startup | Off (`HiberbootEnabled=0`, no policy behind it). Hibernate is available and `hiberfil.sys` is 13.7 GB. |
| Power button | Shut down. Sleep is S3 with hybrid sleep on. |
| Security | VBS, HVCI and the hypervisor are off. Secure Boot is on, so CSM is off (the manual says Secure Boot is only configurable with CSM disabled). BitLocker device encryption is "waiting for activation" (clear key, no PIN). |
| Boot config | One boot entry. `BootmgrUserInputTime=0`. `SystemStartOptions` = `NOEXECUTE=OPTIN FVEBOOT=… NOVGA`, so no debugger. |

---

## Where the time goes

### The 21:15 restart, marker by marker

All of these are readable without admin. "Since kernel start" counts from `Kernel-General 12`, which stamps the start with the real-time clock to the whole second, so it is ±0.5 s. The differences between markers are exact.

| Clock | Marker (source) | Since kernel start |
|---|---|---|
| — | Firmware starts the boot manager at **4,620 ms** (`FwPOSTTime`; `Kernel-Boot 30` StartOSImageStart). It began loading it at 4,573 ms. | — |
| — | Winload calls ExitBootServices at 5,127 ms, so the boot manager plus winload took 0.5 s (`Kernel-Boot 30`) | — |
| 21:15:14.5 | Kernel starts (`Kernel-General 12`). Boot type 0x0, a full boot (`Kernel-Boot 27`). 0 ms waiting at the boot menu (`Kernel-Boot 32`). | 0 |
| 21:15:16.16 | Filter Manager loads FileInfo, Wof and WdFilter, and C: mounts healthy at 16.44 (`FilterManager 6`, `Ntfs 98`) | 1.7 |
| 21:15:18.83 | Processor driver starts (`Kernel-Processor-Power 55`) | 4.3 |
| 21:15:19.02 | **PS/2 keyboard `ACPI\PNP0303\0` reported failing and removed** (`Kernel-PnP/Device Management 1011`) | 4.5 |
| 21:15:22.03 | Bluetooth radio up (`BTHUSB 18`) | 7.5 |
| 21:15:24.23 | **Wininit runs**, and LSASS starts at 24.51 (`Wininit 12, 19–25`) | 9.7 |
| 21:15:24.85 | Winlogon's first subscriber notification (`Winlogon/Operational 811`). The event log service starts at 24.95 and reports "system uptime 10 seconds" (`EventLog 6005/6013`). | 10.4 |
| 21:15:26.43 | **Autologon: "Authentication started"**, and stopped 24 ms later (`Winlogon/Operational 1/2`) | 11.9 |
| 21:15:26.52 | Logon notification (`Winlogon 7001`). `ntuser.dat` loads from 26.53 to 26.56 (`User Profile Service 1/5/2`). | 12.0 |
| 21:15:26.98 | Shell logon tasks start (`Shell-Core 62170`). This is the explorer phase, outside this ticket. | 12.5 |

In the Winlogon phase:
- Each Winlogon subscriber, GPClient included, handled its notification in 0–47 ms. **Group Policy doesn't wait here.**
- The ~1.5 s between Winlogon's first notification and autologon has no event in any readable log. Only a boot trace can explain it.

### How the kernel phase grew

These are the same markers on selected boots since install, in seconds from kernel start. "What changed" comes from `Kernel-PnP/Configuration 400`, Windows Update `19`, the uninstall keys and device arrival and removal dates.

| Boot | What changed before it | Volume | Proc. driver | Bluetooth | **Wininit** | **Autologon** |
|---|---|---|---|---|---|---|
| 09-29 09:24 | Fresh install, Microsoft Basic Display | 1.6 | 2.0 | 3.3 | **3.6** | **4.0** |
| 09-29 09:36 | AMD GPU driver (09:25), Xbox Wireless Adapter (09:35), AMD PSP, SteelSeries | 1.6 | 2.3 | 3.4 | 6.0 | 6.8 |
| 09-29 21:42 | Realtek LAN (16:19), XFSET (17:00), Realtek audio (17:46), autologon set up | 1.6 | 2.7 | 5.4 | 10.7 | 12.7 |
| 10-07 12:29 | PawnIO (10-06). The UEFI change on 10-06 brought in the PS/2 ghost. | 1.7 | 2.6 | 6.0 | 9.3 | 11.4 |
| 10-09 15:34 | **Xbox Wireless Adapter unplugged** (10-09 14:31) | 1.7 | 2.4 | 5.6 | 8.1 | 10.1 |
| 10-13 16:05 | SudoVDA and Vibeshine virtual gamepad, Logitech receiver, Sunshine VDD | 1.7 | 2.5 | 5.2 | 7.7 | 9.7 |
| 10-15 21:58 | Defender platform 4.18.26090.9 (10:49). Arctis Nova 7 dongle gained a new HID interface `MI_05` (14:35). | 1.7 | **4.2** | 8.1 | 9.4 | 10.5 |
| 10-21 21:15 | AMD driver 32.0.32015.7029 (16:39) | 1.7 | **4.3** | 7.5 | 9.7 | 11.9 |

Back-to-back restarts with nothing changed vary by about ±0.3 s; for example, five on 2026-10-07 had Wininit between 10.3 and 10.7 s. One outlier, 2026-10-21 15:43, reached Wininit at 13.1 s.

What the history says:

- **Kernel start to boot volume has been 1.6–1.7 s on every boot since install.** Winload, the boot-start drivers and the boot volume aren't where the time went. The only third-party boot-start driver is `amdpsp`.
- **Microsoft's boot phases put the growth in session initialisation.** Microsoft's own boot-optimisation series says the kernel phase "initializes BOOT_START drivers … loads and initializes SYSTEM_START drivers". It says session initialisation does "video initialization" and "loads and starts all remaining drivers … lasts until all PnP devices have been started", and that "the most common issue during this phase is slow driver initialization with graphics drivers being the typical culprit" (Renoden, parts 11 and 12). The markers grew between the boot volume and Wininit, which is exactly that phase.
- **The Xbox Wireless Adapter cost real time.** On the 2026-10-06 and 10-07 boots, PnP's Driver Watchdog logged its driver taking 4.7 s to start (`Kernel-PnP/Driver Watchdog 902/903`: `USB\VID_045E&PID_02E6`, service `mt7612US`, "Total run time in milliseconds: 4702"). It was removed on 2026-10-09 at 14:31, and Wininit came 1.2–2.5 s earlier from the next boot on. The controller now pairs over Bluetooth LE (`BTHLEDevice … VID&02045e_PID&0b13`, 2026-10-18), so the adapter isn't needed.
- **The PS/2 keyboard is a ghost.** This board has no PS/2 port: neither manual revision lists one on the back panel.
  - `ACPI\PNP0303\0` first appeared on 2026-10-06 at 23:11, on the first boot after a 36-minute stay in the UEFI menu. That visit is the one that cut POST.
  - On that boot, `i8042prt`'s start of the device ran for 10.3 s (`Driver Watchdog 902/903`), and Wininit came at 23.8 s.
  - Since then it fails on every boot, logging problem code 24 and `Device Management 1011` "surprise removed as it was reported to be failing".
  - It's not clear that it still costs time. Two boots right after a UEFI save (2026-10-16 22:01 and 10-21 15:47) logged no failure, and they weren't faster.
- **1.8 s appeared between 2026-10-13 and 10-15**, before the processor driver starts: 2.5 s became 4.2–4.5 s. So it is early, kernel-phase device or driver work, and no boot since has gone back. The two changes in that window were the Defender platform update and the Arctis Nova 7 dongle's new interface. Unplugging the dongle for one boot tests the second without admin.
- **The AMD graphics stack is the prime suspect for the Bluetooth-to-Wininit 2.5 s.** That gap was 0.3 s before the AMD driver and adapter arrived. It became 2.6 s at 09:36 on install day and stayed at 2.2–2.5 s after the adapter left. A trace can confirm it.
- **The Winlogon wait grew from 0.1–0.3 s to 1.5 s**, between 09:57 and 21:42 on install day. Those early boots were setup-time logons, so the comparison is weak. The trace's Winlogon region will show what Winlogon is waiting for.

---

## Firmware POST (4.6 s)

**What `FwPOSTTime` measures.** Windows logs the firmware's boot performance record in `Kernel-Boot 30` and copies StartOSImageStart into `FwPOSTTime`. The record's fields, as defined in EDK2's ACPI headers, are:
- *ResetEnd*: "timer value logged at the beginning of firmware image execution". It is 0 here.
- *OsLoaderLoadImageStart*: "just prior to loading the OS boot loader".
- *OsLoaderStartImageStart*: "just prior to launching the previously loaded OS boot loader image".
- *ExitBootServicesEntry/Exit*.

So the 4.6 s runs from the firmware starting to execute to the boot manager starting. **It doesn't include the power supply's power-good delay or the AMD PSP's work before reset is released.** A stopwatch from the power button sees more than `FwPOSTTime`, and only a phone video can measure that part.

**History** (`Kernel-Boot 30`, every boot since install):
- **8.25–8.93 s** until 2026-10-06.
- **12.5–14.0 s** on the first boot after each visit to the UEFI menu: 2026-10-06 23:04, 10-07 10:23, 10-16 22:01 and 10-21 15:47. That fits memory retraining after settings change.
- **4.57–4.80 s** on every other boot since 2026-10-06 23:11, apart from two at 5.5 s.
- **Cold boots post just as fast.** The two boots after Shut down with the power off (2026-10-07 18:53 after 114 s, and 10-11 12:53 after 21 h) posted in 4.67 and 4.68 s. Memory isn't retrained on each boot, so AMD's Memory Context Restore has nothing to save here.

The 2026-10-06 visit cut 3.8 s. Gigabyte ships **Fast Boot** as Disabled, so it was probably turned on then, but only the UEFI menu shows which settings are in place now.

**UEFI options that touch POST**, from the manual's Boot and Settings › IO Ports chapters (rev. 1101):

| Option (menu) | What the manual says | Here |
|---|---|---|
| **Fast Boot** (Boot) | "Enables or disables Fast Boot to shorten the OS boot process. Ultra Fast provides the fastest bootup speed. (Default: Disabled)" | Probably Enabled since 10-06. Ultra Fast is the next step. |
| USB Support (Boot, with Fast Boot) | Disabled, Full Initial (default) or Partial Initial. "configurable only when Fast Boot is set to Enabled. This function is disabled when Fast Boot is set to Ultra Fast." | With Ultra Fast, no USB keyboard works during POST. The UEFI menu is still reachable with `shutdown /r /fw` or Settings › Recovery › Advanced startup. The user already does this (`shutdown.exe` restarts on 10-16 and 10-21). |
| SATA Support (Boot, with Fast Boot) | "Last Boot SATA Devices Only" (default) or "All SATA Devices" | Leave it |
| NetWork Stack Driver Support (Boot, with Fast Boot) | "Disabled Disables booting from the network. (Default)" | Check it is Disabled |
| **Full Screen LOGO Show** (Boot) | "Disabled skips the GIGABYTE Logo when the system starts up. (Default: Enabled)" | Disable it. It also removes the logo from the TV. |
| CSM Support (Boot) | Default Enabled | Already Disabled, since Secure Boot is on |
| Boot Option Priorities (Boot) | Overall boot order | Windows Boot Manager first, and disable other entries. Firmware boot entries can only be listed with admin (`bcdedit /enum firmware`). |
| Chipset SATA Port Enable (Settings › IO Ports › SATA Configuration) | "Enables or disables the integrated SATA controllers. (Default: Enabled)" | No SATA drives here, so it can be Disabled |
| Port 60/64 Emulation and Legacy USB Support (Settings › IO Ports › USB Configuration) | Port 60/64 Emulation "should be enabled for full legacy support for USB keyboards/mice in MS-DOS" (default Disabled). Legacy USB Support default Enabled. | Prime suspect for the PS/2 ghost. Check Port 60/64 Emulation is Disabled. |
| HD Audio Controller (Settings › IO Ports) | Onboard Realtek ALC1220 on or off (default Enabled) | Can be Disabled if audio only goes over HDMI to the TV and the USB headset |
| ErP (Settings) | Cuts power in S5 | Don't change it. It can stop USB wake devices like the Pico Wake Bridge. |

The manual gives no time for any of these, and no primary source does for this board. Each has to be measured with `FwPOSTTime` (see *Measuring without admin*).

---

## Kernel to sign-in (~12 s)

**The phases**, as Microsoft's tools name them (Renoden part 3; WPT `xperf -a boot`):
- *PreSMSS*: OS loader and kernel initialisation, including boot-start and system-start drivers.
- *SMSSInit*: session initialisation, the remaining drivers and every PnP device start, up to Winlogon.
- *WinlogonInit*: services, machine policy, logon and profile.
- *ExplorerInit*.

On this PC, kernel start to Wininit (9.7 s) is PreSMSS plus nearly all of SMSSInit. Wininit to autologon (2.2 s) is the start of WinlogonInit.

**What's on the path**, read without admin:

- **Third-party drivers.**
  - Running: AMD's kernel display driver, Crash Defender (`amdfendr`, `amdfendrmgr`), `AMDXE`, `AMDSAFD`, `AtiHDAudioService`, the GPIO drivers, `AMDPCIDev`, `amdpsp` (the only third-party boot-start driver) and `AMDRyzenMasterDriverV32` (auto-start).
  - Also running: Realtek LAN (`rt25cx21`), Realtek audio (`IntcAzAudAddService`), Realtek Bluetooth filter (`RtkBtFilter`), `PawnIO` and `SteamStreamingSpeakers`.
  - There are no third-party class filter drivers.
- **Virtual devices.**
  - Two virtual display drivers: SudoMaker VDA from Apollo and Sunshine VDD from Vibepollo. Both streaming hosts are installed, along with a Vibeshine virtual gamepad.
  - The Xbox virtual disk `Xvdd` (Game Pass), RDP's indirect display, and Steam Streaming Speakers.
  - The display adapters are UMDF drivers, which start once the Service Control Manager is up (`DriverFrameworks-UserMode 10118`). So they sit after Wininit and matter more to sign-in and explorer than to the kernel phase.
- **Unused hardware.**
  - The onboard Wi-Fi (`PCI\VEN_10EC&DEV_C852`, RTL8852CE) has no driver (problem code 28), so nothing starts for it. Its Bluetooth half (`USB\VID_0BDA&PID_0852`) carries the Xbox controller and has to stay.
  - The ghost PS/2 keyboard (above).
- **USB devices at boot:**
  - Logitech Unifying receiver
  - Rii mini keyboard (`VID_1997`)
  - SteelSeries Arctis Nova 7 dongle
  - "Pico Wake Bridge" (`VID_2E8A`)
  - Gigabyte's ITE RGB controller (`VID_048D&PID_5702`, on the board)
  - two Genesys hubs

  Unplugging the external ones one at a time and comparing the markers costs nothing and needs no admin.
- **Services.**
  - 70 are Automatic and 10 of them are third-party: AMD Crash Defender, AMD External Events, AUEP (delayed), Apollo, GameInput Redist, Realtek audio, Realtek Bluetooth, Xbox Service, `sshd` and `ssh-agent`.
  - Automatic services start in the Winlogon phase, in parallel with logon (Renoden part 13).
  - Logon only waits for them when Group Policy runs synchronously ("Always wait for the network", folder redirection, roaming profiles and so on). That doesn't happen on this workgroup PC, where GPClient takes 1 ms.
  - So trimming services won't shorten kernel-to-sign-in. It lowers CPU and disk contention for explorer and Playnite afterwards, which belongs to the later phases.
- **Already as good as it gets:**
  - VBS, HVCI and the hypervisor are off.
  - Boot menu wait is 0 ms, with one boot entry.
  - BitLocker has no PIN. Microsoft's Boot Manager metric only calls out PIN entry.
  - Group Policy takes 1 ms, the profile load 47 ms and autologon 24 ms.
  - `BootExecute` is the default `autocheck autochk *`.
  - The only pending rename is one Gaming Services DLL.

### XFSET and OmniConsole

The #98 research found XFSET 0.9.4 still in place, with two boot-triggered SYSTEM tasks:
- **XFSET-SetPanelDimensions** rewrites `DeviceForm` on every boot.
- **XFSET-SimulateTouchService** injects touch into every session.

XFSET also sets `DeviceForm=46` (handheld) and enables ViVe feature IDs 52580392, 50902630 and 59765208. What this PC shows:

- **The task cost can't be read without admin.**
  - `Microsoft-Windows-TaskScheduler/Operational` is disabled on this PC (`IsEnabled: False`), so there is no task history.
  - The SYSTEM tasks aren't visible to a non-admin: the `TaskCache` registry and `C:\Windows\System32\Tasks` both deny access.
  - Nothing they write shows up in the readable logs during the boot window. The only PowerShell-host events at boot are Playnite's, at 21:15:44.
- **They can't be in POST or kernel-to-Wininit.**
  - A boot trigger fires when the Task Scheduler service starts, which happens after Wininit and the Service Control Manager (+10.5 s here).
  - At most they overlap the 1.5 s Winlogon wait and the explorer phase.
  - XFSET's own log (`C:\Program Files\8bit2qubit\Xbox FullScreen Experience Tool\XboxFullScreenExperienceTool.log`) shows it was enabled on 2026-09-29 at 17:00:53 and restarted. That boot reached the event log service 0.5 s later than the one before, but Realtek's LAN driver arrived in the same window, so the two can't be separated.
  - Its kernel driver `PhysPanelDrv` is *not* installed (`Drv=False` in its log, and there is no service).
- **OmniConsole 4.6.2.0** is two developer-signed packages (`b5fbce6b-…`, `4fa8e044-…`, publisher `CN=8bit2qubit`). Neither manifest declares a `windows.startupTask`, so neither launches itself at sign-in. It only runs if the full screen experience opens it as its home app.
- **Uninstalling XFSET.** Per its README, XFSET's MSI uninstall reverts `DeviceForm`, the feature flags and the tasks. Its `DeviceForm.bak` holds `DELETE_ON_RESTORE`, meaning the value didn't exist before, so the uninstall deletes it.

  Removing XFSET also brings the PC in line with the map, which rules out faking a handheld and ViVe flags.

---

## Fast Startup

**What it does.** From Microsoft Learn:
- "Fast startup logs off user sessions, but the contents of kernel (session 0) are written to hard disk."
- On the next power-on "a fast startup simply loads the hibernation file (Hiberfil.sys) into memory", instead of the kernel configuring core functions, enumerating devices and loading drivers.
- "During user-initiated shutdowns, the kernel, drivers, and services are preserved and restored, not just restarted."
- Its phases are BIOS Initialization, Hiberfile Read, Resume Devices, WinLogon Resume and Explorer Initialization.

**What it would save here.** It removes almost all of the 9.7 s from kernel start to Wininit and the services start-up. In their place come:
- a hiberfile read of the kernel session only. Estimated at 0.5–1 s; this PC wrote its last hiberfile at 1,283 MB/s (`HiberWriteRate`).
- device resume, which took 1.26 s on this PC's last resume from sleep (`DeviceResumeTime`).
- Winlogon resume and autologon.

**Estimate: kernel-to-sign-in drops from ~12 s to ~3–5 s, a saving of about 7–9 s.** POST stays the same, because the firmware runs in full when the PC powers on from S4. The bad devices above also drop out of the path, since resumed devices aren't enumerated again. The WPR `FastStartup` scenario measures the real figure (see below).

**What it doesn't change.** Users are logged off on shutdown, so autologon runs, the user's shell and startup apps start fresh, and Playnite cold-starts. This matches the ticket's note.

**Caveats:**
- **It isn't a full cold boot.** `Kernel-Boot 27` would report a hybrid boot type instead of 0x0. The map's stopwatch says "cold boot only", so the map has to decide whether a power-on after Shut down with Fast Startup counts. Windows' default shutdown is hybrid (Microsoft's Wake on LAN article: "In Windows 10, the default shutdown behavior puts the system into the hybrid shutdown (also known as Fast Startup) state (S4)"). So a stock Xbox mode PC booting from Shut down is most likely doing this.
- **Restart always does a full boot** ("the restart process in Windows continues to perform a full boot cycle"), and so does any update or driver install that needs a restart. The full-boot items in this checklist still matter for those boots.
- **Services and session 0 are resumed, not restarted.** So a SYSTEM task with an *At startup* trigger, or a service's start code, won't run again after a Fast Startup boot. This is an inference from the sentence above. Anything the map's tool does should hang off sign-in, which the map already intends.
- **Wake on LAN from off stops working.** Under hybrid shutdown, "network adapters are explicitly not armed for WOL". Wake from sleep isn't affected.
- Drivers must treat a fast startup as a cold start. Most do, and this PC's drivers resume from S3 sleep many times a day without trouble (`Kernel-Power 42/107`).

**How to turn it on (admin):**
- Control Panel › Power Options › *Choose what the power buttons do* › *Change settings that are currently unavailable* › *Turn on fast startup*.
- Or set `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power\HiberbootEnabled=1`.

Hibernation is already on. Turn it on, Shut down once (that shutdown writes the first image), then power on. Check that `Kernel-Boot 27` no longer says 0x0. Start › Shut down does a hybrid shutdown; the power button set to Shut down should too, but check it with `Kernel-Boot 27`.

---

## Boot traces: what they'd show, and what needs admin

**Readable without admin** (everything above):
- `Kernel-Boot 27/30/32`
- `Kernel-General 12/13`
- the Wininit, EventLog 6005 and Winlogon 7001 events in System
- `Winlogon/Operational` (autologon timing and subscriber timings)
- `Kernel-PnP/Configuration`, `Device Management` and `Driver Watchdog` (slow device starts over 3 s, failing devices)
- `User Profile Service/Operational`
- the Power registry values (`FwPOSTTime`)
- WMI, PnP device properties and `powercfg /a`

**Needs admin:**
- `Microsoft-Windows-Diagnostics-Performance/Operational`. This is Windows' own boot timing: event 100 gives the boot duration, split into main path and post-boot. Events 101, 102, 103 and 109 name the app, driver, service or device that slowed boot. The log refused access here ("unauthorized operation"), but at 21:15:25 `Diagnosis-DPS 100` recorded that its `diagperf.dll` module "detected a problem" on this boot. Microsoft publishes no field reference for these events.
- `GroupPolicy/Operational`.
- Security 4688 process creation, which also needs auditing turned on.
- `TaskScheduler/Operational`, which is disabled and needs admin to turn on.
- The scheduled task definitions.
- `bcdedit /enum firmware`.
- **Capturing any boot trace.**

**What a boot trace shows.** It covers the phases (PreSMSS, SMSSInit, WinlogonInit, ExplorerInit), each boot-start and system-start driver's load time, and PnP device start durations. It also shows services starting, each Winlogon subscriber, Group Policy, process lifetimes (including XFSET's task processes) and CPU and disk use (Renoden parts 3, 11–13; WPT `boot` action). That is enough to name what fills the 8 s and the 1.5 s Winlogon wait.

**Steps for the user, in this order** (admin PowerShell or Terminal):

1. **Read Windows' own verdict.** No reboot is needed:
   `Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-Diagnostics-Performance/Operational'; Id=100,101,102,103,106,109} -MaxEvents 40 | Format-List TimeCreated,Id,Message`
2. **Capture one cold-boot trace with WPR**, which ships with Windows (`C:\Windows\System32\wpr.exe`; no ADK needed to record):
   1. `wpr -boottrace -addboot GeneralProfile -filemode`
   2. Use Start › Shut down, wait, then press the power button.
   3. Let it reach Playnite and wait about a minute.
   4. `wpr -boottrace -stopboot C:\Users\gabe\boot.etl`

   This is the same recipe Microsoft gives Insiders. `-addboot` "does not immediately start the trace. After reboot, the autologger will be started by the operating system."
3. **Analyse it.**
   - Install Windows Performance Analyzer (Microsoft Store, or the ADK's Windows Performance Toolkit).
   - Open `boot.etl` and add the *FullBoot.Boot.Regions* Regions of Interest file. Or use `xperf -i boot.etl -o summary.xml -a boot` (ADK) for the phase table and the slow-driver list.
   - The ETL can be handed back to an agent to read.
4. **Optional: measure Fast Startup before deciding.** Turn it on, then run `wpr -start GeneralProfile -onoffscenario FastStartup -onoffresultspath C:\traces -numiterations 1`, and compare with step 2. WPR's on/off scenarios restart or shut down the PC themselves.
5. **Legacy alternative:** the ADK's `xbootmgr -trace boot …` does the same as step 2. WPR replaces it.

---

## The checklist

Savings marked *measured* come from this PC's logs. *Estimate* means reasoned from the sources above. *Unknown* means only the measurements M1–M3 can tell. Run M1–M3 first; they change nothing.

| # | Step | Expected saving | Risk | Admin | Reboot | UEFI |
|---|---|---|---|---|---|---|
| M1 | Read Diagnostics-Performance 100–110 (step 1 above) | none; names the culprits | none | yes | no | no |
| M2 | One WPR cold-boot trace (step 2 above) | none; names what fills the 8 s and the 1.5 s Winlogon wait | none; a large ETL | yes | yes (one cold boot) | no |
| M3 | Film the TV from the power button to the first picture | none; finds the power-on time before `FwPOSTTime` starts | none | no | — | no |
| 1 | **Keep the Xbox Wireless Adapter unplugged.** Already done on 10-09; the controller now uses Bluetooth LE. | **1.2–2.5 s, measured** (driver start was 4.7 s) | none | no | — | no |
| 2 | **Uninstall XFSET** with its MSI (reverts `DeviceForm`, the ViVe flags and both tasks). **Remove OmniConsole**'s two packages and its `CN=8bit2qubit` certificate from `LocalMachine\TrustedPeople`. **Then re-measure the baseline.** | ≈0 s for POST and kernel-to-sign-in (the tasks start after Wininit, +10.5 s); unknown and probably small after sign-in | low. The handheld form, the full screen experience flags and touch injection go, which the map rules out anyway. Coordinate with #98. | yes | yes | no |
| 3 | **Remove the ghost PS/2 keyboard.** In UEFI, set Port 60/64 Emulation to Disabled (and check Legacy USB Support). Or disable "Standard PS/2 Keyboard" `ACPI\PNP0303\0` in Device Manager. Confirm with `Kernel-PnP/Device Management 1011`. | 0–2 s, unproven. It ran 10.3 s once; boots without the failure weren't faster. | low (the board has no PS/2 port) | yes (Device Manager) | yes | either way |
| 4 | **Find the 1.8 s added between 10-13 and 10-15.** First boot once with the Arctis Nova 7 dongle unplugged and compare the "proc. driver" marker. Otherwise read M2. | up to 1.8 s (measured growth) | none to test | no to test | yes | no |
| 5 | **Turn on Fast Startup** — *if the map's stopwatch accepts a hybrid boot* | **≈7–9 s, estimate** (kernel-to-sign-in ~12 s → ~3–5 s) | medium: not a full cold boot; Restart and updates still boot in full; services and boot triggers resume rather than restart; no Wake on LAN from off | yes | first Shut down → power-on | no |
| 6 | UEFI: **Fast Boot → Ultra Fast** (if it is Enabled now) | unknown; measure with `FwPOSTTime` | low. No USB during POST, so reach UEFI with `shutdown /r /fw`. The first boot after saving posts in ~13 s while memory retrains. | no | yes | yes |
| 7 | UEFI: **Full Screen LOGO Show → Disabled** | unknown, small | none (also hides the logo on the TV) | no | yes | yes |
| 8 | UEFI: **Chipset SATA Port Enable → Disabled** (no SATA drives) | unknown, small | low (SATA ports dead until re-enabled) | no | yes | yes |
| 9 | UEFI: check **Network Stack / NetWork Stack Driver Support = Disabled** and **Boot Option #1 = Windows Boot Manager**, with other entries disabled | 0 if already so; seconds if PXE is on | none | no | yes | yes |
| 10 | **Onboard audio off** if sound only goes over HDMI and the headset: UEFI HD Audio Controller → Disabled. This removes the Realtek device, its driver, two services and a Run-key app. | unknown; mostly after sign-in | low (analogue jacks stop working) | no | yes | yes |
| 11 | **Slim the AMD stack**: install Adrenalin's driver-only option, and drop Ryzen Master's auto-start driver and AUEP | unknown; do it only if M1/M2 blame AMD (prime suspect for ~2.2 s) | medium (Adrenalin features lost) | yes | yes | no |
| 12 | **Keep one streaming host** (Apollo *or* Vibepollo) and its virtual display, or none if Moonlight is done | unknown; mostly sign-in and display set-up | low (Moonlight streaming) | yes | yes | no |
| — | No action needed: VBS and HVCI (off), boot menu (0 ms, one entry), memory training (none at boot), CSM (off), Group Policy (1 ms), profile (47 ms), autologon (24 ms), BitLocker (no PIN), the driverless Wi-Fi | 0 | — | — | — | — |

**Order of play.**
1. Do M1–M3.
2. Do item 2, re-measure, and record that as the new baseline.
3. Then items 3 and 4.
4. Make the UEFI changes one at a time, each with one cold boot, reading `FwPOSTTime`.
5. Decide Fast Startup last, with the map's stopwatch rule in hand. It hides most of the kernel-phase work, so measure the full-boot items before turning it on.

---

## Measuring without admin

This prints the markers used above for the last N boots. Run it after each change and a cold boot. `type` is `Kernel-Boot 27`'s boot type (0x0 on every boot here). `POST` is `FwPOSTTime`. The rest are seconds from kernel start (±0.5 s; the differences between them are exact).

```powershell
param([int]$Boots = 5)
$starts = Get-WinEvent -FilterHashtable @{LogName='System'; ProviderName='Microsoft-Windows-Kernel-General'; Id=12} -MaxEvents $Boots
foreach ($s in ($starts | Sort-Object TimeCreated)) {
  $k0 = [datetime]::Parse((([xml]$s.ToXml()).Event.EventData.Data | Where-Object Name -eq 'StartTime').'#text').ToLocalTime()
  $w  = @{ StartTime = $s.TimeCreated.AddSeconds(-1); EndTime = $s.TimeCreated.AddSeconds(90) }
  function First($log, $prov, $id) {
    $f = @{ LogName = $log } + $w; if ($prov) { $f.ProviderName = $prov }; if ($id) { $f.Id = $id }
    (Get-WinEvent -FilterHashtable $f -ErrorAction SilentlyContinue | Sort-Object TimeCreated | Select-Object -First 1)
  }
  function Sec($e) { if ($e) { '{0,5:N1}' -f ($e.TimeCreated - $k0).TotalSeconds } else { '  -  ' } }
  $fw   = First 'System' 'Microsoft-Windows-Kernel-Boot' 30
  $post = if ($fw) { (([xml]$fw.ToXml()).Event.EventData.Data | Where-Object Name -eq 'StartOSImageStart').'#text' } else { '-' }
  $type = First 'System' 'Microsoft-Windows-Kernel-Boot' 27
  $btype = if ($type) { (([xml]$type.ToXml()).Event.EventData.Data)[0].'#text' } else { '-' }
  '{0:yyyy-MM-dd HH:mm:ss} type={1} POST={2,5}ms | from kernel start: volume={3} wininit={4} eventlog={5} autologon={6} logon={7}' -f $k0, $btype, $post,
    (Sec (First 'System' 'Microsoft-Windows-FilterManager' $null)),
    (Sec (First 'System' 'Microsoft-Windows-Wininit' $null)),
    (Sec (First 'System' 'EventLog' 6005)),
    (Sec (First 'Microsoft-Windows-Winlogon/Operational' $null 1)),
    (Sec (First 'System' 'Microsoft-Windows-Winlogon' 7001))
}
```

Output on 2026-10-21:

```
2026-10-21 21:06:32 type=0 POST= 4627ms | from kernel start: volume=  1.7 wininit=  9.9 eventlog= 10.6 autologon= 12.1 logon= 12.3
2026-10-21 21:15:14 type=0 POST= 4620ms | from kernel start: volume=  1.7 wininit=  9.7 eventlog= 10.5 autologon= 11.9 logon= 12.0
```

To see failing or slow devices too, add `Get-WinEvent -LogName 'Microsoft-Windows-Kernel-PnP/Driver Watchdog'` and the `1011` events in `Microsoft-Windows-Kernel-PnP/Device Management`.

---

## Sources

- Microsoft Learn, [WPR command-line options](https://learn.microsoft.com/windows-hardware/test/wpt/wpr-command-line-options): `-boottrace -addboot/-stopboot`, `-onoffscenario Boot|FastStartup|…`.
- Microsoft Learn, [Windows Insider: feedback categories and diagnostics](https://learn.microsoft.com/windows-insider/feedback-categories#include-the-diagnostics-needed-to-investigate): the `wpr -boottrace -addboot GeneralProfile -filemode` recipe.
- Microsoft Learn, [Recording on/off transitions](https://learn.microsoft.com/windows-hardware/test/wpt/recording-onoff-transitions) and [xperf `boot` action](https://learn.microsoft.com/windows-hardware/test/wpt/boot).
- Microsoft Learn, [Optimizing performance and responsiveness: fast startup behavior](https://learn.microsoft.com/windows-hardware/test/wpt/optimizing-performance-and-responsiveness#fast-startup-behavior) (phases of a fast startup).
- Microsoft Learn, [Distinguishing fast startup from wake-from-hibernation](https://learn.microsoft.com/windows-hardware/drivers/kernel/distinguishing-fast-startup-from-wake-from-hibernation).
- Microsoft Learn, [System power states: hibernate and fast startup](https://learn.microsoft.com/windows/win32/power/system-power-states#hibernate-state-s4).
- Microsoft Learn, [Delivering a great startup and shutdown experience](https://learn.microsoft.com/windows-hardware/test/weg/delivering-a-great-startup-and-shutdown-experience) ("the kernel, drivers, and services are preserved and restored, not just restarted").
- Microsoft Learn, [Fast startup causes hibernation or shutdown to fail](https://learn.microsoft.com/troubleshoot/windows-client/setup-upgrade-and-drivers/fast-startup-causes-system-hibernation-shutdown-fail#more-information) (Restart always does a full boot).
- Microsoft Learn, [Wake on LAN behavior in Windows 10](https://learn.microsoft.com/troubleshoot/windows-client/setup-upgrade-and-drivers/wake-on-lan-feature) (hybrid shutdown is the default; no WoL from it).
- Microsoft Learn, [On/Off transition performance](https://learn.microsoft.com/windows-hardware/test/assessments/onoff-transition-performance) ("If fast startup is turned off … both shutdown and restart occur as full reboots").
- Microsoft Learn, [Boot Manager metric](https://learn.microsoft.com/windows-hardware/test/assessments/boot-manager) and [Winlogon Resume Duration](https://learn.microsoft.com/windows-hardware/test/assessments/winlogon-resume-duration).
- Microsoft Learn, [Turn on automatic logon](https://learn.microsoft.com/troubleshoot/windows-server/user-profiles-and-logon/turn-on-automatic-logon) and [Sysinternals Autologon](https://learn.microsoft.com/sysinternals/downloads/autologon) (LSA secret).
- Microsoft (archived blog), Mark Renoden, *Windows Enterprise Client Boot and Logon Optimization*: [Part 3, trace capture](https://learn.microsoft.com/archive/blogs/markrenoden/windows-enterprise-client-boot-and-logon-optimization-part-3-trace-capture-and-benchmarking), [Part 11, OS loader and kernel initialization](https://learn.microsoft.com/archive/blogs/markrenoden/windows-enterprise-client-boot-and-logon-optimization-part-11-boot-phase-os-loader-and-kernel-initialization), [Part 12, session initialization](https://learn.microsoft.com/archive/blogs/markrenoden/windows-enterprise-client-boot-and-logon-optimization-part-12-boot-phase-session-initialization), [Part 13, Winlogon](https://learn.microsoft.com/archive/blogs/markrenoden/windows-enterprise-client-boot-and-logon-optimization-part-13-boot-phase-winlogon).
- Gigabyte, B550I AORUS PRO AX user's manual, [rev. 1101](https://download.gigabyte.com/FileList/Manual/mb_manual_b550i-aorus-pro-ax_e_1101.pdf) (BIOS Setup chapter: Boot, IO Ports) and [rev. 1303](https://download.gigabyte.com/FileList/Manual/mb_manual_b550i-aorus-pro-ax_1303_e.pdf) (PCB rev. 1.3 Wi-Fi, back panel). Rev. 1303 points to Gigabyte's AMD 500-series BIOS page for settings; that page returned 403 to this agent.
- TianoCore EDK2, [`MdePkg/Include/IndustryStandard/Acpi50.h`](https://github.com/tianocore/edk2/blob/master/MdePkg/Include/IndustryStandard/Acpi50.h), `EFI_ACPI_5_0_FPDT_FIRMWARE_BASIC_BOOT_RECORD` (field meanings behind `Kernel-Boot 30` and `FwPOSTTime`).
- This PC, read without admin on 2026-10-21: the event logs, registry values, WMI classes and device properties named in each section.
