using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// key here is marked handled; the keyboard's own arrows, Enter, Space and Escape stand in for the pad's, and a click
    /// on a profile signs it in. A boot video that is missing or can't play ends as one that finished would; the core
    /// ends one that runs long. While it shows, Playnite's own navigation sound is silenced, as Home takes its first focus
    /// behind it, and comes back once it has gone.
    /// </summary>
    public partial class StartupWindow : Window
    {
        /// <summary>The fade out that reveals Home.</summary>
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(300);

        /// <summary>The swirl's fade in, as the boot video gives way to Who's playing today?.</summary>
        private static readonly Duration SwirlInDuration = TimeSpan.FromMilliseconds(300);

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly StartupState startup;
        private readonly Action<bool> silenceThemeSounds;
        private readonly Storyboard spin;
        private readonly DispatcherTimer fade;
        private DateTime fadeStarted;
        private bool spinning;
        private bool closing;

        /// <summary>Counts the videos played, so only the one playing ends the boot video, once.</summary>
        private int videos;

        /// <param name="silenceThemeSounds">Silences Playnite's own navigation and activation sounds (true), or puts them back (false).</param>
        public StartupWindow(ExtensionCore core, Action<bool> silenceThemeSounds)
        {
            InitializeComponent();
            this.core = core;
            this.silenceThemeSounds = silenceThemeSounds;
            startup = core.Startup;
            DataContext = startup;
            spin = (Storyboard)Resources["Spin"];
            startup.PropertyChanged += OnStartupChanged;
            SizeChanged += (_, __) => FitToScreen();
            Deactivated += (_, __) => KeepForeground();
            Video.MediaEnded += (_, __) => EndVideo(videos);
            Video.MediaFailed += (_, e) =>
            {
                logger.Warn(e.ErrorException, "Can't play the boot video");
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
            startup.PropertyChanged -= OnStartupChanged;
            fade.Stop();
            StopVideo();
            HideSwirl();
            silenceThemeSounds(false);
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
            HideSwirl();
            if (!File.Exists(path))
            {
                logger.Warn($"No boot video at {path}; going on without it");
                Dispatcher.BeginInvoke(new Action(() => EndVideo(playing)));
                return;
            }

            Video.Visibility = Visibility.Visible;
            Video.Source = new Uri(path, UriKind.Absolute);
            Video.Play();
        }

        private void StopVideo()
        {
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
        /// The boot video playing ended or failed: the sequence goes on, once, and only if it is still the
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

        /// <summary>
        /// The swirl fading in over whatever was behind it, as the boot video, and set turning, Signing you in's light
        /// with it. The swirl turns while it is visible.
        /// </summary>
        private void ShowSwirl()
        {
            if (!spinning)
            {
                spin.Begin(this, true);
                spinning = true;
            }

            SwirlLayer.Visibility = Visibility.Visible;
            if (SwirlLayer.Opacity < 1 && !SwirlLayer.HasAnimatedProperties)
            {
                SwirlLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, SwirlInDuration));
            }
        }

        /// <summary>The swirl gone and still, as the boot video plays or once the window has hidden.</summary>
        private void HideSwirl()
        {
            if (spinning)
            {
                spin.Stop(this);
                spinning = false;
            }

            SwirlLayer.BeginAnimation(OpacityProperty, null);
            SwirlLayer.Opacity = 0;
            SwirlLayer.Visibility = Visibility.Hidden;
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
        /// foreground so no key reaches Home behind it, silencing Playnite's own navigation sound as Home's focus moves.
        /// </summary>
        private void ShowOverPlaynite()
        {
            fade.Stop();
            silenceThemeSounds(true);
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
                if (front != handle && NativeMethods.IsOfThisProcess(front))
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
            HideSwirl();
            SetOpaque(handle);

            // Only now, with Home in front and focused again behind the fade, so its focus coming back makes no sound.
            silenceThemeSounds(false);
        }

        /// <summary>Playnite's own window, or <see cref="IntPtr.Zero"/> before it has one.</summary>
        private IntPtr PlayniteWindow()
        {
            var main = Application.Current?.MainWindow;
            return main == null || main == this ? IntPtr.Zero : new WindowInteropHelper(main).Handle;
        }

        // The fade is the window's own alpha, set on it as a layered window only while it fades, so the boot video and
        // the swirl draw as an ordinary window's would. Off again once hidden.
        private static void SetAlpha(IntPtr handle, byte alpha) => NativeMethods.SetAlpha(handle, alpha);

        private static void SetOpaque(IntPtr handle) => NativeMethods.SetOpaque(handle);

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
            if (!e.IsRepeat && ButtonFor(e) is ControllerButton button)
            {
                core.OnControllerButton(button, ButtonState.Pressed);
            }
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
            if (ButtonFor(e) is ControllerButton button)
            {
                core.OnControllerButton(button, ButtonState.Released);
            }
        }

        /// <summary>
        /// The pad button a real key stands in for, so the sequence can be got through without a pad: the arrows move
        /// along the profiles, Enter and Space are A, Escape is B, and any of them skips the boot video. Null for the
        /// pad's own key events, which reach the core through its controller callback already, and for any other key.
        /// </summary>
        private static ControllerButton? ButtonFor(KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) != null)
            {
                return null;
            }

            switch (e.Key)
            {
                case Key.Left: return ControllerButton.DPadLeft;
                case Key.Right: return ControllerButton.DPadRight;
                case Key.Enter:
                case Key.Space: return ControllerButton.A;
                case Key.Escape: return ControllerButton.B;
            }

#if DEBUG
            return PlayniteInput.DebugButtonFor(e.Key);
#else
            return null;
#endif
        }

        /// <summary>A click on a profile in Who's playing today?: focus moves to it, as the arrows would, and A signs it in.</summary>
        private void OnProfileClick(object sender, MouseButtonEventArgs e)
        {
            if (startup.Phase != StartupPhase.WhosPlaying || !((sender as FrameworkElement)?.DataContext is Profile clicked))
            {
                return;
            }

            e.Handled = true;
            var profiles = startup.Profiles.ToList();
            var target = profiles.FindIndex(p => p.Id == clicked.Id);
            for (var step = 0; step < profiles.Count && startup.FocusedProfile?.Id != clicked.Id; step++)
            {
                var here = profiles.FindIndex(p => p.Id == startup.FocusedProfile?.Id);
                Tap(here < target ? ControllerButton.DPadRight : ControllerButton.DPadLeft);
            }

            if (startup.FocusedProfile?.Id == clicked.Id)
            {
                Tap(ControllerButton.A);
            }
        }

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
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
