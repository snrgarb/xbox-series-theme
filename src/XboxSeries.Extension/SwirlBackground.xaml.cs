using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The console's green swirl, drawn in XAML: behind Start-up's Who's playing today? and Signing you in, and behind
    /// Home while no Recent-row tile has focus. It turns and drifts only while it is loaded and visible in the window in
    /// front, so it never keeps WPF rendering behind a running game; elsewhere it holds still where it is.
    /// </summary>
    public partial class SwirlBackground : UserControl
    {
        private readonly Storyboard swirl;
        private Window window;
        private bool begun;
        private bool running;

        public SwirlBackground()
        {
            InitializeComponent();
            swirl = (Storyboard)Resources["Swirl"];
            IsVisibleChanged += (_, __) => Animate();
            Loaded += (_, __) =>
            {
                Watch(Window.GetWindow(this));
                Animate();
            };
            Unloaded += (_, __) =>
            {
                Watch(null);
                Animate();
            };
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

        /// <summary>Sets the swirl moving as it shows in front, and holds it still as it hides, goes behind or is taken out.</summary>
        private void Animate()
        {
            var moves = IsLoaded && IsVisible && window != null && window.IsActive;
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
        }
    }
}
