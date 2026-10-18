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

        /// <summary>Starts an App: its path with its arguments, as Playnite's own Software tools menu does.</summary>
        void StartApp(App app);

        /// <summary>Opens the game's Game hub (Playnite's Game details) on the Dashboard, with focus on Play.</summary>
        void OpenGameHub(Guid gameId);

        /// <summary>Closes <paramref name="gameId"/>'s Game hub, if it is the one open, back to the page it was opened from.</summary>
        void CloseGameHub(Guid gameId);

        /// <summary>Opens Playnite's own game menu for the game: Manage game.</summary>
        void OpenGameMenu(Guid gameId);

        /// <summary>Marks the game as a favourite in Playnite's library, or unmarks it.</summary>
        void SetFavourite(Guid gameId, bool favourite);

        /// <summary>
        /// Keeps the user's own Fullscreen filter and sort, before the Dashboard puts its own on the main list. They are
        /// kept until <see cref="ReleaseUserFilter"/>, even across a crash.
        /// </summary>
        void SaveUserFilter();

        /// <summary>
        /// Puts the filter and sort kept by <see cref="SaveUserFilter"/> back on the main list, still keeping them while
        /// My games &amp; apps is up, as Full library has them.
        /// </summary>
        void RestoreUserFilter();

        /// <summary>
        /// My games &amp; apps is done with the main list, closing or Playnite stopping, with the user's own filter and
        /// sort back on it: they go into Playnite's saved settings, and the copy kept for a crash goes.
        /// </summary>
        void ReleaseUserFilter();

        /// <summary>Shows <paramref name="filter"/>'s games in the main list, in its order, as a filter preset the extension builds.</summary>
        void ShowInMainList(GameListFilter filter);

        /// <summary>
        /// Selects the saved filter preset <paramref name="presetId"/> on the main list, as its tab on Full library does:
        /// its filter and, if it has one, its sort. Only while it shows the user's own filter, as Full library.
        /// </summary>
        void SelectFilterPreset(Guid presetId);

        /// <summary>Playnite's own sort of the main list; null for one the core doesn't know, as by platform.</summary>
        GameSort? MainListSort { get; }

        /// <summary>Which way Playnite's own sort of the main list runs.</summary>
        SortDirection MainListSortDirection { get; }

        /// <summary>
        /// Sorts the main list with Playnite's own sort, leaving its filter and preset as they are. Only while it shows
        /// the user's own filter, as Full library.
        /// </summary>
        void SortMainList(GameSort sort, SortDirection direction);

        /// <summary>
        /// Narrows the main list to games whose name holds <paramref name="name"/>, with Playnite's own name filter, on top
        /// of its filter and preset; empty shows them whole. Only while it shows the user's own filter, as Full library.
        /// </summary>
        void FilterMainListByName(string name);

        /// <summary>
        /// Playnite's own entry of the main list's name filter, its on-screen keyboard in Fullscreen, which narrows the
        /// list as the user types and puts the name back if cancelled. Returns the name the main list is filtered by once
        /// it closes. Only while it shows the user's own filter, as Full library.
        /// </summary>
        string TypeMainListName();

        /// <summary>
        /// How many games the main list shows under the user's own filter: the one kept by <see cref="SaveUserFilter"/>
        /// while the Dashboard's is up, otherwise the current one. Null when Playnite can't say yet.
        /// </summary>
        int? CountUserFilterGames();

        /// <summary>Opens Playnite settings: Playnite's own Fullscreen Settings window.</summary>
        void OpenPlayniteSettings();

        /// <summary>Opens Playnite's own Fullscreen main menu: Switch to Desktop mode, Update library, Minimise, Exit.</summary>
        void OpenMainMenu();

        /// <summary>Carries out <paramref name="command"/> as Playnite's own Fullscreen menu does, closing Playnite cleanly first where it must.</summary>
        void Power(PowerCommand command);
#if DEBUG

        /// <summary>Debug builds only: posts a Playnite notification, which comes back as any new one does.</summary>
        void AddNotification(string id, string text);
#endif
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
        Settings,
    }
}
