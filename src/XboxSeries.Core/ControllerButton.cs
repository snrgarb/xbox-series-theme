namespace XboxSeries.Core
{
    /// <summary>XInput controller buttons, independent of Playnite's own input types.</summary>
    public enum ControllerButton
    {
        A,
        B,
        X,
        Y,
        LeftShoulder,
        RightShoulder,
        Back,
        Start,
        Guide,
        LeftStick,
        RightStick,
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,
    }

    public enum ButtonState
    {
        Pressed,
        Released,
    }
}
