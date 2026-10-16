using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>A wide dialog, as the Guide draws Power. It follows its <see cref="Dialog"/> and holds no logic.</summary>
    public partial class WideDialogPanel : UserControl
    {
        public WideDialogPanel()
        {
            InitializeComponent();
            IsVisibleChanged += (_, __) => SlideInOnceDrawn();
        }

        // The slide-in, waiting for the band's first frame to be drawn.
        private EventHandler pendingSlide;

        /// <summary>
        /// As it shows, holds the band and its contents off the left of the screen and slides them in once the band's
        /// first frame has been drawn, as the Guide's panel does, so the slide isn't spent while that frame is laid out.
        /// </summary>
        private void SlideInOnceDrawn()
        {
            if (pendingSlide != null)
            {
                CompositionTarget.Rendering -= pendingSlide;
                pendingSlide = null;
            }

            BandSlide.BeginAnimation(TranslateTransform.XProperty, null);
            ContentSlide.BeginAnimation(TranslateTransform.XProperty, null);
            if (!IsVisible)
            {
                BandSlide.X = ContentSlide.X = 0;
                return;
            }

            // Off the screen, which the band spans, whatever its width.
            var offscreen = -Math.Max(ActualWidth, 1920);
            BandSlide.X = ContentSlide.X = offscreen;
            var frames = 0;
            pendingSlide = (_, __) =>
            {
                // The first Rendering is the frame that draws the band; the next comes once it is drawn.
                if (++frames < 2)
                {
                    return;
                }

                CompositionTarget.Rendering -= pendingSlide;
                pendingSlide = null;
                BandSlide.BeginAnimation(TranslateTransform.XProperty, SlideIn(TimeSpan.Zero));
                ContentSlide.BeginAnimation(TranslateTransform.XProperty, SlideIn(OverlayWindow.ContentLag));
            };
            CompositionTarget.Rendering += pendingSlide;
        }

        private static DoubleAnimation SlideIn(TimeSpan lag) =>
            new DoubleAnimation(0, OverlayWindow.OpenDuration)
            {
                BeginTime = lag,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };

        /// <summary>The core the mouse reports to, in debug builds.</summary>
        internal ExtensionCore Core { get; set; }

        // Debug builds only: the mouse points at and picks options. Release keeps clicks to closing the Guide (#10).
        private void OnOptionMouseEnter(object sender, MouseEventArgs e)
        {
#if DEBUG
            Core?.FocusOption((DialogOption)((FrameworkElement)sender).DataContext);
#endif
        }

        private void OnOptionClick(object sender, MouseButtonEventArgs e)
        {
#if DEBUG
            Core?.ChooseOption((DialogOption)((FrameworkElement)sender).DataContext);
#endif
        }
    }
}
