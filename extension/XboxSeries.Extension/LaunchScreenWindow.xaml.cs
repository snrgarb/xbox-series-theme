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
    /// The Launch screen's window. It follows the core's <see cref="LaunchScreenState"/> and holds no logic: it fades in
    /// over the whole monitor as the Launch screen shows, and fades out, then hides, as it goes, showing the game's art
    /// meanwhile. It never takes focus or the mouse. The Guide opens over it, and Toasts show over it.
    /// </summary>
    public partial class LaunchScreenWindow : Window
    {
        /// <summary>The fade in, and out.</summary>
        private static readonly Duration FadeDuration = TimeSpan.FromMilliseconds(300);

        private readonly LaunchScreenState launchScreen;

        /// <summary>The windows that stay above the Launch screen as it shows: the Toast's, then the Overlay, which hosts the Guide.</summary>
        private readonly Window[] above;

        private readonly HeroLayer art = new HeroLayer();

        /// <summary>Counts the goings, so only the latest one's end hides the window.</summary>
        private int goings;

        /// <param name="above">The windows to keep above it, lowest first.</param>
        public LaunchScreenWindow(LaunchScreenState launchScreen, params Window[] above)
        {
            InitializeComponent();
            this.launchScreen = launchScreen;
            this.above = above;
            Root.Children.Add(art.Root);
            launchScreen.PropertyChanged += OnLaunchScreenChanged;
            SourceInitialized += (_, __) =>
            {
                // See-through as the Toast is, drawn by the graphics card, and layered at full alpha so clicks pass through.
                var handle = new WindowInteropHelper(this).Handle;
                NativeMethods.MakeClickThrough(handle);
                NativeMethods.SetAlpha(handle, 255);
                NativeMethods.SeeThrough(HwndSource.FromHwnd(handle));
            };
        }

        private void OnLaunchScreenChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LaunchScreenState.Art))
            {
                ShowArt();
            }
            else if (e.PropertyName == nameof(LaunchScreenState.IsShown))
            {
                if (launchScreen.IsShown)
                {
                    ShowArt();
                    ShowOverForeground();
                    Fade(1, null);
                }
                else
                {
                    var going = ++goings;
                    Fade(0, () =>
                    {
                        // Not if the Launch screen has shown again, or gone again since, whose own end follows.
                        if (going == goings && !launchScreen.IsShown)
                        {
                            Root.CacheMode = null;
                            Hide();
                        }
                    });
                }
            }
        }

        /// <summary>The game's art, or black for none; it stays as the Launch screen goes, so it fades out as it was.</summary>
        private void ShowArt()
        {
            var shown = launchScreen.Art;
            art.Show(shown);
            art.Root.Opacity = shown == null ? 0 : 1;
        }

        /// <summary>
        /// Over the whole monitor of the window in front, Playnite's as a game launches, in physical pixels, without
        /// activating; then the Toast's and the Overlay raised over it again, so a Guide or Toast already up stays in front.
        /// </summary>
        private void ShowOverForeground()
        {
            var bounds = NativeMethods.MonitorBoundsOf(NativeMethods.GetForegroundWindow());
            Show();
            NativeMethods.SetWindowPos(
                new WindowInteropHelper(this).Handle,
                NativeMethods.HWND_TOPMOST,
                bounds.Left,
                bounds.Top,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);

            foreach (var window in above)
            {
                var handle = window == null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero && window.IsVisible)
                {
                    NativeMethods.SetWindowPos(
                        handle,
                        NativeMethods.HWND_TOPMOST,
                        0,
                        0,
                        0,
                        0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
            }
        }

        /// <summary>
        /// The whole screen fading to <paramref name="to"/> from wherever it is, as part way through the other fade, drawn
        /// once as a picture meanwhile so a blurred cover isn't blurred again on every frame at 4K.
        /// </summary>
        private void Fade(double to, Action done)
        {
            if (!(Root.CacheMode is BitmapCache))
            {
                Root.CacheMode = new BitmapCache(1);
            }

            var fade = new DoubleAnimation(to, FadeDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            if (done != null)
            {
                fade.Completed += (_, __) => done();
            }

            Root.BeginAnimation(OpacityProperty, fade);
        }
    }
}
