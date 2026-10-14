using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Playnite.SDK;
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
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly double offscreenX;

        // Once focus has gone back, WPF can stop rendering the Overlay's frames until the next input, leaving the
        // slide-out stuck part way. This timer runs off the dispatcher, not rendering, and finishes the slide-out.
        private readonly DispatcherTimer closeDeadline;

        public OverlayWindow(ExtensionCore core)
        {
            InitializeComponent();
            offscreenX = Slide.X;
            this.core = core;
            DataContext = core.Guide;
            DialogPanel.Core = core;
            WidePanel.Core = core;
            core.Guide.PropertyChanged += OnGuideChanged;
            closeDeadline = new DispatcherTimer { Interval = CloseDuration.TimeSpan + TimeSpan.FromMilliseconds(100) };
            closeDeadline.Tick += (_, __) => FinishStalledSlideOut();
            SizeChanged += (_, __) => FitToScreen();
        }

        /// <summary>The Guide is laid out 1080 tall, as wide as the screen's aspect makes that, and scaled to fit.</summary>
        private void FitToScreen()
        {
            if (ActualHeight > 0)
            {
                Root.Width = Root.Height * ActualWidth / ActualHeight;
            }
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
            else if (e.PropertyName == nameof(GuideState.IsOpen) || (e.PropertyName == nameof(GuideState.ShowsPanel) && guide.IsOpen))
            {
                if (guide.IsOpen)
                {
                    // Power on its own keeps the dim but not the panel, which slides out if it was in.
                    closeDeadline.Stop();
                    if (guide.ShowsPanel)
                    {
                        Animate(0, 1, OpenDuration, EasingMode.EaseOut, null);
                    }
                    else
                    {
                        Animate(offscreenX, 1, CloseDuration, EasingMode.EaseIn, null);
                    }
                }
                else
                {
                    Animate(offscreenX, 0, CloseDuration, EasingMode.EaseIn, HideWhenDone);
                    closeDeadline.Start();
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

        private void FinishStalledSlideOut()
        {
            closeDeadline.Stop();
            if (core.Guide.IsOpen || Slide.X <= offscreenX)
            {
                return;
            }

            logger.Info("Guide slide-out stalled; finishing it without the animation");
            Slide.BeginAnimation(TranslateTransform.XProperty, null);
            Slide.X = offscreenX;
            Dim.BeginAnimation(Brush.OpacityProperty, null);
            Dim.Opacity = 0;
            HideWhenDone();
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
#if DEBUG
            else if (!e.IsRepeat && PlayniteInput.DebugButtonFor(e.Key) is ControllerButton button)
            {
                core.OnControllerButton(button, ButtonState.Pressed);
            }
#endif
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
#if DEBUG
            if (PlayniteInput.DebugButtonFor(e.Key) is ControllerButton button)
            {
                core.OnControllerButton(button, ButtonState.Released);
            }
#endif
        }

        // Debug builds only: the mouse points at and picks entries (and, in MenuPanel, options). Release keeps clicks to closing the Guide (#10).
        private void OnEntryMouseEnter(object sender, MouseEventArgs e)
        {
#if DEBUG
            core.FocusItem((GuideItem)((FrameworkElement)sender).DataContext);
#endif
        }

        private void OnEntryClick(object sender, MouseButtonEventArgs e)
        {
#if DEBUG
            core.ChooseItem((GuideItem)((FrameworkElement)sender).DataContext);
#endif
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!GuidePanel.IsMouseOver && !DialogPanel.IsMouseOver && !WidePanel.IsMouseOver)
            {
                core.DismissGuide();
            }
        }
    }
}
