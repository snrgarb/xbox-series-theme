using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: focus back on the game status view as Playnite's window is restored over it, as when Playnite minimised
    /// itself for the game. Playnite puts it back on the view's first Close button (PART_PanelActionButtons), which the
    /// Dashboard's view leaves out so B, not a button, closes it; without this nothing in the view had focus, its B and
    /// Escape never fired, and the pad drove Home, hidden under it.
    /// </summary>
    internal static class GameStatusFocus
    {
        private const string GameStatusType = "Playnite.FullscreenApp.Controls.Views.GameStatus";

        public static void Register(Window main)
        {
            main.StateChanged += (_, __) =>
            {
                if (main.WindowState == WindowState.Minimized)
                {
                    return;
                }

                // After Playnite's own handler, and once the restored window is laid out again.
                main.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => FocusViewHost(main)));
            };
        }

        private static void FocusViewHost(Window main)
        {
            if (Find(main) is Control status && status.IsVisible
                && status.Template?.FindName("PART_ViewHost", status) is UIElement host && !host.IsKeyboardFocusWithin)
            {
                host.Focus();
            }
        }

        private static DependencyObject Find(DependencyObject node)
        {
            if (node.GetType().FullName == GameStatusType)
            {
                return node;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                if (Find(VisualTreeHelper.GetChild(node, i)) is DependencyObject found)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
