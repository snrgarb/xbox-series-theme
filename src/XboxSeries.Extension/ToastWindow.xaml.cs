using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Toast's window. It follows the core's <see cref="ToastState"/>, rising in at the bottom of the screen in
    /// front and fading away, and holds no logic. It never takes focus or the mouse.
    /// </summary>
    public partial class ToastWindow : Window
    {
        /// <summary>How much of the 1080 the window covers, from the bottom: room for the card and its shadow.</summary>
        public const double StripHeight = 240;

        private static readonly Duration InDuration = TimeSpan.FromMilliseconds(220);
        private static readonly Duration OutDuration = TimeSpan.FromMilliseconds(180);

        private readonly ToastState toast;

        /// <summary>The text's fade between the Toast's line and "Press (Guide) to view", running while it shows.</summary>
        private readonly Storyboard textCycle;

        public ToastWindow(ToastState toast)
        {
            InitializeComponent();
            textCycle = (Storyboard)Resources["TextCycle"];
            this.toast = toast;
            DataContext = toast;
            toast.PropertyChanged += OnToastChanged;
            SizeChanged += (_, __) => FitToScreen();
            SourceInitialized += (_, __) => NativeMethods.MakeClickThrough(new WindowInteropHelper(this).Handle);
        }

        private void FitToScreen()
        {
            if (ActualHeight > 0)
            {
                Root.Width = Root.Height * ActualWidth / ActualHeight;
            }
        }

        private void OnToastChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ToastState.IsShown))
            {
                return;
            }

            if (toast.IsShown)
            {
                ShowOverForeground();

                // On the Toast's line first, each time it shows.
                textCycle.Begin(this, true);
                Animate(0, 1, InDuration, null);
            }
            else
            {
                Animate(40, 0, OutDuration, () =>
                {
                    if (!toast.IsShown)
                    {
                        textCycle.Stop(this);
                        Hide();
                    }
                });
            }
        }

        /// <summary>Along the bottom of the monitor of the window in front, in physical pixels, without activating.</summary>
        private void ShowOverForeground()
        {
            var bounds = NativeMethods.MonitorBoundsOf(NativeMethods.GetForegroundWindow());
            var height = (int)Math.Round((bounds.Bottom - bounds.Top) * StripHeight / 1080);
            Show();
            NativeMethods.SetWindowPos(
                new WindowInteropHelper(this).Handle,
                NativeMethods.HWND_TOPMOST,
                bounds.Left,
                bounds.Bottom - height,
                bounds.Right - bounds.Left,
                height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
        }

        private void Animate(double riseTo, double opacityTo, Duration duration, Action completed)
        {
            var fade = new DoubleAnimation(opacityTo, duration);
            if (completed != null)
            {
                fade.Completed += (_, __) => completed();
            }

            Rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(riseTo, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            Card.BeginAnimation(OpacityProperty, fade);
        }
    }
}
