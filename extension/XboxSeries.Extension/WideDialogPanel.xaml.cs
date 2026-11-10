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

        // How long after the band shows its contents start to come in.
        private static readonly TimeSpan ContentLag = TimeSpan.FromMilliseconds(40);

        // How far left of where they settle the contents start.
        private const double ContentTravel = 200;

        // The slide-in, waiting for the band's first frame to be drawn.
        private EventHandler pendingSlide;

        /// <summary>
        /// As it shows, the band is there at once while its contents are hidden, and a moment after the band's first
        /// frame has been drawn they slide in a short way from the left as they fade in, so the slide isn't spent while
        /// that frame is laid out.
        /// </summary>
        private void SlideInOnceDrawn()
        {
            if (pendingSlide != null)
            {
                CompositionTarget.Rendering -= pendingSlide;
                pendingSlide = null;
            }

            ContentSlide.BeginAnimation(TranslateTransform.XProperty, null);
            Contents.BeginAnimation(OpacityProperty, null);
            if (!IsVisible)
            {
                ContentSlide.X = 0;
                Contents.Opacity = 1;
                return;
            }

            ContentSlide.X = -ContentTravel;
            Contents.Opacity = 0;
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

                // With Reduce motion on, no time long and no lag: the contents are in their place as that frame is
                // drawn.
                ContentSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Motion.Of(OverlayWindow.OpenDuration))
                {
                    BeginTime = Motion.Of(ContentLag),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
                Contents.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Motion.Of(OverlayWindow.OpenDuration)) { BeginTime = Motion.Of(ContentLag) });
            };
            CompositionTarget.Rendering += pendingSlide;
        }

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
