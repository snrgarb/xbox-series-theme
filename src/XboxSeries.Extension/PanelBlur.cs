using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Guide's panel and its contents behind a menu or prompt, blurred as a BlurEffect of radius 12 on each of them
    /// blurred them: drawn together into one picture, blurred as it is drawn, once. The picture is a unit's pixel to a 1080p one;
    /// the blur is soft enough that the 4K screen's 2 pixels to one add nothing, and it is quick to make.
    /// </summary>
    internal static class PanelBlur
    {
        /// <summary>The radius of the blur, in units of the Guide's 1080p layout, as the effect it replaces.</summary>
        public const double Radius = 12;

        /// <summary>
        /// The picture of <paramref name="parts"/> as they are drawn now in <paramref name="within"/>, blurred, and where
        /// in it to show it: round them all, as far out as the blur reaches. Null if nothing is laid out yet.
        /// </summary>
        public static BitmapSource Draw(Visual within, FrameworkElement[] parts, out Rect bounds)
        {
            var placed = parts.Where(p => p.ActualWidth > 0 && p.ActualHeight > 0)
                .Select(p => new { Part = p, Rect = p.TransformToAncestor(within).TransformBounds(new Rect(0, 0, p.ActualWidth, p.ActualHeight)) })
                .ToList();
            bounds = Rect.Empty;
            if (placed.Count == 0)
            {
                return null;
            }

            foreach (var p in placed)
            {
                bounds.Union(p.Rect);
            }
            bounds.Inflate(Radius, Radius);

            // Each part drawn as itself, as far out round it as the blur reaches, where it sits among the others.
            var host = new Canvas { Width = bounds.Width, Height = bounds.Height, Effect = new BlurEffect { Radius = Radius, KernelType = KernelType.Gaussian } };
            foreach (var p in placed)
            {
                FrameworkElement part = p.Part;
                var rect = p.Rect;
                // A VisualBrush draws its visual where it sits in its parent, so the part's own offset there is added back.
                var offset = VisualTreeHelper.GetOffset(part);
                var drawn = new Rectangle
                {
                    Width = rect.Width + 2 * Radius,
                    Height = rect.Height + 2 * Radius,
                    Fill = new VisualBrush(part)
                    {
                        Viewbox = new Rect(offset.X - Radius, offset.Y - Radius, part.ActualWidth + 2 * Radius, part.ActualHeight + 2 * Radius),
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Stretch = Stretch.Fill,
                    },
                };
                Canvas.SetLeft(drawn, rect.Left - bounds.Left - Radius);
                Canvas.SetTop(drawn, rect.Top - bounds.Top - Radius);
                host.Children.Add(drawn);
            }

            var size = new Size(bounds.Width, bounds.Height);
            host.Measure(size);
            host.Arrange(new Rect(size));
            var picture = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height), 96, 96, PixelFormats.Pbgra32);
            picture.Render(host);
            picture.Freeze();
            return picture;
        }
    }
}
