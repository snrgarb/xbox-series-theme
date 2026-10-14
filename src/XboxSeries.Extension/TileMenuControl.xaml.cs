using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Playnite.SDK.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The tile menu, injected over Home as a custom element. It follows the core's <see cref="HomeState.TileMenu"/>,
    /// centred on the screen, and holds no menu logic: the core drives it from the pad, as it does the Guide.
    /// Focus stays on the tile underneath, so the tile keeps its ring and size; Playnite's own keys for the pad are
    /// swallowed instead, so nothing behind the menu reacts to them.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class TileMenuControl : PluginUserControl
    {
        private static readonly Duration OpenDuration = TimeSpan.FromMilliseconds(250);
        private static readonly Duration CloseDuration = TimeSpan.FromMilliseconds(150);

        /// <summary>How far the menu flies up as it opens, and back down as it closes, at 1080p.</summary>
        private const double FlyDistance = 80;

        private readonly ExtensionCore core;
        private readonly TranslateTransform rise = new TranslateTransform();
        private Window window;
#if DEBUG
        private readonly HashSet<Key> debugKeysDown = new HashSet<Key>();
#endif

        internal TileMenuControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.Home;
            Panel.Core = core;
            Panel.RenderTransform = rise;
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
        }

        private void OnHomeChanged(object sender, PropertyChangedEventArgs e)
        {
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
