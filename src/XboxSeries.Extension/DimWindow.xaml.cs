using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Idle screen's dim. It follows the core's <see cref="IdleState.IsDimmed"/> and holds no logic: it fades in over
    /// the whole monitor as the screen dims, over every window but the Toast's, and fades out, then hides, as it wakes. It
    /// never takes focus, the foreground or the mouse.
    /// </summary>
    public partial class DimWindow : Window
    {
        /// <summary>The fade in, slow so the screen settles rather than blinks.</summary>
        internal static readonly Duration FadeInDuration = TimeSpan.FromMilliseconds(800);

        /// <summary>The fade out, quick, as the screen wakes.</summary>
        internal static readonly Duration FadeOutDuration = TimeSpan.FromMilliseconds(250);

        private readonly IdleState idle;

        /// <summary>The windows that stay above the dim, the Toast's.</summary>
        private readonly Window[] above;

        /// <summary>Counts the goings, so only the latest one's end hides the window.</summary>
        private int goings;

        /// <param name="below">
        /// Windows of the extension's own that raise themselves to the top as they show, the Overlay and the Launch
        /// screen's: the dim goes back over them when one shows while it is up. Playnite's windows, its dialogs, are
        /// followed as they load.
        /// </param>
        /// <param name="above">The windows to keep above it, lowest first.</param>
        public DimWindow(IdleState idle, Window[] below, params Window[] above)
        {
            InitializeComponent();
            this.idle = idle;
            this.above = above;
            idle.PropertyChanged += OnIdleChanged;
            SourceInitialized += (_, __) =>
            {
                // See-through as the Toast is, drawn by the graphics card, and layered at full alpha so clicks pass through.
                var handle = new WindowInteropHelper(this).Handle;
                NativeMethods.MakeClickThrough(handle);
                NativeMethods.SetAlpha(handle, 255);
                NativeMethods.SeeThrough(HwndSource.FromHwnd(handle));
            };

            foreach (var window in below)
            {
                if (window != null)
                {
                    window.IsVisibleChanged += (_, e) =>
                    {
                        if ((bool)e.NewValue)
                        {
                            RaiseSoon();
                        }
                    };
                }
            }

            // Any window's, on any thread: only those on this one, Playnite's, can be raised from here.
            EventManager.RegisterClassHandler(typeof(Window), LoadedEvent, new RoutedEventHandler((sender, _) =>
            {
                if (sender != this && sender is Window window && window.Dispatcher == Dispatcher)
                {
                    RaiseSoon();
                }
            }));
        }

        private void OnIdleChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IdleState.IsDimmed))
            {
                return;
            }

            if (idle.IsDimmed)
            {
                ShowOverForeground();
                Fade(1, FadeInDuration, null);
            }
            else
            {
                var going = ++goings;
                Fade(0, FadeOutDuration, () =>
                {
                    // Not if the screen has dimmed again, or woken again since, whose own end follows.
                    if (going == goings && !idle.IsDimmed)
                    {
                        Hide();
                    }
                });
            }
        }

        /// <summary>
        /// Over the whole monitor of the window in front, Playnite's while it idles, in physical pixels, without
        /// activating, above every window already there; then the Toast's raised over it again.
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
            RaiseAbove();
        }

        /// <summary>
        /// Back over a window that has just shown while the dim is up, once it has placed itself, which it does after
        /// showing: the Guide opened by the core, or a dialog of Playnite's.
        /// </summary>
        private void RaiseSoon()
        {
            if (!IsVisible || !idle.IsDimmed)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (IsVisible && idle.IsDimmed)
                {
                    Raise(this);
                    RaiseAbove();
                }
            }));
        }

        private void RaiseAbove()
        {
            foreach (var window in above)
            {
                if (window != null && window.IsVisible)
                {
                    Raise(window);
                }
            }
        }

        /// <summary><paramref name="window"/> to the top of the windows above all others, where it is, without activating.</summary>
        private static void Raise(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
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

        /// <summary>The dim fading to <paramref name="to"/> from wherever it is, as part way through the other fade.</summary>
        private void Fade(double to, Duration duration, Action done)
        {
            var fade = new DoubleAnimation(to, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            if (done != null)
            {
                fade.Completed += (_, __) => done();
            }

            Shade.BeginAnimation(OpacityProperty, fade);
        }
    }
}
