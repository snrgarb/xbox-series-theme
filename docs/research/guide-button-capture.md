# Guide button capture from a Playnite extension

Research for [#2](https://github.com/snrgarb/xbox-series-theme/issues/2) (part of #1).

**Question.** How can a Playnite 10 Generic extension (.NET Framework 4.6.2) detect Guide button presses from XInput controllers system-wide, while Playnite is in the background, a game has focus, and Xbox Game Bar's Guide binding is off?

**Date researched:** 2026-10-13. Sources: Playnite 10.62 source (upstream tag `10.62`, `8434b34`), the binaries in a local Playnite 10.62 install, SDL 2/3 source, Microsoft Learn (Win32 XInput and GDK GameInput docs), Handheld Companion source, and the nefarius HidHide/ViGEm repos.

---

## TL;DR / recommendation

1. **Don't write a new input stack. Override `Plugin.OnControllerButtonStateChanged` and react to `ControllerInput.Guide`.** Playnite 10 Fullscreen already polls every controller through SDL about every 16 ms on a background thread. It already reads the Guide bit (through `XInputGetStateEx`, xinput1_4 ordinal 100, inside SDL). It already forwards every button change to every plugin, whether or not Playnite has focus. Playnite's own "Guide button focuses Playnite" setting depends on this same path working while Playnite is in the background. Our code adds no polling cost, needs no P/Invoke, and covers the same devices Playnite does: XInput pads, plus PS4/PS5/Switch pads through SDL's HIDAPI drivers.
2. **Fallback, if we ever need it outside Playnite's loop:** P/Invoke `xinput1_4.dll` ordinal `#100` (`XInputGetStateEx`) and test bit `0x0400`. SDL and Handheld Companion both do this. It is undocumented but has been stable for more than 10 years.
3. **No user-mode mechanism can stop the game from seeing the press.** In practice this rarely matters. Most games read the documented `XInputGetState`, which never reports Guide. Games that do see it are those using SDL, Windows.Gaming.Input or GameInput, plus Steam's overlay. The only real way to swallow the press is a driver-level "hide and re-emit" setup (HidHide plus a virtual pad). That is out of scope for a theme extension.
4. **GameInput** (`IGameInput::RegisterSystemButtonCallback` with `GameInputEnableBackgroundGuideButton`) is the Microsoft-sanctioned future path. It is native C++ only, though, and still can't consume the press from the background. Keep it as a later option, not the first choice.

---

## How Playnite 10 already reads the Guide button

All paths below refer to Playnite's repo (`JosefNemec/Playnite`, tag `10.62`).

- **SDL stack.** Playnite 10.62 ships `SDL2.dll` 2.32.66, whose file description is "Simple DirectMedia Layer 2.0 wrapper" (sdl2-compat), alongside `SDL3.dll` 3.4.4. Both were checked in a local `%LOCALAPPDATA%\Playnite` install. The joystick backend is therefore SDL 3.4.4.
- **Init.** `FullscreenApplication.InitSDL()` calls `SDL_Init(SDL_INIT_GAMECONTROLLER | SDL_INIT_AUDIO)` and sets `SDL_HINT_JOYSTICK_RAWINPUT=0`. Its comment cites [SDL#13047](https://github.com/libsdl-org/SDL/issues/13047) and [Playnite#3794](https://github.com/JosefNemec/Playnite/issues/3794). It then starts a dedicated `SDLEventLoop` thread that calls `GameController.ProcessInputs()` and then `Thread.Sleep(16)`. ([FullscreenApplication.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/Playnite.FullscreenApp/FullscreenApplication.cs))
- **Polling.** `GameControllerManager.ProcessState` calls `SDL_GameControllerGetButton(..., SDL_CONTROLLER_BUTTON_GUIDE)` alongside every other button. On a change, `SendControllerInput` posts to the UI thread and raises `ButtonChanged`. Only the WPF keyboard-simulation branch checks for an active input source; `ButtonChanged` is raised regardless of focus. ([Input/GameController.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/Playnite/Input/GameController.cs))
- **Plugin fan-out.** `FullscreenAppViewModel.GameControllerInputButtonChanged` first handles the built-in `GuideButtonFocus` / `GuideButtonFocusOnlyWhenNoGameRunning` restore-window behaviour. It then calls `plugin.Plugin.OnControllerButtonStateChanged(e)` for every loaded plugin, with no focus check. Desktop mode does the same through `OnDesktopControllerButtonStateChanged`. ([FullscreenAppViewModel.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs), [PlayniteSDK/Plugins/Plugin.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/PlayniteSDK/Plugins/Plugin.cs))
- **Why it works in the background.** SDL drops joystick input only when `SDL_HasWindows() && SDL_GetKeyboardFocus() == NULL` ([SDL 3.4.4 `SDL_PrivateJoystickShouldIgnoreEvent`](https://github.com/libsdl-org/SDL/blob/release-3.4.4/src/joystick/SDL_joystick.c); the SDL2 code is identical). Playnite renders with WPF and never creates an SDL window, so SDL never treats it as unfocused.
- **Which SDL driver handles Xbox pads.** RawInput is disabled by Playnite. The GameInput driver is off by default on desktop (`SDL_GAMEINPUT_DEFAULT false` unless GDK). HIDAPI-Xbox is off by default on Windows (per `SDL_hints.h`). The WGI driver skips XInput devices while XInput is enabled. So Xbox/XInput pads go through SDL's XInput driver, which resolves `XInputGetState` to **ordinal 100** of `XInput1_4.dll` ([SDL `SDL_xinput.c`](https://github.com/libsdl-org/SDL/blob/main/src/core/windows/SDL_xinput.c): *"100 is the ordinal for _XInputGetStateEx, which returns the same struct as XinputGetState, but with extra data in wButtons for the guide button"*).
- **Exposed settings.** `IPlayniteSettingsAPI` exposes `GuideButtonFocus` and `GuideButtonFocusOnlyWhenNoGameRunning`. If the user turns on Playnite's built-in Guide behaviour, Playnite restores its window on Guide release in addition to whatever our extension does. The extension should read these settings and either defer to Playnite or tell the user to turn the built-in option off.

**Practical shape:**

```csharp
public override void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args)
{
    if (args.Button == ControllerInput.Guide && args.State == ControllerInputState.Pressed)
    {
        // Guide pressed on args.Controller; Playnite may be backgrounded here.
    }
}
```

Caveats:

- The event exists only in Fullscreen mode. Desktop mode has the separate `OnDesktopControllerButtonStateChanged`.
- It covers only controllers Playnite hasn't disabled (`DisabledGameControllers`).
- It is raised on the UI thread through `SynchronizationContext.Post`, so handlers must be quick.
- Bringing Playnite to the foreground from the background is a separate problem (Windows foreground-lock rules; see below). It belongs to the downstream tickets (#8, #10), not to detection.

---

## Mechanism comparison

| Mechanism | Sees Guide? | Background (game focused) | Cost | Can consume? | Notes |
|---|---|---|---|---|---|
| **Playnite `OnControllerButtonStateChanged`** (SDL → XInputGetStateEx) | Yes | Yes (no SDL window, so no focus gating) | None added; Playnite already polls at about 60 Hz | No | Recommended. Also covers PS/Switch pads through SDL HIDAPI. |
| **`XInputGetStateEx`** (xinput1_4 `#100`, bit `0x0400`) | Yes | Yes in practice (see below) | 4 slot polls per tick; MS advises against polling *empty* slots every frame | No | Undocumented. Used by SDL and Handheld Companion. Max 4 pads. |
| **Documented `XInputGetState`** | **No.** Guide isn't in the documented `wButtons` bits | Yes | Same | n/a | This is why most games never see Guide. |
| **Windows.Gaming.Input** (`Gamepad`) | Only through the undocumented bit `0x40000000` | **No** for practical purposes (input stops when the app loses focus) | Polling | No | Needs WinRT interop from net462. |
| **Raw Input / HID** | **No** for XUSB devices (the HID mapping has no Guide usage). BT Xbox pads send Guide in a vendor-specific report | Yes (`RIDEV_INPUTSINK`) | Event-driven | No | SDL calls this path "no guide button" and falls back to XInput/WGI for it. |
| **GameInput** (`RegisterSystemButtonCallback`) | Yes, first-class | Yes, with `GameInputEnableBackgroundGuideButton` | Callback, near-zero | Only for the *foreground* process | Native C++ API plus runtime; awkward from net462. |
| **HidHide + virtual pad** (Handheld Companion / DS4Windows pattern) | Yes (reads the physical device) | Yes | Kernel drivers plus a feeder | **Yes**, by re-emitting a pad without Guide | Heavy; ViGEmBus is retired. Out of scope. |

### XInputGetStateEx (xinput1_4 ordinal 100)

- **What it is.** A hidden export that fills the same `XINPUT_STATE` layout as `XInputGetState` but also sets bit `0x0400` for Guide. The documented `XINPUT_GAMEPAD.wButtons` table skips `0x0400`; per the docs, *"Bits that are set but not defined above are reserved, and their state is undefined"* ([XINPUT_GAMEPAD](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad)). It isn't present in XInput 9.1.0 (SDL source comment).
- **Who uses it.** SDL ([SDL_xinput.c](https://github.com/libsdl-org/SDL/blob/main/src/core/windows/SDL_xinput.c)) and Handheld Companion, which declares `[DllImport("xinput1_4.dll", EntryPoint = "#100")]` and tests `wButtons` against `Xbox = 1024` ([XInputController.cs](https://github.com/Valkirie/HandheldCompanion/blob/main/HandheldCompanion/Controllers/XInputController.cs)). On the research machine (Windows 11 build 26300), `GetProcAddress(LoadLibrary("xinput1_4.dll"), 100)` resolves.
- **Background behaviour.** The `XInputEnable` docs say that on Windows 10+ *"game controller input is automatically enabled/disabled by the system based on the application window focus"* ([XInputEnable](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputenable)). Practice contradicts this for input, though rumble does stop. A Microsoft Q&A report says *"I can still poll the controller with XInputGetState and it doesn't report neutral values. The buttons I press are registered even when the app isn't focused"* ([Q&A](https://learn.microsoft.com/answers/a/12803981)). SDL's code comments say the same: *"Prefer XInput over WindowsGamingInput, it continues to provide data in the background"* ([SDL_rawinputjoystick.c](https://github.com/libsdl-org/SDL/blob/main/src/joystick/windows/SDL_rawinputjoystick.c)). Playnite's shipping Guide-focus feature depends on this behaviour.
- **Cost.** One kernel round-trip per slot per poll. MS guidance: *"don't call XInputGetState for an 'empty' user slot every frame … space out checks for new controllers every few seconds"* ([Getting started with XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput)). Comparing `dwPacketNumber` lets you skip unchanged states.
- **Risk.** It's an undocumented ordinal, so Microsoft could remove it. SDL relies on it, which makes removal unlikely.

### Windows.Gaming.Input

- `GamepadButtons` has no documented Guide value. SDL reads the undocumented `0x40000000` bit (`#define GamepadButtons_GUIDE 0x40000000` in [SDL_rawinputjoystick.c](https://github.com/libsdl-org/SDL/blob/main/src/joystick/windows/SDL_rawinputjoystick.c)).
- Background: Microsoft documents that *"UWP apps cannot receive input from a game controller while the app is not in focus"* ([Add controls tutorial](https://learn.microsoft.com/windows/uwp/gaming/tutorial--adding-controls#adding-gamepad-support)). SDL also avoids WGI for background use. Its comment says XinputUap.dll (XInput-on-WGI) has *"no devices at startup, no background input"* ([SDL_xinput.c](https://github.com/libsdl-org/SDL/blob/main/src/core/windows/SDL_xinput.c)), and its WGI driver notes *"The axes are all zero when the application loses focus"* ([SDL_windows_gaming_input.c](https://github.com/libsdl-org/SDL/blob/main/src/joystick/windows/SDL_windows_gaming_input.c)).
- **Verdict: not usable** for our background scenario.

### Raw Input / HID

- The XUSB driver's HID interface (what Raw Input and DirectInput see) maps A/B/X/Y/LB/RB/Back/Start/LS/RS only. There is **no Guide usage**, and the triggers share one axis ([DirectInput and XUSB Devices](https://learn.microsoft.com/windows/win32/xinput/directinput-and-xusb-devices)).
- SDL: *"This is the packet format for Xbox 360 and Xbox One controllers on Windows, however with this interface there is no rumble support, no guide button … We use XInput and Windows.Gaming.Input to make up for these shortcomings."* SDL's RawInput driver does register with `RIDEV_INPUTSINK` to get background input ([SDL_rawinputjoystick.c](https://github.com/libsdl-org/SDL/blob/main/src/joystick/windows/SDL_rawinputjoystick.c)).
- Bluetooth Xbox pads expose a richer HID report, sometimes with Guide in a separate packet, depending on firmware ([SDL_hidapi_xboxone.c](https://github.com/libsdl-org/SDL/blob/release-3.4.4/src/joystick/hidapi/SDL_hidapi_xboxone.c) packet notes). Parsing that per firmware is a rabbit hole.
- **Verdict: not viable** as a Guide source.

### GameInput

- `IGameInput::RegisterSystemButtonCallback(device, GameInputSystemButtonGuide, …)` fires on Guide/Share press and release ([docs](https://learn.microsoft.com/gaming/gdk/docs/reference/input/gameinput/interfaces/igameinput/methods/igameinput_registersystembuttoncallback?view=gdk-2604)). Delivery depends on `SetFocusPolicy`. `GameInputEnableBackgroundGuideButton` *"Ensures that Guide button events are sent to the game even when not in focus. Guide button presses will still not be sent to the application when the in focus process [has] exclusive access."* ([GameInputFocusPolicy](https://learn.microsoft.com/gaming/gdk/docs/reference/input/gameinput/enums/gameinputfocuspolicy?view=gdk-2604))
- SDL 3.4 sets exactly `GameInputEnableBackgroundInput | GameInputEnableBackgroundGuideButton | GameInputEnableBackgroundShareButton` when its GameInput driver is on ([SDL_gameinputjoystick.cpp](https://github.com/libsdl-org/SDL/blob/release-3.4.4/src/joystick/gdk/SDL_gameinputjoystick.cpp)). That driver is off by default on desktop, so Playnite doesn't use it today.
- **Consumption is foreground-only.** `GameInputExclusiveForegroundGuideButton` *"Prevents other processes using GameInput from seeing Guide button presses … only when the game is in focus."* A backgrounded Playnite can't claim it. A focused GameInput game *can* claim it and starve us.
- From net462 this means a native C++ shim or hand-written vtable interop, plus the runtime. The runtime ships as `GameInputRedist.msi` in the [Microsoft.GameInput NuGet](https://www.nuget.org/packages/Microsoft.GameInput) / `winget install Microsoft.GameInput`, and needs Windows 10 19H1+ ([FAQ](https://learn.microsoft.com/gaming/gdk/docs/features/common/input/overviews/input-faq?view=gdk-2604)). `GameInputSvc` is present and running on the research machine. The only C# wrapper found is the third-party alpha [GameInputSharp.Core](https://www.nuget.org/packages/GameInputSharp.Core).
- **Verdict:** technically the cleanest and the most future-proof, but not worth the interop and runtime-dependency cost while Playnite's XInput path works. Revisit if Microsoft ever removes ordinal 100, or if Playnite moves SDL onto GameInput (which would keep the plugin event working anyway).

---

## Can the press be consumed so the game never sees it?

**Not from user mode, and not from a background process.**

- XInput has no exclusivity concept. Any process can poll any slot.
- GameInput exclusivity applies to the foreground process only (quoted above).
- In practice most games never see Guide anyway, because the documented `XInputGetState` doesn't report it. The exposure is limited to:
  - games built on SDL (ordinal 100);
  - WGI/GameInput titles that opt in;
  - Steam's in-game overlay, where Guide chords are a Steam Input feature ([Steam Input player guide](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_players)).
- The only true "swallow" is device virtualisation:
  - hide the physical pad with the [HidHide](https://github.com/nefarius/HidHide) filter driver (per-device, allow-list by executable path; [setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/));
  - read the physical pad yourself;
  - re-emit a virtual X360 pad without Guide.

  Handheld Companion does this through VIIPER plus HidHide ([README](https://github.com/Valkirie/HandheldCompanion/blob/main/README.md)), and DS4Windows does it through ViGEmBus. **ViGEmBus is retired** ([repo, archived](https://github.com/nefarius/ViGEmBus)). This adds kernel drivers, an installer and admin rights, which is far out of scope for a Playnite theme extension.

**Recommendation:** accept a non-consuming design. Use a long press, or act on *release*, and document the double-handling cases below.

---

## Interference matrix

| Situation | Effect on detection | Source / confidence |
|---|---|---|
| **Game Bar "Open Xbox Game Bar using this button on a controller" off** (`HKCU\Software\Microsoft\GameBar\UseNexusForGameBarEnabled = 0`) | Target configuration. Nothing else on the OS side reacts to Guide; Playnite sees it. | [Windows 11 settings reference](https://learn.microsoft.com/windows/apps/develop/settings/settings-windows-11#gaming-game-bar,-game-mode,-gaming-shortcuts) |
| **Game Bar binding on** | Both fire: Game Bar opens *and* XInputGetStateEx still reports the bit. No primary source says Game Bar swallows it in normal desktop mode. The setting reads/writes are documented. | Inferred; **needs a live test** |
| **Windows Full Screen Experience (FSE / "Xbox mode") active** | *"When FSE is active, Windows has exclusive access to the Guide button."* Apps only get Guide when the user presses View+Menu (or via the Task Switcher). **Detection breaks.** Detect it with `IsGamingFullScreenExperienceActive` / `RegisterGamingFullScreenExperienceChangeNotification` (`api-ms-win-gaming-experience-l1-1-0`, SDK 10.0.26100.3916+) and warn the user. | [Game launchers and handhelds](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers?view=gdk-2604) (primary) |
| **Focused game uses GameInput with `ExclusiveForegroundGuideButton`** | That game can hide Guide from other *GameInput* clients. Whether this also masks XInputGetStateEx is undocumented. | GameInput docs; XInput impact **unverified** |
| **Steam running, Steam Input on for Xbox pads** | Steam reacts to Guide itself (Big Picture / overlay / Guide chords). The physical pad stays visible to XInput, so we still see it, and both act. | Steam docs (chords); overlap from community reports; **needs a live test** |
| **DS4Windows / Handheld Companion with HidHide** | The physical pad is hidden from Playnite; Playnite sees the virtual X360 pad, whose Guide is fed from the PS/Home button. Works. Without HidHide, Playnite sees both pads (the DS4 through SDL HIDAPI, the virtual one through XInput), so each press arrives twice from two `Controller`s. | HidHide docs, HC README, SDL HIDAPI; double-event from code reading |
| **More than 4 XInput pads** | XInput caps at 4 slots. | XInput docs |

---

## Related constraint for downstream tickets (#8, #10)

Detecting Guide is the easy part. Acting on it (raising Playnite's window over a game) has its own rule: *"A foreground window can bring other apps to the foreground, but background apps can't take the foreground. Windows enforces foreground control as an operating system–level privilege."* ([Game launchers and handhelds](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers?view=gdk-2604#foreground-and-background-management)). Playnite's built-in `RestoreWindow()` on Guide already works around this; reuse it or study it rather than reimplementing.

---

## Open questions / needs a live test

1. With the Game Bar binding **on**, does XInputGetStateEx still report `0x0400`, and does Game Bar's overlay take focus first? This affects whether we tell users to turn it off or merely recommend it.
2. Does a focused game using `GameInputExclusiveForegroundGuideButton` mask the bit from XInputGetStateEx?
3. Steam Input on, Steam game focused: does Playnite still get Guide, and how do the overlays collide?
4. Is latency from Playnite's 16 ms loop plus `SynchronizationContext.Post` acceptable when the game hogs the CPU?

## Sources

- Playnite 10.62: [FullscreenApplication.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/Playnite.FullscreenApp/FullscreenApplication.cs), [GameController.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/Playnite/Input/GameController.cs), [FullscreenAppViewModel.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/Playnite.FullscreenApp/ViewModels/FullscreenAppViewModel.cs), [Plugin.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/PlayniteSDK/Plugins/Plugin.cs), [ApplicationEvents.cs](https://github.com/JosefNemec/Playnite/blob/10.62/source/PlayniteSDK/Events/ApplicationEvents.cs).
- SDL: [SDL_xinput.c](https://github.com/libsdl-org/SDL/blob/main/src/core/windows/SDL_xinput.c), [SDL_rawinputjoystick.c](https://github.com/libsdl-org/SDL/blob/main/src/joystick/windows/SDL_rawinputjoystick.c), [SDL_windows_gaming_input.c](https://github.com/libsdl-org/SDL/blob/main/src/joystick/windows/SDL_windows_gaming_input.c), [SDL 3.4.4 SDL_joystick.c](https://github.com/libsdl-org/SDL/blob/release-3.4.4/src/joystick/SDL_joystick.c), [SDL 3.4.4 SDL_gameinputjoystick.cpp](https://github.com/libsdl-org/SDL/blob/release-3.4.4/src/joystick/gdk/SDL_gameinputjoystick.cpp), [SDL 3.4.4 SDL_hints.h](https://github.com/libsdl-org/SDL/blob/release-3.4.4/include/SDL3/SDL_hints.h), [SDL2 SDL_joystick.c](https://github.com/libsdl-org/SDL/blob/SDL2/src/joystick/SDL_joystick.c)
- Microsoft: [XINPUT_GAMEPAD](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad), [XInputEnable](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputenable), [Getting started with XInput](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput), [DirectInput and XUSB devices](https://learn.microsoft.com/windows/win32/xinput/directinput-and-xusb-devices), [GameInputFocusPolicy](https://learn.microsoft.com/gaming/gdk/docs/reference/input/gameinput/enums/gameinputfocuspolicy?view=gdk-2604), [RegisterSystemButtonCallback](https://learn.microsoft.com/gaming/gdk/docs/reference/input/gameinput/interfaces/igameinput/methods/igameinput_registersystembuttoncallback?view=gdk-2604), [GameInput FAQ](https://learn.microsoft.com/gaming/gdk/docs/features/common/input/overviews/input-faq?view=gdk-2604), [Game launchers and handhelds (FSE)](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/pc-dev/handheld/handheld-launchers?view=gdk-2604), [Windows 11 settings reference](https://learn.microsoft.com/windows/apps/develop/settings/settings-windows-11), [UWP add-controls tutorial](https://learn.microsoft.com/windows/uwp/gaming/tutorial--adding-controls), [Q&A: XInput background polling](https://learn.microsoft.com/answers/a/12803981)
- Others: [Handheld Companion XInputController.cs](https://github.com/Valkirie/HandheldCompanion/blob/main/HandheldCompanion/Controllers/XInputController.cs) and [README](https://github.com/Valkirie/HandheldCompanion/blob/main/README.md), [HidHide](https://github.com/nefarius/HidHide), [ViGEmBus (retired)](https://github.com/nefarius/ViGEmBus), [Steam Input player guide](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_players)
