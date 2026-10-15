using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Home's top bar's status area, injected as a custom element: the controller's battery and the clock, in place of
    /// Playnite's PC battery and clock. The Dashboard's profile shows the profile signed in, <see cref="State"/>'s
    /// Profile, and opens the Guide on Profile &amp; system with <see cref="OpenProfileCommand"/>. A <see cref="PluginUserControl"/>, because Playnite binds any other custom
    /// element's DataContext to the selected game.
    /// </summary>
    public sealed class StatusControl : PluginUserControl
    {
        private FrameworkElement topBar;

        internal StatusControl(ExtensionCore core)
        {
            Focusable = false;
            IsHitTestVisible = false;
            State = core.Status;
            Content = new StatusArea { DataContext = core.Status };
            OpenProfileCommand = new RelayCommand(core.OpenProfile);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>What the status area shows, and the profile signed in, which the top bar's profile shows.</summary>
        public StatusState State { get; }

        /// <summary>The top bar's profile: the Guide, on Profile &amp; system.</summary>
        public ICommand OpenProfileCommand { get; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            topBar = HomeRows.FindPart(this, "XboxTopBar");
            if (topBar != null)
            {
                topBar.PreviewKeyDown += OnTopBarPreviewKeyDown;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (topBar != null)
            {
                topBar.PreviewKeyDown -= OnTopBarPreviewKeyDown;
                topBar = null;
            }
        }

        /// <summary>
        /// Left and Right move to the top bar's next button that way, and stay put at its ends, as the console's. WPF's
        /// own choice of the nearest element that way would drop from My games &amp; apps to the Recent row below it,
        /// which is nearer than the profile far to its left.
        /// </summary>
        private void OnTopBarPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key != Key.Left && e.Key != Key.Right) || !(Keyboard.FocusedElement is UIElement focused))
            {
                return;
            }

            double X(UIElement element) => element.TranslatePoint(new Point(), topBar).X;
            var here = X(focused);
            var others = Buttons(topBar).Where(button => button != focused);
            var next = e.Key == Key.Left
                ? others.Where(button => X(button) < here).OrderByDescending(X).FirstOrDefault()
                : others.Where(button => X(button) > here).OrderBy(X).FirstOrDefault();

            e.Handled = true;
            next?.Focus();
        }

        /// <summary>The focusable elements on screen under <paramref name="parent"/>, not looking inside them.</summary>
        private static IEnumerable<UIElement> Buttons(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is UIElement element && element.Focusable && element.IsVisible && element.IsEnabled)
                {
                    yield return element;
                }
                else
                {
                    foreach (var button in Buttons(child))
                    {
                        yield return button;
                    }
                }
            }
        }
    }
}
