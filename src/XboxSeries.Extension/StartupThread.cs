using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// One Start-up sequence, run with its window on a thread of its own, so it animates and answers the pad from its
    /// first frame whatever Playnite's UI thread is doing: building Home, or every extension's start. Made on the UI
    /// thread with the core's <see cref="StartupPlan"/>, it starts an STA thread with its own dispatcher, makes the
    /// <see cref="StartupSequence"/> and its <see cref="StartupWindow"/> there, and the thread ends once the window has
    /// faded out to Home. Each sequence has a thread of its own, the one on waking from sleep too.
    /// <para>
    /// The two threads never wait on each other; everything between them is posted. Who signed in goes to the core on
    /// the UI thread; the UI thread hands over the end of the sequence, the Guide button, which only Playnite reads, and
    /// the real keys bound for Home while the sequence shows. Those keys, and Playnite's own key events for the pad, are
    /// held back from Home here on the UI thread, where WPF's input for Playnite's window runs: Playnite's window takes
    /// the foreground as it first shows, and a press landing on Home's focused tile would open its Game hub.
    /// </para>
    /// </summary>
    internal sealed class StartupThread
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly Dispatcher ui;
        private readonly Func<bool> isStartupShown;
        private readonly Action<Profile> signedIn;
        private readonly Action finished;

        /// <summary>Guards <see cref="dispatcher"/> and <see cref="waiting"/>, the only state both threads touch.</summary>
        private readonly object gate = new object();

        /// <summary>What the UI thread posted before the Start-up thread had its dispatcher, run as it gets one.</summary>
        private readonly List<Action> waiting = new List<Action>();

        /// <summary>The Start-up thread's dispatcher, once it has made its sequence and window; null until then.</summary>
        private Dispatcher dispatcher;

        /// <summary>Playnite's own window, set from the UI thread and read on the Start-up thread.</summary>
        private volatile IntPtr playniteWindow;

        /// <summary>Whether Home's input is being held back; UI thread only.</summary>
        private bool guarding;

        /// <summary>Whether the core has been told who signed in, so it is told once; Start-up thread only.</summary>
        private bool toldSignedIn;

        // The Start-up thread's own; null if they couldn't be made.
        private StartupSequence sequence;
        private StartupWindow window;

        /// <param name="ui">Playnite's UI thread, which runs the core.</param>
        /// <param name="pad">Pad 1, read on the Start-up thread.</param>
        /// <param name="sounds">The sequence's sounds, played from the Start-up thread.</param>
        /// <param name="playniteWindow">Playnite's own window, or <see cref="IntPtr.Zero"/> before it has one.</param>
        /// <param name="isStartupShown">Whether the core has Start-up covering the screen, asked on the UI thread.</param>
        /// <param name="signedIn">A profile signed in, called on the UI thread.</param>
        /// <param name="finished">The window has faded out and the thread is ending, called on the UI thread.</param>
        public StartupThread(
            StartupPlan plan,
            Dispatcher ui,
            GamingInputController pad,
            ISoundPlayer sounds,
            IntPtr playniteWindow,
            Func<bool> isStartupShown,
            Action<Profile> signedIn,
            Action finished)
        {
            this.ui = ui;
            this.isStartupShown = isStartupShown;
            this.signedIn = signedIn;
            this.finished = finished;
            this.playniteWindow = playniteWindow;

            // On the UI thread's own input, which is the only one that sees keys bound for Home.
            InputManager.Current.PreProcessInput += OnPreProcessInput;
            guarding = true;

            // Copied here, as only this thread may read them.
            var themeResources = StartupResources.Copy();
            var thread = new Thread(() => Run(plan, pad, sounds, themeResources))
            {
                IsBackground = true,
                Name = "XboxSeries Start-up",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <summary>Playnite's own window, from the UI thread, for one that had none when the sequence began.</summary>
        public void SetPlayniteWindow(IntPtr window) => playniteWindow = window;

        /// <summary>
        /// The core has shown Signing you in for long enough: the sequence ends, its window fading out to Home, and the
        /// keys go to Home again at once. Called on the UI thread.
        /// </summary>
        public void End()
        {
            StopGuarding();
            Post(() =>
            {
                sequence?.End();
                if (window == null)
                {
                    Finish();
                }
            });
        }

        /// <summary>A button only Playnite reads, the Guide button, for the sequence. Any thread may call it.</summary>
        public void Press(ControllerButton button, ButtonState state) => Post(() => sequence?.OnButton(button, state));

        /// <summary>Playnite is closing: the window closes at once, and the thread ends. Called on the UI thread.</summary>
        public void Shutdown()
        {
            StopGuarding();
            Post(() =>
            {
                window?.Shutdown();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        /// <summary>The Start-up thread: the sequence begun, its window made to show it, and its dispatcher run until it ends.</summary>
        private void Run(StartupPlan plan, GamingInputController pad, ISoundPlayer sounds, IReadOnlyDictionary<string, object> themeResources)
        {
            var here = Dispatcher.CurrentDispatcher;

            // Nothing on this thread may take Playnite down with it.
            here.UnhandledException += (_, e) =>
            {
                logger.Error(e.Exception, "The Start-up thread failed");
                e.Handled = true;
            };

            try
            {
                sequence = new StartupSequence(new DispatcherClock(here), sounds);
                sequence.SignedIn += TellSignedIn;
                sequence.Begin(plan);
                window = new StartupWindow(sequence, themeResources, pad.ButtonsDown, () => playniteWindow, ActivatePlaynite);
                window.FadedOut += () =>
                {
                    window.Shutdown();
                    Finish();
                };
                window.Follow();
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // The core waits to hear who signed in, holding the pad back from Home until it does.
                logger.Error(e, "Couldn't show the Start-up sequence; the last profile signs in without it");
                window = null;
                TellSignedIn(plan.LastProfile);
            }

            lock (gate)
            {
                dispatcher = here;
                foreach (var action in waiting)
                {
                    here.BeginInvoke(action);
                }

                waiting.Clear();
            }

            Dispatcher.Run();
        }

        /// <summary>Runs <paramref name="action"/> on the Start-up thread, in order, without waiting for it; from any thread.</summary>
        private void Post(Action action)
        {
            lock (gate)
            {
                if (dispatcher == null)
                {
                    waiting.Add(action);
                }
                else
                {
                    dispatcher.BeginInvoke(action);
                }
            }
        }

        /// <summary>Who signed in, to the core on the UI thread, once. On the Start-up thread.</summary>
        private void TellSignedIn(Profile profile)
        {
            if (toldSignedIn)
            {
                return;
            }

            toldSignedIn = true;
            ui.BeginInvoke(new Action(() => signedIn(profile)));
        }

        /// <summary>The window has faded out, or there was none: the UI thread is told, and this thread ends. On the Start-up thread.</summary>
        private void Finish()
        {
            ui.BeginInvoke(new Action(() => finished()));
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        }

        /// <summary>WPF's own active window, Playnite's, on its thread, as the window hands Windows' foreground back to it.</summary>
        private void ActivatePlaynite() => ui.BeginInvoke(new Action(() => Application.Current?.MainWindow?.Activate()));

        private void StopGuarding()
        {
            if (guarding)
            {
                guarding = false;
                InputManager.Current.PreProcessInput -= OnPreProcessInput;
            }
        }

        /// <summary>
        /// A key or pad button on its way to Home while the sequence shows, on the UI thread: it never gets there. Playnite's
        /// window takes the foreground as it first shows, and the Start-up window may not have taken it back before a
        /// press arrives. A real key is handed to the Start-up window, to do what it would have done there; Playnite's
        /// own key events for the pad are only held back, the pad being read on the Start-up thread.
        /// </summary>
        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!isStartupShown() || !(e.StagingItem.Input is KeyEventArgs key))
            {
                return;
            }

            // Playnite's window, or nowhere at all, as with it deactivated behind the Start-up window; a dialog of
            // Playnite's own is left its keys.
            var target = key.Source as DependencyObject ?? Keyboard.FocusedElement as DependencyObject;
            if (target != null && Window.GetWindow(target) != Application.Current?.MainWindow)
            {
                return;
            }

            e.Cancel();
            if (PlayniteInput.PadButtonOf(key) is Playnite.SDK.Events.ControllerInput pad)
            {
                if (key.RoutedEvent == Keyboard.KeyDownEvent)
                {
                    logger.Info($"Held back pad {pad} from Home during Start-up");
                }
            }
            else if (key.RoutedEvent == Keyboard.PreviewKeyDownEvent || key.RoutedEvent == Keyboard.PreviewKeyUpEvent)
            {
                var down = key.RoutedEvent == Keyboard.PreviewKeyDownEvent;
                var pressed = key.Key;
                var repeat = key.IsRepeat;
                if (down)
                {
                    logger.Info($"Held back {pressed} from Home during Start-up");
                }

                Post(() => window?.OnHeldBackKey(pressed, down, repeat));
            }
        }
    }
}
