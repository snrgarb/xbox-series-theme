using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The extension's single facade. Controller button events, library snapshots and port events go in;
    /// view state comes out, and commands go out through the ports.
    /// </summary>
    public sealed class ExtensionCore
    {
        /// <summary>How many recent games fit on the landing tab, as on the console.</summary>
        private const int RecentGameCount = 4;

        /// <summary>
        /// How many games the Recent row on Home holds, before its Play history tile. The console shows 8; 7 lets the
        /// row span the top bar on 16:9 and 16:10 screens at a size close to the console's.
        /// </summary>
        private const int RecentRowCount = 7;

        /// <summary>How many games the Play history tile's collage shows.</summary>
        private const int PlayHistoryCollageCount = 4;

        /// <summary>
        /// How many games a Group on Home shows before its See all tile: as many as the Recent row, so that every row
        /// on Home is the same width.
        /// </summary>
        private const int GroupTileCount = RecentRowCount;

        /// <summary>How soon a second A on the same Home tile counts as a double-tap, which launches the game.</summary>
        private static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(300);

        /// <summary>How long focus has to stay on a Recent-row tile before the hero follows it, so scrolling past doesn't flicker it.</summary>
        private static readonly TimeSpan HeroSettle = TimeSpan.FromSeconds(1.5);

        private static readonly TimeSpan ReleaseFallback = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(80);

        /// <summary>How long a game has to close by itself before the force-quit prompt.</summary>
        private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

        private readonly IClock clock;
        private readonly IWindowSystem windowSystem;
        private readonly IPlayniteHost playnite;
        private readonly ISoundPlayer sounds;
        private LibrarySnapshot library = LibrarySnapshot.Empty;
        private Guid? runningGame;
        private IntPtr recordedForeground;
        private ControllerButton? dismissButton;
        private IDisposable releaseFallback;
        private Action pendingHandBack;
        private ControllerButton? heldDirection;
        private IDisposable repeatTimer;
        private Guid? quittingGame;
        private IDisposable quitTimer;
        private Guid? startAfterQuit;
        private Guid? startingGame;
        private Guid? heroGame;
        private Guid? tappedGame;
        private IDisposable tapTimer;
        private IDisposable heroTimer;
        private HomeTile focusedTile;

        /// <summary>Whether A, still held, answered the tile menu: Playnite's own Enter for it must not choose the tile.</summary>
        private bool tileMenuHoldsA;

        /// <summary>Whether My games &amp; apps has put its own filter on the main list, with the user's kept to give back.</summary>
        private bool ownsMainList;

        public ExtensionCore(IClock clock, IWindowSystem windowSystem, IPlayniteHost playnite, ISoundPlayer sounds)
        {
            this.clock = clock;
            this.windowSystem = windowSystem;
            this.playnite = playnite;
            this.sounds = sounds;
        }

        public GuideState Guide { get; } = new GuideState();

        public HomeState Home { get; } = new HomeState();

        public MyGamesState MyGames { get; } = new MyGamesState();

        public void OnLibraryChanged(LibrarySnapshot snapshot)
        {
            library = snapshot;
            RefreshHome();
        }

        /// <summary>
        /// Playnite's game starting event, raised for every launch, including the Dashboard's own Play. Returns
        /// whether the launch may go ahead. A launch while another game runs is held back and asks
        /// "Quit X and start Y?" instead; one while a game is quitting or launching is simply refused.
        /// </summary>
        public bool OnGameStarting(Guid gameId)
        {
            if (gameId == startingGame || gameId == runningGame)
            {
                return true;
            }

            if (runningGame != null || IsQuittingOrLaunching)
            {
                PlayGame(gameId);
                return false;
            }

            startingGame = gameId;
            return true;
        }

        /// <summary>Playnite's game started event.</summary>
        public void OnGameStarted(Guid gameId)
        {
            if (quittingGame != null && quittingGame != gameId)
            {
                // Another game started from elsewhere mid-quit; the quit, and any prompt about it, no longer applies.
                Guide.Dialog = null;
                EndQuit();
            }

            runningGame = gameId;
            startingGame = null;
            RefreshHome();
        }

        /// <summary>Playnite's game startup cancelled event: a launch that never got going.</summary>
        public void OnGameStartupCancelled(Guid gameId)
        {
            if (startingGame == gameId)
            {
                startingGame = null;
            }
        }

        /// <summary>Playnite's game stopped event.</summary>
        public void OnGameStopped(Guid gameId)
        {
            if (startingGame == gameId)
            {
                startingGame = null;
            }

            if (runningGame != gameId)
            {
                return;
            }

            runningGame = null;
            RefreshHome();

            // Any menu or prompt was about the game that has just gone.
            Guide.Dialog = null;
            var next = quittingGame == gameId ? startAfterQuit : null;
            EndQuit();
            if (next != null)
            {
                LaunchAfterQuit(next.Value);
            }
            else if (Guide.IsOpen)
            {
                RefreshLandingTab();
            }
        }

        /// <summary>
        /// The quit flow, shared by the Guide, the Game hub and the tile menu: a graceful close first, then a
        /// force-quit prompt if the game hasn't stopped by the timeout.
        /// </summary>
        public void QuitGame(Guid gameId)
        {
            if (gameId != runningGame || quittingGame != null)
            {
                return;
            }

            quittingGame = gameId;
            var window = RunningGameWindow();
            if (window != IntPtr.Zero)
            {
                windowSystem.CloseGracefully(window);
            }

            ArmQuitTimeout();
        }

        /// <summary>
        /// Plays a game, shared by the Guide, the Game hub and the tile menu. With another game running it asks
        /// "Quit X and start Y?" first. Two games are never launched at once: while one is quitting or launching,
        /// this does nothing.
        /// </summary>
        public void PlayGame(Guid gameId)
        {
            if (IsQuittingOrLaunching || gameId == runningGame)
            {
                return;
            }

            if (runningGame == null)
            {
                Launch(gameId);
                return;
            }

            var running = runningGame.Value;
            ShowDialog(new Dialog(
                $"Quit {NameOf(running)} and start {NameOf(gameId)}?",
                null,
                isPrompt: true,
                CloseDialog,
                new DialogOption("Quit and start", DialogIcon.Play, () =>
                {
                    CloseDialog();
                    QuitGame(running);
                    if (quittingGame == running)
                    {
                        startAfterQuit = gameId;
                    }
                }),
                new DialogOption("Cancel", DialogIcon.Cancel, CloseDialog)));
        }

        public void OnControllerButton(ControllerButton button, ButtonState state)
        {
            if (state == ButtonState.Released)
            {
                if (button == dismissButton)
                {
                    HandFocusBack();
                }

                if (button == heldDirection)
                {
                    StopRepeat();
                }

                if (button == ControllerButton.A)
                {
                    tileMenuHoldsA = false;
                }

                return;
            }

            var dialog = ActiveDialog;
            if (button == ControllerButton.Guide && !Guide.IsOpen)
            {
                sounds.Play(UiSound.OpenMenu);
                OpenGuide();
            }
            else if (button == ControllerButton.B && dialog != null)
            {
                sounds.Play(UiSound.PromptClose);
                dialog.Decline();
            }
            else if ((button == ControllerButton.Guide || button == ControllerButton.B) && Guide.IsOpen)
            {
                Dismiss(button, ReturnToRecordedWindow);
            }
            else if (button == ControllerButton.A && dialog != null)
            {
                tileMenuHoldsA = dialog == Home.TileMenu;
                Choose(dialog, dialog.FocusedOption);
            }
            else if (button == ControllerButton.A && Guide.IsOpen)
            {
                Select(Guide.FocusedItem, handBack => Dismiss(button, handBack));
            }
            else if (button == ControllerButton.Start && Guide.IsOpen && dialog == null)
            {
                ShowMenuFor(Guide.FocusedItem);
            }
            else if (button == ControllerButton.Start && dialog == null && focusedTile != null && !Guide.IsOverlayShown)
            {
                ShowTileMenu(focusedTile);
            }
            else if (button == ControllerButton.A && dialog == null && focusedTile != null && !Guide.IsOverlayShown)
            {
                // Playnite hands the pad's A to WPF as a key with no Key, which never clicks the tile.
                ChooseHomeTile(focusedTile);
            }
            else if ((button == ControllerButton.DPadUp || button == ControllerButton.DPadDown) && (Guide.IsOpen || dialog != null))
            {
                StopRepeat();
                var by = button == ControllerButton.DPadUp ? -1 : 1;
                MoveFocus(by);
                heldDirection = button;
                repeatTimer = clock.After(RepeatDelay, () => Repeat(by));
            }
        }

        /// <summary>
        /// A game tile on Home got focus: Menu now opens its tile menu. Once focus has stayed on a Recent-row tile for a
        /// moment, the hero also crossfades to its game's art, which stays once focus moves on.
        /// </summary>
        public void FocusHomeTile(HomeTile tile)
        {
            focusedTile = tile;
            CancelHeroFollow();
            if (!Home.RecentRow.Contains(tile))
            {
                return;
            }

            var gameId = tile.GameId;
            heroTimer = clock.After(HeroSettle, () =>
            {
                heroTimer = null;
                ShowHeroOf(gameId);
            });
        }

        /// <summary>Focus left a game tile on Home, to elsewhere on the Dashboard or with Playnite leaving the foreground.</summary>
        public void LeaveHomeTile(HomeTile tile)
        {
            if (focusedTile == tile)
            {
                focusedTile = null;
                CancelHeroFollow();
            }
        }

        /// <summary>Puts the game's art behind Home. A game with no art at all leaves the last art up rather than blanking the hero.</summary>
        private void ShowHeroOf(Guid gameId)
        {
            if (!Home.RecentRow.Any(t => t.GameId == gameId))
            {
                return;
            }

            var art = HeroArtOf(GameById(gameId));
            if (art != null)
            {
                heroGame = gameId;
                Home.Hero = art;
            }
        }

        private void CancelHeroFollow()
        {
            heroTimer?.Dispose();
            heroTimer = null;
        }

        /// <summary>The tile menu went from the Dashboard without an answer, as when Playnite leaves the foreground.</summary>
        public void CloseTileMenu()
        {
            if (Home.TileMenu != null)
            {
                StopRepeat();
                Home.TileMenu = null;
            }
        }

        /// <summary>
        /// Menu on a game tile: Play game (Quit while it runs), Add to or Remove from Favourites, and Manage game.
        /// Each closes the menu first, so focus is back on the tile before anything else opens.
        /// </summary>
        private void ShowTileMenu(HomeTile tile)
        {
            var game = GameById(tile.GameId);
            if (game == null)
            {
                return;
            }

            var gameId = game.Id;
            var play = gameId == runningGame
                ? new DialogOption("Quit", DialogIcon.Quit, () => FromTileMenu(() => QuitGame(gameId)))
                : new DialogOption("Play game", DialogIcon.Play, () => FromTileMenu(() => PlayGame(gameId)));
            var favourite = game.Favorite
                ? new DialogOption("Remove from Favourites", DialogIcon.Unfavourite, () => FromTileMenu(() => playnite.SetFavourite(gameId, false)))
                : new DialogOption("Add to Favourites", DialogIcon.Favourite, () => FromTileMenu(() => playnite.SetFavourite(gameId, true)));
            var manage = new DialogOption("Manage game", DialogIcon.Manage, () => FromTileMenu(() => playnite.OpenGameMenu(gameId)));

            // A pending A on the tile would otherwise open its Game hub under the menu.
            CancelTap();
            StopRepeat();
            sounds.Play(UiSound.PromptOpen);
            Home.TileMenu = new Dialog(game.Name, null, isPrompt: false, CloseTileMenu, play, favourite, manage);
        }

        private void FromTileMenu(Action action)
        {
            CloseTileMenu();
            action();
        }

        /// <summary>
        /// A on a Home tile. It opens the game's Game hub, unless a second A on the same tile follows within the
        /// double-tap window: that plays the game instead, so the hub waits out the window before opening.
        /// </summary>
        public void ChooseHomeTile(HomeTile tile)
        {
            if (tileMenuHoldsA)
            {
                return;
            }

            var gameId = tile.GameId;
            if (tapTimer != null && tappedGame == gameId)
            {
                CancelTap();
                if (gameId == runningGame)
                {
                    ReturnToRunningGame();
                }
                else
                {
                    PlayGame(gameId);
                }

                return;
            }

            CancelTap();
            tappedGame = gameId;
            tapTimer = clock.After(DoubleTapWindow, () =>
            {
                CancelTap();
                playnite.OpenGameHub(gameId);
            });
        }

        private void CancelTap()
        {
            tappedGame = null;
            tapTimer?.Dispose();
            tapTimer = null;
        }

        /// <summary>
        /// Rebuilds the Recent row: the running game first, then played games by last played, padded with recently
        /// added ones, never hidden ones. The Play history tile's collage is the played games that come next.
        /// </summary>
        private void RefreshHome()
        {
            var running = GameById(runningGame);
            var played = PlayedGames().ToList();
            var unplayed = library.Games
                .Where(g => g.LastActivity == null && !g.Hidden)
                .OrderByDescending(g => g.Added ?? DateTime.MinValue);
            var row = (running == null ? played : new[] { running }.Concat(played))
                .Concat(unplayed)
                .Distinct()
                .Take(RecentRowCount)
                .ToList();

            Home.RecentRow = row.Select(g => new HomeTile(g.Id, g.Name, g.CoverImage, g.Source, g == running)).ToList();
            Home.PlayHistory = played.Where(g => !row.Contains(g))
                .Take(PlayHistoryCollageCount)
                .Select(g => new HomeTile(g.Id, g.Name, g.CoverImage, g.Source, isRunning: false))
                .ToList();
            Home.Groups = Groups();
            MyGames.GameCount = GameListFilter.Installed.Apply(library.Games).Count();
            RefreshPlayHistory();

            // The hero stays on the last focused game while it exists, and starts on the first game in the row.
            var hero = row.Concat(played).FirstOrDefault(g => g.Id == heroGame) ?? row.FirstOrDefault();
            heroGame = hero?.Id;
            Home.Hero = HeroArtOf(hero);
        }

        /// <summary>
        /// One Group per filter preset flagged for Fullscreen quick selection, in Playnite's order, sorted by the
        /// preset's own sort. With none flagged, a Favourites Group stands in, in name order and without hidden games.
        /// </summary>
        private IReadOnlyList<HomeGroup> Groups()
        {
            var groups = library.FilterPresets
                .Where(p => p.ShowInFullscreenQuickSelection)
                .Select(p => Group(
                    p.Name,
                    p.Id,
                    p.Games.Select(id => GameById(id)).Where(g => g != null),
                    p.Sort ?? GameSort.Name,
                    p.SortDirection))
                .ToList();
            if (groups.Count == 0)
            {
                groups.Add(Group(
                    "Favourites",
                    null,
                    library.Games.Where(g => g.Favorite && !g.Hidden),
                    GameSort.Name,
                    SortDirection.Ascending));
            }

            return groups;
        }

        /// <summary>A Group of its first games, and the See all tile's collage of the ones after them.</summary>
        private HomeGroup Group(string name, Guid? presetId, IEnumerable<LibraryGame> games, GameSort sort, SortDirection direction)
        {
            var sorted = GameListFilter.Sorted(games, sort, direction).ToList();
            return new HomeGroup(
                name,
                presetId,
                sorted.Take(GroupTileCount)
                    .Select(TileOf)
                    .ToList(),
                sorted.Skip(GroupTileCount)
                    .Take(PlayHistoryCollageCount)
                    .Select(g => new HomeTile(g.Id, g.Name, g.CoverImage, g.Source, isRunning: false))
                    .ToList());
        }

        /// <summary>A game's tile, marked when the game is running.</summary>
        private HomeTile TileOf(LibraryGame game) => new HomeTile(game.Id, game.Name, game.CoverImage, game.Source, game.Id == runningGame);

        /// <summary>
        /// Opens My games &amp; apps on <paramref name="item"/>, or moves it there. Full library is the main list under the
        /// user's own filter and sort. Games and Play history take the main list over, keeping the user's filter and sort
        /// first (as Full library last left them), to give back on Full library or when the page goes.
        /// </summary>
        public void OpenMyGames(MyGamesItem item)
        {
            if (item == MyGamesItem.FullLibrary)
            {
                GiveBackMainList();
            }
            else
            {
                if (!ownsMainList)
                {
                    playnite.SaveUserFilter();
                    ownsMainList = true;
                }

                if (!MyGames.IsShown || MyGames.Item != item)
                {
                    playnite.ShowInMainList(item == MyGamesItem.Games ? GameListFilter.Installed : GameListFilter.LastPlayed);
                }
            }

            // On opening, for today's sections. Moving through the nav keeps it: rebuilding it rebuilds every row and its art.
            if (!MyGames.IsShown)
            {
                RefreshPlayHistory();
            }

            MyGames.Item = item;
            MyGames.IsShown = true;
        }

        /// <summary>
        /// B on the page: back to Home. Playnite's own B reaches the page before the pad's reaches the core, so a menu or
        /// prompt still showing answers the B instead.
        /// </summary>
        public void LeaveMyGames()
        {
            if (MyGames.IsShown && ActiveDialog == null && !Guide.IsOverlayShown)
            {
                CloseMyGames();
            }
        }

        /// <summary>Playnite is leaving Fullscreen, or closing: the user's own filter and sort go back.</summary>
        public void OnApplicationStopped()
        {
            GiveBackMainList();
        }

        private void CloseMyGames()
        {
            MyGames.IsShown = false;
            GiveBackMainList();
        }

        private void GiveBackMainList()
        {
            if (ownsMainList)
            {
                ownsMainList = false;
                playnite.RestoreUserFilter();
            }
        }

        /// <summary>Rebuilds Play history: played games, newest first, never hidden ones, in sections by when they were played.</summary>
        private void RefreshPlayHistory()
        {
            var today = clock.Now.Date;
            MyGames.PlayHistory = PlayedGames()
                .GroupBy(g => PlayedWhen(g.LastActivity.Value, today))
                .Select(section => new PlayHistorySection(
                    section.Key,
                    section.Select(g => new PlayHistoryEntry(TileOf(g), g.LastActivity.Value, g.Platform)).ToList()))
                .ToList();
        }

        /// <summary>
        /// The Play history section of a game last played at <paramref name="played"/>: Playnite's own last-activity
        /// segments, by calendar day, week (from the culture's first day), month and year.
        /// </summary>
        private static string PlayedWhen(DateTime played, DateTime today)
        {
            var day = played.Date;
            var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var thisWeek = today.AddDays(-(((int)today.DayOfWeek - (int)firstDay + 7) % 7));
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            return day >= today ? "Today"
                : day == today.AddDays(-1) ? "Yesterday"
                : day >= thisWeek ? "This week"
                : day >= thisWeek.AddDays(-7) ? "Last week"
                : day >= thisMonth ? "This month"
                : day >= thisMonth.AddMonths(-1) ? "Last month"
                : day.Year == today.Year ? "This year"
                : "Older";
        }

        /// <summary>A game's background art, or its cover when it has none; null for no game or no art.</summary>
        private static HeroArt HeroArtOf(LibraryGame game)
        {
            if (game == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(game.BackgroundImage))
            {
                return new HeroArt(game.Id, game.BackgroundImage, isCover: false);
            }

            return string.IsNullOrEmpty(game.CoverImage) ? null : new HeroArt(game.Id, game.CoverImage, isCover: true);
        }

        /// <summary>
        /// The Guide button, or a prompt that needs the Guide: open it on the landing tab, over whatever is in front.
        /// It takes the pad, so any tile menu goes.
        /// </summary>
        private void OpenGuide()
        {
            CloseTileMenu();

            if (Guide.IsOverlayShown)
            {
                // Reopened before focus was handed back: the Overlay is still in front, so keep the window recorded first.
                CancelReleaseWait();
            }
            else
            {
                recordedForeground = windowSystem.ForegroundWindow;
            }

            ShowLandingTab();
            Guide.IsOverlayShown = true;
            Guide.IsOpen = true;

            // Taking the foreground can fail; the Overlay is Topmost and the Guide follows Playnite's callback either way.
            windowSystem.TakeForeground();
        }

        /// <summary>
        /// Selects <paramref name="item"/>. Most entries close the Guide through <paramref name="dismiss"/>, handing
        /// focus on as the entry says; a recent game while another game runs asks "Quit X and start Y?" instead.
        /// </summary>
        private void Select(GuideItem item, Action<Action> dismiss)
        {
            sounds.Play(UiSound.Enter);
            if (item.Kind == GuideItemKind.RecentGame && (runningGame != null || IsQuittingOrLaunching))
            {
                PlayGame(item.GameId.Value);
                return;
            }

            var handBack = HandBackFor(item);
            if (handBack != null)
            {
                dismiss(handBack);
            }
        }

        /// <summary>
        /// What selecting <paramref name="item"/> does once the Guide has closed and focus is handed back,
        /// or null when it does nothing.
        /// </summary>
        private Action HandBackFor(GuideItem item)
        {
            switch (item.Kind)
            {
                case GuideItemKind.Home:
                    return () =>
                    {
                        CloseMyGames();
                        playnite.BringToFront(DashboardPage.Home);
                    };
                case GuideItemKind.MyGamesAndApps:
                    return () =>
                    {
                        playnite.BringToFront(DashboardPage.MyGamesAndApps);
                        OpenMyGames(MyGamesItem.Games);
                    };
                case GuideItemKind.RunningGame:
                    return ReturnToRunningGame;
                case GuideItemKind.RecentGame:
                    var gameId = item.GameId.Value;
                    return () =>
                    {
                        ReturnToRecordedWindow();
                        Launch(gameId);
                    };
                default:
                    return null;
            }
        }

        /// <summary>
        /// Closes the Guide from the pad. Focus is handed back by <paramref name="handBack"/> only once
        /// <paramref name="button"/> is released (or after a 1 s fallback), so the window behind never sees it.
        /// </summary>
        private void Dismiss(ControllerButton button, Action handBack)
        {
            CloseGuide();
            dismissButton = button;
            pendingHandBack = handBack;
            releaseFallback = clock.After(ReleaseFallback, HandFocusBack);
        }

        /// <summary>D-pad hold-repeat, at Playnite's own speed.</summary>
        private void Repeat(int by)
        {
            MoveFocus(by);
            repeatTimer = clock.After(RepeatRate, () => Repeat(by));
        }

        private void StopRepeat()
        {
            heldDirection = null;
            repeatTimer?.Dispose();
            repeatTimer = null;
        }

        /// <summary>
        /// Moves focus through the menu or prompt when one is showing, otherwise through the landing tab. Only a move
        /// that lands somewhere new makes a sound: none at either end.
        /// </summary>
        private void MoveFocus(int by)
        {
            var dialog = ActiveDialog;
            if (dialog != null)
            {
                dialog.FocusedOption = Step(dialog.Options, dialog.FocusedOption, by);
                return;
            }

            if (!Guide.IsOpen)
            {
                return;
            }

            Guide.FocusedItem = Step(Guide.Items, Guide.FocusedItem, by);
        }

        /// <summary>The entry <paramref name="by"/> places from <paramref name="focused"/>, held at either end; navigate sounds if it moved.</summary>
        private T Step<T>(IReadOnlyList<T> entries, T focused, int by) where T : class
        {
            var list = entries.ToList();
            var next = list[Clamp(list.IndexOf(focused) + by, list.Count)];
            if (next != focused)
            {
                sounds.Play(UiSound.Navigate);
            }

            return next;
        }

        private static int Clamp(int index, int count) => Math.Max(0, Math.Min(count - 1, index));

        /// <summary>The menu or prompt that has the pad: the Guide's while the Guide is open, otherwise the tile menu.</summary>
        private Dialog ActiveDialog => Guide.IsOpen ? Guide.Dialog : Home.TileMenu;

        /// <summary>Esc, or a click on the dimmed area. No pad button is held, so focus goes back at once.</summary>
        public void DismissGuide()
        {
            if (Guide.IsOpen)
            {
                DismissAtOnce(ReturnToRecordedWindow);
            }
        }

        /// <summary>The mouse pointing at an entry.</summary>
        public void FocusItem(GuideItem item)
        {
            if (Guide.IsOpen && Guide.Dialog == null && Guide.Items.Contains(item))
            {
                Guide.FocusedItem = item;
            }
        }

        /// <summary>A click on an entry. No pad button is held, so focus goes on at once.</summary>
        public void ChooseItem(GuideItem item)
        {
            if (!Guide.IsOpen || Guide.Dialog != null || !Guide.Items.Contains(item))
            {
                return;
            }

            Guide.FocusedItem = item;
            Select(item, DismissAtOnce);
        }

        /// <summary>The mouse pointing at a menu or prompt option.</summary>
        public void FocusOption(DialogOption option)
        {
            var dialog = ActiveDialog;
            if (dialog != null && dialog.Options.Contains(option))
            {
                dialog.FocusedOption = option;
            }
        }

        /// <summary>A click on a menu or prompt option.</summary>
        public void ChooseOption(DialogOption option)
        {
            var dialog = ActiveDialog;
            if (dialog != null && dialog.Options.Contains(option))
            {
                dialog.FocusedOption = option;
                Choose(dialog, option);
            }
        }

        /// <summary>Answers a menu or prompt with <paramref name="option"/>.</summary>
        private void Choose(Dialog dialog, DialogOption option)
        {
            sounds.Play(dialog.ChooseSound);
            option.Choose();
        }

        private void DismissAtOnce(Action handBack)
        {
            CloseGuide();
            pendingHandBack = handBack;
            HandFocusBack();
        }

        /// <summary>Closes the Guide, declining any menu or prompt it was showing.</summary>
        private void CloseGuide()
        {
            StopRepeat();
            Guide.Dialog?.Decline();
            Guide.IsOpen = false;
        }

        /// <summary>Menu on an entry. Only the running game has a menu, with Quit.</summary>
        private void ShowMenuFor(GuideItem item)
        {
            if (item.Kind != GuideItemKind.RunningGame)
            {
                return;
            }

            var gameId = item.GameId.Value;
            ShowDialog(new Dialog(
                item.Title,
                null,
                isPrompt: false,
                CloseDialog,
                new DialogOption("Quit", DialogIcon.Quit, () =>
                {
                    CloseDialog();
                    QuitGame(gameId);
                })));
        }

        /// <summary>Shows a menu or prompt over the Guide, opening the Guide first if it is closed.</summary>
        private void ShowDialog(Dialog dialog)
        {
            if (!Guide.IsOpen)
            {
                OpenGuide();
            }

            StopRepeat();
            sounds.Play(UiSound.PromptOpen);
            Guide.Dialog = dialog;
        }

        /// <summary>Takes the menu or prompt away, leaving the Guide open.</summary>
        private void CloseDialog()
        {
            Guide.Dialog = null;
        }

        /// <summary>The game hasn't closed by itself in time: ask whether to force-kill it.</summary>
        private void OfferForceQuit()
        {
            quitTimer = null;
            ShowDialog(new Dialog(
                $"Force quit {NameOf(quittingGame.Value)}?",
                "It hasn't closed. Anything unsaved will be lost.",
                isPrompt: true,
                DeclineForceQuit,
                new DialogOption("Force quit", DialogIcon.Quit, ForceQuit),
                new DialogOption("Cancel", DialogIcon.Cancel, DeclineForceQuit)));
        }

        private void ForceQuit()
        {
            CloseDialog();
            var window = RunningGameWindow();
            if (window == IntPtr.Zero)
            {
                // Nothing to kill: asking again would never help, so give up and leave the game to Playnite.
                EndQuit();
                return;
            }

            windowSystem.ForceKill(window);

            // If it somehow survives, the prompt comes back rather than the quit hanging forever.
            ArmQuitTimeout();
        }

        private void ArmQuitTimeout()
        {
            quitTimer = clock.After(QuitTimeout, OfferForceQuit);
        }

        /// <summary>While a game is quitting or launching, no other game may be launched.</summary>
        private bool IsQuittingOrLaunching => quittingGame != null || startingGame != null;

        /// <summary>Declining leaves the game running, and drops any game that was to start after it.</summary>
        private void DeclineForceQuit()
        {
            CloseDialog();
            EndQuit();
        }

        /// <summary>The quit is over, whether the game stopped or the user gave up on it.</summary>
        private void EndQuit()
        {
            quittingGame = null;
            startAfterQuit = null;
            quitTimer?.Dispose();
            quitTimer = null;
        }

        /// <summary>"Quit X and start Y?" was confirmed and X has stopped: close the Guide and launch Y.</summary>
        private void LaunchAfterQuit(Guid gameId)
        {
            if (Guide.IsOpen)
            {
                DismissAtOnce(() =>
                {
                    ReturnToRecordedWindow();
                    Launch(gameId);
                });
            }
            else
            {
                Launch(gameId);
            }
        }

        /// <summary>Starts a game through Playnite, holding off other launches until Playnite reports it started or didn't.</summary>
        private void Launch(Guid gameId)
        {
            startingGame = gameId;
            playnite.StartGame(gameId);
        }

        private string NameOf(Guid gameId) => GameById(gameId)?.Name ?? "the game";

        private LibraryGame GameById(Guid? gameId) => library.Games.FirstOrDefault(g => g.Id == gameId);

        /// <summary>Games that have been played, newest first, never hidden ones.</summary>
        private IEnumerable<LibraryGame> PlayedGames() =>
            library.Games.Where(g => g.LastActivity != null && !g.Hidden).OrderByDescending(g => g.LastActivity);

        /// <summary>A game went with the Guide open: rebuild the entries, keeping focus at the same place in the list.</summary>
        private void RefreshLandingTab()
        {
            var index = Guide.Items.ToList().IndexOf(Guide.FocusedItem);
            ShowLandingTab();
            Guide.FocusedItem = Guide.Items[Clamp(index, Guide.Items.Count)];
        }

        /// <summary>The Guide always opens here: the landing tab, focus on Home.</summary>
        private void ShowLandingTab()
        {
            var running = GameById(runningGame);
            var recent = PlayedGames().Where(g => g != running).Take(RecentGameCount);
            var games = running == null ? recent : new[] { running }.Concat(recent);

            // A divider separates the games from Home and My games & apps.
            var home = new GuideItem(GuideItemKind.Home, "Home");
            Guide.Items = new[] { home, new GuideItem(GuideItemKind.MyGamesAndApps, "My games & apps") }
                .Concat(games.Select((g, index) => new GuideItem(
                    g == running ? GuideItemKind.RunningGame : GuideItemKind.RecentGame, g.Name, g.Id, hasDividerAbove: index == 0)))
                .ToList();
            Guide.FocusedItem = home;
        }

        private void HandFocusBack()
        {
            var handBack = pendingHandBack;
            CancelReleaseWait();
            handBack();
            Guide.IsOverlayShown = false;
        }

        /// <summary>
        /// To the running game's own window, found from its install directory, because the Guide may have opened
        /// over the Dashboard or another app. When it can't be found, back to the window that was in front.
        /// </summary>
        private void ReturnToRunningGame()
        {
            var gameWindow = RunningGameWindow();
            if (gameWindow != IntPtr.Zero)
            {
                windowSystem.SetForeground(gameWindow);
            }
            else
            {
                ReturnToRecordedWindow();
            }
        }

        /// <summary>The running game's own window, found from its install directory; <see cref="IntPtr.Zero"/> when there is none.</summary>
        private IntPtr RunningGameWindow()
        {
            var directory = GameById(runningGame)?.InstallDirectory;
            return string.IsNullOrEmpty(directory) ? IntPtr.Zero : windowSystem.FindWindowUnder(directory);
        }

        /// <summary>Back to the window that was in front when the Guide opened, or to Playnite if it has gone.</summary>
        private void ReturnToRecordedWindow()
        {
            if (windowSystem.Exists(recordedForeground))
            {
                windowSystem.SetForeground(recordedForeground);
            }
            else
            {
                playnite.BringToFront();
            }
        }

        private void CancelReleaseWait()
        {
            dismissButton = null;
            pendingHandBack = null;
            releaseFallback?.Dispose();
            releaseFallback = null;
        }
    }
}
