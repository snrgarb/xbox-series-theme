using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Which of Home's rows (the Recent row or the Groups) last held focus, so that the one the Game hub was opened
    /// from takes focus back when the hub closes. Shared by the rows of one Home.
    /// </summary>
    internal sealed class HomeFocus
    {
        public object LastFocusedRow { get; set; }
    }

    /// <summary>What Home's rows share: moving focus with the D-pad, and finding their place in the Dashboard.</summary>
    internal static class HomeRows
    {
        /// <summary>
        /// Moves focus with the D-pad (arrow keys) ahead of any ScrollViewer around the rows: one scrolls a line rather
        /// than move focus to a tile out of its view, so the next Group could never be reached. Leaves keys with
        /// nowhere to go alone.
        /// </summary>
        public static void MoveFocus(KeyEventArgs e)
        {
            FocusNavigationDirection direction;
            switch (e.Key)
            {
                case Key.Left: direction = FocusNavigationDirection.Left; break;
                case Key.Right: direction = FocusNavigationDirection.Right; break;
                case Key.Up: direction = FocusNavigationDirection.Up; break;
                case Key.Down: direction = FocusNavigationDirection.Down; break;
                default: return;
            }

            if (Keyboard.FocusedElement is UIElement focused && focused.PredictFocus(direction) is UIElement next && next.Focus())
            {
                e.Handled = true;
            }
        }

        /// <summary>The ScrollViewer the Dashboard scrolls Home's rows in; null when it has none.</summary>
        public static ScrollViewer PageScroller(DependencyObject row)
        {
            for (var node = VisualTreeHelper.GetParent(row); node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is ScrollViewer scroller)
                {
                    return scroller;
                }
            }

            return null;
        }

        /// <summary>Playnite's Game details view in the Main template a row sits in; null if the theme has none.</summary>
        public static FrameworkElement FindGameDetails(DependencyObject row)
        {
            for (var node = row; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is Control control && control.Template?.FindName("PART_ElemGameDetails", control) is FrameworkElement details)
                {
                    return details;
                }
            }

            return null;
        }

        public static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                var nested = FindChild<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
