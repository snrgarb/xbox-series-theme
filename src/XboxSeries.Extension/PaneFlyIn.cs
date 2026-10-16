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
    /// fading in as it slows to a stop. Shared by My games &amp; apps and Settings.
    /// </summary>
    internal static class PaneFlyIn
    {
        /// <summary>How far from where it settles the new content starts, at 1080p.</summary>
        public const double Travel = 50;

        /// <summary>How long it takes to settle, as on the console.</summary>
        private static readonly Duration SlideDuration = TimeSpan.FromMilliseconds(230);

        // Each element's slide, so a newer one takes it over and an older one ending leaves it alone.
        private static readonly ConditionalWeakTable<UIElement, Slide> slides = new ConditionalWeakTable<UIElement, Slide>();

        /// <summary>
        /// Brings <paramref name="panes"/> in, from below for a <paramref name="direction"/> down the nav (positive) or
        /// from above for one up it (negative), starting over from wherever any slide before it had got to.
        /// </summary>
        public static void Play(int direction, params UIElement[] panes)
        {
            if (direction == 0)
            {
                Stop(panes);
                return;
            }

            new Slide(panes).Start(Math.Sign(direction) * Travel);
        }

        /// <summary>Puts <paramref name="panes"/> where they settle at once, as when the page opens or closes.</summary>
        public static void Stop(params UIElement[] panes)
        {
            foreach (var pane in panes)
            {
                if (pane == null)
                {
                    continue;
                }

                if (slides.TryGetValue(pane, out var slide))
                {
                    slide.Cancel();
                }

                AtRest(pane);
            }
        }

        private static void AtRest(UIElement pane)
        {
            pane.BeginAnimation(UIElement.OpacityProperty, null);
            pane.Opacity = 1;
            if (pane.RenderTransform is TranslateTransform shift)
            {
                shift.BeginAnimation(TranslateTransform.YProperty, null);
                shift.Y = 0;
            }

            pane.CacheMode = null;
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

        private sealed class Slide
        {
            private readonly List<UIElement> panes = new List<UIElement>();
            private EventHandler pendingStart;

            public Slide(IEnumerable<UIElement> panes)
            {
                foreach (var pane in panes)
                {
                    if (pane != null)
                    {
                        this.panes.Add(pane);
                    }
                }
            }

            public void Start(double from)
            {
                foreach (var pane in panes)
                {
                    if (slides.TryGetValue(pane, out var older))
                    {
                        older.Cancel();
                    }

                    slides.Add(pane, this);

                    if (!(pane.RenderTransform is TranslateTransform) || pane.RenderTransform.IsFrozen)
                    {
                        pane.RenderTransform = new TranslateTransform();
                    }

                    // Hidden where it starts at once, so the old content goes at once, whatever an older slide had got to.
                    pane.BeginAnimation(UIElement.OpacityProperty, null);
                    pane.Opacity = 0;
                    var shift = (TranslateTransform)pane.RenderTransform;
                    shift.BeginAnimation(TranslateTransform.YProperty, null);
                    shift.Y = from;

                    // While it slides, it is drawn once into a picture at the screen's own resolution, so sharp, and the
                    // graphics card moves that picture rather than redrawing the content every frame.
                    pane.CacheMode = new BitmapCache(PixelsPerUnit(pane)) { SnapsToDevicePixels = true };
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
                foreach (var pane in panes)
                {
                    var settle = new DoubleAnimation(0, SlideDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    var fade = new DoubleAnimation(1, SlideDuration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                    var settling = pane;
                    fade.Completed += (_, __) =>
                    {
                        if (slides.TryGetValue(settling, out var current) && current == this)
                        {
                            slides.Remove(settling);
                            AtRest(settling);
                        }
                    };
                    ((TranslateTransform)pane.RenderTransform).BeginAnimation(TranslateTransform.YProperty, settle);
                    pane.BeginAnimation(UIElement.OpacityProperty, fade);
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

                foreach (var pane in panes)
                {
                    if (slides.TryGetValue(pane, out var current) && current == this)
                    {
                        slides.Remove(pane);
                        AtRest(pane);
                    }
                }
            }
        }
    }
}
