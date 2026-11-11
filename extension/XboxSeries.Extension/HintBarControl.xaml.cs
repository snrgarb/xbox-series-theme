using System;
using System.ComponentModel;
using System.Windows;
using Playnite.SDK.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The hint bar, injected over the Dashboard as a custom element. It shows the core's <see cref="HomeState.Hints"/>
    /// and decides nothing itself: it shows while there are any and hides once there are none.
    /// It also tells the core when focus is on Playnite's own parts with a menu, which no control of the extension's is
    /// on to report: a game in My games &amp; apps' grid (Playnite's main list) and the Game hub. Each loses focus as
    /// Playnite leaves the foreground, as when a dialog opens in its own window.
    /// It sits over the whole page and the Game hub alike, so it also keeps the hub's backdrop (<see cref="HubBackdrop"/>):
    /// the page under the hub, taken as the hub opens and let go as it closes.
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class HintBarControl : PluginUserControl
    {
        private readonly ExtensionCore core;
        private FrameworkElement mainList;
        private FrameworkElement gameHub;
        private HubBackdrop backdrop;

        internal HintBarControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Loaded again without an Unloaded between, it lets go of what it followed before following afresh.
            Detach();
            core.Home.PropertyChanged += OnHomeChanged;
            Follow();

            mainList = HomeRows.FindPart(this, "PART_ListGameItems");
            if (mainList != null)
            {
                mainList.IsKeyboardFocusWithinChanged += OnMainListFocusChanged;
            }

            gameHub = HomeRows.FindGameDetails(this);
            if (gameHub != null)
            {
                gameHub.IsKeyboardFocusWithinChanged += OnGameHubFocusChanged;
            }

            backdrop = HubBackdrop.Follow(core.HubBackdrop, gameHub, HomeRows.FindPart(this, "PART_ViewHost"));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var followedList = mainList != null;
            var followedHub = gameHub != null;
            Detach();
            if (followedList)
            {
                core.LeaveGameGrid();
            }

            if (followedHub)
            {
                core.LeaveGameHub();
            }
        }

        /// <summary>Stops following the core, Playnite's main list and the Game hub, letting go of the hub's backdrop.</summary>
        private void Detach()
        {
            core.Home.PropertyChanged -= OnHomeChanged;
            backdrop?.Stop();
            backdrop = null;
            if (mainList != null)
            {
                mainList.IsKeyboardFocusWithinChanged -= OnMainListFocusChanged;
                mainList = null;
            }

            if (gameHub != null)
            {
                gameHub.IsKeyboardFocusWithinChanged -= OnGameHubFocusChanged;
                gameHub = null;
            }
        }

        private void OnMainListFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                core.FocusGameGrid();
            }
            else
            {
                core.LeaveGameGrid();
            }
        }

        private void OnGameHubFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                core.FocusGameHub();
            }
            else
            {
                core.LeaveGameHub();
            }
        }

        private void OnHomeChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HomeState.Hints))
            {
                Follow();
            }
        }

        /// <summary>
        /// With hints, the bar shows them at once, on the frame focus lands; with none, it is hidden at once, rather than
        /// collapsed, so it keeps its place and size and coming back lays nothing out. No fade, nor waiting for the page
        /// to settle: on My games &amp; apps' grid, as its tiles are built, either left it coming up late.
        /// </summary>
        private void Follow()
        {
            var hints = core.Home.Hints;
            if (hints.Count == 0)
            {
                Bar.Visibility = Visibility.Hidden;
                return;
            }

            if (Hints.ItemsSource != hints)
            {
                Hints.ItemsSource = hints;
            }

            Bar.Visibility = Visibility.Visible;
        }
    }
}
