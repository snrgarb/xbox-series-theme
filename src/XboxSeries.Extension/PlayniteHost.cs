using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the running Playnite instance.</summary>
    internal sealed class PlayniteHost : IPlayniteHost
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI api;

        public PlayniteHost(IPlayniteAPI api)
        {
            this.api = api;
        }

        public void BringToFront()
        {
            // Playnite's own main window (Fullscreen). Not GetCurrentAppWindow(): that can be the Overlay, which is active.
            var main = Application.Current?.MainWindow;
            if (main == null)
            {
                logger.Warn("Can't bring Playnite to the front: no main window");
                return;
            }

            var handle = new WindowInteropHelper(main).Handle;
            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
            }

            NativeMethods.BringToForeground(handle);
        }

        public void BringToFront(DashboardPage page)
        {
            BringToFront();
            switch (page)
            {
                case DashboardPage.Home:
                    // Home is Playnite's list view; a Game hub open on top of it is its details view.
                    if (api.MainView.ActiveFullscreenView == FullscreenView.Details)
                    {
                        api.MainView.ToggleFullscreenView();
                    }

                    break;
                case DashboardPage.MyGamesAndApps:
                    // The page itself lands with #35; until then this only brings Playnite to the front.
                    break;
            }
        }

        public void StartGame(Guid gameId)
        {
            api.StartGame(gameId);
        }

        public void OpenGameHub(Guid gameId)
        {
            // The hub is Playnite's details view of the main list's selected game, which opens with focus on Play.
            // A game outside the user's current filter can't be selected; opening details then would show another game.
            api.MainView.SelectGame(gameId);
            if (!api.MainView.SelectedGames.Any(g => g.Id == gameId))
            {
                logger.Warn($"Can't open the Game hub of {gameId}: it isn't in the main list's current filter");
                return;
            }

            if (api.MainView.ActiveFullscreenView == FullscreenView.List)
            {
                api.MainView.ToggleFullscreenView();
            }
        }
    }
}
