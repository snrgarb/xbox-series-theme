using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Overlay. It follows the core's <see cref="GuideState"/>: shown while the Overlay is up, the panel sliding in
    /// and out as the Guide opens and closes. It holds no Guide logic.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private static readonly Duration OpenDuration = TimeSpan.FromMilliseconds(180);
        private static readonly Duration CloseDuration = TimeSpan.FromMilliseconds(150);

        private readonly ExtensionCore core;
        private readonly double offscreenX;

        public OverlayWindow(ExtensionCore core)
        {
            InitializeComponent();
            offscreenX = Slide.X;
            this.core = core;
            DataContext = core.Guide;
            core.Guide.PropertyChanged += OnGuideChanged;
        }

        private void OnGuideChanged(object sender, PropertyChangedEventArgs e)
        {
            var guide = core.Guide;
            if (e.PropertyName == nameof(GuideState.IsOverlayShown))
            {
                if (guide.IsOverlayShown)
                {
                    ShowOverForeground();
                }
                else
                {
                    HideWhenDone();
                }
            }
            else if (e.PropertyName == nameof(GuideState.IsOpen))
            {
                if (guide.IsOpen)
                {
                    Animate(0, 1, OpenDuration, EasingMode.EaseOut, null);
                }
                else
                {
                    Animate(offscreenX, 0, CloseDuration, EasingMode.EaseIn, HideWhenDone);
                }
            }
        }

        /// <summary>
        /// Hides once the slide-out has finished and the core has handed focus back, whichever comes last.
        /// Hiding while the Overlay still holds focus would hand the game focus early.
        /// </summary>
        private void HideWhenDone()
        {
            if (!core.Guide.IsOpen && !core.Guide.IsOverlayShown && Slide.X <= offscreenX)
            {
                Hide();
            }
        }

        private void ShowOverForeground()
        {
            // Cover the monitor of the window in front, in physical pixels, which avoids DPI maths.
            var bounds = NativeMethods.MonitorBoundsOf(NativeMethods.GetForegroundWindow());
            Show();
            NativeMethods.SetWindowPos(
                new WindowInteropHelper(this).Handle,
                NativeMethods.HWND_TOPMOST,
                bounds.Left,
                bounds.Top,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top,
                NativeMethods.SWP_SHOWWINDOW);
        }

        private void Animate(double slideTo, double dimTo, Duration duration, EasingMode easing, Action completed)
        {
            // To-only animations, so reopening mid slide-out continues from where the panel is.
            var slide = new DoubleAnimation(slideTo, duration) { EasingFunction = new CubicEase { EasingMode = easing } };
            if (completed != null)
            {
                slide.Completed += (_, __) => completed();
            }

            Slide.BeginAnimation(TranslateTransform.XProperty, slide);
            Dim.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(dimTo, duration));
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // Playnite routes controller input here as key events. The Guide is driven by Playnite's controller
            // callback instead, so every key is marked handled and the Dashboard behind never reacts.
            e.Handled = true;
            if (e.Key == Key.Escape)
            {
                core.DismissGuide();
            }
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!GuidePanel.IsMouseOver)
            {
                core.DismissGuide();
            }
        }
    }
}
