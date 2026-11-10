using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The console's green swirl, drawn in XAML: behind Start-up's Who's playing today? and Signing you in, and behind
    /// Home while no Recent-row tile has focus. It turns and drifts only while it is loaded and visible in the window in
    /// front, so it never keeps WPF rendering behind a running game; elsewhere it holds still where it is. With
    /// Settings' Reduce motion on it stands still wherever it shows, and extensions.log says so.
    /// </summary>
    public partial class SwirlBackground : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly Storyboard swirl;
        private Window window;
        private bool begun;
        private bool running;

        /// <summary>What the log last said of this swirl shown in front: true moving, false still, null nothing yet.</summary>
        private bool? saidMoving;

        public SwirlBackground()
        {
            InitializeComponent();
            swirl = (Storyboard)Resources["Swirl"];
            IsVisibleChanged += (_, __) => Animate();
            Loaded += (_, __) =>
            {
                Watch(Window.GetWindow(this));
                Motion.Changed -= OnMotionChanged;
                Motion.Changed += OnMotionChanged;
                Animate();
            };
            Unloaded += (_, __) =>
            {
                Watch(null);
                Motion.Changed -= OnMotionChanged;
                Animate();
            };
        }

        /// <summary>
        /// Reduce motion turned over, on Playnite's UI thread: the swirl stops or moves again on its own, which for
        /// Start-up's is another. One whose thread has ended without unloading it, as Start-up's can, lets go here.
        /// </summary>
        private void OnMotionChanged()
        {
            if (Dispatcher.HasShutdownStarted)
            {
                Motion.Changed -= OnMotionChanged;
            }
            else if (Dispatcher.CheckAccess())
            {
                Animate();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(Animate));
            }
        }

        /// <summary>Follows whether the swirl's window is in front, which it moves only while.</summary>
        private void Watch(Window shownIn)
        {
            if (window != null)
            {
                window.Activated -= OnWindowActivation;
                window.Deactivated -= OnWindowActivation;
            }

            window = shownIn;
            if (window != null)
            {
                window.Activated += OnWindowActivation;
                window.Deactivated += OnWindowActivation;
            }
        }

        private void OnWindowActivation(object sender, EventArgs e) => Animate();

        /// <summary>
        /// Sets the swirl moving as it shows in front, and holds it still as it hides, goes behind or is taken out, and
        /// wherever it shows while Reduce motion is on.
        /// </summary>
        private void Animate()
        {
            var inFront = IsLoaded && IsVisible && window != null && window.IsActive;
            var moves = inFront && !Motion.Reduced;
            if (moves && !running)
            {
                if (!begun)
                {
                    swirl.Begin(this, true);
                    begun = true;
                }
                else
                {
                    swirl.Resume(this);
                }

                running = true;
            }
            else if (!moves && running)
            {
                swirl.Pause(this);
                running = false;
            }

            // Only of a swirl shown in front, and only as it changes between moving and standing still for Reduce
            // motion: hiding or going behind holds it still too, and says nothing. The end-to-end test reduce-motion
            // reads these.
            if (inFront && saidMoving != moves)
            {
                saidMoving = moves;
                logger.Info(moves ? "[Motion] swirl moving" : "[Motion] swirl still");
            }
        }
    }
}
