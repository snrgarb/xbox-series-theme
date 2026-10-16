using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private static readonly Duration OpenDuration = TimeSpan.FromMilliseconds(120);
        private static readonly Duration CloseDuration = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan ContentLag = TimeSpan.FromMilliseconds(50);
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly double offscreenX;

        // Once focus has gone back, WPF can stop rendering the Overlay's frames until the next input, leaving the
        // slide-out stuck part way. This timer runs off the dispatcher, not rendering, and finishes the slide-out.
        private readonly DispatcherTimer closeDeadline;

        // The open's slide and dim, waiting for the Overlay's first frame to be drawn.
        private EventHandler pendingOpen;

        // Counts slides, so only the latest one's finish, its contents' included, stops the panel being drawn as a picture.
        private int slideRun;

        public OverlayWindow(ExtensionCore core)
        {
            Status = core.Status;
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

        /// <summary>
        /// Shows the Overlay once where it starts, off every screen, without taking focus, and hides it again, so that
        /// its window and the Guide's panel are made and laid out before the Guide first opens rather than as it opens.
        /// </summary>
        public void Prewarm()
        {
            if (IsVisible || new WindowInteropHelper(this).Handle != IntPtr.Zero)
            {
                return;
            }

            ShowActivated = false;
            Show();
            UpdateLayout();
            Hide();
            ShowActivated = true;
        }

        /// <summary>
        /// The Overlay is see-through by Windows drawing its transparent pixels so, not by WPF's AllowsTransparency,
        /// whose layered window is copied back through the processor every frame: at 4K that stutters the slide.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.SeeThrough((HwndSource)PresentationSource.FromVisual(this));
        }

        /// <summary>The status area at the top right, and the Notifications utility's count.</summary>
        public StatusState Status { get; }

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
            if (e.PropertyName == nameof(GuideState.FocusedItem))
            {
                ScrollFocusedIntoView();
            }
            else if (e.PropertyName == nameof(GuideState.IsOverlayShown))
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
                        AnimateOnceDrawn(0, 1, OpenDuration, EasingMode.EaseOut);
                    }
                    else
                    {
                        AnimateOnceDrawn(offscreenX, 1, CloseDuration, EasingMode.EaseIn);
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
        /// Keeps the focused entry on screen as focus moves down a list longer than its panel, such as many
        /// notifications in their flyout, or down an open dropdown's devices, once its entry is laid out. A new tab or
        /// flyout starts from the top.
        /// </summary>
        private void ScrollFocusedIntoView()
        {
            var guide = core.Guide;
            var focused = guide.FocusedItem;
            var inFlyout = guide.Flyout != null;
            var items = inFlyout ? guide.Flyout.Items : guide.Items;
            var dropdown = inFlyout && focused != null ? items.FirstOrDefault(item => item.Options?.Contains(focused) == true) : null;
            if (focused == null || (dropdown == null && !items.Contains(focused)))
            {
                return;
            }

            if (focused == items[0])
            {
                (inFlyout ? FlyoutScroller : EntriesScroller).ScrollToTop();
                return;
            }

            var entries = inFlyout ? FlyoutEntries : Entries;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                ContainerOf(entries, focused, dropdown)?.BringIntoView()));
        }

        /// <summary>
        /// The laid-out row of <paramref name="item"/> among <paramref name="entries"/>; a device in an open dropdown's
        /// list is found in that dropdown's own list, falling back to the dropdown's row.
        /// </summary>
        private static FrameworkElement ContainerOf(ItemsControl entries, GuideItem item, GuideItem dropdown)
        {
            if (dropdown == null)
            {
                return entries.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            }

            var row = entries.ItemContainerGenerator.ContainerFromItem(dropdown) as ContentPresenter;
            var options = row?.ContentTemplate?.FindName("Options", row) as ItemsControl;
            return options?.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement ?? row;
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
            HideContents();
            StopSlidingAsPicture();
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

        /// <summary>
        /// Starts the open's slide and dim once the Overlay's first frame has been drawn. Showing the window and laying
        /// out and drawing the Guide for that frame takes longer than a frame, on every open, and an animation started
        /// with it would have run most of its course by the time anything showed, the panel jumping most of the way in.
        /// Until then the panel is off screen and the dim clear, so waiting for that frame shows nothing later.
        /// </summary>
        private void AnimateOnceDrawn(double slideTo, double dimTo, Duration duration, EasingMode easing)
        {
            CancelPendingOpen();
            var frames = 0;
            pendingOpen = (_, __) =>
            {
                // The first Rendering is the frame that draws the Overlay; the next comes once it is drawn.
                if (++frames < 2)
                {
                    return;
                }

                Animate(slideTo, dimTo, duration, easing, null);
            };
            CompositionTarget.Rendering += pendingOpen;
        }

        private void CancelPendingOpen()
        {
            if (pendingOpen != null)
            {
                CompositionTarget.Rendering -= pendingOpen;
                pendingOpen = null;
            }
        }

        private void Animate(double slideTo, double dimTo, Duration duration, EasingMode easing, Action completed)
        {
            CancelPendingOpen();

            // To-only animations, so reopening mid slide-out continues from where the panel and its contents are.
            var slide = new DoubleAnimation(slideTo, duration) { EasingFunction = new CubicEase { EasingMode = easing } };
            var slidesIn = slideTo > offscreenX;
            var run = ++slideRun;
            slide.Completed += (_, __) =>
            {
                // A newer slide, started before this one finished, keeps the picture until it finishes. Sliding in,
                // the contents finish later and stop it then; slid out, they are put back to wait for the next open.
                if (run == slideRun && !slidesIn)
                {
                    HideContents();
                    StopSlidingAsPicture();
                }

                completed?.Invoke();
            };

            SlideAsPicture();
            Slide.BeginAnimation(TranslateTransform.XProperty, slide);
            Dim.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(dimTo, duration));

            // As on the console, the contents come in a moment behind the panel's background, but go out with it,
            // held where they are, so a close before they have started never brings them in after.
            if (slidesIn)
            {
                var contents = new DoubleAnimation(0, duration)
                {
                    BeginTime = ContentLag,
                    EasingFunction = new CubicEase { EasingMode = easing },
                };
                contents.Completed += (_, __) =>
                {
                    if (run == slideRun)
                    {
                        StopSlidingAsPicture();
                    }
                };
                ContentSlide.BeginAnimation(TranslateTransform.XProperty, contents);
            }
            else
            {
                var x = ContentSlide.X;
                ContentSlide.BeginAnimation(TranslateTransform.XProperty, null);
                ContentSlide.X = x;
            }
        }

        /// <summary>Puts the panel's contents back off its left edge, for the next open to bring them in.</summary>
        private void HideContents()
        {
            ContentSlide.BeginAnimation(TranslateTransform.XProperty, null);
            ContentSlide.X = offscreenX;
        }

        /// <summary>
        /// While it slides, the panel's contents and the flyout are each drawn once into a picture at the screen's own
        /// resolution, so sharp, and the graphics card moves those pictures rather than redrawing the covers' shadows
        /// and the flyout's every frame. The panel itself, a gradient, is cheap to redraw, and caching it with its
        /// contents would redraw the picture every frame as the contents move within it. Still, each is drawn as
        /// itself again.
        /// </summary>
        private void SlideAsPicture()
        {
            var source = PresentationSource.FromVisual(this);
            if (source == null || Root.ActualHeight <= 0)
            {
                return;
            }

            var pixelsPerUnit = ActualHeight / Root.ActualHeight * source.CompositionTarget.TransformToDevice.M22;
            if (!(PanelContents.CacheMode is BitmapCache cache) || cache.RenderAtScale != pixelsPerUnit)
            {
                PanelContents.CacheMode = new BitmapCache(pixelsPerUnit) { SnapsToDevicePixels = true };
                FlyoutPanel.CacheMode = new BitmapCache(pixelsPerUnit) { SnapsToDevicePixels = true };
            }
        }

        private void StopSlidingAsPicture() => PanelContents.CacheMode = FlyoutPanel.CacheMode = null;

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
            if (!GuidePanel.IsMouseOver && !FlyoutPanel.IsMouseOver && !DialogPanel.IsMouseOver && !WidePanel.IsMouseOver)
            {
                core.DismissGuide();
            }
        }
    }
}
