using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// A game's art filling the space it is given, as Home's hero and the Launch screen show it: background art as it
    /// is, or a cover blurred and darkened. It starts unseen; whoever holds it fades its <see cref="Root"/>.
    /// </summary>
    internal sealed class HeroLayer
    {
        private readonly Image image = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
        };

        private readonly Rectangle darken = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)) };

        /// <summary>The width a cover is decoded at, to be blurred: blurred, it keeps no detail a larger one would show.</summary>
        private const int CoverDecodeWidth = 120;

        /// <summary>The cover's blur, an eighth of its width as at any size.</summary>
        private const double CoverBlurRadius = CoverDecodeWidth / 8.0;

        /// <summary>The cover last asked for while it was still downloading, to be blurred and shown as it arrives.</summary>
        private BitmapSource pendingCover;

        /// <summary>The width background art is decoded at; a cover, blurred, needs less.</summary>
        private readonly int artDecodeWidth;

        /// <param name="artDecodeWidth">The width background art is decoded at: the full screen's unless the layer is smaller.</param>
        public HeroLayer(int artDecodeWidth = 1920)
        {
            this.artDecodeWidth = artDecodeWidth;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Root.Children.Add(image);
            Root.Children.Add(darken);
            Root.Opacity = 0;
        }

        public Grid Root { get; } = new Grid { ClipToBounds = true };

        /// <summary>Shows <paramref name="art"/>, read and decoded here and now; for none, keeps the last, to fade out as it is.</summary>
        public void Show(HeroArt art)
        {
            if (art != null)
            {
                Show(art, Decode(art));
            }
        }

        /// <summary>
        /// Whether <paramref name="art"/> can be decoded away from the UI thread (<see cref="Decode"/>): a file can, read
        /// whole and frozen. A URL can't: it downloads on the dispatcher of the thread that asked for it, which a
        /// pool thread hasn't, and isn't frozen until it has arrived.
        /// </summary>
        public static bool DecodesOffThread(HeroArt art) =>
            Uri.TryCreate(art.Image, UriKind.Absolute, out var uri) && uri.IsFile;

        /// <summary>
        /// The picture of <paramref name="art"/> at the size this layer shows it, for <see cref="Show(HeroArt, BitmapImage)"/>.
        /// Reading and decoding large background art takes tenths of a second, so whoever follows focus with it does this
        /// on another thread when <see cref="DecodesOffThread"/>. Null when it can't be read.
        /// </summary>
        public BitmapImage Decode(HeroArt art) =>
            art.IsCover
                ? DecodedImageConverter.Load(art.Image, CoverDecodeWidth)
                : DecodedImageConverter.Load(art.Image, artDecodeWidth);

        /// <summary>Shows <paramref name="art"/>, whose picture <paramref name="decoded"/> is, from <see cref="Decode"/>.</summary>
        public void Show(HeroArt art, BitmapImage decoded)
        {
            // A cover is small and portrait: blurred, it reads as colour behind what is over it rather than as a stretched
            // picture. It overhangs the layer so the blur's soft edges fall outside it.
            var isCover = art.IsCover;
            pendingCover = null;
            if (isCover)
            {
                ShowCover(decoded);
            }
            else
            {
                image.Source = decoded;
            }

            image.Margin = isCover ? new Thickness(-120) : new Thickness(0);
            darken.Visibility = isCover ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// The cover blurred once, into a picture of its own, then only scaled up to fill the layer. A blur effect on
        /// the image would blur it again every frame it is drawn in: nothing on the GPU, but seconds a frame on the CPU,
        /// as WPF draws just after Windows wakes, at the 4K screen's size and three times over in the Idle card's cache.
        /// A cover still downloading shows nothing until it has arrived, as stretched sharp it is a few big pixels.
        /// </summary>
        private void ShowCover(BitmapSource cover)
        {
            if (cover == null || !cover.IsDownloading)
            {
                image.Source = cover == null ? null : BlurredOnce(cover);
                return;
            }

            image.Source = null;
            cover.DownloadCompleted += (_, __) =>
            {
                // Not if other art has shown since.
                if (pendingCover == cover)
                {
                    image.Source = BlurredOnce(cover);
                }
            };
            pendingCover = cover;
        }

        private static BitmapSource BlurredOnce(BitmapSource cover)
        {
            var size = new Size(cover.PixelWidth, cover.PixelHeight);
            var blurred = new Image { Source = cover, Stretch = Stretch.Fill, Effect = new BlurEffect { Radius = CoverBlurRadius } };
            blurred.Measure(size);
            blurred.Arrange(new Rect(size));
            var picture = new RenderTargetBitmap(cover.PixelWidth, cover.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            picture.Render(blurred);
            picture.Freeze();
            return picture;
        }
    }
}
