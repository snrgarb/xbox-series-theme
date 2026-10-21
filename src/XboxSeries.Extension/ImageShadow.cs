using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The soft shadow a DropShadowEffect puts under a picture, for a picture drawn Uniform in this element's size, as an
    /// Image with the same Source and Stretch beside it draws it: the shadow alone, of the picture's own shape, made once
    /// for each picture and size with the effect itself and then only drawn. An effect is worked out again every time its
    /// element is drawn, and once WPF loses the graphics card, as it does whenever Windows sleeps, that is on the CPU at the
    /// 4K screen's size; one frame of that took minutes (#95). Drawn outside its own bounds, by the shadow's reach.
    /// </summary>
    internal sealed class ImageShadow : FrameworkElement
    {
        /// <summary>
        /// The shadow is made at one pixel to a unit: it is soft all over, so stretched to the screen's 2 or 3 it looks the
        /// same, and a picture the size of a tile is quick to blur once.
        /// </summary>
        private const double PixelsPerUnit = 1;

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source), typeof(ImageSource), typeof(ImageShadow), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

        public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
            nameof(BlurRadius), typeof(double), typeof(ImageShadow), new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShadowDepthProperty = DependencyProperty.Register(
            nameof(ShadowDepth), typeof(double), typeof(ImageShadow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
            nameof(ShadowOpacity), typeof(double), typeof(ImageShadow), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// The shadows made so far, for as long as their pictures are kept, by size and look. Frozen, so any thread can
        /// draw them, and the table and each picture's list are locked as they're read or added to.
        /// </summary>
        private static readonly ConditionalWeakTable<ImageSource, Dictionary<string, BitmapSource>> made =
            new ConditionalWeakTable<ImageSource, Dictionary<string, BitmapSource>>();

        public ImageShadow()
        {
            IsHitTestVisible = false;
        }

        /// <summary>The picture, as its Image's Source.</summary>
        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <summary>As a DropShadowEffect's BlurRadius.</summary>
        public double BlurRadius
        {
            get => (double)GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        /// <summary>As a DropShadowEffect's ShadowDepth, always straight down (its Direction of 270).</summary>
        public double ShadowDepth
        {
            get => (double)GetValue(ShadowDepthProperty);
            set => SetValue(ShadowDepthProperty, value);
        }

        /// <summary>As a DropShadowEffect's Opacity.</summary>
        public double ShadowOpacity
        {
            get => (double)GetValue(ShadowOpacityProperty);
            set => SetValue(ShadowOpacityProperty, value);
        }

        /// <summary>A picture still downloading has no shape yet: its shadow is made once it has.</summary>
        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is BitmapSource bitmap && !bitmap.IsFrozen && bitmap.IsDownloading)
            {
                var shadow = (ImageShadow)d;
                bitmap.DownloadCompleted += (_, __) => shadow.InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            var source = Source;
            if (source == null || (source is BitmapSource bitmap && bitmap.IsDownloading) || source.Width <= 0 || source.Height <= 0
                || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            // Where an Image with Stretch Uniform draws the picture: as large as fits, centred.
            var scale = Math.Min(ActualWidth / source.Width, ActualHeight / source.Height);
            var width = (int)Math.Round(source.Width * scale);
            var height = (int)Math.Round(source.Height * scale);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var shadow = Made(source, width, height, BlurRadius, ShadowDepth, ShadowOpacity);
            var reach = Reach(BlurRadius, ShadowDepth);
            dc.DrawImage(shadow, new Rect((ActualWidth - width) / 2 - reach, (ActualHeight - height) / 2 - reach,
                                          width + 2 * reach, height + 2 * reach));
        }

        private static double Reach(double blurRadius, double depth) => Math.Ceiling(blurRadius + depth) + 1;

        private static BitmapSource Made(ImageSource source, int width, int height, double blurRadius, double depth, double opacity)
        {
            var key = FormattableString.Invariant($"{width} {height} {blurRadius} {depth} {opacity}");
            Dictionary<string, BitmapSource> sizes;
            lock (made)
            {
                sizes = made.GetValue(source, _ => new Dictionary<string, BitmapSource>());
            }
            lock (sizes)
            {
                if (!sizes.TryGetValue(key, out var shadow))
                {
                    sizes[key] = shadow = Make(source, width, height, blurRadius, depth, opacity);
                }
                return shadow;
            }
        }

        /// <summary>
        /// Draws the picture once with the effect and once without. Where the picture covers a fraction s of a pixel and the
        /// two together cover c, the shadow there covers (c - s) / (1 - s); under the picture's solid parts it can't be
        /// seen, and is left as it is.
        /// </summary>
        private static BitmapSource Make(ImageSource source, int width, int height, double blurRadius, double depth, double opacity)
        {
            var reach = Reach(blurRadius, depth);
            var size = new Size(width + 2 * reach, height + 2 * reach);
            var pixelWidth = (int)Math.Ceiling(size.Width * PixelsPerUnit);
            var pixelHeight = (int)Math.Ceiling(size.Height * PixelsPerUnit);

            byte[] Draw(bool shadowed)
            {
                var image = new Image { Source = source, Stretch = Stretch.Fill, Width = width, Height = height };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                if (shadowed)
                {
                    image.Effect = new DropShadowEffect { BlurRadius = blurRadius, ShadowDepth = depth, Direction = 270, Opacity = opacity, Color = Colors.Black };
                }
                var host = new Grid { Width = size.Width, Height = size.Height };
                host.Children.Add(image);
                host.Measure(size);
                host.Arrange(new Rect(size));
                var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * PixelsPerUnit, 96 * PixelsPerUnit, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var data = new byte[pixelWidth * pixelHeight * 4];
                bitmap.CopyPixels(data, pixelWidth * 4, 0);
                return data;
            }

            var both = Draw(true);
            var picture = Draw(false);
            var shadow = new byte[both.Length];
            for (var i = 3; i < both.Length; i += 4)
            {
                double s = picture[i] / 255.0, c = both[i] / 255.0;
                shadow[i] = (byte)Math.Round(255 * (s > 0.98 ? c : Math.Max(0, (c - s) / (1 - s))));
            }
            var made = BitmapSource.Create(pixelWidth, pixelHeight, 96 * PixelsPerUnit, 96 * PixelsPerUnit, PixelFormats.Pbgra32, null, shadow, pixelWidth * 4);
            made.Freeze();
            return made;
        }
    }
}
