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

        /// <summary>
        /// Keeps the user's own Fullscreen filter and sort, before the Dashboard first puts its own on the main list.
        /// They are saved settings, so they are kept until <see cref="RestoreUserFilter"/>, even across a crash.
        /// </summary>
        void SaveUserFilter();

        /// <summary>Puts the filter and sort kept by <see cref="SaveUserFilter"/> back on the main list.</summary>
        void RestoreUserFilter();

        /// <summary>Shows <paramref name="filter"/>'s games in the main list, in its order, as a filter preset the extension builds.</summary>
        void ShowInMainList(GameListFilter filter);

        /// <summary>
        /// How many games the main list shows under the user's own filter: the one kept by <see cref="SaveUserFilter"/>
        /// while the Dashboard's is up, otherwise the current one. Null when Playnite can't say yet.
        /// </summary>
        int? CountUserFilterGames();

        /// <summary>Opens Playnite's own Fullscreen settings.</summary>
        void OpenSettings();

        /// <summary>Carries out <paramref name="command"/> as Playnite's own Fullscreen menu does, closing Playnite cleanly first where it must.</summary>
        void Power(PowerCommand command);
    }

    /// <summary>What the Guide's Power can do, beyond Close.</summary>
    public enum PowerCommand
    {
        TurnOffPc,
        RestartPc,
        Sleep,
        ExitPlaynite,
    }

    /// <summary>The Dashboard pages the Guide can take the user to.</summary>
    public enum DashboardPage
    {
        Home,
        MyGamesAndApps,
    }
}
