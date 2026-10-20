namespace XboxSeries.Core
{
    /// <summary>The Idle screen's timers, run off the clock, and the wake press it swallows.</summary>
    public sealed partial class ExtensionCore
    {
        public IdleState Idle { get; } = new IdleState();

        /// <summary>
        /// Any input but the pad's buttons as the core gets them: a key, the mouse, or a pad button as Playnite routes it
        /// to the focused element (<paramref name="padButton"/>), which comes before <see cref="OnControllerButton(ControllerButton, ButtonState)"/>.
        /// Resets the timers. True when it only woke the screen: the caller swallows it, and a pad button's press and
        /// release then reach <see cref="OnControllerButton(ControllerButton, ButtonState)"/> swallowed too.
        /// </summary>
        public bool OnUserInput(ControllerButton? padButton = null) => false;
    }
}
