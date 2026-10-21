using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The soft black shadow a DropShadowEffect (no offset) puts round a rounded rectangle of this element's size, drawn
    /// round its edge from a picture of one made once, rather than worked out again every frame. Once WPF loses the
    /// graphics card, as it does whenever Windows sleeps, it works every effect out on the CPU at the 4K screen's size,
    /// and one frame of that took minutes (#95); this is a handful of small pictures, quick to draw anywhere.
    /// The picture is of one corner, and a sliver of the edge between, the shadow's whole depth either side of the edge;
    /// each corner is drawn as it is and each sliver stretched along its edge, as the shadow doesn't change along a
    /// straight edge, so it is for rectangles at least two corners across, each a corner's radius and twice the shadow's
    /// depth (a little over 100 for the panels here). Inside the rectangle there is nothing, so it can sit over a panel's
    /// background as well as under it. Drawn outside its own bounds, by the shadow's depth; it never takes the pointer.
    /// </summary>
    internal sealed class SoftShadow : FrameworkElement
    {
        /// <summary>The picture is drawn at 4 pixels to a unit: the 4K screen's 2, or the Idle card's 3, and some over.</summary>
        private const double PixelsPerUnit = 4;

        public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
            nameof(BlurRadius), typeof(double), typeof(SoftShadow), new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(SoftShadow), new FrameworkPropertyMetadata(new CornerRadius(), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
            nameof(ShadowOpacity), typeof(double), typeof(SoftShadow), new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>The pictures made so far, by depth, corners and strength, for every thread: they are frozen.</summary>
        private static readonly Dictionary<string, Pieces> made = new Dictionary<string, Pieces>();

        public SoftShadow()
        {
            IsHitTestVisible = false;
        }

        /// <summary>As a DropShadowEffect's BlurRadius: how far out the shadow reaches.</summary>
        public double BlurRadius
        {
            get => (double)GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        /// <summary>The rounded rectangle's corners, as the Border it is the shadow of.</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>As a DropShadowEffect's Opacity.</summary>
        public double ShadowOpacity
        {
            get => (double)GetValue(ShadowOpacityProperty);
            set => SetValue(ShadowOpacityProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            var p = Made(BlurRadius, CornerRadius, ShadowOpacity);
            double inner = p.Reach - p.Depth, across = Math.Max(0, w - 2 * inner), down = Math.Max(0, h - 2 * inner);
            dc.DrawImage(p.TopLeft, new Rect(-p.Depth, -p.Depth, p.Reach, p.Reach));
            dc.DrawImage(p.TopRight, new Rect(w - inner, -p.Depth, p.Reach, p.Reach));
            dc.DrawImage(p.BottomLeft, new Rect(-p.Depth, h - inner, p.Reach, p.Reach));
            dc.DrawImage(p.BottomRight, new Rect(w - inner, h - inner, p.Reach, p.Reach));
            dc.DrawImage(p.Top, new Rect(inner, -p.Depth, across, p.Reach));
            dc.DrawImage(p.Bottom, new Rect(inner, h - inner, across, p.Reach));
            dc.DrawImage(p.Left, new Rect(-p.Depth, inner, p.Reach, down));
            dc.DrawImage(p.Right, new Rect(w - inner, inner, p.Reach, down));
        }

        private static Pieces Made(double blurRadius, CornerRadius corners, double opacity)
        {
            var key = FormattableString.Invariant($"{blurRadius} {corners} {opacity}");
            lock (made)
            {
                if (!made.TryGetValue(key, out var pieces))
                {
                    made[key] = pieces = Make(blurRadius, corners, opacity);
                }
                return pieces;
            }
        }

        /// <summary>
        /// Draws the shadow once, with the effect itself, round a rectangle, and takes the rectangle back out of it. Its pieces
        /// reach the shadow's depth out from the edge and a corner's radius and the depth again in. The rectangle is as wide
        /// as two pieces and more, so the shadow along each edge, and round each corner, is as deep as along a long one.
        /// </summary>
        private static Pieces Make(double blurRadius, CornerRadius corners, double opacity)
        {
            var depth = Math.Ceiling(blurRadius) + 1;
            var corner = Math.Ceiling(Math.Max(Math.Max(corners.TopLeft, corners.TopRight), Math.Max(corners.BottomLeft, corners.BottomRight))) + 1;
            // In along the edge as far as the corner rounds, and then as far again as the shadow reaches, where the
            // shadow along the edge is as it is along a long straight one.
            var reach = depth + corner + depth;
            var side = 2 * reach + 2;
            var size = side + 2 * depth;

            byte[] Draw(bool shadowed)
            {
                var shape = new Border { Width = side, Height = side, CornerRadius = corners, Background = Brushes.Black };
                if (shadowed)
                {
                    shape.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = blurRadius, Opacity = opacity, Color = Colors.Black };
                }
                var host = new Grid { Width = size, Height = size };
                host.Children.Add(shape);
                host.Measure(new Size(size, size));
                host.Arrange(new Rect(0, 0, size, size));
                var pixels = (int)(size * PixelsPerUnit);
                var bitmap = new RenderTargetBitmap(pixels, pixels, 96 * PixelsPerUnit, 96 * PixelsPerUnit, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var data = new byte[pixels * pixels * 4];
                bitmap.CopyPixels(data, pixels * 4, 0);
                return data;
            }

            // The shadow alone is what the shadowed rectangle covers beyond the rectangle; black, so only its alpha.
            var withShape = Draw(true);
            var shapeOnly = Draw(false);
            for (var i = 3; i < withShape.Length; i += 4)
            {
                withShape[i] = (byte)Math.Max(0, withShape[i] - shapeOnly[i]);
                withShape[i - 1] = withShape[i - 2] = withShape[i - 3] = 0;
            }
            var all = (int)(size * PixelsPerUnit);
            var whole = BitmapSource.Create(all, all, 96, 96, PixelFormats.Pbgra32, null, withShape, all * 4);
            whole.Freeze();

            BitmapSource Piece(double x, double y, double width, double height)
            {
                var piece = new CroppedBitmap(whole, new Int32Rect((int)(x * PixelsPerUnit), (int)(y * PixelsPerUnit), (int)(width * PixelsPerUnit), (int)(height * PixelsPerUnit)));
                piece.Freeze();
                return piece;
            }

            // The slivers are from the middle of each edge, where it is straight.
            var middle = size / 2 - 0.5;
            return new Pieces
            {
                Depth = depth,
                Reach = reach,
                TopLeft = Piece(0, 0, reach, reach),
                TopRight = Piece(size - reach, 0, reach, reach),
                BottomLeft = Piece(0, size - reach, reach, reach),
                BottomRight = Piece(size - reach, size - reach, reach, reach),
                Top = Piece(middle, 0, 1, reach),
                Bottom = Piece(middle, size - reach, 1, reach),
                Left = Piece(0, middle, reach, 1),
                Right = Piece(size - reach, middle, reach, 1),
            };
        }

        private sealed class Pieces
        {
            public double Depth;
            public double Reach;
            public BitmapSource TopLeft, TopRight, BottomLeft, BottomRight, Top, Bottom, Left, Right;
        }
    }
}
