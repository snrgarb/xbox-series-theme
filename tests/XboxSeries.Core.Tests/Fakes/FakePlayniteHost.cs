using System;
using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>Playnite Fullscreen as one window on a <see cref="FakeWindowSystem"/>.</summary>
    public sealed class FakePlayniteHost : IPlayniteHost
    {
        private readonly FakeWindowSystem windows;

        public FakePlayniteHost(FakeWindowSystem windows)
        {
            this.windows = windows;
            Window = windows.OpenWindow();
        }

        /// <summary>Playnite Fullscreen's main window.</summary>
        public IntPtr Window { get; }

        /// <summary>The Dashboard page Playnite was last brought to the front on, or null if never.</summary>
        public DashboardPage? Page { get; private set; }

        /// <summary>Every game Playnite was asked to start, in order.</summary>
        public List<Guid> StartedGames { get; } = new List<Guid>();

        public void BringToFront() => windows.SetForeground(Window);

        public void BringToFront(DashboardPage page)
        {
            BringToFront();
            Page = page;
        }

        /// <summary>Every game whose Game hub Playnite was asked to open, in order.</summary>
        public List<Guid> OpenedHubs { get; } = new List<Guid>();

        public void StartGame(Guid gameId) => StartedGames.Add(gameId);

        public void OpenGameHub(Guid gameId) => OpenedHubs.Add(gameId);

        /// <summary>Every game whose game menu Playnite was asked to open, in order.</summary>
        public List<Guid> OpenedGameMenus { get; } = new List<Guid>();

        public void OpenGameMenu(Guid gameId) => OpenedGameMenus.Add(gameId);

        /// <summary>Every change to a game's Favourite flag Playnite was asked to make, in order.</summary>
        public List<(Guid GameId, bool Favourite)> FavouriteChanges { get; } = new List<(Guid, bool)>();

        public void SetFavourite(Guid gameId, bool favourite) => FavouriteChanges.Add((gameId, favourite));

        private GameListFilter savedFilter;

        /// <summary>The filter and sort the core last put on the main list; null while it shows the user's own.</summary>
        public GameListFilter MainListFilter { get; private set; }

        public bool ShowsUsersOwnFilter => MainListFilter == null;

        /// <summary>How many times the user's own filter and sort were put back.</summary>
        public int UserFilterRestores { get; private set; }

        public void SaveUserFilter() => savedFilter = MainListFilter;

        public void RestoreUserFilter()
        {
            MainListFilter = savedFilter;
            UserFilterRestores++;
        }

        public void ShowInMainList(GameListFilter filter) => MainListFilter = filter;

        /// <summary>How many games the user's own filter shows; null when Playnite can't say.</summary>
        public int? UserFilterGameCount { get; set; }

        public int? CountUserFilterGames() => UserFilterGameCount;

        /// <summary>How many times Playnite was asked to open its Fullscreen settings.</summary>
        public int PlayniteSettingsOpened { get; private set; }

        public void OpenPlayniteSettings() => PlayniteSettingsOpened++;

        /// <summary>How many times Playnite was asked to open its Fullscreen main menu.</summary>
        public int MainMenuOpened { get; private set; }

        public void OpenMainMenu() => MainMenuOpened++;

        /// <summary>Every power command Playnite was asked to carry out, in order.</summary>
        public List<PowerCommand> PowerCommands { get; } = new List<PowerCommand>();

        public void Power(PowerCommand command) => PowerCommands.Add(command);
#if DEBUG

        /// <summary>Every notification posted to Playnite, in order.</summary>
        public List<(string Id, string Text)> PostedNotifications { get; } = new List<(string, string)>();

        public void AddNotification(string id, string text) => PostedNotifications.Add((id, text));
#endif
    }
}
