using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
        private static readonly Duration CrossfadeTime = TimeSpan.FromMilliseconds(400);

        /// <summary>The crossfade with Settings' Reduce motion on: still a fade, so the art doesn't flash in, but a short one.</summary>
        private static readonly Duration ReducedCrossfadeTime = TimeSpan.FromMilliseconds(150);

        private readonly HomeState home;

        /// <summary>The swirl, under the art: shown while art is fading in or out over it, and none is up.</summary>
        private readonly SwirlBackground swirl = new SwirlBackground();
        private HeroLayer front;
        private HeroLayer back;

        /// <summary>The art the hero last turned to, up or fading in; null for the swirl.</summary>
        private HeroArt shown;

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
            shown = art;
            front.Show(art);
            front.Root.BeginAnimation(OpacityProperty, null);
            front.Root.Opacity = art == null ? 0 : 1;
            swirl.Visibility = art == null ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// The hero changed: the new art, once read, fades in on top while the old fades out beneath it. A file is read
        /// and decoded on another thread, so that large art (4K art takes tenths of a second) doesn't hold up the frames
        /// in which focus moves along the row; the old art stays up meanwhile. Art that focus has left again by the time
        /// it is read is dropped: what the hero changed to since is on its way, or up.
        /// </summary>
        private async void CrossfadeTo(HeroArt art)
        {
            var name = art == null ? "the swirl" : Path.GetFileName(art.Image);
            HeroTiming.Log($"{name} asked for");
            BitmapImage decoded = null;
            if (art != null)
            {
                var decoding = Stopwatch.StartNew();
                var layer = front;
                var offThread = HeroLayer.DecodesOffThread(art);
                decoded = offThread ? await Task.Run(() => layer.Decode(art)) : layer.Decode(art);
                if (decoded != null && offThread && !decoded.IsFrozen)
                {
                    // Not to be shown on this thread unless frozen; read here instead.
                    decoded = layer.Decode(art);
                }

                HeroTiming.Log($"{name} decoded in {decoding.ElapsedMilliseconds} ms {(offThread ? "off" : "on")} the UI thread,");

                // Focus moved on while it was read, or the same art was asked for twice and the other is up already.
                if (!Equals(home.Hero, art) || Equals(shown, art))
                {
                    return;
                }
            }

            Crossfade(art, decoded, name);
        }

        private void Crossfade(HeroArt art, BitmapImage decoded, string name)
        {
            var old = front;
            front = back;
            back = old;
            shown = art;

            if (art != null)
            {
                front.Show(art, decoded);
            }

            HeroTiming.LogNextFrame($"{name} fading in");
            Panel.SetZIndex(front.Root, 1);
            Panel.SetZIndex(back.Root, 0);
            // To-only, so a crossfade interrupted by quick focus changes carries on from where each layer is. The swirl
            // shows beneath throughout, and goes, so it stops turning, once art covers it again.
            swirl.Visibility = Visibility.Visible;
            var crossfade = Motion.Reduced ? ReducedCrossfadeTime : CrossfadeTime;
            var fadeIn = new DoubleAnimation(art == null ? 0 : 1, crossfade);
            fadeIn.Completed += (_, __) =>
            {
                HeroTiming.Log($"{name} shown");
                if (home.Hero != null && Equals(home.Hero, art))
                {
                    swirl.Visibility = Visibility.Collapsed;
                }
            };
            front.Root.BeginAnimation(OpacityProperty, fadeIn);
            back.Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, crossfade));
        }
    }
}
