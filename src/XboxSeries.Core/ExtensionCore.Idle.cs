using System;

namespace XboxSeries.Core
{
    /// <summary>The Idle screen's timers, run off the clock, and the wake press it swallows.</summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>Dim after's timer, counting from the last input; null while it isn't counting.</summary>
        private IDisposable dimTimer;

        /// <summary>Show things after's timer, counting from the last input too, not from the dim; null while it isn't counting.</summary>
        private IDisposable showThingsTimer;

        public IdleState Idle { get; } = new IdleState();

        /// <summary>
        /// Any input but the pad's buttons as the core gets them: a key, the mouse, or a pad button as Playnite routes it
        /// to the focused element (<paramref name="padButton"/>), which comes before <see cref="OnControllerButton(ControllerButton, ButtonState)"/>.
        /// Resets the timers. True when it only woke the screen: the caller swallows it, and a pad button's press and
        /// release then reach <see cref="OnControllerButton(ControllerButton, ButtonState)"/> swallowed too.
        /// </summary>
        public bool OnUserInput(ControllerButton? padButton = null)
        {
            RestartIdle();
            return false;
        }

        /// <summary>
        /// Dim after: how long with no input before the screen dims, or null for Never. It counts from now; not saved
        /// yet, until the Idle settings page keeps it.
        /// </summary>
        public void SetDimAfter(TimeSpan? dimAfter)
        {
            Idle.DimAfter = dimAfter;
            RestartIdle();
        }

        /// <summary>
        /// Show things after: how long with no input before the Idle screen shows things, or null for Never. It counts
        /// from now; not saved yet, until the Idle settings page keeps it.
        /// </summary>
        public void SetShowThingsAfter(TimeSpan? showThingsAfter)
        {
            Idle.ShowThingsAfter = showThingsAfter;
            RestartIdle();
        }

        /// <summary>The screen is awake, and the timers count again from now.</summary>
        private void RestartIdle()
        {
            dimTimer?.Dispose();
            dimTimer = null;
            showThingsTimer?.Dispose();
            showThingsTimer = null;
            Idle.Stage = IdleStage.Awake;
            if (Idle.DimAfter is TimeSpan dimAfter)
            {
                dimTimer = clock.After(dimAfter, () =>
                {
                    dimTimer = null;

                    // Show things after no later than Dim after has already shown things, the dim with them.
                    if (Idle.Stage == IdleStage.Awake)
                    {
                        Idle.Stage = IdleStage.Dimmed;
                    }
                });
            }

            if (Idle.ShowThingsAfter is TimeSpan showThingsAfter)
            {
                showThingsTimer = clock.After(showThingsAfter, () =>
                {
                    showThingsTimer = null;
                    Idle.Stage = IdleStage.ShowingThings;
                });
            }
        }
    }
}
