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

        /// <summary>How often the Idle screen looks at whether Playnite is in front, which nothing tells of.</summary>
        private static readonly TimeSpan IdleWatchInterval = TimeSpan.FromSeconds(1);

        /// <summary>Whether the Idle screen waits, as last looked at: the timers don't count meanwhile.</summary>
        private bool idlePaused;

        /// <summary>
        /// How soon after Playnite routes a wake press to the focused element the pad's own report of it has to come to be
        /// swallowed with it; later, a press of that button is a new one.
        /// </summary>
        private static readonly TimeSpan WakePressWindow = TimeSpan.FromSeconds(1);

        /// <summary>The pad button that woke the screen, whose press and release the pad events swallow; null for none.</summary>
        private ControllerButton? wakeButton;

        /// <summary>Whether the pad events have seen the wake button's press, so its release is the one to swallow.</summary>
        private bool wakeButtonDown;

        /// <summary>Until when the pad's report of a routed wake press is still that press.</summary>
        private DateTime wakePressBy;

        public IdleState Idle { get; } = new IdleState();

        /// <summary>
        /// Any input but the pad's buttons as the core gets them: a key, the mouse, or a pad button as Playnite routes it
        /// to the focused element (<paramref name="padButton"/>), which comes before <see cref="OnControllerButton(ControllerButton, ButtonState)"/>.
        /// Resets the timers. True when it only woke the screen: the caller swallows it, and a pad button's press and
        /// release then reach <see cref="OnControllerButton(ControllerButton, ButtonState)"/> swallowed too.
        /// </summary>
        public bool OnUserInput(ControllerButton? padButton = null)
        {
            var woke = Idle.IsDimmed;
            wakeButton = woke ? padButton : null;
            wakeButtonDown = false;
            wakePressBy = clock.Now + WakePressWindow;
            RestartIdle();
            return woke;
        }

        /// <summary>
        /// Whether a pad button, as the pad reports it, is the Idle screen's to swallow: a press while the screen is
        /// dimmed only wakes it, as does the press of the button Playnite routed as the wake just before, and either's
        /// release goes with it. Any other press counts as input, and forgets a wake button whose press never came.
        /// </summary>
        private bool SwallowedByIdle(ControllerButton button, ButtonState state)
        {
            if (state == ButtonState.Released)
            {
                if (button != wakeButton || !wakeButtonDown)
                {
                    return false;
                }

                wakeButton = null;
                wakeButtonDown = false;
                return true;
            }

            var routedWake = button == wakeButton && !wakeButtonDown && clock.Now <= wakePressBy;
            var woke = routedWake || Idle.IsDimmed;
            wakeButton = woke ? button : (ControllerButton?)null;
            wakeButtonDown = woke;
            RestartIdle();
            return woke;
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

        /// <summary>
        /// Whether the Idle screen waits: Start-up or a Launch screen shows, a game runs, or Playnite isn't in front.
        /// </summary>
        private bool IsIdlePausedNow =>
            IsStartupShown || LaunchScreen.IsShown || runningGame != null || !windowSystem.IsPlaynites(windowSystem.ForegroundWindow);

        /// <summary>
        /// Starts the Idle screen's timers as the core is made. Start-up, a game and the Launch screen tell the core as
        /// they begin and end, but nothing tells of the foreground changing, so it is looked at every
        /// <see cref="IdleWatchInterval"/>.
        /// </summary>
        private void WatchIdle()
        {
            LaunchScreen.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(LaunchScreenState.IsShown))
                {
                    LookAtIdlePause();
                }
            };
            idlePaused = IsIdlePausedNow;
            RestartIdle();
            WatchIdleForeground();
        }

        private void WatchIdleForeground() => clock.After(IdleWatchInterval, () =>
        {
            LookAtIdlePause();
            WatchIdleForeground();
        });

        /// <summary>
        /// A pause beginning wakes the screen and stops the timers; one ending has them count again from zero.
        /// </summary>
        private void LookAtIdlePause()
        {
            var paused = IsIdlePausedNow;
            if (paused != idlePaused)
            {
                idlePaused = paused;
                RestartIdle();
            }
        }

        /// <summary>
        /// A timer fell due: whether it may change the stage, the pause looked at first, since the foreground may have
        /// changed since the last look.
        /// </summary>
        private bool IdleTimerMayAct()
        {
            LookAtIdlePause();
            return !idlePaused;
        }

        /// <summary>The screen is awake, and unless the Idle screen waits, the timers count again from now.</summary>
        private void RestartIdle()
        {
            dimTimer?.Dispose();
            dimTimer = null;
            showThingsTimer?.Dispose();
            showThingsTimer = null;
            Idle.Stage = IdleStage.Awake;
            if (idlePaused)
            {
                return;
            }

            if (Idle.DimAfter is TimeSpan dimAfter)
            {
                dimTimer = clock.After(dimAfter, () =>
                {
                    dimTimer = null;
                    if (!IdleTimerMayAct())
                    {
                        return;
                    }

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
                    if (IdleTimerMayAct())
                    {
                        Idle.Stage = IdleStage.ShowingThings;
                    }
                });
            }
        }
    }
}
