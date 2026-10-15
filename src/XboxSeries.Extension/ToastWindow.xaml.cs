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
    /// The Toast's window. It follows the core's <see cref="ToastState"/> and holds no logic, coming in and going as the
    /// console's Toast does: the banner grows from its centre as it fades in, with its text at its left where the tile
    /// will be; after a moment the tile slides out from behind the banner's left edge as it fades in, and the text moves
    /// right into its place; it goes by growing a little as it fades out. Only render transforms and opacity move, so the
    /// banner's size never changes. It never takes focus or the mouse.
    /// </summary>
    public partial class ToastWindow : Window
    {
        /// <summary>
        /// How much of the 1080 the window covers, from the bottom: room for the 128 card 64 up, grown to 1.1 as it goes.
        /// </summary>
        public const double StripHeight = 240;

        /// <summary>The banner's grow from <see cref="GrowFrom"/> and fade in.</summary>
        private static readonly Duration GrowDuration = TimeSpan.FromMilliseconds(200);

        /// <summary>How far in the banner starts its grow.</summary>
        private const double GrowFrom = 0.3;

        /// <summary>From the start of the grow (or another Toast cutting in) to the start of the tile's slide.</summary>
        private static readonly TimeSpan SlideAt = TimeSpan.FromMilliseconds(900);

        /// <summary>The tile's slide in and the text's move right.</summary>
        private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(450);

        /// <summary>The tile's width and its gap to the text: how far left the tile and text start.</summary>
        private const double SlideBy = 96 + 24;

        /// <summary>The banner's grow to <see cref="GrowTo"/> and fade out.</summary>
        private static readonly Duration OutDuration = TimeSpan.FromMilliseconds(180);

        /// <summary>How far out the banner grows as it goes.</summary>
        private const double GrowTo = 1.1;

        private readonly ToastState toast;

        /// <summary>The text's fade between the Toast's line and "Press (Guide) to view", running while it shows.</summary>
        private readonly Storyboard textCycle;

        /// <summary>Counts the goings, so only the latest one's end hides the window.</summary>
        private int goings;

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
            if (e.PropertyName == nameof(ToastState.Text) && toast.IsShown)
            {
                // Another Toast cutting in, as a call over a notification: on its line first too, its tile sliding in again.
                textCycle.Begin(this, true);
                SlideIn();
                return;
            }

            if (e.PropertyName != nameof(ToastState.IsShown))
            {
                return;
            }

            if (toast.IsShown)
            {
                ShowOverForeground();

                // On the Toast's line first, each time it shows, and from the start of the grow, even mid-going.
                textCycle.Begin(this, true);
                GrowIn();
                SlideIn();
            }
            else
            {
                GrowOut();
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

        /// <summary>The banner growing from its centre as it fades in.</summary>
        private void GrowIn()
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Scale(new DoubleAnimation(GrowFrom, 1, GrowDuration) { EasingFunction = ease });
            Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, GrowDuration) { EasingFunction = ease });
        }

        /// <summary>
        /// The tile, from behind the banner's left edge and unseen, held there until <see cref="SlideAt"/> and then sliding
        /// in as it fades in, the text moving right with it. Each starts at once from its hidden place, whatever it was at.
        /// </summary>
        private void SlideIn()
        {
            TileShift.BeginAnimation(TranslateTransform.XProperty, Slide(-SlideBy, 0));
            TextShift.BeginAnimation(TranslateTransform.XProperty, Slide(-SlideBy, 0));
            Tile.BeginAnimation(OpacityProperty, Slide(0, 1));
        }

        /// <summary>The banner growing a little as it fades out, then the window hiding if the Toast hasn't shown again.</summary>
        private void GrowOut()
        {
            var going = ++goings;
            var fade = new DoubleAnimation(0, OutDuration);
            fade.Completed += (_, __) =>
            {
                if (going == goings && !toast.IsShown)
                {
                    textCycle.Stop(this);
                    Hide();
                }
            };

            Scale(new DoubleAnimation(GrowTo, OutDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            Card.BeginAnimation(OpacityProperty, fade);
        }

        private void Scale(DoubleAnimation animation)
        {
            Grow.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            Grow.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        /// <summary>From <paramref name="from"/>, held until <see cref="SlideAt"/>, then easing out to <paramref name="to"/>, and held there.</summary>
        private static DoubleAnimationUsingKeyFrames Slide(double from, double to)
        {
            var slide = new DoubleAnimationUsingKeyFrames();
            slide.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            slide.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(SlideAt)));
            slide.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(SlideAt + SlideDuration), new CubicEase { EasingMode = EasingMode.EaseOut }));
            return slide;
        }
    }
}
