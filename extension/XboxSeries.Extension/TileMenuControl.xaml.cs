using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The tile menu, injected over Home as a custom element. It follows the core's <see cref="HomeState.TileMenu"/>,
    /// centred on the screen, and holds no menu logic: the core drives it from the pad, as it does the Guide.
    /// Focus stays on the tile underneath, or the Game hub's button, so it keeps its ring and size and has focus again
    /// once the menu closes; Playnite's own keys and routed pad presses are swallowed instead, while the core says the
    /// menu holds them (<see cref="ExtensionCore.TileMenuHolds"/>), so nothing behind the menu reacts to them.
    /// It also has Playnite's own ways into its game menu open this one instead (<see cref="GameMenuTakeover"/>).
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class TileMenuControl : PluginUserControl
    {
        private static readonly Duration OpenDuration = TimeSpan.FromMilliseconds(250);
        private static readonly Duration CloseDuration = TimeSpan.FromMilliseconds(150);

        /// <summary>How far the menu flies up as it opens, and back down as it closes, at 1080p.</summary>
        private const double FlyDistance = 80;

        /// <summary>How quickly More options' flyout comes in from the left, and from how far, as the Guide's Flyout.</summary>
        private static readonly Duration FlyoutInDuration = TimeSpan.FromMilliseconds(120);

        private const double FlyoutInDistance = 48;

        /// <summary>The menu's opacity behind its flyout: dimmed, as the Guide's tab behind its Flyout.</summary>
        private const double DimmedBehindFlyout = 0.45;

        private readonly ExtensionCore core;
        private readonly GameMenuTakeover takeover;
        private readonly TranslateTransform rise = new TranslateTransform();
        private readonly TranslateTransform flyoutSlide = new TranslateTransform();
        private Window window;

        /// <summary>Playnite's Game hub, whose template is applied, with its ways into the game menu, as it first shows.</summary>
        private FrameworkElement gameHub;
#if DEBUG
        private readonly HashSet<Key> debugKeysDown = new HashSet<Key>();
#endif

        internal TileMenuControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            takeover = new GameMenuTakeover(core);
            DataContext = core.Home;
            Panel.Core = core;
            Panel.RenderTransform = rise;
            FlyoutPanel.Core = core;
            FlyoutPanel.RenderTransform = flyoutSlide;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged += OnHomeChanged;
            window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewKeyDown += OnWindowKeyDown;
                window.PreviewKeyUp += OnWindowKeyUp;
                window.Deactivated += OnWindowDeactivated;
            }

            InputManager.Current.PreProcessInput += OnPreProcessInput;
            gameHub = HomeRows.FindGameDetails(this);
            if (gameHub != null)
            {
                gameHub.IsVisibleChanged += OnGameHubVisibleChanged;
            }

            TakeOverGameMenu();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged -= OnHomeChanged;
            if (window != null)
            {
                window.PreviewKeyDown -= OnWindowKeyDown;
                window.PreviewKeyUp -= OnWindowKeyUp;
                window.Deactivated -= OnWindowDeactivated;
                window = null;
            }

            InputManager.Current.PreProcessInput -= OnPreProcessInput;
            if (gameHub != null)
            {
                gameHub.IsVisibleChanged -= OnGameHubVisibleChanged;
                gameHub = null;
            }
        }

        private void OnGameHubVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                TakeOverGameMenu();
            }
        }

        /// <summary>Once Playnite has bound its game menu, after the window's templates and its own focus have settled.</summary>
        private void TakeOverGameMenu() =>
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => takeover.TakeOver(window, gameHub)));

        /// <summary>
        /// Playnite's routed copy of a pad press the menu holds: cancelled before any handler sees it, so A doesn't click
        /// the Game hub's button under the menu, nor B close the hub, nor Menu open Playnite's game menu. Playnite routes a
        /// press before raising it to the core, so the menu is still up for it; a held button's resends can come once the
        /// press has closed the menu, so the core holds that button until it is released.
        /// </summary>
        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            var input = e.StagingItem.Input;
            if ((input.RoutedEvent == Keyboard.KeyDownEvent || input.RoutedEvent == Keyboard.KeyUpEvent)
                && input is KeyEventArgs key
                && PlayniteInput.PadButtonOf(key) is ControllerInput pad
                && PlayniteInput.ToButton(pad) is ControllerButton button
                && core.TileMenuHolds(button))
            {
                e.Cancel();
            }
        }

        private void OnHomeChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HomeState.TileMenuFlyout))
            {
                ShowFlyout(core.Home.TileMenuFlyout);
                return;
            }

            if (e.PropertyName != nameof(HomeState.TileMenu))
            {
                return;
            }

            var menu = core.Home.TileMenu;
            if (menu != null)
            {
                Open(menu);
            }
            else if (Root.Visibility == Visibility.Visible)
            {
                Close();
            }
        }

        /// <summary>The screen dims at once, and the menu flies up into place, fading in.</summary>
        private void Open(Dialog menu)
        {
            Panel.DataContext = menu;
            Root.Visibility = Visibility.Visible;
            Dim.Visibility = Visibility.Visible;
            Root.IsHitTestVisible = true;
            Fly(0, 1, OpenDuration, EasingMode.EaseOut, from: FlyDistance, completed: null);
        }

        /// <summary>
        /// More options' flyout: in from the left, fading in, the menu dimmed behind it; a prompt in its place (Remove)
        /// takes the same spot at once. Gone, the menu is lit again.
        /// </summary>
        private void ShowFlyout(Dialog flyout)
        {
            var wasShown = FlyoutPanel.DataContext != null;
            FlyoutPanel.DataContext = flyout;
            MenuLayer.Opacity = flyout == null ? 1 : DimmedBehindFlyout;
            if (flyout == null || wasShown)
            {
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            flyoutSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-FlyoutInDistance, 0, Motion.Of(FlyoutInDuration)) { EasingFunction = ease });
            FlyoutPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Motion.Of(FlyoutInDuration)));
        }

        /// <summary>
        /// The opposite: the dim goes at once, and the menu flies back down, fading out. It keeps showing what it held as
        /// it goes, and nothing on it can be clicked meanwhile.
        /// </summary>
        private void Close()
        {
            Dim.Visibility = Visibility.Collapsed;
            Root.IsHitTestVisible = false;
            Fly(FlyDistance, 0, CloseDuration, EasingMode.EaseIn, from: null, completed: () =>
            {
                // Reopened while closing: the open animation has taken over.
                if (core.Home.TileMenu == null)
                {
                    Root.Visibility = Visibility.Collapsed;
                    Panel.DataContext = null;
                }
            });
        }

        private void Fly(double riseTo, double opacityTo, Duration duration, EasingMode easing, double? from, Action completed)
        {
            // With Reduce motion on, no time long: the menu is there, or gone, at once, and closing still ends.
            duration = Motion.Of(duration);
            var ease = new CubicEase { EasingMode = easing };
            var move = new DoubleAnimation(riseTo, duration) { EasingFunction = ease };
            var fade = new DoubleAnimation(opacityTo, duration);

            // Opening starts from below and clear; closing goes on from wherever it is, so a quick close never jumps.
            if (from != null)
            {
                move.From = from;
                fade.From = 0;
            }

            if (completed != null)
            {
                move.Completed += (_, __) => completed();
            }

            rise.BeginAnimation(TranslateTransform.YProperty, move);
            Panel.BeginAnimation(OpacityProperty, fade);
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
#if DEBUG
            // Debug builds only: M on a tile stands in for Menu, for testing without a controller.
            if (core.Home.TileMenu == null && !e.IsRepeat && e.Key == Key.M)
            {
                debugKeysDown.Add(e.Key);
                core.OnControllerButton(ControllerButton.Start, ButtonState.Pressed);
            }
#endif
            if (core.Home.TileMenu == null)
            {
                return;
            }

            // Playnite sends the pad's D-pad and A here as keys too; the core already has them from the pad.
            e.Handled = true;
            if (e.Key == Key.Escape)
            {
                core.CloseTileMenu();
            }
#if DEBUG
            else if (!e.IsRepeat && e.Key != Key.M && PlayniteInput.DebugButtonFor(e.Key) is ControllerButton button)
            {
                debugKeysDown.Add(e.Key);
                core.OnControllerButton(button, ButtonState.Pressed);
            }
#endif
        }

        private void OnWindowKeyUp(object sender, KeyEventArgs e)
        {
#if DEBUG
            if (debugKeysDown.Remove(e.Key) && PlayniteInput.DebugButtonFor(e.Key) is ControllerButton button)
            {
                core.OnControllerButton(button, ButtonState.Released);
            }
#endif
        }

        /// <summary>Playnite left the foreground: the pad no longer belongs to the menu.</summary>
        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            core.CloseTileMenu();
        }

        private void OnDimClick(object sender, MouseButtonEventArgs e)
        {
            core.CloseTileMenu();
        }
    }
}
