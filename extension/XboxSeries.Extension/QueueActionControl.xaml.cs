using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Game hub's Queue, injected after Manage: shown while the game is in the Queue, Installing or Queued
    /// (<see cref="ExtensionCore.HubShowsQueue"/>), and gone as it leaves it, the hub still open. A, Enter or a click opens
    /// Manage Queue (<see cref="ExtensionCore.OpenQueueFromHub"/>); B there opens the hub again, and Queue takes focus
    /// (<see cref="ExtensionCore.TakeHubQueueFocus"/>). Hidden with focus, focus goes to Manage beside it.
    /// What it does is logged for the e2e: "[Hub] Queue action shown|hidden for &lt;game&gt;" as the hub shows or the Queue
    /// changes, "Manage Queue opened from the Game hub for &lt;game&gt;", and "[Hub] back from Manage Queue, focus on
    /// Queue for &lt;game&gt;" (or on what has it instead).
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class QueueActionControl : PluginUserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;

        /// <summary>The hub's root, whose showing is the hub opening; null until loaded.</summary>
        private FrameworkElement hub;
        private bool hubShown;
        private Game game;

        /// <summary>What was last logged, so each change is logged once: the game, and whether Queue showed for it.</summary>
        private Guid? loggedGame;
        private bool loggedShown;

        internal QueueActionControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            game = newContext;

            // Playnite follows the selection behind the hub too; the hub checks again as it opens.
            if (hub == null || hubShown)
            {
                Refresh();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            hub = HomeRows.FindPart(this, "PART_ViewHost");
            if (hub != null)
            {
                hub.IsVisibleChanged += OnHubVisibleChanged;
                hubShown = hub.IsVisible;
            }

            // The Queue changing is the game joining it, or leaving it, finished or not. -= first, so it is hooked once.
            core.MyGames.PropertyChanged -= OnMyGamesChanged;
            core.MyGames.PropertyChanged += OnMyGamesChanged;
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged -= OnMyGamesChanged;
            if (hub != null)
            {
                hub.IsVisibleChanged -= OnHubVisibleChanged;
                hub = null;
            }

            hubShown = false;
            loggedGame = null;
        }

        private void OnMyGamesChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MyGamesState.Queue))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Refresh));
            }
        }

        /// <summary>
        /// The hub opening, checked again first so Queue is there before the hub is drawn. Opened again by B on Manage
        /// Queue, Queue takes focus once Playnite, and the extension's own Install, have put it on the hub's first action.
        /// </summary>
        private void OnHubVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            hubShown = hub != null && hub.IsVisible;
            if (!hubShown)
            {
                loggedGame = null;
                return;
            }

            Refresh();
            var returning = game;
            if (returning == null || !core.HubReturnsToQueue(returning.Id))
            {
                return;
            }

            Defer(() =>
            {
                if (!core.TakeHubQueueFocus(returning.Id) || !hubShown)
                {
                    return;
                }

                if (IsVisible)
                {
                    QueueButton.Focus();
                }

                var focused = Keyboard.FocusedElement as FrameworkElement;
                var on = QueueButton.IsKeyboardFocused ? "Queue" : focused?.Name is string name && name.Length > 0 ? name : focused?.GetType().Name ?? "nothing";
                logger.Info($"[Hub] back from Manage Queue, focus on {on} for {returning.Name}");
            });
        }

        private void Refresh()
        {
            var shown = game != null && core.HubShowsQueue(game.Id);
            var hadFocus = QueueButton.IsKeyboardFocusWithin;
            Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            if (hubShown && game != null && (loggedGame != game.Id || loggedShown != shown))
            {
                loggedGame = game.Id;
                loggedShown = shown;
                logger.Info($"[Hub] Queue action {(shown ? "shown" : "hidden")} for {game.Name}");
            }

            // Gone with focus, as the game finishes or stops installing, or from LB or RB to another game: Manage takes it.
            if (!shown && hadFocus && hubShown)
            {
                Defer(() => HomeRows.FindPart(this, "PART_ButtonOptions")?.Focus());
            }
        }

        /// <summary>After the bindings and Playnite's own focus have settled, and before any input waiting.</summary>
        private void Defer(Action action) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, action);

        // The keyboard's Enter and the mouse; the pad's A arrives as OnKeyDown.
        private void OnClick(object sender, RoutedEventArgs e) => OpenQueue();

        // Playnite raises the pad's buttons as KeyDown alone, never PreviewKeyDown, and a plain Button ignores them.
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                OpenQueue();
            }
        }

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (game != null)
            {
                logger.Info($"[Hub] focus on Queue for {game.Name}");
            }
        }

        private void OpenQueue()
        {
            if (game != null && IsVisible && core.HubShowsQueue(game.Id))
            {
                logger.Info($"Manage Queue opened from the Game hub for {game.Name}");
                core.OpenQueueFromHub(game.Id);
            }
        }
    }
}
