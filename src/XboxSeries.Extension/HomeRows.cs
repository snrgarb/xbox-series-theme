using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Home's two screens: the Recent row at rest, and the Groups below it. Moving down from the Recent row flies it
    /// up and out as the Groups fly in from below, fading; moving up from the first Group flies them back. Shared by
    /// the rows of one Home, which each show or hide themselves as it changes.
    /// </summary>
    internal sealed class HomeScreen
    {
        public event EventHandler Changed;

        public bool ShowsGroups { get; private set; }

        /// <summary>Whether the Dashboard has placed the Groups, so that there is a screen to move down to.</summary>
        public bool HasGroups { get; set; }

        /// <summary>
        /// Which of Home's rows (the Recent row or the Groups) last held focus, so that the one the Game hub was opened
        /// from takes focus back when the hub closes.
        /// </summary>
        public object LastFocusedRow { get; set; }

        public void Show(bool groups)
        {
            if (ShowsGroups != groups)
            {
                ShowsGroups = groups;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>What Home's rows share: moving focus with the D-pad, flying in and out, and finding their place in the Dashboard.</summary>
    internal static class HomeRows
    {
        private static readonly Duration FlyDuration = TimeSpan.FromMilliseconds(300);

        /// <summary>How far a row travels as it flies in or out, in screen pixels at 1080p.</summary>
        public const double FlyDistance = 140;

        /// <summary>Moves focus with the D-pad (arrow keys) to the nearest element that way. Leaves keys with nowhere to go alone.</summary>
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

        /// <summary>
        /// Flies <paramref name="row"/> in, or out toward <paramref name="hiddenOffset"/> (up when negative), fading as it
        /// goes. A row that is out can't be focused or clicked, so focus never lands on a tile no one can see.
        /// </summary>
        public static void Fly(FrameworkElement row, bool shown, double hiddenOffset, bool animate = true)
        {
            if (!(row.RenderTransform is TranslateTransform transform))
            {
                row.RenderTransform = transform = new TranslateTransform();
            }

            var opacity = shown ? 1.0 : 0.0;
            var offset = shown ? 0.0 : hiddenOffset;
            if (animate)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                row.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(opacity, FlyDuration) { EasingFunction = ease });
                transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offset, FlyDuration) { EasingFunction = ease });
            }
            else
            {
                row.BeginAnimation(UIElement.OpacityProperty, null);
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                row.Opacity = opacity;
                transform.Y = offset;
            }

            row.IsHitTestVisible = shown;
            if (shown)
            {
                row.IsEnabled = true;
            }
            else
            {
                // Disabled once the row coming in has taken focus: disabling the focused row first would drop focus.
                row.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (!row.IsHitTestVisible)
                    {
                        row.IsEnabled = false;
                    }
                }));
            }
        }

        /// <summary>Playnite's Game details view in the Main template a row sits in; null if the theme has none.</summary>
        public static FrameworkElement FindGameDetails(DependencyObject row) => FindPart(row, "PART_ElemGameDetails");

        /// <summary>The element called <paramref name="name"/> in the template <paramref name="element"/> sits in; null if there is none.</summary>
        public static FrameworkElement FindPart(DependencyObject element, string name)
        {
            for (var node = element; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is Control control && control.Template?.FindName(name, control) is FrameworkElement part)
                {
                    return part;
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
