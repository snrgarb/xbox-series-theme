using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The console's loading spinner, as the core's <see cref="Spinner"/> shapes it: a white arc with a green tail chasing
    /// round a ring that fills the control, its stroke inside the control's bounds, with round ends unless set and no
    /// track ring under it; resting at the top between turns unless <see cref="Unbroken"/>. It turns only while it is visible, starting each time it shows and stopping once it is hidden or
    /// unloaded, so it never keeps WPF rendering behind Playnite.
    /// </summary>
    public sealed class LoadingSpinner : FrameworkElement
    {
        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(7.0, FrameworkPropertyMetadataOptions.AffectsRender, OnPenChanged));

        /// <summary>The white arc, at the head. White unless set.</summary>
        public static readonly DependencyProperty HeadBrushProperty = DependencyProperty.Register(
            nameof(HeadBrush), typeof(Brush), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender, OnPenChanged));

        /// <summary>The green arc, the tail. The theme's GlyphLightBrush unless set.</summary>
        public static readonly DependencyProperty TailBrushProperty = DependencyProperty.Register(
            nameof(TailBrush), typeof(Brush), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPenChanged));

        /// <summary>How the arcs' ends are drawn. Round unless set.</summary>
        public static readonly DependencyProperty LineCapProperty = DependencyProperty.Register(
            nameof(LineCap), typeof(PenLineCap), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(PenLineCap.Round, FrameworkPropertyMetadataOptions.AffectsRender, OnPenChanged));

        /// <summary>Whether it turns on without resting, as <see cref="Spinner.Unbroken"/>. False unless set.</summary>
        public static readonly DependencyProperty UnbrokenProperty = DependencyProperty.Register(
            nameof(Unbroken), typeof(bool), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnUnbrokenChanged));

        /// <summary>Seconds into the spinning, run from 0 to one turn over and over while visible.</summary>
        private static readonly DependencyProperty ElapsedProperty = DependencyProperty.Register(
            "Elapsed", typeof(double), typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private Pen headPen;
        private Pen tailPen;
        private bool spinning;

        public LoadingSpinner()
        {
            Focusable = false;
            IsHitTestVisible = false;
            // Shown before it is loaded, it starts once loaded; loaded again while still shown, it starts again.
            IsVisibleChanged += (_, __) => UpdateSpinning();
            Loaded += (_, __) => UpdateSpinning();
            Unloaded += (_, __) => UpdateSpinning();
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public Brush HeadBrush
        {
            get => (Brush)GetValue(HeadBrushProperty);
            set => SetValue(HeadBrushProperty, value);
        }

        public Brush TailBrush
        {
            get => (Brush)GetValue(TailBrushProperty);
            set => SetValue(TailBrushProperty, value);
        }

        public PenLineCap LineCap
        {
            get => (PenLineCap)GetValue(LineCapProperty);
            set => SetValue(LineCapProperty, value);
        }

        public bool Unbroken
        {
            get => (bool)GetValue(UnbrokenProperty);
            set => SetValue(UnbrokenProperty, value);
        }

        /// <summary>The time one turn takes, its rest included unless it is unbroken.</summary>
        private TimeSpan Period => Unbroken ? Spinner.UnbrokenPeriod : Spinner.Period;

        /// <summary>Turning, it starts its turns again at the new length.</summary>
        private static void OnUnbrokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var spinner = (LoadingSpinner)d;
            if (spinner.spinning)
            {
                spinner.Spin();
            }
        }

        private static void OnPenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var spinner = (LoadingSpinner)d;
            spinner.headPen = null;
            spinner.tailPen = null;
        }

        /// <summary>Turning from the start of a turn while visible and loaded; still, and drawing nothing new, otherwise.</summary>
        private void UpdateSpinning()
        {
            var shouldSpin = IsVisible && IsLoaded;
            if (shouldSpin == spinning)
            {
                return;
            }

            spinning = shouldSpin;
            if (spinning)
            {
                Spin();
            }
            else
            {
                BeginAnimation(ElapsedProperty, null);
            }
        }

        /// <summary>Turns from the start of a turn, over and over.</summary>
        private void Spin()
        {
            // At the screen's own rate: Signing you in's is 404 px across, and at 30 frames a second it stutters.
            BeginAnimation(ElapsedProperty, new DoubleAnimation(0, Period.TotalSeconds, new Duration(Period))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            });
        }

        /// <summary>
        /// The theme's GlyphLightBrush for the tail unless one is set, looked up only now, in its window: looked up as it
        /// is made, with no window yet, it comes from Playnite's application resources, which belong to the UI thread,
        /// where the Start-up window, on a thread of its own, has a copy of its own (StartupResources).
        /// </summary>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            if (ReadLocalValue(TailBrushProperty) == DependencyProperty.UnsetValue)
            {
                SetResourceReference(TailBrushProperty, "GlyphLightBrush");
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var thickness = StrokeThickness;
            var radius = (Math.Min(RenderSize.Width, RenderSize.Height) - thickness) / 2;
            if (radius <= 0 || thickness <= 0)
            {
                return;
            }

            var centre = new Point(RenderSize.Width / 2, RenderSize.Height / 2);
            var elapsed = TimeSpan.FromSeconds((double)GetValue(ElapsedProperty));
            var arcs = Unbroken ? Spinner.Unbroken(elapsed) : Spinner.At(elapsed);

            if (TailBrush != null)
            {
                tailPen = tailPen ?? MakePen(TailBrush, thickness, LineCap);
                // A degree on under the white, so no seam shows where square ends meet.
                DrawArc(drawingContext, tailPen, centre, radius, arcs.GreenTail, Math.Min(arcs.WhiteTail + 1, arcs.Head));
            }

            if (HeadBrush != null)
            {
                headPen = headPen ?? MakePen(HeadBrush, thickness, LineCap);
                DrawArc(drawingContext, headPen, centre, radius, arcs.WhiteTail, arcs.Head);
            }
        }

        private static Pen MakePen(Brush brush, double thickness, PenLineCap cap)
        {
            var pen = new Pen(brush, thickness) { StartLineCap = cap, EndLineCap = cap };
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            return pen;
        }

        /// <summary>
        /// The arc clockwise from <paramref name="from"/> to <paramref name="to"/>, in degrees from the top; one of no
        /// length, as the green tail is at the start and end of a turn, is nothing. The white arc is never shorter than
        /// the spinner's dot, nor either arc a whole turn.
        /// </summary>
        private static void DrawArc(DrawingContext dc, Pen pen, Point centre, double radius, double from, double to)
        {
            var sweep = to - from;
            if (sweep <= 0.01)
            {
                return;
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(PointAt(centre, radius, from), false, false);
                context.ArcTo(PointAt(centre, radius, to), new Size(radius, radius), 0, sweep > 180,
                              SweepDirection.Clockwise, true, false);
            }

            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        /// <summary>The point on the ring <paramref name="degrees"/> clockwise from the top.</summary>
        private static Point PointAt(Point centre, double radius, double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return new Point(centre.X + radius * Math.Sin(radians), centre.Y - radius * Math.Cos(radians));
        }
    }
}
