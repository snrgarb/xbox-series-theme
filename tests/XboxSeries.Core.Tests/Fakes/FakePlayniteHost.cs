using System;
using System.Collections.Generic;
using System.Linq;
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
            Window = windows.OpenPlayniteWindow();
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

        /// <summary>Every App Playnite was asked to start, in order.</summary>
        public List<App> StartedApps { get; } = new List<App>();

        public void StartApp(App app) => StartedApps.Add(app);

        public void OpenGameHub(Guid gameId) => OpenedHubs.Add(gameId);

        /// <summary>How many times Playnite was asked to close the Game hub.</summary>
        public List<Guid> ClosedGameHubs { get; } = new List<Guid>();

        public void CloseGameHub(Guid gameId) => ClosedGameHubs.Add(gameId);

        /// <summary>Every game Playnite was asked to install with its own Install, in order.</summary>
        public List<Guid> PlayniteInstalls { get; } = new List<Guid>();

        public void InstallGame(Guid gameId) => PlayniteInstalls.Add(gameId);

        /// <summary>Every game Playnite was asked to uninstall with its own Uninstall, in order.</summary>
        public List<Guid> PlayniteUninstalls { get; } = new List<Guid>();

        public void UninstallGame(Guid gameId) => PlayniteUninstalls.Add(gameId);

        /// <summary>Every game whose game menu Playnite was asked to open, in order.</summary>
        public List<Guid> OpenedGameMenus { get; } = new List<Guid>();

        public void OpenGameMenu(Guid gameId) => OpenedGameMenus.Add(gameId);

        /// <summary>Every non-play action Playnite was asked to run, by its place among the game's, in order.</summary>
        public List<(Guid GameId, int Index)> RanActions { get; } = new List<(Guid, int)>();

        public void RunGameAction(Guid gameId, int index) => RanActions.Add((gameId, index));

        /// <summary>Every change to a game's Hidden flag Playnite was asked to make, in order.</summary>
        public List<(Guid GameId, bool Hidden)> HiddenChanges { get; } = new List<(Guid, bool)>();

        public void SetHidden(Guid gameId, bool hidden) => HiddenChanges.Add((gameId, hidden));

        /// <summary>Whether the display can show HDR; it can't until a test says so.</summary>
        public bool IsHdrSupported { get; set; }

        /// <summary>Every change to a game's HDR switch Playnite was asked to make, in order.</summary>
        public List<(Guid GameId, bool Enabled)> HdrChanges { get; } = new List<(Guid, bool)>();

        public void SetSystemHdr(Guid gameId, bool enabled) => HdrChanges.Add((gameId, enabled));

        /// <summary>Every game whose Extensions picker Playnite was asked to open, in order.</summary>
        public List<Guid> OpenedExtensionMenus { get; } = new List<Guid>();

        public void OpenExtensionsMenu(Guid gameId) => OpenedExtensionMenus.Add(gameId);

        /// <summary>Every game Playnite was asked to remove from its library, in order.</summary>
        public List<Guid> RemovedGames { get; } = new List<Guid>();

        public void RemoveGame(Guid gameId) => RemovedGames.Add(gameId);

        /// <summary>Every change to a game's Favourite flag Playnite was asked to make, in order.</summary>
        public List<(Guid GameId, bool Favourite)> FavouriteChanges { get; } = new List<(Guid, bool)>();

        public void SetFavourite(Guid gameId, bool favourite) => FavouriteChanges.Add((gameId, favourite));

        private GameListFilter savedFilter;

        /// <summary>The sort and name filter <see cref="SaveUserFilter"/> kept with the user's filter.</summary>
        private (GameSort? Sort, SortDirection Direction, string Name) savedView;

        /// <summary>The filter and sort the core last put on the main list; null while it shows the user's own.</summary>
        public GameListFilter MainListFilter { get; private set; }

        public bool ShowsUsersOwnFilter => MainListFilter == null;

        /// <summary>How many times the user's own filter and sort were put back.</summary>
        public int UserFilterRestores { get; private set; }

        public void SaveUserFilter()
        {
            savedFilter = MainListFilter;
            savedView = (MainListSort, MainListSortDirection, MainListName);
        }

        public void RestoreUserFilter()
        {
            MainListFilter = savedFilter;
            (MainListSort, MainListSortDirection, MainListName) = savedView;
            UserFilterRestores++;
        }

        /// <summary>How many times the core put a filter and sort of its own on the main list, each a refilter for Playnite.</summary>
        public int MainListShows { get; private set; }

        public void ShowInMainList(GameListFilter filter)
        {
            // Its own preset, with its own sort and no name.
            MainListFilter = filter;
            MainListSort = filter.Sort;
            MainListSortDirection = filter.Direction;
            MainListName = string.Empty;
            MainListShows++;
        }

        /// <summary>Playnite's own sort of the main list, A–Z until something changes it.</summary>
        public GameSort? MainListSort { get; private set; } = GameSort.Name;

        public SortDirection MainListSortDirection { get; private set; } = SortDirection.Ascending;

        /// <summary>The main list's name filter; empty for none.</summary>
        public string MainListName { get; private set; } = string.Empty;

        /// <summary>How many times the core sorted the main list.</summary>
        public int MainListSorts { get; private set; }

        public void SortMainList(GameSort sort, SortDirection direction)
        {
            RequireUsersOwnFilter();
            MainListSort = sort;
            MainListSortDirection = direction;
            MainListSorts++;
        }

        public void FilterMainListByName(string name)
        {
            RequireUsersOwnFilter();
            MainListName = name ?? string.Empty;
        }

        /// <summary>
        /// The user switching Full library's tab to a preset with a sort of its own, as Playnite applies it; null for a
        /// preset without one. Playnite's name filter is the main list's, so it stays.
        /// </summary>
        public void SwitchPreset(GameSort? sort, SortDirection direction = SortDirection.Ascending)
        {
            if (sort != null)
            {
                MainListSort = sort;
                MainListSortDirection = direction;
            }
        }

        /// <summary>The saved filter preset selected on the main list, its tab lit on Full library; null for none.</summary>
        public Guid? SelectedPreset { get; private set; }

        public void SelectFilterPreset(Guid presetId)
        {
            // As Playnite applies a saved preset: its filter and, if it has one, its sort; the name filter stays.
            RequireUsersOwnFilter();
            var preset = Library.FilterPresets.Single(p => p.Id == presetId);
            SelectedPreset = presetId;
            SwitchPreset(preset.Sort, preset.SortDirection);
        }

        /// <summary>The library the main list is over, for <see cref="MainList"/> and the count of the user's filter.</summary>
        public LibrarySnapshot Library { get; set; }

        /// <summary>
        /// The names of the games Playnite's main list shows, in its order: the core's own filter while it shows one;
        /// otherwise the user's, here every game not hidden, narrowed by name and in Playnite's sort.
        /// </summary>
        public string[] MainList() =>
            (MainListFilter != null
                ? MainListFilter.Apply(Library.Games)
                : Sorted(UsersGames(MainListName), MainListSort, MainListSortDirection))
            .Select(g => g.Name)
            .ToArray();

        private IEnumerable<LibraryGame> UsersGames(string name) =>
            Library.Games.Where(g => !g.Hidden && g.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) >= 0
                && (SelectedPreset == null || Library.FilterPresets.Single(p => p.Id == SelectedPreset).Games.Contains(g.Id)));

        /// <summary>As Playnite sorts: games without the field last, ties by name.</summary>
        private static IEnumerable<LibraryGame> Sorted(IEnumerable<LibraryGame> games, GameSort? sort, SortDirection direction)
        {
            Func<LibraryGame, IComparable> key;
            switch (sort)
            {
                case GameSort.LastActivity: key = g => g.LastActivity; break;
                case GameSort.Added: key = g => g.Added; break;
                case GameSort.Modified: key = g => g.Modified; break;
                case GameSort.ReleaseDate: key = g => g.ReleaseDate; break;
                case GameSort.Playtime: key = g => g.Playtime; break;
                case GameSort.PlayCount: key = g => g.PlayCount; break;
                default: key = g => g.SortingName; break;
            }

            var byName = StringComparer.CurrentCultureIgnoreCase;
            var withField = games.OrderBy(g => key(g) == null);
            var ordered = direction == SortDirection.Descending ? withField.ThenByDescending(key) : withField.ThenBy(key);
            return ordered.ThenBy(g => g.SortingName, byName);
        }

        private void RequireUsersOwnFilter()
        {
            if (!ShowsUsersOwnFilter)
            {
                throw new InvalidOperationException("Only the user's own filter, as Full library shows it, is sorted or filtered by name");
            }
        }

        /// <summary>How many times the user's own filter was let go of, into Playnite's saved settings.</summary>
        public int UserFilterReleases { get; private set; }

        /// <summary>Whether the user's filter was ever let go of while the core's own was still on the main list.</summary>
        public bool ReleasedUnderOwnFilter { get; private set; }

        public void ReleaseUserFilter()
        {
            UserFilterReleases++;
            ReleasedUnderOwnFilter |= !ShowsUsersOwnFilter;
        }

        /// <summary>How many games the user's own filter shows; null when Playnite can't say.</summary>
        public int? UserFilterGameCount { get; set; }

        public int? CountUserFilterGames() =>
            UserFilterGameCount
            ?? (Library == null ? (int?)null : UsersGames(ShowsUsersOwnFilter ? MainListName : savedView.Name ?? string.Empty).Count());

        /// <summary>How many times Playnite was asked to open its Fullscreen settings.</summary>
        public int PlayniteSettingsOpened { get; private set; }

        public void OpenPlayniteSettings() => PlayniteSettingsOpened++;

        /// <summary>How many times Playnite was asked to open its Fullscreen main menu.</summary>
        public int MainMenuOpened { get; private set; }

        public void OpenMainMenu() => MainMenuOpened++;

        public bool IsLibraryBusy { get; set; }

        /// <summary>
        /// Every library update asked for, by its done callback, in order: called with the games added, or null for a run
        /// cancelled or failed.
        /// </summary>
        public List<Action<IReadOnlyList<Guid>>> LibraryUpdates { get; } = new List<Action<IReadOnlyList<Guid>>>();

        public void UpdateLibrary(Action<IReadOnlyList<Guid>> done) => LibraryUpdates.Add(done);

        /// <summary>
        /// Every metadata download asked for, with its games and done callback, in order: called with true when it
        /// finished, false when cancelled or failed.
        /// </summary>
        public List<(IReadOnlyList<Guid> Games, Action<bool> Done)> MetadataDownloads { get; } = new List<(IReadOnlyList<Guid>, Action<bool>)>();

        public void DownloadMetadata(IReadOnlyList<Guid> games, Action<bool> done) => MetadataDownloads.Add((games, done));

        /// <summary>
        /// Every download of the games missing metadata asked for, by its done callback, in order: true when it finished,
        /// false when cancelled or failed.
        /// </summary>
        public List<Action<bool>> MissingMetadataDownloads { get; } = new List<Action<bool>>();

        public void DownloadMissingMetadata(Action<bool> done) => MissingMetadataDownloads.Add(done);

        /// <summary>How many times Playnite was asked to stop its library update or metadata download.</summary>
        public int LibraryTaskCancels { get; private set; }

        public void CancelLibraryTask() => LibraryTaskCancels++;

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
