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

        /// <summary>How far below its place the row starts as Home is revealed, and how far too high it rises, at 1080p.</summary>
        private const double RevealRiseDistance = 90;
        private const double RevealOvershoot = 12;

        /// <summary>
        /// The reveal's row: risen to its highest at <see cref="RevealRisen"/>, settled at <see cref="RevealSettled"/>, faded
        /// in over <see cref="RevealFade"/>. The top bar's buttons start down <see cref="TopBarDelay"/> after the row
        /// settles, from above the screen's top (the bar sits 84 below it and is 60 tall), and land after <see cref="TopBarDrop"/>.
        /// </summary>
        private static readonly TimeSpan RevealRisen = TimeSpan.FromMilliseconds(400);
        private static readonly TimeSpan RevealSettled = TimeSpan.FromMilliseconds(650);
        private static readonly TimeSpan RevealFade = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan TopBarDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan TopBarDrop = TimeSpan.FromMilliseconds(400);
        private const double TopBarDropDistance = 150;

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
            var transform = Translate(row);

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

        /// <summary>
        /// Home revealed from black as Start-up ends, as on the console: <paramref name="row"/> (the Recent row and the
        /// placeholder tiles under it) flies up into place from a little below, fading in, rises a little too high and
        /// drops back to settle; a moment after, the top bar's buttons (My games &amp; apps, Search and Settings) drop in
        /// from above the screen's top, fading in. Until then they are disabled as well as unseen, so that Up from the
        /// row can't focus one no one can see. Both start where they start at once, while the Start-up window still
        /// covers Home black, so nothing at rest shows first. A <see cref="Fly"/> during it, as Down to the Groups,
        /// takes the row over from wherever it has got to.
        /// </summary>
        /// <remarks>
        /// Every animation is of a render transform, opacity or IsEnabled, never layout, and stops when it ends, leaving
        /// each element's own value, which is where it rests, so nothing is left hidden or disabled whatever happens to
        /// the row meanwhile. The top bar is the Dashboard's, found by its Search button's name; without it, only the row flies.
        /// </remarks>
        public static void Reveal(FrameworkElement row)
        {
            Fly(row, shown: true, hiddenOffset: 0, animate: false);
            var rise = new DoubleAnimationUsingKeyFrames { Duration = RevealSettled, FillBehavior = FillBehavior.Stop };
            rise.KeyFrames.Add(new DiscreteDoubleKeyFrame(RevealRiseDistance, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            rise.KeyFrames.Add(new EasingDoubleKeyFrame(-RevealOvershoot, KeyTime.FromTimeSpan(RevealRisen), new CubicEase { EasingMode = EasingMode.EaseOut }));
            rise.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(RevealSettled), new SineEase { EasingMode = EasingMode.EaseInOut }));
            Translate(row).BeginAnimation(TranslateTransform.YProperty, rise);
            row.BeginAnimation(UIElement.OpacityProperty, FadeIn(TimeSpan.Zero, RevealFade));

            // The StackPanel holding the top bar's middle buttons. Left alone with focus in it, which disabling would drop.
            var topBar = FindPart(row, "XboxTopBarSearch") is FrameworkElement search ? VisualTreeHelper.GetParent(search) as FrameworkElement : null;
            if (topBar == null || topBar.IsKeyboardFocusWithin)
            {
                return;
            }

            var dropAt = RevealSettled + TopBarDelay;
            var drop = new DoubleAnimationUsingKeyFrames { Duration = dropAt + TopBarDrop, FillBehavior = FillBehavior.Stop };
            drop.KeyFrames.Add(new DiscreteDoubleKeyFrame(-TopBarDropDistance, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            drop.KeyFrames.Add(new DiscreteDoubleKeyFrame(-TopBarDropDistance, KeyTime.FromTimeSpan(dropAt)));
            drop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(dropAt + TopBarDrop), new CubicEase { EasingMode = EasingMode.EaseOut }));
            Translate(topBar).BeginAnimation(TranslateTransform.YProperty, drop);
            topBar.BeginAnimation(UIElement.OpacityProperty, FadeIn(dropAt, RevealFade));
            var disabled = new BooleanAnimationUsingKeyFrames { Duration = dropAt, FillBehavior = FillBehavior.Stop };
            disabled.KeyFrames.Add(new DiscreteBooleanKeyFrame(false, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            topBar.BeginAnimation(UIElement.IsEnabledProperty, disabled);
        }

        /// <summary>Unseen until <paramref name="after"/>, then fading in over <paramref name="fade"/>; the element's own opacity once done.</summary>
        private static AnimationTimeline FadeIn(TimeSpan after, TimeSpan fade)
        {
            var fadeIn = new DoubleAnimationUsingKeyFrames { Duration = after + fade, FillBehavior = FillBehavior.Stop };
            fadeIn.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fadeIn.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(after)));
            fadeIn.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(after + fade), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            return fadeIn;
        }

        /// <summary>The translate <paramref name="element"/> is moved by, shared by its flies and its reveal; made the first time.</summary>
        private static TranslateTransform Translate(UIElement element)
        {
            if (!(element.RenderTransform is TranslateTransform transform))
            {
                element.RenderTransform = transform = new TranslateTransform();
            }

            return transform;
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
