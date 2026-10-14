using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
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
        private readonly ExtensionCore core;
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
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
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
            if (window != null)
            {
                window.PreviewKeyDown -= OnWindowKeyDown;
                window.PreviewKeyUp -= OnWindowKeyUp;
                window.Deactivated -= OnWindowDeactivated;
                window = null;
            }
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
