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
    /// A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's DataContext to the selected game.
    /// </summary>
    public partial class HintBarControl : PluginUserControl
    {
        private readonly ExtensionCore core;
        private FrameworkElement mainList;
        private FrameworkElement gameHub;

        internal HintBarControl(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
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
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            core.Home.PropertyChanged -= OnHomeChanged;
            if (mainList != null)
            {
                mainList.IsKeyboardFocusWithinChanged -= OnMainListFocusChanged;
                mainList = null;
                core.LeaveGameGrid();
            }

            if (gameHub != null)
            {
                gameHub.IsKeyboardFocusWithinChanged -= OnGameHubFocusChanged;
                gameHub = null;
                core.LeaveGameHub();
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
