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
    /// The Game hub's Install for a game the core installs silently (<see cref="ExtensionCore.CanInstallSilently"/>),
    /// injected beside Playnite's own main action, whose Install would open the client's own install dialog. It shows only
    /// for such a game, and the Dashboard hides Playnite's action while it does; A, Enter or a click asks the core to
    /// install it. Focus goes to it as the hub opens, as it would to Playnite's action, and it hands focus back to that
    /// action when another game's hub, from LB or RB, can't use it. Playnite sounds focus landing on any button, so it
    /// sounds as Play does.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class InstallActionControl : PluginUserControl
    {
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

            game = newContext;
            if (game != null)
            {
                game.PropertyChanged += OnGameChanged;
            }

            // The theme's content reads the source from the button's game, as Playnite's action's reads its own.
            InstallButton.DataContext = game;

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

            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
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
            var shown = game != null && core.CanInstallSilently(game.Id);
            var hadFocus = InstallButton.IsKeyboardFocusWithin;
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

        private void Install()
        {
            if (game != null && IsVisible)
            {
                core.InstallGame(game.Id);
            }
        }
    }
}
