using System;

namespace XboxSeries.Core
{
    /// <summary>Port for the running Playnite instance.</summary>
    public interface IPlayniteHost
    {
        /// <summary>Brings Playnite Fullscreen to the front, on whatever page it was showing.</summary>
        void BringToFront();

        /// <summary>Brings Playnite Fullscreen to the front on <paramref name="page"/>.</summary>
        void BringToFront(DashboardPage page);

        /// <summary>Starts a game through Playnite.</summary>
        void StartGame(Guid gameId);

        /// <summary>Opens the game's Game hub (Playnite's Game details) on the Dashboard, with focus on Play.</summary>
        void OpenGameHub(Guid gameId);
    }

    /// <summary>The Dashboard pages the Guide can take the user to.</summary>
    public enum DashboardPage
    {
        Home,
        MyGamesAndApps,
    }
}
