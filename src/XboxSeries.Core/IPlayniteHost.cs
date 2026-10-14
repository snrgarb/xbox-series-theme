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

        /// <summary>Opens Playnite's own game menu for the game: Manage game.</summary>
        void OpenGameMenu(Guid gameId);

        /// <summary>Marks the game as a favourite in Playnite's library, or unmarks it.</summary>
        void SetFavourite(Guid gameId, bool favourite);
    }

    /// <summary>The Dashboard pages the Guide can take the user to.</summary>
    public enum DashboardPage
    {
        Home,
        MyGamesAndApps,
    }
}
