using System.Windows.Input;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Maps Playnite's controller input to the core's buttons.</summary>
    internal static class PlayniteInput
    {
        /// <summary>
        /// Whether keyboard focus came to an element from nothing rather than from a move: from the window itself, where
        /// WPF leaves focus when what had it hides or goes, as a tile under a page opening over it or a menu closing.
        /// Playnite then puts focus on the window's first focusable element ("Lost keyboard focus from known controls"),
        /// which with a page of ours up is its nav's first item. A nav that opens what takes focus must not take that for
        /// a move along it (#146): it asks here, opens nothing, and puts focus where its page wants it.
        /// </summary>
        public static bool FocusCameFromNothing(KeyboardFocusChangedEventArgs e) => e.OldFocus is System.Windows.Window;

        public static ControllerButton? ToButton(ControllerInput input)
        {
            switch (input)
            {
                case ControllerInput.A: return ControllerButton.A;
                case ControllerInput.B: return ControllerButton.B;
                case ControllerInput.X: return ControllerButton.X;
                case ControllerInput.Y: return ControllerButton.Y;
                case ControllerInput.LeftShoulder: return ControllerButton.LeftShoulder;
                case ControllerInput.RightShoulder: return ControllerButton.RightShoulder;
                case ControllerInput.Back: return ControllerButton.Back;
                case ControllerInput.Start: return ControllerButton.Start;
                case ControllerInput.Guide: return ControllerButton.Guide;
                case ControllerInput.LeftStick: return ControllerButton.LeftStick;
                case ControllerInput.RightStick: return ControllerButton.RightStick;
                case ControllerInput.DPadUp: return ControllerButton.DPadUp;
                case ControllerInput.DPadDown: return ControllerButton.DPadDown;
                case ControllerInput.DPadLeft: return ControllerButton.DPadLeft;
                case ControllerInput.DPadRight: return ControllerButton.DPadRight;

                // The left stick moves as the D-pad does, as on the console: Playnite reports each way it is pushed as a button.
                case ControllerInput.LeftStickUp: return ControllerButton.DPadUp;
                case ControllerInput.LeftStickDown: return ControllerButton.DPadDown;
                case ControllerInput.LeftStickLeft: return ControllerButton.DPadLeft;
                case ControllerInput.LeftStickRight: return ControllerButton.DPadRight;
                default: return null;
            }
        }

        public static ButtonState ToState(ControllerInputState state) =>
            state == ControllerInputState.Pressed ? ButtonState.Pressed : ButtonState.Released;

        /// <summary>
        /// The pad button behind one of Playnite's own key events; null for a real key. Playnite routes the pad's
        /// buttons to the focused element as key events with no Key, before it raises them to extensions. Its event
        /// type isn't in the SDK, so the button is read by name.
        /// </summary>
        public static ControllerInput? PadButtonOf(KeyEventArgs e) =>
            e.GetType().GetProperty("Button")?.GetValue(e) as ControllerInput?;

        /// <summary>
        /// The pad button behind a key event, counting the real keys Playnite's view host binds to the same commands as
        /// its pad buttons, pressed with no modifier: Y its keyboard to filter by name, as the pad's Y; F its filters, as
        /// RS; F2 and F3 the previous and next filter preset, as LB and RB. Null for any other real key.
        /// </summary>
        public static ControllerInput? PlayniteButtonOf(KeyEventArgs e)
        {
            var pad = PadButtonOf(e);
            if (pad != null || Keyboard.Modifiers != ModifierKeys.None)
            {
                return pad;
            }

            switch (e.Key)
            {
                case Key.Y: return ControllerInput.Y;
                case Key.F: return ControllerInput.RightStick;
                case Key.F2: return ControllerInput.LeftShoulder;
                case Key.F3: return ControllerInput.RightShoulder;
                default: return null;
            }
        }

#if DEBUG
        /// <summary>
        /// Debug builds only: keys standing in for pad buttons in the Guide and the tile menu, for testing without a
        /// controller. Not the arrows: Playnite sends the pad's D-pad as arrow keys, so they would move focus twice.
        /// The core repeats held keys itself, so the keyboard's own auto-repeat is ignored.
        /// </summary>
        public static ControllerButton? DebugButtonFor(Key key)
        {
            switch (key)
            {
                case Key.W: return ControllerButton.DPadUp;
                case Key.S: return ControllerButton.DPadDown;
                case Key.Enter: return ControllerButton.A;
                case Key.Back: return ControllerButton.B;
                case Key.M: return ControllerButton.Start;
                case Key.A: return ControllerButton.DPadLeft;
                case Key.D: return ControllerButton.DPadRight;
                case Key.Q: return ControllerButton.LeftShoulder;
                case Key.E: return ControllerButton.RightShoulder;
                case Key.P: return ControllerButton.Guide;
                default: return null;
            }
        }
#endif
    }
}
