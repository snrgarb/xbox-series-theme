using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Playnite.SDK.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The tile menu, injected over Home as a custom element. It follows the core's <see cref="HomeState.TileMenu"/>,
    /// placing it on the focused tile, and holds no menu logic: the core drives it from the pad, as it does the Guide.
    /// Focus stays on the tile underneath, so the tile keeps its ring and size; Playnite's own keys for the pad are
    /// swallowed instead, so nothing behind the menu reacts to them.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class TileMenuControl : PluginUserControl
    {
        /// <summary>How far the menu rises above its tile, so the title sits over the tile's top edge and the rows on it.</summary>
        private const double TitleRise = 38;

        /// <summary>The menu keeps at least this far from the screen's edges.</summary>
        private const double EdgeMargin = 40;

        private readonly ExtensionCore core;
        private Window window;
        private FrameworkElement tile;
#if DEBUG
        private readonly HashSet<Key> debugKeysDown = new HashSet<Key>();
#endif

        internal TileMenuControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            DataContext = core.Home;
            Panel.Core = core;
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
            if (e.PropertyName != nameof(HomeState.TileMenu) || core.Home.TileMenu == null)
            {
                return;
            }

            // The tile Menu was pressed on still has focus. Hidden until placed, once the new menu has been laid out.
            tile = Keyboard.FocusedElement as FrameworkElement;
            Panel.Opacity = 0;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PlaceOnTile));
        }

        /// <summary>Puts the menu on its tile, left edges together, kept on screen; centred if the tile can't be found.</summary>
        private void PlaceOnTile()
        {
            if (core.Home.TileMenu == null)
            {
                return;
            }

            var size = new Size(Panel.ActualWidth, Panel.ActualHeight);
            var left = (ActualWidth - size.Width) / 2;
            var top = (ActualHeight - size.Height) / 2;
            if (tile != null && tile.IsVisible && Window.GetWindow(tile) == window)
            {
                var bounds = tile.TransformToVisual(this).TransformBounds(new Rect(tile.RenderSize));
                left = bounds.Left;
                top = bounds.Top - TitleRise;
            }

            Canvas.SetLeft(Panel, Clamp(left, EdgeMargin, ActualWidth - size.Width - EdgeMargin));
            Canvas.SetTop(Panel, Clamp(top, EdgeMargin, ActualHeight - size.Height - EdgeMargin));
            Panel.Opacity = 1;
        }

        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

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
