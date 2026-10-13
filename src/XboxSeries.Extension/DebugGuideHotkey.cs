#if DEBUG
using System;
using System.Windows.Interop;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Debug builds only: Ctrl+Alt+G anywhere acts as a tap of the Guide button, for testing without a controller.
    /// A tap means a dismiss through it hands focus back at once rather than on release.
    /// </summary>
    internal sealed class DebugGuideHotkey : IDisposable
    {
        private const int HotkeyId = 0x5842; // "XB"
        private const uint VK_G = 0x47;
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly HwndSource window;

        public DebugGuideHotkey(ExtensionCore core)
        {
            this.core = core;
            window = new HwndSource(new HwndSourceParameters("XboxSeries debug Guide hotkey") { ParentWindow = new IntPtr(-3) }); // HWND_MESSAGE
            window.AddHook(OnMessage);
            if (!NativeMethods.RegisterHotKey(window.Handle, HotkeyId, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, VK_G))
            {
                logger.Warn("Couldn't register the debug Guide hotkey Ctrl+Alt+G; another app may own it");
            }
        }

        private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
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
