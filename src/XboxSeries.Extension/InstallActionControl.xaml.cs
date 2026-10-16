using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Game hub's Install for a game the core installs silently (<see cref="ExtensionCore.InstallActionLabel"/>),
    /// injected beside Playnite's own main action, whose Install would open the client's own install dialog. It shows only
    /// for such a game, and the Dashboard hides Playnite's action while it does; A, Enter or a click asks the core to
    /// install it. Once queued it stays, reading Queued or Installing... and doing nothing, as Playnite's action does
    /// while Playnite installs: Playnite never sees this install, so its own action would offer Install again. Focus goes to it as the hub opens, as it would to Playnite's action, and it hands focus back to that
    /// action when another game's hub, from LB or RB, can't use it. Playnite sounds focus landing on any button, so it
    /// sounds as Play does.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class InstallActionControl : PluginUserControl
    {
        // The label of the action that installs; the others are the queue's, which do nothing.
        private const string InstallLabel = "Install";

        private readonly ExtensionCore core;

        /// <summary>The hub's root, whose showing is the hub opening; null until loaded.</summary>
        private FrameworkElement hub;
        private bool hubShown;
        private Game game;

        internal InstallActionControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            if (oldContext != null)
            {
                oldContext.PropertyChanged -= OnGameChanged;
            }

            if (game != null)
            {
                game.PropertyChanged -= OnGameChanged;
            }

            game = newContext;

            // Only while loaded: Unloaded can't unhook a game handed over after it, and Loaded hooks the game it finds.
            if (game != null && IsLoaded)
            {
                game.PropertyChanged += OnGameChanged;
            }

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

            // The queue changing is the game being queued, starting to download, or leaving the queue unfinished.
            // Each -= first, so nothing is hooked twice: a game handed over while loading has been already.
            core.MyGames.PropertyChanged -= OnMyGamesChanged;
            core.MyGames.PropertyChanged += OnMyGamesChanged;
            if (game != null)
            {
                game.PropertyChanged -= OnGameChanged;
                game.PropertyChanged += OnGameChanged;
            }

            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.MyGames.PropertyChanged -= OnMyGamesChanged;

            // The game outlives the hub, and would otherwise hold this control for as long as it does. It is kept,
            // unhooked, for Loaded to hook again: Playnite needn't hand it over again if the hub opens on it once more.
            if (game != null)
            {
                game.PropertyChanged -= OnGameChanged;
            }

            if (hub != null)
            {
                hub.IsVisibleChanged -= OnHubVisibleChanged;
                hub = null;
            }

            hubShown = false;
        }

        /// <summary>The game can finish, start or stop installing while its hub is open.</summary>
        private void OnGameChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Game.IsInstalled) || e.PropertyName == nameof(Game.IsInstalling))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Refresh));
            }
        }

        private void OnMyGamesChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MyGamesState.Queue))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Refresh));
            }
        }

        /// <summary>
        /// The hub opening. It is checked again first, so the Dashboard has hidden the right action before the hub is
        /// drawn; then focus is put on Install once Playnite has put it on the hub's first action, whichever it found.
        /// </summary>
        private void OnHubVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            hubShown = hub != null && hub.IsVisible;
            if (!hubShown)
            {
                return;
            }

            Refresh();
            if (Visibility == Visibility.Visible)
            {
                Defer(() =>
                {
                    if (hubShown && IsVisible && (hub.IsKeyboardFocusWithin || FocusIsLost()))
                    {
                        InstallButton.Focus();
                    }
                });
            }
        }

        /// <summary>
        /// Shown in an open hub, from LB or RB to a game it installs: it takes focus from Playnite's action, which the
        /// Dashboard has just hidden. The hub opening is <see cref="OnHubVisibleChanged"/>'s.
        /// </summary>
        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible && hubShown)
            {
                Defer(() =>
                {
                    if (IsVisible && FocusIsLost())
                    {
                        InstallButton.Focus();
                    }
                });
            }
        }

        private void Refresh()
        {
            var label = game == null ? null : core.InstallActionLabel(game.Id);
            var shown = label != null;
            var hadFocus = InstallButton.IsKeyboardFocusWithin;
            InstallButton.Content = label;

            // The theme's content reads the source from the button's DataContext, as Playnite's action's reads its own
            // game's, and shows it under the label only for a game neither installed nor installing. Playnite never sees
            // the extension's installs, so its game reads as neither while queued: the button is given that game as
            // installing, so Queued and Installing... carry no subtitle, as Playnite's Installing... doesn't.
            InstallButton.DataContext = shown ? new ActionGame(game, installing: label != InstallLabel) : null;
            Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            // Hidden with focus, from LB or RB to a game it doesn't install: Playnite's action, shown again, takes it.
            if (!shown && hadFocus && hubShown)
            {
                Defer(() => MainAction()?.Focus());
            }
        }

        /// <summary>Focus nowhere, on something hidden, or on Playnite's action, which the Dashboard hides while this shows.</summary>
        private bool FocusIsLost()
        {
            var focused = Keyboard.FocusedElement as UIElement;
            return focused == null || !focused.IsVisible || focused == MainAction();
        }

        private FrameworkElement MainAction() => HomeRows.FindPart(this, "PART_ButtonContext");

        /// <summary>
        /// After the bindings and Playnite's own focus have settled, and before any input waiting: Loaded runs ahead of
        /// Input, so a press as the hub opens still lands on what this focuses.
        /// </summary>
        private void Defer(Action action) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, action);

        // The keyboard's Enter and the mouse; the pad's A arrives as OnPreviewKeyDown.
        private void OnClick(object sender, RoutedEventArgs e) => Install();

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                Install();
            }
        }

        /// <summary>Only Install acts; Queued and Installing... do nothing, as Playnite's Installing... starts no second install.</summary>
        private void Install()
        {
            if (game != null && IsVisible && core.InstallActionLabel(game.Id) == InstallLabel)
            {
                core.InstallGame(game.Id);
            }
        }

        /// <summary>
        /// The game as the theme's main action content reads it (XboxHubMainActionContent): the same property names as
        /// Playnite's own game, its install state as the queue has it. Remade on each refresh, so it needs no notifying.
        /// </summary>
        private sealed class ActionGame
        {
            public ActionGame(Game game, bool installing)
            {
                Source = game.Source;
                SourceId = game.SourceId;
                IsInstalled = game.IsInstalled;
                IsInstalling = installing || game.IsInstalling;
            }

            public GameSource Source { get; }
            public Guid SourceId { get; }
            public bool IsInstalled { get; }
            public bool IsInstalling { get; }
        }
    }
}
