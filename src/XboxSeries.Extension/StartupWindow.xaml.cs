using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Start-up sequence's window. It follows the core's <see cref="StartupState"/> and holds no logic: shown over the
    /// monitor Playnite's window is on while <see cref="StartupState.IsShown"/>, holding the foreground so no key reaches
    /// Home behind it, and fading out to reveal Home once the sequence ends, the foreground handed back to Playnite's
    /// window. The pad drives the sequence through the core's controller callback, whatever window is in front, so every
    /// key here is marked handled. Nothing it shows can hold Playnite up: a boot video that is missing, can't play, or
    /// runs long ends as one that finished would.
    /// </summary>
    public partial class StartupWindow : Window
    {
        /// <summary>The fade out that reveals Home.</summary>
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(300);

        /// <summary>The swirl's fade in, as the boot video gives way to Who's playing today?.</summary>
        private static readonly Duration SwirlInDuration = TimeSpan.FromMilliseconds(300);

        /// <summary>The longest any boot video is let run before the sequence goes on without it; each is under 15 s.</summary>
        private static readonly TimeSpan VideoDeadline = TimeSpan.FromSeconds(20);

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly StartupState startup;
        private readonly Storyboard swirl;
        private readonly DispatcherTimer videoDeadline;
        private readonly DispatcherTimer fade;
        private DateTime fadeStarted;
        private bool swirlRunning;
        private bool closing;

        /// <summary>Counts the videos played, so only the one playing ends the boot video, once.</summary>
        private int videos;

        public StartupWindow(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            startup = core.Startup;
            DataContext = startup;
            swirl = (Storyboard)Resources["Swirl"];
            startup.PropertyChanged += OnStartupChanged;
            SizeChanged += (_, __) => FitToScreen();
            Deactivated += (_, __) => KeepForeground();
            Video.MediaEnded += (_, __) => EndVideo(videos);
            Video.MediaFailed += (_, e) =>
            {
                logger.Warn(e.ErrorException, "Can't play the boot video");
                EndVideo(videos);
            };

            videoDeadline = new DispatcherTimer { Interval = VideoDeadline };
            videoDeadline.Tick += (_, __) =>
            {
                logger.Warn("The boot video ran past its deadline; going on without it");
                EndVideo(videos);
            };

            // A timer, not an animation: the fade is the window's own alpha, which only Windows draws.
            fade = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
            fade.Tick += (_, __) => StepFade();
        }

        /// <summary>The window as Playnite closes; anything else closing it, as Alt+F4, is refused.</summary>
        public void Shutdown()
        {
            closing = true;
            StopVideo();
            Close();
        }

        /// <summary>Shows whatever phase the sequence is already in, for one begun before the window was made.</summary>
        public void Follow()
        {
            OnStartupChanged(this, new PropertyChangedEventArgs(nameof(StartupState.Phase)));
        }

        private void FitToScreen()
        {
            if (ActualHeight > 0)
            {
                Root.Width = Root.Height * ActualWidth / ActualHeight;
            }
        }

        private void OnStartupChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StartupState.FocusedProfile))
            {
                ScrollFocusedIntoView();
                return;
            }

            // A boot video chosen again counts only while one plays; Settings choosing one changes nothing here.
            var videoChanged = e.PropertyName == nameof(StartupState.BootVideo) && startup.Phase == StartupPhase.BootVideo;
            if (e.PropertyName != nameof(StartupState.Phase) && !videoChanged)
            {
                return;
            }

            var phase = startup.Phase;
            if (phase == StartupPhase.BootVideo)
            {
                PlayVideo();
            }
            else
            {
                StopVideo();
            }

            if (phase == StartupPhase.WhosPlaying || phase == StartupPhase.SigningIn)
            {
                ShowSwirl();
            }

            if (startup.IsShown)
            {
                ShowOverPlaynite();
                if (phase == StartupPhase.WhosPlaying)
                {
                    ScrollFocusedIntoView();
                }
            }
            else
            {
                FadeOut();
            }
        }

        private void PlayVideo()
        {
            var playing = ++videos;
            var path = Path.Combine(Path.GetDirectoryName(typeof(StartupWindow).Assembly.Location), "Videos", startup.BootVideo + ".mp4");
            SwirlLayer.BeginAnimation(OpacityProperty, null);
            SwirlLayer.Opacity = 0;
            if (!File.Exists(path))
            {
                logger.Warn($"No boot video at {path}; going on without it");
                Dispatcher.BeginInvoke(new Action(() => EndVideo(playing)));
                return;
            }

            Video.Visibility = Visibility.Visible;
            Video.Source = new Uri(path, UriKind.Absolute);
            Video.Play();
            videoDeadline.Stop();
            videoDeadline.Start();
        }

        private void StopVideo()
        {
            videoDeadline.Stop();
            if (Video.Source == null)
            {
                return;
            }

            Video.Stop();
            Video.Close();
            Video.Source = null;
            Video.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// The boot video playing ended, failed or ran long: the sequence goes on, once, and only if it is still the
        /// video that is playing, not one a press has skipped already.
        /// </summary>
        private void EndVideo(int playing)
        {
            if (playing != videos || startup.Phase != StartupPhase.BootVideo)
            {
                return;
            }

            ++videos;
            StopVideo();
            core.OnBootVideoEnded();
        }

        /// <summary>The swirl fading in over whatever was behind it, as the boot video, and set turning.</summary>
        private void ShowSwirl()
        {
            if (!swirlRunning)
            {
                swirl.Begin(this, true);
                swirlRunning = true;
            }

            if (SwirlLayer.Opacity < 1 && !SwirlLayer.HasAnimatedProperties)
            {
                SwirlLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, SwirlInDuration));
            }
        }

        private void StopSwirl()
        {
            if (swirlRunning)
            {
                swirl.Stop(this);
                swirlRunning = false;
            }

            SwirlLayer.BeginAnimation(OpacityProperty, null);
            SwirlLayer.Opacity = 0;
        }

        /// <summary>
        /// Keeps the focused profile on screen, for a row wider than it, once its picture is laid out.
        /// </summary>
        private void ScrollFocusedIntoView()
        {
            var focused = startup.FocusedProfile;
            if (focused == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                (ProfileRow.ItemContainerGenerator.ContainerFromItem(focused) as FrameworkElement)?.BringIntoView()));
        }

        /// <summary>
        /// Covers the monitor Playnite's window is on, in physical pixels, opaque again if it was fading, and takes the
        /// foreground so no key reaches Home behind it.
        /// </summary>
        private void ShowOverPlaynite()
        {
            fade.Stop();
            var playnite = PlayniteWindow();
            var bounds = NativeMethods.MonitorBoundsOf(playnite != IntPtr.Zero ? playnite : NativeMethods.GetForegroundWindow());
            Show();
            var handle = new WindowInteropHelper(this).Handle;
            SetOpaque(handle);
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HWND_TOPMOST,
                bounds.Left,
                bounds.Top,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top,
                NativeMethods.SWP_SHOWWINDOW);
            TakeForeground(handle);
        }

        private void TakeForeground(IntPtr handle)
        {
            Activate();
            if (NativeMethods.GetForegroundWindow() != handle)
            {
                NativeMethods.BringToForeground(handle);
            }

            Keyboard.Focus(this);
        }

        /// <summary>
        /// Takes the foreground back while the sequence shows if Playnite's own window takes it, as it does when it first
        /// shows, so Home never gets the keys Playnite makes of the pad. Another program in front is left there.
        /// </summary>
        private void KeepForeground()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (!startup.IsShown || !IsVisible || fade.IsEnabled)
                {
                    return;
                }

                var handle = new WindowInteropHelper(this).Handle;
                var front = NativeMethods.GetForegroundWindow();
                if (front != handle && IsOurs(front))
                {
                    TakeForeground(handle);
                }
            }));
        }

        /// <summary>
        /// Hands the foreground back to Playnite's window at once, so Home has the keys, and fades out over it, hiding at
        /// the end unless the sequence has shown again since.
        /// </summary>
        private void FadeOut()
        {
            if (!IsVisible || fade.IsEnabled)
            {
                return;
            }

            var playnite = PlayniteWindow();
            if (playnite != IntPtr.Zero)
            {
                Application.Current.MainWindow?.Activate();
                if (NativeMethods.GetForegroundWindow() != playnite)
                {
                    NativeMethods.BringToForeground(playnite);
                }
            }

            fadeStarted = DateTime.UtcNow;
            SetAlpha(new WindowInteropHelper(this).Handle, 255);
            fade.Start();
        }

        private void StepFade()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var done = (DateTime.UtcNow - fadeStarted).TotalMilliseconds / FadeDuration.TotalMilliseconds;
            if (done < 1)
            {
                SetAlpha(handle, (byte)Math.Round(255 * (1 - done)));
                return;
            }

            fade.Stop();
            Hide();
            StopSwirl();
            SetOpaque(handle);
        }

        /// <summary>Playnite's own window, or <see cref="IntPtr.Zero"/> before it has one.</summary>
        private IntPtr PlayniteWindow()
        {
            var main = Application.Current?.MainWindow;
            return main == null || main == this ? IntPtr.Zero : new WindowInteropHelper(main).Handle;
        }

        private static bool IsOurs(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }

            GetWindowThreadProcessId(window, out var processId);
            using (var self = Process.GetCurrentProcess())
            {
                return processId == self.Id;
            }
        }

        // The fade is the window's own alpha, set on it as a layered window only while it fades, so the boot video and
        // the swirl draw as an ordinary window's would. Off again once hidden.
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const uint LWA_ALPHA = 0x2;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private static void SetAlpha(IntPtr handle, byte alpha)
        {
            var style = GetWindowLong(handle, GWL_EXSTYLE);
            if ((style & WS_EX_LAYERED) == 0)
            {
                SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_LAYERED);
            }

            SetLayeredWindowAttributes(handle, 0, alpha, LWA_ALPHA);
        }

        private static void SetOpaque(IntPtr handle)
        {
            var style = GetWindowLong(handle, GWL_EXSTYLE);
            if ((style & WS_EX_LAYERED) != 0)
            {
                SetWindowLong(handle, GWL_EXSTYLE, style & ~WS_EX_LAYERED);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = !closing;
            base.OnClosing(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // Playnite routes controller input here as key events. The sequence is driven by Playnite's controller
            // callback instead, so every key, Alt+F4 too, is marked handled and Home behind never reacts.
            e.Handled = true;
#if DEBUG
            if (!e.IsRepeat && PlayniteInput.DebugButtonFor(e.Key) is ControllerButton button)
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
    }

    /// <summary>Whether a profile in Who's playing today?'s row is the one focused: the row's item, then the focused profile.</summary>
    internal sealed class SameProfileConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values.Length == 2 && values[0] != null && ReferenceEquals(values[0], values[1]);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
