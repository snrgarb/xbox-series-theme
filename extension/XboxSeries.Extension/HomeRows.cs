using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Home as one page that scrolls (#206): the top bar, the hero's gap, the Recent row, the wide tiles, then the Groups.
    /// Where it rests follows focus, by the core's <see cref="HomeRests"/>: the page (the Recent row and the Groups, each
    /// moved by <see cref="Scroll"/>) and the top bar (the theme's, moved by <see cref="TopBarScroll"/>, which it binds
    /// through the Recent row) slide there together, or jump with Reduce motion on. Shared by the rows of one Home.
    /// </summary>
    internal sealed class HomePage
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly Duration SlideDuration = TimeSpan.FromMilliseconds(300);

        /// <summary>Where the page rests; null until focus first comes to Home, which rests on the Recent row until then.</summary>
        private HomeFocus? focus;
        private HomeRest rest;

        // Made by the first row to ask, on Playnite's UI thread, where they are animated, rather than with the plugin.
        private TranslateTransform scroll;
        private TranslateTransform topBarScroll;

        /// <summary>The page's rise, shared by the Recent row and the Groups so that they move as one.</summary>
        public TranslateTransform Scroll => scroll ?? (scroll = new TranslateTransform());

        /// <summary>The top bar's rise: none on the Recent row and the wide tiles, with the page from there into the Groups.</summary>
        public TranslateTransform TopBarScroll => topBarScroll ?? (topBarScroll = new TranslateTransform());

        /// <summary>Where focus is, as far as the page goes: the Recent row until focus first comes to Home.</summary>
        public HomeFocus Focus => focus ?? HomeFocus.RecentRow;

        public bool RestsOnGroups => Focus.Kind == HomeRestKind.Group;

        /// <summary>Whether the Dashboard has placed the Groups, so that there is somewhere to move down to.</summary>
        public bool HasGroups => Groups != null;

        /// <summary>The Groups, which say where they are laid out and what each is called; null without them.</summary>
        public IHomeGroupsView Groups { get; set; }

        /// <summary>
        /// Which of Home's rows (the Recent row or the Groups) last held focus, so that the one the Game hub was opened
        /// from takes focus back when the hub closes.
        /// </summary>
        public object LastFocusedRow { get; set; }

        /// <summary>Down from the wide tiles: the Groups take focus, on the first.</summary>
        public event EventHandler GroupsWanted;

        /// <summary>Up from the first Group (true: onto the wide tiles), or B on the Groups (false: onto the Recent row's last game).</summary>
        public event Action<bool> RowWanted;

        public void EnterGroups() => GroupsWanted?.Invoke(this, EventArgs.Empty);

        public void LeaveGroups(bool toWideTiles) => RowWanted?.Invoke(toWideTiles);

        /// <summary>
        /// Focus has come to <paramref name="now"/>: the page slides to its resting point there, saying so in the log
        /// when it is a new one. Called again for the same place, as a rebuilt row takes focus back, it only follows
        /// the layout if that has changed.
        /// </summary>
        public void RestOn(HomeFocus now)
        {
            var layout = Groups?.Layout() ?? new HomePageLayout(1080, null, 0, 0);
            var next = HomeRests.At(now, layout);
            var moved = focus != now;
            if (!moved && next.PageOffset == rest.PageOffset && next.TopBarOffset == rest.TopBarOffset)
            {
                return;
            }

            focus = now;
            rest = next;
            Slide(Scroll, -next.PageOffset);
            Slide(TopBarScroll, -next.TopBarOffset);
            if (moved)
            {
                logger.Info(HomeRests.Says(now, now.Kind == HomeRestKind.Group ? Groups?.NameOf(now.GroupIndex) : null));
            }
        }

        /// <summary>
        /// Slides <paramref name="transform"/> to <paramref name="to"/> in 300 ms with the cubic ease Home has always
        /// moved with, from wherever it has got to; there at once with Reduce motion on.
        /// </summary>
        private static void Slide(TranslateTransform transform, double to)
        {
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(to, Motion.Of(SlideDuration)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    /// <summary>What the page asks of the Groups: where they are laid out, and a Group's name for the log.</summary>
    internal interface IHomeGroupsView
    {
        HomePageLayout Layout();

        string NameOf(int group);
    }

    /// <summary>What Home's rows share: moving focus with the D-pad, the reveal after Start-up, and finding their place in the Dashboard.</summary>
    internal static class HomeRows
    {
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
        /// Puts <paramref name="row"/> where it rests, at once: in place, seen and enabled, stopping whatever an earlier
        /// reveal had it doing.
        /// </summary>
        public static void Settle(FrameworkElement row)
        {
            var transform = Translate(row);
            row.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            row.Opacity = 1;
            transform.Y = 0;
            row.IsHitTestVisible = true;
            row.IsEnabled = true;
        }

        /// <summary>
        /// Home revealed from under Start-up's swirl as it fades, as on the console: <paramref name="row"/> (the Recent
        /// row and the placeholder tiles under it) flies up into place from a little below, fading in, rises a little
        /// too high and drops back to settle; a moment after, the top bar's buttons (My games &amp; apps, Search and
        /// Settings) drop in from above the screen's top, fading in. Until then they are disabled as well as unseen, so
        /// that Up from the row can't focus one no one can see. Both start where they start at once, while the Start-up
        /// window still covers Home, so nothing at rest shows first. The page sliding meanwhile, as on Down to the wide
        /// tiles, moves the row's content within it, so the two add up. With Reduce motion on, the row is in its place
        /// at once and the top bar is left as it rests, there and enabled.
        /// </summary>
        /// <remarks>
        /// Every animation is of a render transform, opacity or IsEnabled, never layout, and stops when it ends, leaving
        /// each element's own value, which is where it rests, so nothing is left hidden or disabled whatever happens to
        /// the row meanwhile. The top bar is the Dashboard's, found by its Search button's name; without it, only the row flies.
        /// </remarks>
        public static void Reveal(FrameworkElement row)
        {
            Settle(row);
            if (Motion.Reduced)
            {
                return;
            }

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

        /// <summary>The translate <paramref name="element"/> is moved by in its reveal; made the first time.</summary>
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
