using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: the navigation sound as focus lands on the Game hub's page toggle or its description's scroller.
    /// Playnite sounds focus only on its buttons, lists and form controls, which the hub's cards are; a toggle
    /// (other than a checkbox) and a scroller are left silent.
    /// </summary>
    internal static class GameHubFocusSounds
    {
        private const string GameDetailsType = "Playnite.FullscreenApp.Controls.Views.GameDetails";

        /// <summary>Class handlers last for the app's life, so this runs once.</summary>
        public static void Register(ISoundPlayer sounds)
        {
            var handler = new RoutedEventHandler((sender, e) => OnGotFocus(sounds, sender, e));
            EventManager.RegisterClassHandler(typeof(ToggleButton), UIElement.GotFocusEvent, handler);
            EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.GotFocusEvent, handler);
        }

        private static void OnGotFocus(ISoundPlayer sounds, object sender, RoutedEventArgs e)
        {
            // Only the element that took focus, not each of its ancestors as the event bubbles past them.
            if (sender != e.OriginalSource || sender is CheckBox || !((UIElement)sender).IsVisible || !InGameHub((DependencyObject)sender))
            {
                return;
            }

            sounds.Play(UiSound.Navigate);
        }

        private static bool InGameHub(DependencyObject element)
        {
            for (var node = element; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node.GetType().FullName == GameDetailsType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
