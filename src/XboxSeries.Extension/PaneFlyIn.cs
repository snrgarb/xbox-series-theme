using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The console's change of the content beside a page's nav, as focus moves along it: the old content goes at once,
    /// and the new comes in a short way, up from below as focus moves down the nav or down from above as it moves up,
    /// fading in as it slows to a stop. Shared by My games &amp; apps and Settings; Full library's tabs, across the page,
    /// bring theirs in from the side.
    /// </summary>
    internal static class PaneFlyIn
    {
        /// <summary>How far from where it settles the new content starts, at 1080p.</summary>
        private const double Travel = 50;

        /// <summary>How long it takes to settle, as on the console.</summary>
        private static readonly Duration SlideDuration = TimeSpan.FromMilliseconds(230);

        // Each element's slide, so a newer one takes it over and an older one ending leaves it alone.
        private static readonly ConditionalWeakTable<UIElement, Slide> slides = new ConditionalWeakTable<UIElement, Slide>();

        /// <summary>
        /// Shows <paramref name="now"/> in place of what <paramref name="shown"/> held, and keeps it there. Reached by
        /// focus moving <paramref name="alongNav"/>, <paramref name="host"/>, the page's own content, and any
        /// <paramref name="alongside"/> it come in from below for a move down the nav or from above for one up it, by
        /// where <paramref name="indexOf"/> puts each on the nav; reached any other way, as the page opening, they are
        /// just there.
        /// </summary>
        public static void Follow<T>(ref T shown, T now, Func<T, int> indexOf, bool alongNav, UIElement host, UIElement alongside = null)
        {
            var direction = Direction(ref shown, now, indexOf, alongNav);
            if (direction == 0)
            {
                Stop(host, alongside);
                return;
            }

            new Slide(host).Start(new Vector(0, direction * Travel), host, alongside);
        }

        /// <summary>
        /// As <see cref="Follow{T}"/>, for tabs across the page: <paramref name="pane"/> comes in from the right for a move
        /// right along them or from the left for one left. It is drawn as it moves, not as a picture: it's Playnite's main list.
        /// </summary>
        public static void FollowAcross<T>(ref T shown, T now, Func<T, int> indexOf, bool alongTabs, UIElement pane)
        {
            var direction = Direction(ref shown, now, indexOf, alongTabs);
            if (direction == 0)
            {
                Stop(pane);
                return;
            }

            new Slide(null).Start(new Vector(direction * Travel, 0), pane);
        }

        /// <summary>Which way along the nav or tabs <paramref name="now"/> is from what was shown, 0 if not reached along them.</summary>
        private static int Direction<T>(ref T shown, T now, Func<T, int> indexOf, bool along)
        {
            var from = shown;
            shown = now;
            return along ? Math.Sign(indexOf(now) - indexOf(from)) : 0;
        }

        /// <summary>Puts <paramref name="panes"/> where they settle at once, as when the page opens, closes or goes.</summary>
        public static void Stop(params UIElement[] panes)
        {
            foreach (var pane in panes)
            {
                if (pane != null && slides.TryGetValue(pane, out var slide))
                {
                    slide.Cancel();
                }
            }
        }

        /// <summary>
        /// The pixels on screen to one of <paramref name="pane"/>'s units: the 4K screen's 2 to a 1080p one, at whatever
        /// scale the Dashboard is drawn.
        /// </summary>
        private static double PixelsPerUnit(UIElement pane)
        {
            var source = PresentationSource.FromVisual(pane);
            if (source?.RootVisual == null || source.CompositionTarget == null)
            {
                return 1;
            }

            var toRoot = pane.TransformToAncestor(source.RootVisual);
            var unit = toRoot.Transform(new Point(0, 1)) - toRoot.Transform(new Point(0, 0));
            return unit.Length * source.CompositionTarget.TransformToDevice.M22;
        }

        /// <summary>
        /// One element on the move: shifted by a translate of its own, after any transform it already had, which is put
        /// back as it was when it settles.
        /// </summary>
        private sealed class Moving
        {
            public readonly UIElement Pane;
            public readonly TranslateTransform Shift;
            private readonly object ownTransform;

            public Moving(UIElement pane, Vector from)
            {
                Pane = pane;
                ownTransform = pane.ReadLocalValue(UIElement.RenderTransformProperty);
                Shift = new TranslateTransform(from.X, from.Y);
                var own = pane.RenderTransform;
                pane.RenderTransform = own == null || own == Transform.Identity
                    ? (Transform)Shift
                    : new TransformGroup { Children = { own, Shift } };
            }

            public void Rest()
            {
                Pane.BeginAnimation(UIElement.OpacityProperty, null);
                Pane.Opacity = 1;
                Pane.CacheMode = null;
                if (ownTransform == DependencyProperty.UnsetValue)
                {
                    Pane.ClearValue(UIElement.RenderTransformProperty);
                }
                else
                {
                    Pane.SetValue(UIElement.RenderTransformProperty, ownTransform);
                }
            }
        }

        private sealed class Slide
        {
            private readonly UIElement cached;
            private readonly List<Moving> moving = new List<Moving>();
            private EventHandler pendingStart;

            /// <summary>A slide that draws <paramref name="cached"/>, the page's own content, if any, into a picture as it moves.</summary>
            public Slide(UIElement cached)
            {
                this.cached = cached;
            }

            public void Start(Vector from, params UIElement[] panes)
            {
                foreach (var pane in panes)
                {
                    if (pane == null)
                    {
                        continue;
                    }

                    if (slides.TryGetValue(pane, out var older))
                    {
                        older.Cancel();
                    }

                    slides.Add(pane, this);

                    // Hidden where it starts at once, so the old content goes at once, whatever an older slide had got to.
                    moving.Add(new Moving(pane, from));
                    pane.BeginAnimation(UIElement.OpacityProperty, null);
                    pane.Opacity = 0;

                    // While it slides, the page's own content is drawn once into a picture at the screen's own resolution,
                    // so sharp, and the graphics card moves that picture rather than redrawing it every frame. Playnite's
                    // main list isn't: virtualised, its covers loading and its size changing, it would keep redrawing it.
                    if (pane == cached)
                    {
                        pane.CacheMode = new BitmapCache(PixelsPerUnit(pane)) { SnapsToDevicePixels = true };
                    }
                }

                // As the Guide's wide dialog does, the slide starts once the new content's first frame has been drawn, so
                // it isn't spent while that frame is laid out.
                var frames = 0;
                pendingStart = (_, __) =>
                {
                    if (++frames < 2)
                    {
                        return;
                    }

                    CompositionTarget.Rendering -= pendingStart;
                    pendingStart = null;
                    Run();
                };
                CompositionTarget.Rendering += pendingStart;
            }

            private void Run()
            {
                foreach (var held in moving)
                {
                    var settle = new DoubleAnimation(0, SlideDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    var fade = new DoubleAnimation(1, SlideDuration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    var settling = held;
                    fade.Completed += (_, __) => Settle(settling);
                    held.Shift.BeginAnimation(held.Shift.X != 0 ? TranslateTransform.XProperty : TranslateTransform.YProperty, settle);
                    held.Pane.BeginAnimation(UIElement.OpacityProperty, fade);
                }
            }

            /// <summary>Puts <paramref name="held"/> where it settles, unless a newer slide has taken it over.</summary>
            private void Settle(Moving held)
            {
                if (slides.TryGetValue(held.Pane, out var current) && current == this)
                {
                    slides.Remove(held.Pane);
                    held.Rest();
                }
            }

            /// <summary>
            /// Stops this slide, putting what it still holds where it settles, so nothing is left hidden or out of place
            /// when a newer slide takes over only some of it.
            /// </summary>
            public void Cancel()
            {
                if (pendingStart != null)
                {
                    CompositionTarget.Rendering -= pendingStart;
                    pendingStart = null;
                }

                foreach (var held in moving)
                {
                    Settle(held);
                }
            }
        }
    }
}
