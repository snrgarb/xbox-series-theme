using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Playnite.SDK;
using XboxSeries.Core;
using XboxSeries.Shared;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Start-up sequence's window, made on the sequence's own thread (<see cref="StartupThread"/>), so it draws and
    /// answers the pad whatever Playnite's UI thread is doing; it touches nothing of that thread. It follows the
    /// <see cref="StartupSequence"/>'s <see cref="StartupState"/> and holds no logic: shown over the monitor Playnite's
    /// window is on while <see cref="StartupState.IsShown"/>, holding the foreground so no key reaches Home behind it,
    /// and fading out to reveal Home once the sequence ends, the foreground handed back to Playnite's window. While it
    /// shows it reads the pads itself, every frame, through XInput, since Playnite's controller callback waits
    /// on its UI thread; every key here is marked handled, and the keyboard's own arrows, Enter, Space and Escape stand
    /// in for the pad's. A click on a profile signs it in. A boot video that is missing or can't play ends as one that
    /// finished would; the sequence ends one that runs long. It tells the Boot shell, where one runs, of its first frame
    /// that shows something and of the boot video's end (<see cref="BootShellSignals"/>).
    /// </summary>
    public partial class StartupWindow : Window
    {
        /// <summary>
        /// The swirl's fade out that reveals Home, quick, so Home's own rows flying in, begun on Playnite's UI thread at
        /// the same moment, show through it as it lifts.
        /// </summary>
        private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(250);

        /// <summary>Each profile's width in Who's playing today?'s row, as StartupWindow.xaml lays it out.</summary>
        private const double ProfileWidth = 420;

        /// <summary>
        /// The row's slide to centre a newly focused profile: a touch longer than the 0.15 s its ring takes to grow, easing
        /// out, so the two land together.
        /// </summary>
        private static readonly Duration SlideDuration = TimeSpan.FromMilliseconds(220);

        /// <summary>The swirl's fade in, as the boot video gives way to Who's playing today?.</summary>
        private static readonly Duration SwirlInDuration = TimeSpan.FromMilliseconds(300);

        /// <summary>How often the pad is read while the window shows: about every frame.</summary>
        private static readonly TimeSpan PadInterval = TimeSpan.FromMilliseconds(16);

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly StartupSequence sequence;
        private readonly StartupState startup;
        private readonly Func<IReadOnlyCollection<ControllerButton>> readPad;
        private readonly Func<IntPtr> playniteWindow;
        private readonly Action activatePlaynite;
        private readonly DispatcherTimer fade;
        private readonly DispatcherTimer pad;
        private readonly BootShellSignals bootShellSignals;
        private DateTime fadeStarted;
        private bool closing;

        /// <summary>Whether the window has been placed over the monitor, so a frame it draws is seen; false before it first shows.</summary>
        private bool overMonitor;

        /// <summary>The pad's buttons at its last reading, kept only to log each press's time early on.</summary>
        private IReadOnlyCollection<ControllerButton> padWasDown = new ControllerButton[0];

        /// <summary>Counts the videos played, so only the one playing ends the boot video, once.</summary>
        private int videos;

        /// <param name="themeResources">The theme's resources the window uses, copied on the UI thread for this one.</param>
        /// <param name="readPad">The buttons any pad holds down now, read on this thread.</param>
        /// <param name="playniteWindow">Playnite's own window, or <see cref="IntPtr.Zero"/> before it has one; any thread may ask.</param>
        /// <param name="activatePlaynite">Activates Playnite's window in WPF, on Playnite's own thread, as the foreground goes back to it.</param>
        public StartupWindow(StartupSequence sequence, IReadOnlyDictionary<string, object> themeResources, Func<IReadOnlyCollection<ControllerButton>> readPad, Func<IntPtr> playniteWindow, Action activatePlaynite)
        {
            // Playnite's application resources belong to its UI thread: the window looks only in its own, which hold
            // the theme's copied for this thread (StartupResources), then in the system's. It passes no property value
            // down either, its DataContext neither, so that is set on its content.
            InheritanceBehavior = InheritanceBehavior.SkipToThemeNext;
            InitializeComponent();
            foreach (var resource in themeResources)
            {
                Resources[resource.Key] = resource.Value;
            }

            this.sequence = sequence;
            this.readPad = readPad;
            this.playniteWindow = playniteWindow;
            this.activatePlaynite = activatePlaynite;
            startup = sequence.State;
            bootShellSignals = new BootShellSignals(startup, SetBootShellEvent);
            Layers.DataContext = startup;
            startup.PropertyChanged += OnStartupChanged;
            SizeChanged += (_, __) => FitToScreen();
            Deactivated += (_, __) => KeepForeground();
            ContentRendered += (_, __) => StartupTiming.Log("Start-up window's first frame, on its own thread,");
            CompositionTarget.Rendering += WatchFirstFrame;
            Video.MediaEnded += (_, __) => EndVideo(videos);
            Video.MediaFailed += (_, e) =>
            {
                logger.Warn(e.ErrorException, "Can't play the boot video");
                EndVideo(videos);
            };

            // A timer, not an animation: the fade is the window's own alpha, which only Windows draws.
            fade = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
            fade.Tick += (_, __) => StepFade();
            pad = new DispatcherTimer(DispatcherPriority.Input) { Interval = PadInterval };
            pad.Tick += (_, __) => ReadPad();
        }

        /// <summary>The fade out has finished and the window has hidden, or it ended without ever showing: the sequence is over.</summary>
        public event Action FadedOut;

        /// <summary>The window closed, the sequence over or Playnite closing; anything else closing it, as Alt+F4, is refused.</summary>
        public void Shutdown()
        {
            closing = true;
            startup.PropertyChanged -= OnStartupChanged;
            CompositionTarget.Rendering -= WatchFirstFrame;
            fade.Stop();
            pad.Stop();
            StopVideo();
            HideSwirl();
            Close();
        }

        /// <summary>Shows whatever phase the sequence is already in, for one begun before the window was made.</summary>
        public void Follow()
        {
            OnStartupChanged(this, new PropertyChangedEventArgs(nameof(StartupState.Phase)));
        }

        /// <summary>
        /// A real key held back from Home on Playnite's UI thread while the sequence shows, handed over to do here what it
        /// would have done in this window.
        /// </summary>
        public void OnHeldBackKey(Key key, bool down, bool repeat)
        {
            if ((!down || !repeat) && ButtonFor(key) is ControllerButton button)
            {
                sequence.OnButton(button, down ? ButtonState.Pressed : ButtonState.Released);
            }
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
            if (e.PropertyName == nameof(StartupState.FocusedIndex))
            {
                SlideToFocused(animate: startup.Phase == StartupPhase.WhosPlaying);
                return;
            }

            // A boot video chosen again counts only while one plays.
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
                    SlideToFocused(animate: false);
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
            sequence.OnBootVideoEnded();
        }

        /// <summary>
        /// Just before WPF draws each frame on this thread, until one shows something: Start-up's first frame, not the
        /// window's first draw, which is black. That is the first frame after the boot video
        /// has opened, or, with none playing, the first with the swirl's fade in begun, under Who's playing today?
        /// (Signing you in when it doesn't ask). Only once the window covers the monitor: WPF draws within Show too,
        /// while it's still off screen. While watched, WPF draws every frame. The video's player is asked whether it
        /// has opened, not told by MediaOpened, which another add-on's handler can cut short on this thread (#97).
        /// </summary>
        private void WatchFirstFrame(object sender, EventArgs e)
        {
            var video = Video.Visibility == Visibility.Visible && Video.HasVideo;
            var swirl = SwirlLayer.Visibility == Visibility.Visible && SwirlLayer.Opacity > 0;
            if (!overMonitor || !(video || swirl))
            {
                return;
            }

            CompositionTarget.Rendering -= WatchFirstFrame;
            bootShellSignals.FirstFrameShown();
        }

        /// <summary>One of the Boot shell's events set and logged, if it made it; with no Boot shell, nothing is logged.</summary>
        private void SetBootShellEvent(string name)
        {
            try
            {
                if (BootShellEvents.Set(name))
                {
                    logger.Info($"[Boot shell] Set {name} in {startup.Phase}, at {StartupTiming.Now} ms");
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Warn(e, $"[Boot shell] Couldn't set {name}");
            }
        }

        /// <summary>
        /// The swirl fading in over whatever was behind it, as the boot video, and set turning. The swirl, and Signing you
        /// in's spinner, turn while they are visible.
        /// </summary>
        private void ShowSwirl()
        {
            SwirlLayer.Visibility = Visibility.Visible;
            if (SwirlLayer.Opacity < 1 && !SwirlLayer.HasAnimatedProperties)
            {
                SwirlLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, SwirlInDuration));
            }
        }

        /// <summary>The swirl gone and still, as the boot video plays or once the window has hidden.</summary>
        private void HideSwirl()
        {
            SwirlLayer.BeginAnimation(OpacityProperty, null);
            SwirlLayer.Opacity = 0;
            SwirlLayer.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Slides Who's playing today?'s row sideways so the focused profile's centre is at the screen's: the row hangs
        /// from a profile-wide slot centred on the screen, so it moves left a profile's width for each place along. A
        /// focus moved while it shows eases there; one set as it first shows, or before, is there at once, so nothing jumps.
        /// </summary>
        private void SlideToFocused(bool animate)
        {
            var to = -ProfileWidth * startup.FocusedIndex;
            if (animate)
            {
                Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(to, SlideDuration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
            }
            else
            {
                Slide.BeginAnimation(TranslateTransform.XProperty, null);
                Slide.X = to;
            }
        }

        /// <summary>
        /// Covers the monitor Playnite's window is on, in physical pixels, opaque again if it was fading, takes the
        /// foreground so no key reaches Home behind it, and starts reading the pad.
        /// </summary>
        private void ShowOverPlaynite()
        {
            fade.Stop();
            Root.Visibility = Visibility.Visible;
            var playnite = playniteWindow();
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
            overMonitor = true;
            TakeForeground(handle);
            pad.Start();
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
        /// The pads' buttons, read here every frame and handed to the sequence, which finds the presses and releases in
        /// them. For the first seconds after the sequence shows, each press is logged with its time, to show presses
        /// answered while Playnite's UI thread is still busy.
        /// </summary>
        private void ReadPad()
        {
            var down = readPad();
            if (StartupTiming.IsEarly)
            {
                foreach (var button in down.Except(padWasDown))
                {
                    StartupTiming.Log($"Pad {button} pressed, read on the Start-up thread,");
                }
            }

            padWasDown = down;
            sequence.OnPadReading(down);
        }

        /// <summary>
        /// Stops reading the pad, hands the foreground back to Playnite's window at once, so Home has the keys, and fades
        /// out over it, hiding at the end. The profile, spinner and text go at once, leaving only the swirl, still turning,
        /// to lift off Home, so nothing of Signing you in hangs over Home's rows flying in, and nothing goes black. A window
        /// that never showed is done with at once.
        /// </summary>
        private void FadeOut()
        {
            pad.Stop();
            if (fade.IsEnabled)
            {
                return;
            }

            if (!IsVisible)
            {
                FadedOut?.Invoke();
                return;
            }

            var playnite = playniteWindow();
            if (playnite != IntPtr.Zero)
            {
                // Windows' foreground at once, from here; WPF's own active window on Playnite's thread when it gets to it.
                if (NativeMethods.GetForegroundWindow() != playnite)
                {
                    NativeMethods.BringToForeground(playnite);
                }

                activatePlaynite();
            }

            Root.Visibility = Visibility.Hidden;
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
            FadedOut?.Invoke();
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
            // The pad is read from XInput, so every key, Playnite's own for the pad and Alt+F4 too, is
            // marked handled; the real keys standing in for the pad's buttons drive the sequence.
            e.Handled = true;
            if (!e.IsRepeat && ButtonFor(e) is ControllerButton button)
            {
                sequence.OnButton(button, ButtonState.Pressed);
            }
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
            if (ButtonFor(e) is ControllerButton button)
            {
                sequence.OnButton(button, ButtonState.Released);
            }
        }

        /// <summary>Null for Playnite's own key events for the pad's buttons: the pad is read from the pad itself.</summary>
        private static ControllerButton? ButtonFor(KeyEventArgs e) => PlayniteInput.PadButtonOf(e) != null ? null : ButtonFor(e.Key);

        /// <summary>
        /// The pad button a real key stands in for, so the sequence can be got through without a pad: the arrows move
        /// along the profiles, Enter and Space are A, Escape is B, and any of them skips the boot video. Null for any other key.
        /// </summary>
        private static ControllerButton? ButtonFor(Key key)
        {
            switch (key)
            {
                case Key.Left: return ControllerButton.DPadLeft;
                case Key.Right: return ControllerButton.DPadRight;
                case Key.Enter:
                case Key.Space: return ControllerButton.A;
                case Key.Escape: return ControllerButton.B;
            }

#if DEBUG
            return PlayniteInput.DebugButtonFor(key);
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
            sequence.OnButton(button, ButtonState.Pressed);
            sequence.OnButton(button, ButtonState.Released);
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
