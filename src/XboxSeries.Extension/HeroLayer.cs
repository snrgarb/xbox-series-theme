using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
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

        public HeroLayer()
        {
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Root.Children.Add(image);
            Root.Children.Add(darken);
            Root.Opacity = 0;
        }

        public Grid Root { get; } = new Grid { ClipToBounds = true };

        /// <summary>Shows <paramref name="art"/>; for none, keeps the last, to fade out as it is.</summary>
        public void Show(HeroArt art)
        {
            if (art == null)
            {
                return;
            }

            var isCover = art.IsCover;
            image.Source = DecodedImageConverter.Load(art.Image, isCover ? 480 : 1920);

            // A cover is small and portrait: blurred, it reads as colour behind what is over it rather than as a stretched picture.
            // It overhangs the layer so the blur's soft edges fall outside it.
            image.Effect = isCover ? new BlurEffect { Radius = 60, RenderingBias = RenderingBias.Performance } : null;
            image.Margin = isCover ? new Thickness(-120) : new Thickness(0);
            darken.Visibility = isCover ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
