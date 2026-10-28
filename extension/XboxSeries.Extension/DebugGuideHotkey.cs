#if DEBUG
using System;
using System.Windows.Interop;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Debug builds only: G anywhere acts as a tap of the Guide button, for testing without a controller
    /// (and over Moonlight, which takes Ctrl+Alt). It swallows G system-wide while Playnite runs.
    /// A tap means a dismiss through it hands focus back at once rather than on release.
    /// </summary>
    internal sealed class DebugGuideHotkey : IDisposable
    {
        private const int HotkeyId = 0x5842; // "XB"
        private const uint VK_G = 0x47;
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly HwndSource window;

        // HwndSource holds its hooks weakly; without this field the hook is collected and the hotkey silently stops.
        private readonly HwndSourceHook hook;

        public DebugGuideHotkey(ExtensionCore core)
        {
            this.core = core;
            window = new HwndSource(new HwndSourceParameters("XboxSeries debug Guide hotkey") { ParentWindow = new IntPtr(-3) }); // HWND_MESSAGE
            hook = OnMessage;
            window.AddHook(hook);
            if (!NativeMethods.RegisterHotKey(window.Handle, HotkeyId, NativeMethods.MOD_NOREPEAT, VK_G))
            {
                logger.Warn("Couldn't register the debug Guide hotkey G; another app may own it");
            }
        }

        private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                logger.Info("Debug Guide hotkey G pressed");

                // A hotkey never reaches WPF's input, where the Idle screen hears of every other: told here, as the pad's
                // Guide is, so a tap that only wakes the screen is swallowed by the core as it reaches it.
                core.OnUserInput(ControllerButton.Guide);
                core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
                core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            NativeMethods.UnregisterHotKey(window.Handle, HotkeyId);
            window.Dispose();
        }
    }
}
#endif
