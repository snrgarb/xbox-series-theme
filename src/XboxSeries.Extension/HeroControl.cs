using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Playnite.SDK.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The hero behind Home, injected as a custom element. It crossfades to the core's <see cref="HomeState.Hero"/>:
    /// background art as it is, a cover blurred and darkened, and the green swirl for none, as while no Recent-row tile
    /// has focus. It holds no Home logic. A <see cref="PluginUserControl"/> so that Playnite binds the selected game to
    /// its GameContext, not its DataContext.
    /// </summary>
    public sealed class HeroControl : PluginUserControl
    {
        private static readonly Duration Crossfade = TimeSpan.FromMilliseconds(400);

        private readonly HomeState home;

        /// <summary>The swirl, under the art: shown while art is fading in or out over it, and none is up.</summary>
        private readonly SwirlBackground swirl = new SwirlBackground();
        private Layer front;
        private Layer back;

        public HeroControl(HomeState home)
        {
            this.home = home;
            Focusable = false;
            IsHitTestVisible = false;
            front = new Layer();
            back = new Layer();
            var root = new Grid();
            root.Children.Add(swirl);
            root.Children.Add(back.Root);
            root.Children.Add(front.Root);
            Content = root;
            Loaded += (_, __) =>
            {
                home.PropertyChanged += OnHomeChanged;
                ShowAtOnce(home.Hero);
            };
            Unloaded += (_, __) => home.PropertyChanged -= OnHomeChanged;
        }

        private void OnHomeChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HomeState.Hero))
            {
                CrossfadeTo(home.Hero);
            }
        }

        private void ShowAtOnce(HeroArt art)
        {
            front.Show(art);
            front.Root.BeginAnimation(OpacityProperty, null);
            front.Root.Opacity = art == null ? 0 : 1;
            swirl.Visibility = art == null ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>The new art fades in on top while the old fades out beneath it.</summary>
        private void CrossfadeTo(HeroArt art)
        {
            var old = front;
            front = back;
            back = old;

            front.Show(art);
            Panel.SetZIndex(front.Root, 1);
            Panel.SetZIndex(back.Root, 0);
            // To-only, so a crossfade interrupted by quick focus changes carries on from where each layer is. The swirl
            // shows beneath throughout, and goes, so it stops turning, once art covers it again.
            swirl.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(art == null ? 0 : 1, Crossfade);
            fadeIn.Completed += (_, __) =>
            {
                if (home.Hero != null && Equals(home.Hero, art))
                {
                    swirl.Visibility = Visibility.Collapsed;
                }
            };
            front.Root.BeginAnimation(OpacityProperty, fadeIn);
            back.Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, Crossfade));
        }

        /// <summary>One image of the crossfade, with the blur and darkening a cover needs.</summary>
        private sealed class Layer
        {
            private readonly Image image = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
            };

            private readonly Rectangle darken = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)) };

            public Layer()
            {
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                Root.Children.Add(image);
                Root.Children.Add(darken);
                Root.Opacity = 0;
            }

            public Grid Root { get; } = new Grid { ClipToBounds = true };

            public void Show(HeroArt art)
            {
                if (art == null)
                {
                    return;
                }

                var isCover = art.IsCover;
                image.Source = DecodedImageConverter.Load(art.Image, isCover ? 480 : 1920);

                // A cover is small and portrait: blurred, it reads as colour behind Home rather than as a stretched picture.
                // It overhangs the hero so the blur's soft edges fall outside it.
                image.Effect = isCover ? new BlurEffect { Radius = 60, RenderingBias = RenderingBias.Performance } : null;
                image.Margin = isCover ? new Thickness(-120) : new Thickness(0);
                darken.Visibility = isCover ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
