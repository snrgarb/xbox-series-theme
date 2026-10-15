using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private HeroLayer front;
        private HeroLayer back;

        public HeroControl(HomeState home)
        {
            this.home = home;
            Focusable = false;
            IsHitTestVisible = false;
            front = new HeroLayer();
            back = new HeroLayer();
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
    }
}
