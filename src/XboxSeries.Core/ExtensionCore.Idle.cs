using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace XboxSeries.Core
{
    /// <summary>The Idle screen's timers, run off the clock, and the wake press it swallows.</summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>Dim after's timer, counting from the last input; null while it isn't counting.</summary>
        private IDisposable dimTimer;

        /// <summary>Show things after's timer, counting from the last input too, not from the dim; null while it isn't counting.</summary>
        private IDisposable showThingsTimer;

        /// <summary>How often the Idle screen looks for input outside Playnite's windows, which nothing tells of.</summary>
        private static readonly TimeSpan IdleWatchInterval = TimeSpan.FromSeconds(1);

        /// <summary>Whether the Idle screen waits, as last looked at: the timers don't count meanwhile.</summary>
        private bool idlePaused;

        /// <summary>
        /// How soon after one half of a pad button's wake press, Playnite routing it to the focused element or the pad
        /// reporting it, the other has to come to be swallowed with it; later, a press of that button is a new one.
        /// </summary>
        private static readonly TimeSpan WakePressWindow = TimeSpan.FromSeconds(1);

        /// <summary>The pad button whose press woke the screen, swallowed as routed and as reported; null for none.</summary>
        private ControllerButton? wakeButton;

        /// <summary>Whether Playnite's routing of the wake button's press has come.</summary>
        private bool wakeRouted;

        /// <summary>Whether the pad's report of the wake button's press has come, so its release is the one to swallow.</summary>
        private bool wakeReported;

        /// <summary>Until when the half of the wake press still to come is that press.</summary>
        private DateTime wakePressBy;

        /// <summary>
        /// The secret store's name for Theme settings' Idle page's times, as JSON: Dim after and Show things after. Not
        /// secret, but the store keeps the extension's own options.
        /// </summary>
        private const string IdleSecret = "Idle";

        /// <summary>How Never is saved, a time not being one.</summary>
        private const string NeverSaved = "Never";

        /// <summary>What the Idle screen's cards are shuffled with.</summary>
        private readonly Random random;

        public IdleState Idle { get; } = new IdleState();

        /// <summary>
        /// Any input but the pad's buttons as the core gets them: a key, the mouse, or a pad button as Playnite routes it
        /// to the focused element (<paramref name="padButton"/>), which usually comes before <see cref="OnControllerButton(ControllerButton, ButtonState)"/>.
        /// Resets the timers. True when it only woke the screen: the caller swallows it, and a pad button's press and
        /// release then reach <see cref="OnControllerButton(ControllerButton, ButtonState)"/> swallowed too. A pad
        /// button whose press the pad reported first, waking the screen, is swallowed here too. Y's wake press on a game's
        /// card opens its Game hub as well, once, by whichever half comes first.
        /// </summary>
        public bool OnUserInput(ControllerButton? padButton = null)
        {
            if (padButton != null && padButton == wakeButton && !wakeRouted && clock.Now <= wakePressBy)
            {
                wakeRouted = true;
                RestartIdle();
                return true;
            }

            var woke = Idle.IsDimmed;
            var card = Idle.Card;
            if (woke)
            {
                wakeButton = padButton;
                wakeRouted = true;
                wakeReported = false;
                wakePressBy = clock.Now + WakePressWindow;
            }
            else if (padButton != null && padButton != wakeButton)
            {
                wakeButton = null;
            }

            return Woken(woke, padButton, card);
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
                if (button != wakeButton || !wakeReported)
                {
                    return false;
                }

                wakeButton = null;
                return true;
            }

            if (button == wakeButton && !wakeReported && clock.Now <= wakePressBy)
            {
                wakeReported = true;
                RestartIdle();
                return true;
            }

            var woke = Idle.IsDimmed;
            var card = Idle.Card;
            if (woke)
            {
                wakeButton = button;
                wakeRouted = false;
                wakeReported = true;
                wakePressBy = clock.Now + WakePressWindow;
            }
            else
            {
                wakeButton = null;
            }

            return Woken(woke, button, card);
        }

        /// <summary>
        /// The screen is awake after a press, and the timers count again; when the press <paramref name="woke"/> it and
        /// was Y, on <paramref name="card"/> as it showed, the card's game is seen. Returns whether it woke the screen.
        /// </summary>
        private bool Woken(bool woke, ControllerButton? button, IdleCard card)
        {
            RestartIdle();
            if (woke && button == ControllerButton.Y)
            {
                SeeIdleCardsGame(card);
            }

            return woke;
        }

        /// <summary>
        /// Y's wake press on a game's card, the half of it that came first: the screen wakes, and the game's Game hub
        /// opens, on the Dashboard, the Guide closing first if it was up, and the Console keyboard, Settings and a Tile
        /// menu going. The hub is for the main list's selected game, so over Search, or where the main list's filter
        /// leaves the game out (My games' Games, for one not installed), the main list takes every game, given back as
        /// the page goes; over Play history it lists Play history's, as a tile there does. A tip's card, or none, only
        /// wakes, as does any card while a game runs: the game keeps the front, and the press reaches it too.
        /// </summary>
        private void SeeIdleCardsGame(IdleCard card)
        {
            if (!(card?.GameId is Guid gameId) || runningGame != null)
            {
                return;
            }

            void OpenHub()
            {
                if (TextEntry.IsShown)
                {
                    EndTextEntry(keep: false);
                }

                if (Settings.IsShown)
                {
                    CloseSettings();
                }

                CloseTileMenu();
                var game = GameById(gameId);
                if (Search.IsShown || (mainListFilter != null && game != null && !mainListFilter.Apply(new[] { game }).Any()))
                {
                    ShowInMainList(GameListFilter.LastPlayed);
                }
                else
                {
                    ListPlayHistoryInMainList();
                }

                playnite.BringToFront();
                playnite.OpenGameHub(gameId);
            }

            if (Guide.IsOpen)
            {
                DismissAtOnce(OpenHub);
            }
            else
            {
                OpenHub();
            }
        }

        /// <summary>
        /// Dim after: how long with no input before the screen dims, one of <see cref="IdleState.Steps"/>, null for Never.
        /// Past Show things after, it raises Show things after with it, set first so Show things after is among
        /// <see cref="IdleState.ShowThingsAfterSteps"/> as they change. It counts from now. A time not among the steps,
        /// or the one already set, is refused.
        /// </summary>
        public void SetDimAfter(TimeSpan? dimAfter)
        {
            if (!IdleState.Steps.Contains(dimAfter) || dimAfter == Idle.DimAfter)
            {
                return;
            }

            if (IdleState.IsShorter(Idle.ShowThingsAfter, dimAfter))
            {
                Idle.ShowThingsAfter = dimAfter;
            }

            Idle.DimAfter = dimAfter;
            SaveIdleSettings();
            RestartIdle();
        }

        /// <summary>
        /// Show things after: how long with no input before the Idle screen shows things, one of
        /// <see cref="IdleState.ShowThingsAfterSteps"/>, null for Never. It counts from now. A time shorter than Dim
        /// after, not among the steps, or the one already set, is refused.
        /// </summary>
        public void SetShowThingsAfter(TimeSpan? showThingsAfter)
        {
            if (!IdleState.Steps.Contains(showThingsAfter)
                || IdleState.IsShorter(showThingsAfter, Idle.DimAfter)
                || showThingsAfter == Idle.ShowThingsAfter)
            {
                return;
            }

            Idle.ShowThingsAfter = showThingsAfter;
            SaveIdleSettings();
            RestartIdle();
        }

        /// <summary>
        /// Theme settings' Idle page's times saved before, read when the core is made: shared by every Profile. With none
        /// saved, or none readable, 2 min and 5 min; a time not among the steps is its default, and a Show things after
        /// shorter than Dim after is raised to it.
        /// </summary>
        private void LoadIdleSettings()
        {
            SavedIdle saved;
            try
            {
                var json = secrets.Read(IdleSecret);
                saved = json == null ? null : JsonConvert.DeserializeObject<SavedIdle>(json);
            }
            catch (JsonException)
            {
                saved = null;
            }

            var dimAfter = SavedStep(saved?.DimAfter, Idle.DimAfter);
            var showThingsAfter = SavedStep(saved?.ShowThingsAfter, Idle.ShowThingsAfter);
            Idle.ShowThingsAfter = IdleState.IsShorter(showThingsAfter, dimAfter) ? dimAfter : showThingsAfter;
            Idle.DimAfter = dimAfter;
        }

        /// <summary>
        /// A step as saved: "Never" for Never, else a time as "00:10:00"; <paramref name="fallback"/> for none saved, or
        /// one not among <see cref="IdleState.Steps"/>.
        /// </summary>
        private static TimeSpan? SavedStep(string saved, TimeSpan? fallback)
        {
            if (saved == NeverSaved)
            {
                return null;
            }

            return TimeSpan.TryParseExact(saved, "c", CultureInfo.InvariantCulture, out var after) && IdleState.Steps.Contains(after)
                ? after
                : fallback;
        }

        /// <summary>A step to save: "Never" for Never, else the time as "00:10:00".</summary>
        private static string StepToSave(TimeSpan? after) => after?.ToString("c", CultureInfo.InvariantCulture) ?? NeverSaved;

        private void SaveIdleSettings() => secrets.Write(IdleSecret, JsonConvert.SerializeObject(new SavedIdle
        {
            DimAfter = StepToSave(Idle.DimAfter),
            ShowThingsAfter = StepToSave(Idle.ShowThingsAfter),
        }));

        /// <summary>
        /// What the secret store keeps of Theme settings' Idle page's times, each "Never" or a time as "00:10:00"; a time
        /// missing is one never saved, so Never isn't taken for it.
        /// </summary>
        private sealed class SavedIdle
        {
            public string DimAfter { get; set; }

            public string ShowThingsAfter { get; set; }
        }

        /// <summary>
        /// Whether the Idle screen waits: Start-up or a Launch screen shows. Over a running game, or with another app in
        /// front, it counts as over Playnite.
        /// </summary>
        private bool IsIdlePausedNow => IsStartupShown || LaunchScreen.IsShown;

        /// <summary>
        /// Starts the Idle screen's timers as the core is made. Start-up and the Launch screen tell the core as they begin
        /// and end, but Playnite's windows see no input made outside them, over a game or another app, so the
        /// <see cref="IInputActivity"/> port is looked at every <see cref="IdleWatchInterval"/>.
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
            WatchIdleInput();
        }

        private void WatchIdleInput() => clock.After(IdleWatchInterval, () =>
        {
            LookForInputOutsidePlaynite();
            WatchIdleInput();
        });

        /// <summary>
        /// Input anywhere in Windows since the last look, a pad held off its rest included: it counts as input, the timers
        /// counting again from now and a dimmed screen waking, swallowing nothing, the input not being Playnite's to
        /// swallow. Input in Playnite's windows is seen here too, after <see cref="OnUserInput(ControllerButton?)"/> or
        /// <see cref="OnControllerButton(ControllerButton, ButtonState)"/> has woken the screen and set the wake press
        /// swallowing, which it leaves alone; so a dimmed screen with Playnite in front is left for those to wake, should
        /// the look come between the input and Playnite handing it on, which would let the wake press through. Returns
        /// whether it counted.
        /// </summary>
        private bool LookForInputOutsidePlaynite()
        {
            if (!inputActivity.InputSinceLastLook())
            {
                return false;
            }

            if (Idle.IsDimmed && windowSystem.IsPlaynites(windowSystem.ForegroundWindow))
            {
                return false;
            }

            RestartIdle();
            return true;
        }

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
        /// A timer fell due: whether it may change the stage, input outside Playnite looked for first, since there may
        /// have been some since the last look; input counted has the timers count again instead.
        /// </summary>
        private bool IdleTimerMayAct() => !LookForInputOutsidePlaynite();

        /// <summary>The screen is awake, and unless the Idle screen waits, the timers count again from now.</summary>
        private void RestartIdle()
        {
            dimTimer?.Dispose();
            dimTimer = null;
            showThingsTimer?.Dispose();
            showThingsTimer = null;
            Idle.Stage = IdleStage.Awake;

            // After the stage, so the view sees the screen awake as the card goes and hides it with the dim.
            StopIdleCards();
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

                    // Show things after equal to Dim after may have shown things first, the dim with them.
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
                        StartIdleCards();
                    }
                });
            }
        }
    }
}
