using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Every input in Playnite's UI thread, told to the core for the Idle screen's timers: keys, the mouse, and the pad's
    /// buttons as Playnite routes them to the focused element. The press that only wakes the screen is swallowed whole
    /// (<see cref="WakeSwallow{TInput}"/>), so A doesn't launch the focused tile, B doesn't go back and focus stays put.
    /// </summary>
    /// <remarks>
    /// It listens to <see cref="InputManager.PreProcessInput"/>, which runs for each input before WPF routes it, in every
    /// window of the thread: Playnite's, its dialogs, and the Overlay with the Guide. Cancelling an input there stops it
    /// before any handler sees it, class handlers too, so Playnite's InputBindings, its ButtonEx's A, a ListBox's arrows
    /// and the extension's own key handlers never run. Playnite's pad goes through it as well: GameController
    /// (JosefNemec/Playnite, source/Playnite/Input/GameController.cs) builds a GameControllerInputEventArgs, a KeyEventArgs
    /// whose RoutedEvent is KeyDown or KeyUp with no PreviewKeyDown before it, and calls InputManager.ProcessInput with
    /// it; only after that, in the same dispatcher operation, does it raise the button to extensions, so the core hears
    /// of the press here before <see cref="ExtensionCore.OnControllerButton(ControllerButton, ButtonState)"/>. It also
    /// sends the D-pad and left stick as arrow keys (and A as Enter, with "simulate all keys" on) by a WM_KEYDOWN to its
    /// window, a real key to WPF that can arrive just before or after the pad's own event: the two are one press, so
    /// the one that wakes swallows the other. Neither comes while Playnite isn't the active app.
    /// </remarks>
    internal sealed class IdleInput : IDisposable
    {
        private readonly ExtensionCore core;
        private readonly GamingInputController pad;
        private readonly WakeSwallow<object> swallow = new WakeSwallow<object>();
        /// <summary>
        /// How often a moving mouse is told to the core while the screen is awake, each telling restarting its timers;
        /// the first move while it is dimmed is told at once, to wake it.
        /// </summary>
        private static readonly TimeSpan MouseMoveEvery = TimeSpan.FromMilliseconds(250);

        private NativeMethods.POINT lastCursor;
        private DateTime mouseMoveToldAt;

        public IdleInput(ExtensionCore core, GamingInputController pad)
        {
            this.core = core;
            this.pad = pad;
            NativeMethods.GetCursorPos(out lastCursor);
            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        public void Dispose() => InputManager.Current.PreProcessInput -= OnPreProcessInput;

        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            var input = e.StagingItem.Input;
            var routed = input.RoutedEvent;
            if (routed == null)
            {
                // A raw report, before WPF makes it the routed events looked at below.
                return;
            }

            if (routed == Mouse.PreviewMouseMoveEvent)
            {
                // WPF also moves the mouse where it stands as windows and layout change under it: only a real move counts.
                if (NativeMethods.GetCursorPos(out var cursor) && (cursor.X != lastCursor.X || cursor.Y != lastCursor.Y))
                {
                    lastCursor = cursor;
                    var now = DateTime.UtcNow;
                    if (core.Idle.IsDimmed || now - mouseMoveToldAt >= MouseMoveEvery)
                    {
                        mouseMoveToldAt = now;
                        core.OnUserInput();
                    }
                }

                return;
            }

            bool swallowed;
            if (routed == Keyboard.PreviewKeyDownEvent && input is KeyEventArgs pressed)
            {
                var key = KeyOf(pressed);
                swallowed = swallow.Press(key, DateTime.Now, () => core.OnUserInput(core.Idle.IsDimmed ? PadHolding(key) : null), PadTwinsOf(key));
            }
            else if (routed == Keyboard.PreviewKeyUpEvent && input is KeyEventArgs released)
            {
                swallowed = swallow.Release(KeyOf(released));
            }
            else if (routed == Keyboard.KeyDownEvent && input is KeyEventArgs padDown && PlayniteInput.PadButtonOf(padDown) is ControllerInput button)
            {
                swallowed = swallow.Press(button, DateTime.Now, () => core.OnUserInput(PlayniteInput.ToButton(button)), KeyTwinsOf(button));
            }
            else if (routed == Keyboard.KeyUpEvent && input is KeyEventArgs padUp && PlayniteInput.PadButtonOf(padUp) is ControllerInput let)
            {
                swallowed = swallow.Release(let);
            }
            else if (routed == Mouse.PreviewMouseDownEvent && input is MouseButtonEventArgs click)
            {
                swallowed = swallow.Press(click.ChangedButton, DateTime.Now, () => core.OnUserInput(), null);
            }
            else if (routed == Mouse.PreviewMouseUpEvent && input is MouseButtonEventArgs unclick)
            {
                swallowed = swallow.Release(unclick.ChangedButton);
            }
            else if (routed == Mouse.PreviewMouseWheelEvent)
            {
                swallowed = core.OnUserInput();
            }
            else if (routed == TextCompositionManager.PreviewTextInputStartEvent || routed == TextCompositionManager.PreviewTextInputEvent)
            {
                // The character a swallowed key types comes apart from its key, from the WM_CHAR after it.
                swallowed = swallow.Holds(held => held is Key);
            }
            else
            {
                return;
            }

            if (swallowed)
            {
                e.Cancel();
            }
        }

        private static Key KeyOf(KeyEventArgs e) =>
            e.Key == Key.System ? e.SystemKey : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

        /// <summary>
        /// The pad button that <paramref name="key"/> is Playnite sending as a key, while the pad holds it down; null for
        /// a key pressed on a keyboard. Read only to wake the screen, so the core swallows that button as it reaches it.
        /// </summary>
        private ControllerButton? PadHolding(Key key)
        {
            ControllerButton button;
            switch (key)
            {
                case Key.Up: button = ControllerButton.DPadUp; break;
                case Key.Down: button = ControllerButton.DPadDown; break;
                case Key.Left: button = ControllerButton.DPadLeft; break;
                case Key.Right: button = ControllerButton.DPadRight; break;
                case Key.Enter: button = ControllerButton.A; break;
                default: return null;
            }

            return pad.ButtonsDown().Contains(button) ? button : (ControllerButton?)null;
        }

        /// <summary>The pad buttons Playnite sends as <paramref name="key"/>, as its GameController's keyboard map has them.</summary>
        private static IEnumerable<object> PadTwinsOf(Key key) =>
            KeyboardMap.Where(pair => pair.Value == key).Select(pair => (object)pair.Key);

        /// <summary>The key Playnite sends <paramref name="button"/> as too, if any.</summary>
        private static IEnumerable<object> KeyTwinsOf(ControllerInput button) =>
            KeyboardMap.Where(pair => pair.Key == button).Select(pair => (object)pair.Value);

        /// <summary>Playnite's GameController keyboardMap, the buttons it sends as keys besides routing them as themselves.</summary>
        private static readonly KeyValuePair<ControllerInput, Key>[] KeyboardMap =
        {
            new KeyValuePair<ControllerInput, Key>(ControllerInput.A, Key.Enter),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.DPadUp, Key.Up),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.DPadDown, Key.Down),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.DPadLeft, Key.Left),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.DPadRight, Key.Right),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.LeftStickUp, Key.Up),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.LeftStickDown, Key.Down),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.LeftStickLeft, Key.Left),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.LeftStickRight, Key.Right),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.TriggerLeft, Key.PageUp),
            new KeyValuePair<ControllerInput, Key>(ControllerInput.TriggerRight, Key.PageDown),
        };
    }
}
