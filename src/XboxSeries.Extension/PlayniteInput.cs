using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Maps Playnite's controller input to the core's buttons.</summary>
    internal static class PlayniteInput
    {
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
                default: return null;
            }
        }

        public static ButtonState ToState(ControllerInputState state) =>
            state == ControllerInputState.Pressed ? ButtonState.Pressed : ButtonState.Released;
    }
}
