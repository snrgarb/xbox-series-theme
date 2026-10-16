using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The console's loading spinner, as the core's <see cref="Spinner"/> shapes it: a white arc with a green tail chasing
    /// round a ring that fills the control, its stroke inside the control's bounds, with round ends and no track ring
    /// under it. It turns only while it is visible, starting each time it shows and stopping once it is hidden or
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

        /// <summary>Seconds into the spinning, run from 0 to one <see cref="Spinner.Period"/> over and over while visible.</summary>
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
            SetResourceReference(TailBrushProperty, "GlyphLightBrush");
            IsVisibleChanged += (_, __) => UpdateSpinning();
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
                var period = Spinner.Period.TotalSeconds;
                BeginAnimation(ElapsedProperty, new DoubleAnimation(0, period, new Duration(Spinner.Period))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                });
            }
            else
            {
                BeginAnimation(ElapsedProperty, null);
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
            var arcs = Spinner.At(TimeSpan.FromSeconds((double)GetValue(ElapsedProperty)));

            if (TailBrush != null)
            {
                tailPen = tailPen ?? MakePen(TailBrush, thickness);
                DrawArc(drawingContext, tailPen, centre, radius, arcs.GreenTail, arcs.WhiteTail, false);
            }

            if (HeadBrush != null)
            {
                headPen = headPen ?? MakePen(HeadBrush, thickness);
                DrawArc(drawingContext, headPen, centre, radius, arcs.WhiteTail, arcs.Head, true);
            }
        }

        private static Pen MakePen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            return pen;
        }

        /// <summary>
        /// The arc clockwise from <paramref name="from"/> to <paramref name="to"/>, in degrees from the top. One of no
        /// length is a dot, if <paramref name="dotWhenEmpty"/>, or nothing; a whole turn or more is the whole ring.
        /// </summary>
        private static void DrawArc(DrawingContext dc, Pen pen, Point centre, double radius, double from, double to, bool dotWhenEmpty)
        {
            var sweep = to - from;
            if (sweep <= 0.01)
            {
                if (dotWhenEmpty)
                {
                    var dot = pen.Thickness / 2;
                    dc.DrawEllipse(pen.Brush, null, PointAt(centre, radius, to), dot, dot);
                }

                return;
            }

            if (sweep >= 359.99)
            {
                dc.DrawEllipse(null, pen, centre, radius, radius);
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
