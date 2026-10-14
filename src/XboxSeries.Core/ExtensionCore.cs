using System;
using System.Collections.Generic;
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
        /// How many games the Recent row on Home holds, before its Play history tile. The console shows 8 and Play
        /// history; 7 keeps the row to 8 tiles, which fit a 16:10 screen at a readable size.
        /// </summary>
        private const int RecentRowCount = 7;

        /// <summary>How many games the Play history tile's collage shows.</summary>
        private const int PlayHistoryCollageCount = 4;

        /// <summary>How soon a second A on the same Home tile counts as a double-tap, which launches the game.</summary>
        private static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(300);

        private static readonly TimeSpan ReleaseFallback = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(80);

        /// <summary>How long a game has to close by itself before the force-quit prompt.</summary>
        private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

        private readonly IClock clock;
        private readonly IWindowSystem windowSystem;
        private readonly IPlayniteHost playnite;
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

        public ExtensionCore(IClock clock, IWindowSystem windowSystem, IPlayniteHost playnite)
        {
            this.clock = clock;
            this.windowSystem = windowSystem;
            this.playnite = playnite;
        }

        public GuideState Guide { get; } = new GuideState();

        public HomeState Home { get; } = new HomeState();

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
            ShowDialog(new GuideDialog(
                $"Quit {NameOf(running)} and start {NameOf(gameId)}?",
                null,
                CloseDialog,
                new GuideDialogOption("Quit and start", () =>
                {
                    CloseDialog();
                    QuitGame(running);
                    if (quittingGame == running)
                    {
                        startAfterQuit = gameId;
                    }
                }),
                new GuideDialogOption("Cancel", CloseDialog)));
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

                return;
            }

            if (button == ControllerButton.Guide && !Guide.IsOpen)
            {
                OpenGuide();
            }
            else if (button == ControllerButton.B && Guide.Dialog != null)
            {
                Guide.Dialog.Decline();
            }
            else if ((button == ControllerButton.Guide || button == ControllerButton.B) && Guide.IsOpen)
            {
                Dismiss(button, ReturnToRecordedWindow);
            }
            else if (button == ControllerButton.A && Guide.Dialog != null)
            {
                Guide.Dialog.FocusedOption.Choose();
            }
            else if (button == ControllerButton.A && Guide.IsOpen)
            {
                Select(Guide.FocusedItem, handBack => Dismiss(button, handBack));
            }
            else if (button == ControllerButton.Start && Guide.IsOpen && Guide.Dialog == null)
            {
                ShowMenuFor(Guide.FocusedItem);
            }
            else if ((button == ControllerButton.DPadUp || button == ControllerButton.DPadDown) && Guide.IsOpen)
            {
                StopRepeat();
                var by = button == ControllerButton.DPadUp ? -1 : 1;
                MoveFocus(by);
                heldDirection = button;
                repeatTimer = clock.After(RepeatDelay, () => Repeat(by));
            }
        }

        /// <summary>A Recent-row tile got focus: the hero crossfades to its game's art, and keeps it once focus moves on.</summary>
        public void FocusHomeTile(HomeTile tile)
        {
            // A game with no art at all leaves the last art up rather than blanking the hero.
            var art = HeroArtOf(GameById(tile.GameId));
            if (art != null)
            {
                heroGame = tile.GameId;
                Home.Hero = art;
            }
        }

        /// <summary>
        /// A on a Home tile. It opens the game's Game hub, unless a second A on the same tile follows within the
        /// double-tap window: that plays the game instead, so the hub waits out the window before opening.
        /// </summary>
        public void ChooseHomeTile(HomeTile tile)
        {
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

            // The hero stays on the last focused game while it exists, and starts on the first game in the row.
            var hero = row.Concat(played).FirstOrDefault(g => g.Id == heroGame) ?? row.FirstOrDefault();
            heroGame = hero?.Id;
            Home.Hero = HeroArtOf(hero);
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

        /// <summary>The Guide button, or a prompt that needs the Guide: open it on the landing tab, over whatever is in front.</summary>
        private void OpenGuide()
        {
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
                    return () => playnite.BringToFront(DashboardPage.Home);
                case GuideItemKind.MyGamesAndApps:
                    return () => playnite.BringToFront(DashboardPage.MyGamesAndApps);
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

        /// <summary>Moves focus through the menu or prompt when one is showing, otherwise through the landing tab.</summary>
        private void MoveFocus(int by)
        {
            var dialog = Guide.Dialog;
            if (dialog != null)
            {
                var options = dialog.Options.ToList();
                dialog.FocusedOption = options[Clamp(options.IndexOf(dialog.FocusedOption) + by, options.Count)];
                return;
            }

            var items = Guide.Items.ToList();
            Guide.FocusedItem = items[Clamp(items.IndexOf(Guide.FocusedItem) + by, items.Count)];
        }

        private static int Clamp(int index, int count) => Math.Max(0, Math.Min(count - 1, index));

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
        public void FocusOption(GuideDialogOption option)
        {
            if (Guide.Dialog != null && Guide.Dialog.Options.Contains(option))
            {
                Guide.Dialog.FocusedOption = option;
            }
        }

        /// <summary>A click on a menu or prompt option.</summary>
        public void ChooseOption(GuideDialogOption option)
        {
            if (Guide.Dialog != null && Guide.Dialog.Options.Contains(option))
            {
                Guide.Dialog.FocusedOption = option;
                option.Choose();
            }
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
            ShowDialog(new GuideDialog(
                item.Title,
                null,
                CloseDialog,
                new GuideDialogOption("Quit", () =>
                {
                    CloseDialog();
                    QuitGame(gameId);
                })));
        }

        /// <summary>Shows a menu or prompt over the Guide, opening the Guide first if it is closed.</summary>
        private void ShowDialog(GuideDialog dialog)
        {
            if (!Guide.IsOpen)
            {
                OpenGuide();
            }

            StopRepeat();
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
            ShowDialog(new GuideDialog(
                $"Force quit {NameOf(quittingGame.Value)}?",
                "It hasn't closed. Anything unsaved will be lost.",
                DeclineForceQuit,
                new GuideDialogOption("Force quit", ForceQuit),
                new GuideDialogOption("Cancel", DeclineForceQuit)));
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
