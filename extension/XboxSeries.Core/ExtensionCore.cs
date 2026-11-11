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
    public sealed partial class ExtensionCore
    {
        /// <summary>How many games the landing tab lists, the running one included, as on the console.</summary>
        private const int RecentGameCount = 4;

        /// <summary>
        /// How many games the Recent row on Home holds, before its Play history tile. The console shows 8; 7 lets the
        /// row span the top bar on 16:9 and 16:10 screens at a size close to the console's.
        /// </summary>
        private const int RecentRowCount = 7;

        /// <summary>How many games the Play history tile's collage shows.</summary>
        private const int PlayHistoryCollageCount = 4;

        /// <summary>
        /// How many games a Group on Home shows before its See all tile: six tall cards, as on the console's Home, which
        /// with See all span the Recent row's width.
        /// </summary>
        private const int GroupTileCount = 6;

        /// <summary>How soon a second A on the same Home tile counts as a double-tap, which launches the game.</summary>
        private static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// How long focus has to stay on a Recent-row tile before the hero follows it with Background delay on, so the
        /// art changes only for a game the player has stopped on.
        /// </summary>
        private static readonly TimeSpan HeroDelay = TimeSpan.FromSeconds(1.5);

        /// <summary>
        /// The same with Background delay off: only long enough that a held d-pad, which Playnite repeats every 80 ms,
        /// passes over tiles without their art being read and flickered through (#147).
        /// </summary>
        private static readonly TimeSpan HeroSettle = TimeSpan.FromMilliseconds(150);

        /// <summary>
        /// How long the hero waits, once focus has left a Recent-row tile, for it to land on another before the swirl
        /// comes back: WPF tells of the tile left before the one focused.
        /// </summary>
        private static readonly TimeSpan HeroLeave = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// How long after the Guide opens the pad's battery is read: once the Guide is up and has flown in, as the read
        /// can take a moment (for a Bluetooth pad, Windows looks through its devices) that the Guide shouldn't wait for.
        /// </summary>
        private static readonly TimeSpan BatteryReadAfterGuideOpens = TimeSpan.FromMilliseconds(500);

        /// <summary>How often the PC's own battery is read, besides as Windows says the power changed.</summary>
        private static readonly TimeSpan PcBatteryReadEvery = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long focus has to stay on a nav item of My games &amp; apps before the page opens it, when focus came there
        /// this soon after reaching another. Opening one has Playnite refilter its list and the page lay its content out
        /// again, a frame or more each, so focus passing down the nav (a held d-pad repeats faster than this) opens only
        /// the item it stops on, while a single step opens at once.
        /// </summary>
        private static readonly TimeSpan NavSettle = TimeSpan.FromMilliseconds(150);

        // How long after a game quit from the Guide exits the Guide closes: Playnite is still bringing its window back
        // as it says the game stopped, and closing the Guide then left Playnite without the pad until Alt+Tab.
        private static readonly TimeSpan CloseAfterQuit = TimeSpan.FromMilliseconds(500);

        private static readonly TimeSpan ReleaseFallback = TimeSpan.FromSeconds(1);

        /// <summary>How long the Guide button has to be held to open Power, as on the console.</summary>
        private static readonly TimeSpan HoldForPower = TimeSpan.FromMilliseconds(700);

        /// <summary>A tap opens the Guide this long after its press, as the console's does, not at once.</summary>
        private static readonly TimeSpan TapOpensGuide = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(80);

        /// <summary>How long a game has to close by itself before the force-quit prompt.</summary>
        private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// How long a launch the Dashboard sent holds off others while Playnite hasn't yet begun it. Playnite may never
        /// begin it, nor say so: an error before its starting event, say.
        /// </summary>
        private static readonly TimeSpan LaunchBeginTimeout = TimeSpan.FromSeconds(15);

        /// <summary>How far one press of left or right turns the volume, as Windows' own volume keys do.</summary>
        private const int VolumeStep = 2;

        /// <summary>How long a Toast shows, and the pause before the next one waiting.</summary>
        private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ToastGap = TimeSpan.FromSeconds(0.5);

        /// <summary>
        /// How long the Launch screen waits for the game to start before giving up, so a launcher waiting for the user
        /// (a sign-in, an update, an agreement) isn't hidden for good, should it neither take the foreground nor B be pressed.
        /// </summary>
        private static readonly TimeSpan LaunchScreenTimeout = TimeSpan.FromSeconds(90);

        private readonly IClock clock;
        private readonly IWindowSystem windowSystem;
        private readonly IPlayniteHost playnite;
        private readonly ISoundPlayer sounds;
        private readonly IDrives drives;
        private readonly IDownloads downloads;
        private readonly IUserAccount account;
        private readonly IAudio audio;
        private readonly IController controller;
        private readonly IPcBattery pcBattery;
        private readonly IMediaSessions mediaSessions;
        private readonly IAchievements achievements;

        /// <summary>Input anywhere in Windows, which the Idle screen counts outside Playnite's windows too.</summary>
        private readonly IInputActivity inputActivity;
        private LibrarySnapshot library = LibrarySnapshot.Empty;

        /// <summary><see cref="library"/>'s games by id, so a lookup per game (as each Group's) doesn't scan the library.</summary>
        private Dictionary<Guid, LibraryGame> gamesById = new Dictionary<Guid, LibraryGame>();
        private IReadOnlyList<Notification> notifications = new Notification[0];

        /// <summary>The ids of the notifications last seen, so only new ones get a Toast; null until the first are seen.</summary>
        private HashSet<string> seenNotifications;

        /// <summary>The Toasts waiting their turn: new Playnite notifications' and games joining the queue's.</summary>
        private readonly Queue<WaitingToast> waitingToasts = new Queue<WaitingToast>();
        private IDisposable toastTimer;

        /// <summary>The waiting Toast showing, which the call's Toast puts back in line; null for none.</summary>
        private WaitingToast shownToast;

        /// <summary>
        /// What the Toast showing as the Guide button went down told of, which the tap opens the Guide at: Notifications,
        /// the Discord tab's Answer for a call, or Profile &amp; system for the greeting and an unlock; a Queued Toast's tap goes to
        /// Manage Queue instead, and a Ready to play Toast's starts its game. Null for no Toast.
        /// </summary>
        private ToastKind? toastAtGuidePress;

        /// <summary>The game the Ready to play Toast showing as the Guide button went down tells of; null for any other.</summary>
        private Guid? toastGameAtGuidePress;
        private Guid? runningGame;

        /// <summary>Whether the running game is frozen under the Guide; only <see cref="FreezeRunningGame"/> and <see cref="ThawGame"/> change it.</summary>
        private bool gameFrozen;
        private IntPtr recordedForeground;

        /// <summary>
        /// A game exited while the Guide was up: Home, not the window that was in front, nor the Game hub the game was
        /// played from, comes to the front as the Guide hands back.
        /// </summary>
        private bool homeOwed;
        private ControllerButton? dismissButton;
        private IDisposable releaseFallback;
        private Action pendingHandBack;
        private IDisposable holdTimer;
        private DateTime guidePressedAt;
        private IDisposable guideTapTimer;

        /// <summary>
        /// Set by a menu or prompt option that closes the Guide once answered: what hands focus on. The answer's button
        /// is only known where it was pressed, which closes the Guide with it.
        /// </summary>
        private Action answerHandBack;

        private ControllerButton? heldDirection;
        private IDisposable repeatTimer;
        private Guid? quittingGame;
        private IDisposable quitTimer;
        private Guid? startAfterQuit;
        private Guid? startingGame;
        private IDisposable launchBeginTimer;
        private Guid? heroGame;
        private Guid? tappedGame;
        private IDisposable tapTimer;
        private IDisposable heroTimer;
        private IDisposable guideBatteryTimer;
        private HomeTile focusedTile;

        /// <summary>
        /// The buttons pressed while the tile menu had the pad and not yet released: Playnite's own copies of such a press
        /// mustn't reach what lies under the menu, even once the press has closed it (<see cref="TileMenuHolds"/>).
        /// </summary>
        private readonly HashSet<ControllerButton> tileMenuHeld = new HashSet<ControllerButton>();

        /// <summary>
        /// The filter and sort My games &amp; apps last put on the main list, with the user's own kept to give back; null
        /// while the list is under the user's own.
        /// </summary>
        private GameListFilter mainListFilter;

        /// <summary>The open waiting for focus to stay on a nav item of My games &amp; apps; null with none.</summary>
        private IDisposable navTimer;

        /// <summary>When focus last reached a nav item of My games &amp; apps.</summary>
        private DateTime lastNavFocus = DateTime.MinValue;

        public ExtensionCore(
            IClock clock,
            IWindowSystem windowSystem,
            IPlayniteHost playnite,
            ISoundPlayer sounds,
            IDrives drives,
            IUserAccount account,
            IAudio audio,
            IController controller,
            IDiscordTransport discord,
            IDiscordOAuth discordOAuth,
            ISecretStore secrets,
            IDownloads downloads,
            IMediaSessions mediaSessions = null,
            IAchievements achievements = null,
            Random random = null,
            IInputActivity inputActivity = null,
            IPcBattery pcBattery = null)
        {
            this.pcBattery = pcBattery ?? NoPcBattery.Instance;
            this.random = random ?? new Random();
            this.inputActivity = inputActivity ?? NoInputActivity.Instance;
            this.mediaSessions = mediaSessions ?? NoMediaSessions.Instance;
            this.achievements = achievements ?? NoAchievements.Instance;
            this.downloads = downloads;
            this.discord = discord;
            this.discordOAuth = discordOAuth;
            this.secrets = secrets;
            this.drives = drives;
            this.account = account;
            this.audio = audio;
            this.controller = controller;
            this.clock = clock;
            this.windowSystem = windowSystem;
            this.playnite = playnite;
            this.sounds = sounds;
            TickStatus();
            TickPcBattery();
            LoadDiscordApp();
            LoadShortcuts();
            LoadRecentServers();
            LoadStartUp();
            LoadGuideSettings();
            LoadIdleSettings();
            LoadPlayniteSettings();
            WatchHints();
            WatchIdle();
        }

        public StatusState Status { get; } = new StatusState();

        public GuideState Guide { get; } = new GuideState();

        public ToastState Toast { get; } = new ToastState();

        public LaunchScreenState LaunchScreen { get; } = new LaunchScreenState();

        public HomeState Home { get; } = new HomeState();

        public MyGamesState MyGames { get; } = new MyGamesState();

        public SettingsState Settings { get; } = new SettingsState();

        public void OnLibraryChanged(LibrarySnapshot snapshot)
        {
            var previous = library;
            library = snapshot;
            gamesById = new Dictionary<Guid, LibraryGame>(snapshot.Games.Count);
            foreach (var game in snapshot.Games)
            {
                if (!gamesById.ContainsKey(game.Id))
                {
                    gamesById.Add(game.Id, game);
                }
            }

            FollowInstalls(previous);
            ToastFinishedInstalls(previous);
            ToastFinishedUninstalls();
            RefreshQueue();
            RefreshRecentlyInstalled();
            RefreshHomeRows();
            RefreshFullLibraryCount();
            RefreshApps(snapshot.Apps);

            // Once, after the Apps it finds among: Search's tiles read running and installing as Home's do.
            RefreshSearch();

            // The ring only shows on the page; Windows is asked for a drive's space no more often than that needs.
            if (MyGames.IsShown)
            {
                RefreshStorage();
            }
        }

        /// <summary>
        /// Playnite's own notifications changed: the status area counts them, and the Notifications utility lists them.
        /// Each new one gets a Toast, unless the Guide is already listing them; those there from the start don't.
        /// </summary>
        public void OnNotificationsChanged(IReadOnlyList<Notification> current)
        {
            var fresh = seenNotifications == null ? new Notification[0] : current.Where(n => !seenNotifications.Contains(n.Id)).ToArray();
            seenNotifications = new HashSet<string>(current.Select(n => n.Id));
            notifications = current;
            Status.NotificationCount = current.Count;
            if (Guide.IsOpen && Guide.Flyout?.Kind == GuideItemKind.Notifications)
            {
                var index = Guide.Flyout.Items.ToList().IndexOf(Guide.FocusedItem);
                OpenFlyout(GuideItemKind.Notifications);
                var items = Guide.Flyout.Items;
                Guide.FocusedItem = items.Count == 0 ? null : items[Clamp(index, items.Count)];
                return;
            }

            foreach (var notification in fresh)
            {
                waitingToasts.Enqueue(new WaitingToast(notification.Text, notification.Kind, null));
            }

            if (toastTimer == null)
            {
                ShowNextToast();
            }
        }

        /// <summary>
        /// A Toast joins those waiting, to show in its turn as a notification's does, behind any showing; with none
        /// showing or waiting, it shows at once.
        /// </summary>
        private void EnqueueToast(WaitingToast toast)
        {
            waitingToasts.Enqueue(toast);
            if (toastTimer == null)
            {
                ShowNextToast();
            }
        }

        /// <summary>
        /// The next waiting Toast, with the notification sound, or a rare achievement's with its own, unless the call's or
        /// the greeting is showing, which they wait behind, or Start-up covers the screen, which they wait for. An
        /// achievement's shows for the pill's longer <see cref="AchievementToastDuration"/>, a rare one's for its sound's
        /// <see cref="RareAchievementToastDuration"/>, and the next
        /// waits for its <see cref="AchievementToastOutro"/> before the usual gap.
        /// </summary>
        private void ShowNextToast()
        {
            toastTimer = null;
            if (waitingToasts.Count == 0 || ringToastTimer != null || greetingTimer != null || IsStartupShown)
            {
                return;
            }

            shownToast = waitingToasts.Dequeue();
            var rare = shownToast.Kind == ToastKind.RareAchievementUnlocked;
            var pill = rare || shownToast.Kind == ToastKind.AchievementUnlocked;
            var shownFor = rare ? RareAchievementToastDuration : pill ? AchievementShownFor(shownToast.Description) : ToastDuration;
            Toast.Show(
                shownToast.Text, shownToast.Kind, shownToast.Image, Settings.Guide.IsGuideOn, shownToast.Detail, shownToast.Score, shownToast.Rarity, shownToast.Description,
                pill ? shownFor : TimeSpan.Zero);
            sounds.Play(rare ? UiSound.RareAchievement : UiSound.Notification);
            toastTimer = clock.After(shownFor, () =>
            {
                shownToast = null;
                Toast.IsShown = false;
                toastTimer = waitingToasts.Count == 0 ? null : clock.After(pill ? AchievementToastOutro + ToastGap : ToastGap, ShowNextToast);
            });
        }

        /// <summary>The Guide opened: what the Toasts were telling of is in it now, a call ringing on the Discord tab.</summary>
        private void DropToasts()
        {
            toastTimer?.Dispose();
            toastTimer = null;
            ringToastTimer?.Dispose();
            ringToastTimer = null;
            waitingRingToast = null;
            greetingTimer?.Dispose();
            greetingTimer = null;
            shownToast = null;
            waitingToasts.Clear();
            Toast.IsShown = false;
        }

        /// <summary>
        /// A waiting Toast showing, or the gap after one, is cut short: it goes back to the front of those waiting, to
        /// show again in its turn.
        /// </summary>
        private void PutBackShownToast()
        {
            if (toastTimer == null)
            {
                return;
            }

            toastTimer.Dispose();
            toastTimer = null;
            if (Toast.IsShown && Toast.Kind != ToastKind.Call && shownToast != null)
            {
                var waiting = waitingToasts.ToList();
                waitingToasts.Clear();
                waitingToasts.Enqueue(shownToast);
                foreach (var toast in waiting)
                {
                    waitingToasts.Enqueue(toast);
                }
            }

            shownToast = null;
        }

        /// <summary>The status area's clock and battery, read afresh and again on the next minute.</summary>
        private void TickStatus()
        {
            RefreshStatus();
            var now = clock.Now;
            var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind).AddMinutes(1);
            clock.After(nextMinute - now, TickStatus);
        }

        /// <summary>A pad connected or went: the battery shows it now, not on the next minute.</summary>
        public void OnControllersChanged()
        {
            Status.Battery = controller.Battery;
        }

        /// <summary>The PC's own battery, read afresh and again in <see cref="PcBatteryReadEvery"/>.</summary>
        private void TickPcBattery()
        {
            Status.PcBattery = pcBattery.Read();
            if (pcBattery != NoPcBattery.Instance)
            {
                clock.After(PcBatteryReadEvery, TickPcBattery);
            }
        }

        /// <summary>
        /// Windows says the power changed, as the PC is plugged in or unplugged or its battery's state moves on: the PC's
        /// battery shows it now, not on the next read.
        /// </summary>
        public void OnPowerChanged()
        {
            Status.PcBattery = pcBattery.Read();
        }

        private void RefreshStatus()
        {
            RefreshTime();
            Status.Battery = controller.Battery;
        }

        private void RefreshTime()
        {
            Status.Time = clock.Now.ToString("t", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Playnite's main list changed its games. On Full library, that's the user switching tabs, so its count follows,
        /// and the sort chosen on its dropdown is put back over any the tab's preset brought.
        /// </summary>
        public void OnMainListChanged()
        {
            if (MyGames.ShowsFullLibrary)
            {
                RefreshFullLibraryCount();
                FollowLibrarySort();
            }
        }

        /// <summary>
        /// Playnite's game starting event, raised for every launch, including the Dashboard's own Play. Returns
        /// whether the launch may go ahead. A launch while another game runs is held back and asks
        /// "Quit X and start Y?" instead; one while a game is quitting or launching is simply refused.
        /// </summary>
        public bool OnGameStarting(Guid gameId)
        {
            if (gameId == runningGame)
            {
                return true;
            }

            if (gameId == startingGame)
            {
                // The Dashboard's own launch, now begun by Playnite: only now, so a launch Playnite never begins, as one
                // routed to its install dialog, isn't hidden behind the Launch screen.
                StopLaunchBeginTimer();
                ShowLaunchScreen(gameId);
                return true;
            }

            if (runningGame != null || IsQuittingOrLaunching)
            {
                PlayGame(gameId);
                return false;
            }

            startingGame = gameId;
            ShowLaunchScreen(gameId);
            return true;
        }

        /// <summary>Playnite's game started event.</summary>
        public void OnGameStarted(Guid gameId)
        {
            if (quittingGame != null && quittingGame != gameId)
            {
                // Another game started from elsewhere mid-quit; the quit, and any prompt about it, no longer applies.
                CloseGameDialog();
                EndQuit();
            }

            runningGame = gameId;
            startingGame = null;
            homeOwed = false;
            FollowAchievementsOf(gameId);
            HideLaunchScreenFor(gameId);
            RefreshHome();

            // Started with the Guide open, as over its Launch screen: frozen as one the Guide opened over would be, so it
            // can't take the pad behind it, and the Guide shows it running.
            if (Guide.IsOpen)
            {
                FreezeRunningGame();
                ShowRunningGameInGuide();
            }
        }

        /// <summary>
        /// The running game changed with the Guide open: the landing tab lists it running, or no longer, and Profile &amp;
        /// system greys Switch profile, or brings it back.
        /// </summary>
        private void ShowRunningGameInGuide()
        {
            if (Guide.Tab == GuideTabKind.Landing)
            {
                RefreshLandingTab();
            }
            else
            {
                ShowProfileInGuide();
            }
        }

        /// <summary>Playnite's game startup cancelled event: a launch that never got going.</summary>
        public void OnGameStartupCancelled(Guid gameId)
        {
            HideLaunchScreenFor(gameId);
            if (startingGame == gameId)
            {
                startingGame = null;
            }
        }

        /// <summary>
        /// A game's launching or running state changed in Playnite's database. A launch that ends neither launching nor
        /// running, with none of Playnite's events said, failed: Playnite couldn't start the game's program, showed its
        /// error, and cleared the state, raising neither cancelled nor stopped. It is taken as cancelled, so its Launch
        /// screen goes and it holds off no later launch.
        /// </summary>
        public void OnGameStateChanged(Guid gameId, bool wasLaunching, bool isLaunching, bool isRunning)
        {
            if (wasLaunching && !isLaunching && !isRunning)
            {
                OnGameStartupCancelled(gameId);
            }
        }

        /// <summary>Playnite's game stopped event.</summary>
        public void OnGameStopped(Guid gameId)
        {
            HideLaunchScreenFor(gameId);
            if (startingGame == gameId)
            {
                startingGame = null;
            }

            if (runningGame != gameId)
            {
                return;
            }

            // Whatever of it is left frozen under the Guide is let go with it.
            ThawGame();
            runningGame = null;
            RefreshHome();

            // Any menu or prompt was about the game that has just gone.
            CloseGameDialog();
            var quit = quittingGame == gameId;
            var next = quit ? startAfterQuit : null;
            EndQuit();
            if (next != null)
            {
                LaunchAfterQuit(next.Value);
                return;
            }

            if (Guide.IsOpen)
            {
                ShowRunningGameInGuide();
            }

            ReturnHomeFrom(gameId, quit);
        }

        /// <summary>
        /// A game has exited with nothing to launch after it: back to Home, as the console does, wherever it was
        /// launched from, the Recent row taking the pad again on the game just played. Playnite, coming back to the
        /// front, puts its own focus on its main list, which Home hides, so without this nothing on screen had focus
        /// and the pad moved nothing. The Guide or Start-up, when up, keep the foreground; Home is behind them as they
        /// go, the Guide bringing it to the front, out of any Game hub, as it hands back. A game quit from the Guide closes
        /// the Guide onto Home, its Quit done; one that exits on its own leaves the Guide up for whatever it was doing.
        /// </summary>
        private void ReturnHomeFrom(Guid gameId, bool quit)
        {
            CloseMyGames();
            CloseSettings();
            CloseSearch();
            if (Guide.IsOpen)
            {
                homeOwed = true;
                if (quit)
                {
                    clock.After(CloseAfterQuit, () =>
                    {
                        // Unless the Guide has already handed back meanwhile, which settles the debt.
                        if (Guide.IsOpen && homeOwed)
                        {
                            DismissAtOnce(ReturnToRecordedWindow);
                        }
                    });
                }
            }
            else if (!IsStartupShown)
            {
                playnite.BringToFront(DashboardPage.Home);
            }

            Home.ReturnFromGame(gameId);
        }

        /// <summary>
        /// Takes away a menu or prompt about a game. Power is about the PC, so it stays. A prompt shown on its own, with
        /// the Guide off, goes with the dim it showed over, focus handed back to the window in front.
        /// </summary>
        private void CloseGameDialog()
        {
            if (Guide.Dialog != null && !Guide.Dialog.IsWide)
            {
                Guide.Dialog = null;
                if (Guide.IsOpen && !Guide.ShowsPanel)
                {
                    DismissAtOnce(ReturnToRecordedWindow);
                }
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

            // A frozen game can't answer a close; it stays thawed until it has gone, the Guide not freezing it meanwhile.
            ThawGame();
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

        public void OnControllerButton(ControllerButton button, ButtonState state) => OnControllerButton(button, state, clock.Now);

        /// <summary>
        /// A button, stamped <paramref name="at"/> when the pad reported it. The Guide button acts on its release, once
        /// it is known to be a tap and not a hold for Power. A busy UI thread can handle that release before the hold
        /// timer's tick, so a hold is judged by the stamps, not only by the timer.
        /// </summary>
        public void OnControllerButton(ControllerButton button, ButtonState state, DateTime at)
        {
            // The first press while the screen is dimmed only wakes it, the Guide button's too.
            if (SwallowedByIdle(button, state))
            {
                return;
            }

            // Start-up has the pad to itself, the Guide button too, on its own thread; a button held from before it lets
            // go as usual.
            if (IsStartupShown && (state == ButtonState.Pressed || button == ControllerButton.Guide))
            {
                return;
            }

            if (button == ControllerButton.Guide)
            {
                // With the Guide off it is left to whatever else reads it, a tap and a hold alike.
                if (Settings.Guide.IsGuideOn)
                {
                    OnGuideButton(state, at);
                }

                return;
            }

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

                tileMenuHeld.Remove(button);
                return;
            }

            if (Home.TileMenu != null && !Guide.IsOpen)
            {
                tileMenuHeld.Add(button);
            }

            var dialog = ActiveDialog;
            if (button == ControllerButton.B && dialog != null)
            {
                sounds.Play(UiSound.PromptClose);
                dialog.Decline();
                AfterAnswer(handBack => Dismiss(button, handBack));
            }
            else if (button == ControllerButton.B && Guide.IsOpen && OpenDropdown != null)
            {
                StopRepeat();
                CloseDeviceList(OpenDropdown);
            }
            else if (button == ControllerButton.B && Guide.IsOpen && Guide.Flyout != null)
            {
                CloseFlyout();
            }
            else if (button == ControllerButton.B && Guide.IsOpen)
            {
                Dismiss(button, ReturnToRecordedWindow);
            }
            else if (button == ControllerButton.B && LaunchScreen.IsShown && !Guide.IsOverlayShown)
            {
                // The escape hatch for a window needing the user that Windows kept out of the foreground, behind it.
                DismissLaunchScreen();
            }
            else if (button == ControllerButton.A && dialog != null)
            {
                Choose(dialog, dialog.FocusedOption);
                AfterAnswer(handBack => Dismiss(button, handBack));
            }
            else if (button == ControllerButton.A && Guide.IsOpen)
            {
                Select(Guide.FocusedItem, handBack => Dismiss(button, handBack));
            }
            else if (button == ControllerButton.Start && Guide.IsOpen && dialog == null)
            {
                ShowMenuFor(Guide.FocusedItem);
            }
            else if (button == ControllerButton.Start && dialog == null && TileHasThePad)
            {
                ShowTileMenu(focusedTile);
            }
            else if (button == ControllerButton.Back && dialog == null && TileHasThePad
                && MyGames.IsShown && MyGames.Item == MyGamesItem.ManageQueue)
            {
                // Manage Queue's hint bar offers View for the Tile menu, on its rows and Recently installed covers alike.
                ShowTileMenu(focusedTile);
            }
            else if (button == ControllerButton.A && dialog == null && TileHasThePad)
            {
                // Playnite hands the pad's A to WPF as a key with no Key, which never clicks the tile.
                ChooseHomeTile(focusedTile);
            }
            else if ((button == ControllerButton.LeftShoulder || button == ControllerButton.RightShoulder) && Guide.IsOpen && dialog == null && Guide.Flyout == null)
            {
                SwitchTab(button == ControllerButton.LeftShoulder ? -1 : 1);
            }
            else if (IsDirection(button) && (Guide.IsOpen || dialog != null))
            {
                StopRepeat();
                MoveFocus(button);
                heldDirection = button;
                repeatTimer = clock.After(RepeatDelay, () => Repeat(button));
            }
        }

        /// <summary>Whether a tile has focus with nothing over it: neither the Guide nor text entry, which have the pad.</summary>
        private bool TileHasThePad => focusedTile != null && !Guide.IsOverlayShown && !TextEntry.IsShown;

        private static bool IsDirection(ControllerButton button) =>
            button == ControllerButton.DPadUp || button == ControllerButton.DPadDown
            || button == ControllerButton.DPadLeft || button == ControllerButton.DPadRight;

        /// <summary>
        /// Pressed, it waits 0.7 s for a hold, which opens Power as the controller buzzes on its own; released sooner, it
        /// is a tap, which closes the Guide at once or opens it 0.3 s after the press. Nothing shows while it waits,
        /// so a hold never flashes the Guide first. With the call's Toast showing over the Guide at the press, the tap
        /// goes to Answer instead of closing it.
        /// </summary>
        private void OnGuideButton(ButtonState state, DateTime at)
        {
            if (state == ButtonState.Pressed)
            {
                CancelHold();
                guideTapTimer?.Dispose();
                guideTapTimer = null;
                guidePressedAt = at;
                toastAtGuidePress = Toast.IsShown ? Toast.Kind : (ToastKind?)null;
                toastGameAtGuidePress = Toast.IsShown ? shownToast?.GameId : null;
                var left = HoldForPower - (clock.Now - at);
                holdTimer = clock.After(left > TimeSpan.Zero ? left : TimeSpan.Zero, OpenPowerFromHold);
                return;
            }

            if (holdTimer == null)
            {
                // The hold already opened Power, or the press was never seen.
                return;
            }

            CancelHold();
            if (at - guidePressedAt >= HoldForPower)
            {
                OpenPowerFromHold();
            }
            else if (Guide.IsOpen && toastAtGuidePress == ToastKind.Call && ringingChannelId != null && Settings.Discord.HasDiscordTab)
            {
                GoToAnswer();
            }
            else if (Guide.IsOpen)
            {
                DismissAtOnce(ReturnToRecordedWindow);
            }
            else
            {
                var left = TapOpensGuide - (clock.Now - guidePressedAt);
                if (left > TimeSpan.Zero)
                {
                    guideTapTimer = clock.After(left, OpenGuideFromTap);
                }
                else
                {
                    OpenGuideFromTap();
                }
            }
        }

        /// <summary>
        /// The profile on Home's top bar, and Home's Achievements tile: the Guide, on Profile &amp; system; nothing with the
        /// Guide off.
        /// </summary>
        public void OpenProfile()
        {
            if (Guide.IsOpen || !Settings.Guide.IsGuideOn)
            {
                return;
            }

            sounds.Play(UiSound.OpenMenu);
            OpenGuide();
            ShowTab(GuideTabKind.ProfileAndSystem);
        }

        /// <summary>
        /// A tap opens the Guide at the subject of the Toast showing, as the console does: Notifications for a
        /// notification's, the Discord tab, focus on Answer, for a call's, and Profile &amp; system for the greeting's and
        /// an Achievement unlocked Toast's, where the Profile's totals and last unlocks are.
        /// A Queued Toast's subject isn't in the Guide but on Manage Queue, so the tap goes there instead, and a Ready
        /// to play Toast's tap starts its game. An Uninstalled Toast's subject is gone, and a Library updated Toast's is all
        /// over the library, so their taps just open the Guide.
        /// </summary>
        private void OpenGuideFromTap()
        {
            guideTapTimer = null;
            if (toastAtGuidePress == ToastKind.Queued)
            {
                GoToQueueFromToast();
                return;
            }

            if (toastAtGuidePress == ToastKind.Installed && toastGameAtGuidePress != null)
            {
                StartFromToast(toastGameAtGuidePress.Value);
                return;
            }

            sounds.Play(UiSound.OpenMenu);
            OpenGuide();
            if (toastAtGuidePress == ToastKind.Notification || toastAtGuidePress == ToastKind.Screenshot)
            {
                OpenFlyout(GuideItemKind.Notifications);
            }
            else if (toastAtGuidePress == ToastKind.Call && Settings.Discord.HasDiscordTab)
            {
                ShowTab(GuideTabKind.Discord);
            }
            else if (toastAtGuidePress == ToastKind.Greeting || toastAtGuidePress == ToastKind.AchievementUnlocked
                || toastAtGuidePress == ToastKind.RareAchievementUnlocked)
            {
                ShowTab(GuideTabKind.ProfileAndSystem);
            }
        }

        /// <summary>
        /// A tap during a Queued Toast: My games &amp; apps on Manage Queue, as the Guide's My games &amp; apps entry
        /// brings it up, with the Toast going. Other games' Queued Toasts still waiting go too, as Manage Queue now
        /// shows them; the rest still show in their turn.
        /// </summary>
        private void GoToQueueFromToast()
        {
            sounds.Play(UiSound.Enter);
            if (shownToast?.Kind == ToastKind.Queued)
            {
                toastTimer?.Dispose();
                shownToast = null;
                Toast.IsShown = false;
                toastTimer = null;
            }

            var rest = waitingToasts.Where(t => t.Kind != ToastKind.Queued).ToList();
            waitingToasts.Clear();
            foreach (var toast in rest)
            {
                waitingToasts.Enqueue(toast);
            }

            if (toastTimer == null && waitingToasts.Count > 0)
            {
                toastTimer = clock.After(ToastGap, ShowNextToast);
            }

            playnite.BringToFront(DashboardPage.MyGamesAndApps);
            OpenMyGames(MyGamesItem.ManageQueue);
        }

        /// <summary>
        /// A tap during a Ready to play Toast: the Toast goes, as the game starts as Play starts it, and the next waiting
        /// Toast shows in its turn.
        /// </summary>
        private void StartFromToast(Guid gameId)
        {
            if (shownToast?.Kind == ToastKind.Installed)
            {
                toastTimer?.Dispose();
                shownToast = null;
                Toast.IsShown = false;
                toastTimer = waitingToasts.Count == 0 ? null : clock.After(ToastGap, ShowNextToast);
            }

            PlayGame(gameId);
        }

        /// <summary>
        /// A tap with the Guide open while the call's Toast showed, as it does over the Guide: the Discord tab, focus on
        /// Answer, any menu, prompt or flyout over it gone, and the call's Toast with it.
        /// </summary>
        private void GoToAnswer()
        {
            StopRepeat();
            Guide.Dialog?.Decline();
            Guide.Dialog = null;
            answerHandBack = null;
            Guide.ShowsPanel = true;
            sounds.Play(UiSound.Navigate);
            ShowTab(GuideTabKind.Discord);
            HideRingToast();
        }

        private void CancelHold()
        {
            holdTimer?.Dispose();
            holdTimer = null;
        }

        /// <summary>
        /// The Guide button held: Power on its own over the dim, as on the console, in place of the Guide's panel when
        /// the Guide was open. Closing it from there hands focus back to the window in front.
        /// </summary>
        private void OpenPowerFromHold()
        {
            holdTimer = null;
            if (Guide.Dialog != null)
            {
                Guide.Dialog.Decline();
                answerHandBack = null;
            }

            if (Guide.IsOpen)
            {
                StopRepeat();
                Guide.ShowsPanel = false;
            }
            else
            {
                OpenGuide(showsPanel: false);
            }

            ShowPower();
        }

        /// <summary>
        /// "What do you want to do?": Turn off PC, Restart PC, Sleep and Exit Playnite, with focus on Close. Close only
        /// takes it away, back to the Guide, or, with Power on its own, back to the window in front.
        /// </summary>
        private void ShowPower()
        {
            void Close()
            {
                CloseDialog();
                if (!Guide.ShowsPanel)
                {
                    answerHandBack = ReturnToRecordedWindow;
                }
            }

            ShowDialog(Dialog.Wide(
                "What do you want to do?",
                Close,
                new DialogOption("Close", DialogIcon.Cancel, Close),
                PowerOption("Turn off PC", DialogIcon.TurnOff, PowerCommand.TurnOffPc),
                PowerOption("Restart PC", DialogIcon.Restart, PowerCommand.RestartPc),
                PowerOption("Sleep", DialogIcon.Sleep, PowerCommand.Sleep),
                PowerOption("Exit Playnite", DialogIcon.Exit, PowerCommand.ExitPlaynite)));
        }

        /// <summary>
        /// A Power option, carried out once the Guide has closed. Playnite comes to the front first, so anything it asks
        /// on the way shows; Sleep instead goes back to the window in front, so the PC wakes into it.
        /// </summary>
        private DialogOption PowerOption(string label, DialogIcon icon, PowerCommand command) =>
            new DialogOption(label, icon, () =>
            {
                CloseDialog();
                answerHandBack = () =>
                {
                    if (command == PowerCommand.Sleep)
                    {
                        ReturnToRecordedWindow();
                    }
                    else
                    {
                        playnite.BringToFront();
                    }

                    playnite.Power(command);
                };
            });

        /// <summary>A menu or prompt was answered: if the answer closes the Guide, close it with <paramref name="dismiss"/>.</summary>
        private void AfterAnswer(Action<Action> dismiss)
        {
            // A prompt shown on its own, with no Guide behind it to go back to, closes with its answer.
            var handBack = answerHandBack
                ?? (Guide.IsOpen && !Guide.ShowsPanel && Guide.Dialog == null ? (Action)ReturnToRecordedWindow : null);
            answerHandBack = null;
            if (handBack != null)
            {
                dismiss(handBack);
            }
        }

        /// <summary>LB or RB: the tab <paramref name="by"/> places along, held at either end.</summary>
        private void SwitchTab(int by)
        {
            var kinds = Guide.Tabs.Select(t => t.Kind).ToList();
            var next = kinds[Clamp(kinds.IndexOf(Guide.Tab) + by, kinds.Count)];
            if (next == Guide.Tab)
            {
                return;
            }

            StopRepeat();
            sounds.Play(UiSound.Navigate);
            ShowTab(next);
        }

        /// <summary>Shows a tab, focus on its first entry.</summary>
        private void ShowTab(GuideTabKind tab)
        {
            Guide.Tab = tab;
            RefreshTabs();
            Guide.Flyout = null;
            if (tab == GuideTabKind.Landing)
            {
                ShowLandingTab();
                return;
            }

            Guide.Utilities = new GuideItem[0];
            if (tab == GuideTabKind.Discord)
            {
                ShowDiscordTab();
            }
            else
            {
                ShowProfileTab();
            }
        }

        /// <summary>
        /// The icons along the Guide's top, the Discord tab's among them from the first Connect until Disconnect, signed
        /// in or not (<see cref="DiscordSettingsState.HasDiscordTab"/>), before Profile &amp; system.
        /// </summary>
        private void RefreshTabs()
        {
            var kinds = Settings.Discord.HasDiscordTab
                ? new[] { GuideTabKind.Landing, GuideTabKind.Discord, GuideTabKind.ProfileAndSystem }
                : new[] { GuideTabKind.Landing, GuideTabKind.ProfileAndSystem };
            Guide.Tabs = kinds
                .Select(k => new GuideTab(k, k == Guide.Tab, k == GuideTabKind.ProfileAndSystem ? ProfilePicture : null))
                .ToList();
        }

        /// <summary>
        /// Opens the flyout for a utility beside the panel, over the landing tab, focus on its first entry; opened
        /// again, it is rebuilt from what Playnite and Windows report now.
        /// </summary>
        private void OpenFlyout(GuideItemKind kind)
        {
            var items = kind == GuideItemKind.Notifications
                ? notifications.Reverse().Select(n => new GuideItem(GuideItemKind.Notification, n.Text)).ToList()
                : AudioItems();
            Guide.Flyout = new GuideFlyout(kind, kind == GuideItemKind.Notifications ? "Notifications" : "Audio", items);
            Guide.FocusedItem = items.FirstOrDefault();
        }

        /// <summary>B in a flyout: closes it, focus back on the entry that opened it; in a Discord server's, back to the Servers flyout.</summary>
        private void CloseFlyout()
        {
            var kind = Guide.Flyout.Kind;
            StopRepeat();
            if (kind == GuideItemKind.DiscordServer)
            {
                BackToServers();
                return;
            }

            Guide.Flyout = null;
            Guide.FocusedItem = Guide.Utilities.Concat(Guide.Items).First(i => i.Kind == kind);
        }

        /// <summary>
        /// Profile &amp; system: the profile card with the profile signed in, or the Windows user before the first
        /// sign-in, and Switch profile under it, greyed out while a game runs (Start-up would hand the front to Playnite
        /// over it), then the last achievements unlocked, if any, then Settings and Power, each under a divider.
        /// </summary>
        private void ShowProfileTab()
        {
            Guide.Items = new[]
                {
                    new GuideItem(GuideItemKind.Profile, Status.Profile?.Gamertag ?? account.Name, image: ProfilePicture),
                    new GuideItem(GuideItemKind.SwitchProfile, "Switch profile") { IsEnabled = runningGame == null && !IsQuittingOrLaunching },
                }
                .Concat(RecentAchievementItems())
                .Concat(new[]
                {
                    new GuideItem(GuideItemKind.Settings, "Settings", hasDividerAbove: true),
                    new GuideItem(GuideItemKind.Power, "Power", hasDividerAbove: true),
                })
                .ToArray();
            Guide.FocusedItem = Guide.Items[0];
        }

        /// <summary>
        /// A game tile on Home got focus: Menu now opens its tile menu. The hero shows a Recent-row tile's game art at once
        /// coming into the row onto the swirl, and along the row once focus has stayed on a tile for a moment (1.5 s with
        /// Settings' Background delay on, 150 ms with it off, as it is set at that moment), the last art up meanwhile. Any other tile, as a Group's, has the swirl behind Home.
        /// </summary>
        public void FocusHomeTile(HomeTile tile)
        {
            focusedTile = tile;
            RefreshHints();
            CancelHeroFollow();
            if (!Home.RecentRow.Contains(tile))
            {
                ShowSwirl();
                return;
            }

            // Coming into the row onto the swirl, the art shows at once rather than the swirl staying behind a tile.
            var gameId = tile.GameId;
            if (heroGame == null)
            {
                ShowHeroOf(gameId);
                return;
            }

            heroTimer = clock.After(Settings.StartUp.BackgroundDelay ? HeroDelay : HeroSettle, () =>
            {
                heroTimer = null;
                ShowHeroOf(gameId);
            });
        }

        /// <summary>
        /// Focus left a game tile on Home, to elsewhere on the Dashboard or with Playnite leaving the foreground: the
        /// swirl comes back behind Home, unless focus lands on another Recent-row tile straight away.
        /// </summary>
        public void LeaveHomeTile(HomeTile tile)
        {
            if (focusedTile == tile)
            {
                focusedTile = null;
                RefreshHints();
                CancelHeroFollow();
                if (heroGame != null)
                {
                    heroTimer = clock.After(HeroLeave, () =>
                    {
                        heroTimer = null;
                        ShowSwirl();
                    });
                }
            }
        }

        /// <summary>The green swirl behind Home, as when no Recent-row tile has focus.</summary>
        private void ShowSwirl()
        {
            heroGame = null;
            Home.Hero = null;
        }

        /// <summary>Puts the game's art behind Home. A game with no art at all leaves the last art, or the swirl, up rather than blanking the hero.</summary>
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

        /// <summary>
        /// A on a Home tile. It opens the game's Game hub, unless a second A on the same tile follows within the
        /// double-tap window: that plays the game instead, so the hub waits out the window before opening.
        /// </summary>
        public void ChooseHomeTile(HomeTile tile)
        {
            if (TileMenuHolds(ControllerButton.A) || TextEntry.IsShown)
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
                ListPlayHistoryInMainList();
                playnite.OpenGameHub(gameId);
            });
        }

        private void CancelTap()
        {
            tappedGame = null;
            tapTimer?.Dispose();
            tapTimer = null;
        }

        /// <summary>Home's rows and Play history as the library and the Queue are now, and Search's with them.</summary>
        private void RefreshHome()
        {
            RefreshHomeRows();

            // Search's tiles read running and installing as Home's do.
            RefreshSearch();
        }

        /// <summary>
        /// Rebuilds the Recent row: the running game first, then played games by last played, padded with recently
        /// added ones, never hidden ones. The Play history tile's collage is the played games that come next. A row is
        /// only replaced when what it shows changed, since a new row rebuilds every tile and reads every cover again,
        /// and a library update pushes the library several times a second.
        /// </summary>
        private void RefreshHomeRows()
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

            var recentRow = row.Select(TileOf).ToList();
            if (!SameTiles(recentRow, Home.RecentRow))
            {
                Home.RecentRow = recentRow;
            }

            var collage = played.Where(g => !row.Contains(g))
                .Take(PlayHistoryCollageCount)
                .Select(g => new HomeTile(g.Id, g.Name, g.CoverImage, g.Source, isRunning: false, StatusOf(g.Id)))
                .ToList();
            if (!SameTiles(collage, Home.PlayHistory))
            {
                Home.PlayHistory = collage;
            }

            var groups = Groups();
            if (!SameGroups(groups, Home.Groups))
            {
                Home.Groups = groups;
            }

            MyGames.GameCount = GameListFilter.Installed.Apply(library.Games).Count();
            RefreshPlayHistory(played);

            // The hero stays on the focused game while it is still there, with its art as it is now; else the swirl.
            var hero = heroGame == null ? null : row.Concat(played).FirstOrDefault(g => g.Id == heroGame);
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

        /// <summary>Whether two lists of Groups would show the same: the same Groups, each of the same preset, name, tiles and collage.</summary>
        private static bool SameGroups(IReadOnlyList<HomeGroup> a, IReadOnlyList<HomeGroup> b) =>
            a.Count == b.Count
            && a.Zip(b, (x, y) => x.Name == y.Name && x.FilterPresetId == y.FilterPresetId
                && SameTiles(x.Tiles, y.Tiles) && SameTiles(x.SeeAll, y.SeeAll)).All(same => same);

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
                    .Select(g => new HomeTile(g.Id, g.Name, g.CoverImage, g.Source, isRunning: false, StatusOf(g.Id)))
                    .ToList());
        }

        /// <summary>A game's tile, marked when the game is running, and reading "Queued" or "Installing..." while it installs.</summary>
        private HomeTile TileOf(LibraryGame game) =>
            new HomeTile(game.Id, game.Name, game.CoverImage, game.Source, game.Id == runningGame, StatusOf(game.Id));

        /// <summary>
        /// Opens My games &amp; apps on <paramref name="item"/>, in place of Settings or Search if up, or moves it there, at
        /// once: an open still waiting on the nav (<see cref="FocusMyGamesNav"/>) goes. Full library is the main list
        /// under the user's own filter and sort. Games takes the main list over, keeping the user's filter and sort first
        /// (as Full library last left them), to give back on Full library or when the page goes. Play history shows its
        /// own list and leaves the main list as it is, hidden behind it, until one of its games opens
        /// (<see cref="ListPlayHistoryInMainList"/>). Apps shows its own tiles and leaves the main list alone too; with no
        /// Apps to show (Software tools couldn't be read), it opens nothing. Manage and Manage Queue show their own
        /// content too, Manage Queue even with nothing queued, as the console's shows its empty box.
        /// </summary>
        public void OpenMyGames(MyGamesItem item) => OpenMyGames(item, returnsToGame: null);

        /// <summary>Opens the page on <paramref name="item"/>, focus going to <paramref name="returnsToGame"/>'s tile as it opens, else the first.</summary>
        private void OpenMyGames(MyGamesItem item, Guid? returnsToGame)
        {
            CancelNavSettle();
            queueLeavesForHome = false;

            // Search goes first, giving back any filter of its own, before Games puts its filter on.
            CloseSearch();

            // Full library's name filter is its own: it goes before Games keeps the user's filter, or anything else shows.
            if (item != MyGamesItem.FullLibrary)
            {
                ForgetLibraryName();
            }

            if (item == MyGamesItem.Apps)
            {
                if (!MyGames.HasApps)
                {
                    return;
                }
            }
            else if (item == MyGamesItem.FullLibrary)
            {
                GiveBackMainList();
            }
            else if (item == MyGamesItem.Games)
            {
                ShowInMainList(GameListFilter.Installed);
            }

            // On opening, for today's sections, the drive's space now, and a Full library count Playnite couldn't give
            // before its main view was up. Moving through the nav keeps Play history: rebuilding it rebuilds every row and its art.
            if (!MyGames.IsShown)
            {
                RefreshPlayHistory();
                RefreshFullLibraryCount();
                RefreshStorage();
            }

            // The pages are never up together: My games & apps takes Settings' place, as it took Search's above.
            CloseSettings();
            if (!MyGames.IsShown)
            {
                MyGames.ReturnsToGame = returnsToGame;
            }

            MyGames.Item = item;
            MyGames.IsShown = true;
            FollowLibrarySort();
        }

        /// <summary>
        /// Focus moved along My games &amp; apps' nav to <paramref name="item"/>: the page opens it at once, or, with focus
        /// on another item within <see cref="NavSettle"/> before, once focus has stayed there for that long, so focus
        /// passing down the nav doesn't open every item on its way. Back on the item showing, nothing opens. A or Right on the item opens it at once (<see cref="OpenMyGames"/>), so they never
        /// go into the content of the item before. With the page not up, it opens at once.
        /// </summary>
        public void FocusMyGamesNav(MyGamesItem item)
        {
            CancelNavSettle();
            // Not before the last focus: a clock set back, as by a time sync, isn't focus passing by for as long as it was set back.
            var sinceLast = clock.Now - lastNavFocus;
            var passing = sinceLast >= TimeSpan.Zero && sinceLast < NavSettle;
            lastNavFocus = clock.Now;
            if (!MyGames.IsShown || (MyGames.Item != item && !passing))
            {
                OpenMyGames(item);
            }
            else if (MyGames.Item != item)
            {
                navTimer = clock.After(NavSettle, () => OpenMyGames(item));
            }
        }

        /// <summary>Focus left My games &amp; apps' nav other than into an item's content: an item still waiting to open doesn't.</summary>
        public void LeaveMyGamesNav() => CancelNavSettle();

        private void CancelNavSettle()
        {
            navTimer?.Dispose();
            navTimer = null;
        }

        /// <summary>Puts <paramref name="filter"/> on the main list, keeping the user's own first to give back; Playnite refilters only for a change.</summary>
        private void ShowInMainList(GameListFilter filter)
        {
            if (mainListFilter == null)
            {
                playnite.SaveUserFilter();
            }

            if (mainListFilter != filter)
            {
                mainListFilter = filter;
                playnite.ShowInMainList(filter);
            }
        }

        /// <summary>
        /// The Game hub is for the main list's selected game, so a Play history game opening its hub has to be in the
        /// list, which Games' filter or the user's own can leave it out of, uninstalled say. The
        /// main list, hidden behind Play history's own, takes every game Play history lists only now, as one opens, not on
        /// every visit to Play history, which had Playnite refilter and sort it for nothing.
        /// </summary>
        private void ListPlayHistoryInMainList()
        {
            if (MyGames.IsShown && MyGames.Item == MyGamesItem.PlayHistory)
            {
                ShowInMainList(GameListFilter.LastPlayed);
            }
        }

        /// <summary>
        /// The page's Apps, from a library push; null when Software tools couldn't be read. They are only replaced when
        /// they changed, since new Apps rebuild every tile and read every icon again. Left on Apps that went, the page
        /// moves to Games rather than sit on a nav item it hides.
        /// </summary>
        private void RefreshApps(IReadOnlyList<App> apps)
        {
            MyGames.HasApps = apps != null;
            var current = apps ?? new App[0];
            if (current.Count != MyGames.Apps.Count || current.Zip(MyGames.Apps, App.Same).Contains(false))
            {
                MyGames.Apps = current;
            }

            if (!MyGames.HasApps && MyGames.IsShown && MyGames.Item == MyGamesItem.Apps)
            {
                OpenMyGames(MyGamesItem.Games);
            }
        }

        /// <summary>
        /// A on an App's tile: starts the App with its arguments. An App is not a game, so it starts whatever game is
        /// running, quitting or launching, without "Quit X and start Y?" or the Launch screen. A menu or the Guide still
        /// showing over the page answers the A instead.
        /// </summary>
        public void StartApp(Guid appId)
        {
            if (!MyGames.IsShown || MyGames.Item != MyGamesItem.Apps || ActiveDialog != null || Guide.IsOverlayShown || TextEntry.IsShown)
            {
                return;
            }

            var app = MyGames.Apps.FirstOrDefault(a => a.Id == appId);
            if (app != null)
            {
                playnite.StartApp(app);
            }
        }

        /// <summary>
        /// B on the page: back to Home, or from Manage Queue back to Manage, which opened it (Home, from its Downloads tile), or on Full library narrowed by
        /// name, back to its whole tab. Playnite's own B reaches the page before the pad's reaches the core, so a menu or
        /// prompt still showing answers the B instead.
        /// </summary>
        public void LeaveMyGames()
        {
            if (MyGames.IsShown && ActiveDialog == null && !Guide.IsOverlayShown && !TextEntry.IsShown)
            {
                if (MyGames.Item == MyGamesItem.ManageQueue && !queueLeavesForHome)
                {
                    MyGames.Item = MyGamesItem.Manage;
                }
                else if (MyGames.ShowsFullLibrary && MyGames.HasNameFilter)
                {
                    // Narrowed by name, Full library shows its whole tab again first.
                    ClearFullLibraryName();
                }
                else
                {
                    CloseMyGames();
                }
            }
        }

        /// <summary>Opens Settings from Home's top bar, on the rail's first category, Theme settings' boxes; B goes back to the top bar.</summary>
        public void OpenSettings() => ShowSettings(SettingsOrigin.TopBar);

        /// <summary>
        /// Puts Settings up on the rail's first category, Theme settings, on its boxes, every time it opens; or on
        /// <paramref name="inPane"/>, focus in that category's options, on Theme settings' <paramref name="page"/> when
        /// it's given. It takes My games &amp; apps' place, or Search's, which gives the user's own filter and sort back.
        /// <paramref name="game"/> is the game whose tile menu opened it, by a Customise entry, which B goes back to.
        /// </summary>
        private void ShowSettings(SettingsOrigin from, Guid? game = null, SettingsCategory? inPane = null, ThemeSettingsPage page = ThemeSettingsPage.None)
        {
            settingsReturnItem = MyGames.Item;
            CloseMyGames();
            CloseSearch();
            RefreshGamerpics();
            Settings.Category = inPane ?? SettingsCategory.ThemeSettings;
            Settings.ThemePage = page;
            Settings.OpensInPane = inPane != null;
            Settings.OpenedFrom = from;
            Settings.OpenedFromGame = game;
            Settings.IsShown = true;
            Settings.RaiseOpened();
        }

        /// <summary>
        /// Home's Discord tile: the Guide on the Discord tab, or with no Discord tab yet, or the Guide off, Settings on
        /// Theme settings' Discord, focus on its Client ID. B steps back to Theme settings' boxes, then to the tile.
        /// </summary>
        public void OpenDiscordFromHome()
        {
            if (!Settings.Discord.HasDiscordTab || !Settings.Guide.IsGuideOn)
            {
                ShowSettings(SettingsOrigin.HomeTile, inPane: SettingsCategory.ThemeSettings, page: ThemeSettingsPage.Discord);
                return;
            }

            if (Guide.IsOpen)
            {
                return;
            }

            sounds.Play(UiSound.OpenMenu);
            OpenGuide();
            ShowTab(GuideTabKind.Discord);
        }

        /// <summary>Home's Playnite settings tile: Settings on Playnite settings, focus on its Playnite settings box. B goes back to the tile.</summary>
        public void OpenPlayniteSettingsFromHome() => ShowSettings(SettingsOrigin.HomeTile, inPane: SettingsCategory.PlayniteSettings);

        /// <summary>Home's Downloads tile: My games &amp; apps on Manage Queue, B going back to the tile rather than to Manage.</summary>
        public void OpenQueueFromHome()
        {
            OpenMyGames(MyGamesItem.ManageQueue);
            queueLeavesForHome = true;
        }

        /// <summary>Whether Manage Queue was opened from Home's Downloads tile, so B on it goes back to Home.</summary>
        private bool queueLeavesForHome;

        /// <summary>The item My games &amp; apps showed as Settings took its place, which Customise games &amp; apps' B opens again.</summary>
        private MyGamesItem settingsReturnItem;

        /// <summary>
        /// The tile menu's Customise Home, or on My games &amp; apps Customise games &amp; apps: Settings, on Theme
        /// settings' Personalisation, focus in its boxes. B steps back to Theme settings' boxes, then to the page and the
        /// tile whose menu opened it.
        /// </summary>
        private void Customise(Guid gameId) =>
            ShowSettings(MyGames.IsShown ? SettingsOrigin.MyGamesTile : SettingsOrigin.HomeTile, gameId,
                inPane: SettingsCategory.ThemeSettings, page: ThemeSettingsPage.Personalisation);

        /// <summary>
        /// The rail's focus moved to <paramref name="category"/>: the page shows it, Theme settings on its boxes, as focus
        /// on the rail is never in a page of them.
        /// </summary>
        public void FocusSettingsCategory(SettingsCategory category)
        {
            if (Settings.IsShown)
            {
                Settings.Category = category;
                Settings.ThemePage = ThemeSettingsPage.None;
            }
        }

        /// <summary>
        /// A on one of Theme settings' boxes: its page, in the pane, the header following it. Nothing while the page is
        /// gone or the rail is on another category.
        /// </summary>
        public void OpenThemePage(ThemeSettingsPage page)
        {
            if (Settings.IsShown && Settings.Category == SettingsCategory.ThemeSettings)
            {
                Settings.ThemePage = page;
            }
        }

        /// <summary>
        /// B, or Left from its first column, on one of Theme settings' pages: back to its boxes, focus on the box it came
        /// from. Focus stays on the boxes from then on, however the page was opened.
        /// </summary>
        public void LeaveThemePage()
        {
            if (Settings.IsShown)
            {
                Settings.OpensInPane = false;
                Settings.ThemePage = ThemeSettingsPage.None;
            }
        }

        /// <summary>The rail's Playnite settings: Playnite's own restyled Fullscreen Settings window, over the page.</summary>
        public void OpenPlayniteSettings() => playnite.OpenPlayniteSettings();

        /// <summary>The rail's More Playnite options: Playnite's own main menu, over the page.</summary>
        public void OpenMorePlayniteOptions() => playnite.OpenMainMenu();
#if DEBUG

        /// <summary>The Debug menu's notifications posted so far, each getting an id of its own.</summary>
        private int debugNotifications;

        /// <summary>The Debug menu: posts a Playnite notification with this text, which shows its Toast as any new one does.</summary>
        public void DebugNotify(string text) =>
            playnite.AddNotification("xbox-series-debug-" + ++debugNotifications, text);

        /// <summary>
        /// The Debug menu: shows the call's Toast for a ring from this caller, with this image (null for none), without
        /// Discord. Nothing rings, so no Answer appears, and a real ring's caller is left as it was; a Guide tap during
        /// it opens the Guide on the Discord tab, or the landing tab without one.
        /// </summary>
        public void DebugRing(string caller, string image) => ShowRingToast(caller, image);
#endif

        /// <summary>
        /// B on the page: on one of Theme settings' pages, back to its boxes, the page staying; otherwise back to where it
        /// was opened from (<see cref="SettingsState.OpenedFrom"/>). Playnite's own B reaches the page before the pad's
        /// reaches the core, so a menu or prompt still showing answers the B instead.
        /// </summary>
        public void LeaveSettings()
        {
            if (Settings.IsShown && ActiveDialog == null && !Guide.IsOverlayShown && !TextEntry.IsShown)
            {
                // B steps back one level at a time: a page of Theme settings goes back to its boxes first.
                if (Settings.ThemePage != ThemeSettingsPage.None)
                {
                    LeaveThemePage();
                }

                // Opened by Customise games & apps, the page comes back in Settings' place, on its item and that tile.
                else if (Settings.OpenedFrom == SettingsOrigin.MyGamesTile)
                {
                    OpenMyGames(settingsReturnItem, Settings.OpenedFromGame);
                }
                else
                {
                    CloseSettings();
                }
            }
        }

        /// <summary>Takes the page down, with no Theme settings page left showing for next time.</summary>
        private void CloseSettings()
        {
            DropTextEntryOver(TextEntryPage.Settings);
            Settings.IsShown = false;
            Settings.ThemePage = ThemeSettingsPage.None;
        }

        /// <summary>Playnite is leaving Fullscreen, or closing: the user's own filter and sort go back, and a game frozen under the Guide thaws.</summary>
        public void OnApplicationStopped()
        {
            ThawGame();
            ForgetLibraryName();
            GiveBackMainList();
            playnite.ReleaseUserFilter();
        }

        private void CloseMyGames()
        {
            CancelNavSettle();
            ForgetLibraryName();
            chosenLibrarySort = null;
            MyGames.IsShown = false;
            GiveBackMainList();
            playnite.ReleaseUserFilter();
        }

        private void GiveBackMainList()
        {
            if (mainListFilter != null)
            {
                mainListFilter = null;
                playnite.RestoreUserFilter();
            }
        }

        /// <summary>Full library's count: the games the user's own filter shows, as Playnite counts them.</summary>
        private void RefreshFullLibraryCount()
        {
            MyGames.FullLibraryCount = playnite.CountUserFilterGames();
        }

        /// <summary>
        /// The storage ring: the space on the drive holding most installed games (those Games shows), ties to the first
        /// drive by name; none without such a game or when the drive can't be read.
        /// </summary>
        private void RefreshStorage()
        {
            var root = GameListFilter.Installed.Apply(library.Games)
                .Select(g => DriveOf(g.InstallDirectory))
                .Where(r => r != null)
                .GroupBy(r => r)
                .OrderByDescending(d => d.Count())
                .ThenBy(d => d.Key, StringComparer.Ordinal)
                .Select(d => d.Key)
                .FirstOrDefault();
            MyGames.Storage = root == null ? null : StorageRing.Of(drives.SpaceOf(root));
        }

        /// <summary>The drive a directory is on, such as C:\; null for none, or a network share.</summary>
        private static string DriveOf(string directory)
        {
            if (string.IsNullOrEmpty(directory) || directory.Length < 2 || directory[1] != ':' || !char.IsLetter(directory[0]))
            {
                return null;
            }

            return char.ToUpperInvariant(directory[0]) + ":\\";
        }

        /// <summary>
        /// Rebuilds Play history: played games, newest first, never hidden ones, in sections by when they were played.
        /// <paramref name="played"/> is <see cref="PlayedGames"/> when the caller has them already. The sections are only
        /// replaced when what they show changed, since new ones rebuild every row and read every cover again.
        /// </summary>
        private void RefreshPlayHistory(IReadOnlyList<LibraryGame> played = null)
        {
            var today = clock.Now.Date;
            var sections = (played ?? PlayedGames().ToList())
                .GroupBy(g => PlayedWhen(g.LastActivity.Value, today))
                .Select(section => new PlayHistorySection(
                    section.Key,
                    section.Select(g => new PlayHistoryEntry(TileOf(g), g.LastActivity.Value, g.Platform)).ToList()))
                .ToList();
            if (!SameSections(sections, MyGames.PlayHistory))
            {
                MyGames.PlayHistory = sections;
            }
        }

        /// <summary>Whether two Play histories would show the same: the same sections, each of the same games, played when and on what.</summary>
        private static bool SameSections(IReadOnlyList<PlayHistorySection> a, IReadOnlyList<PlayHistorySection> b) =>
            a.Count == b.Count
            && a.Zip(b, (x, y) => x.Title == y.Title && x.Entries.Count == y.Entries.Count
                && x.Entries.Zip(y.Entries, (e, f) => SameTile(e.Tile, f.Tile) && e.LastPlayed == f.LastPlayed && e.Platform == f.Platform)
                    .All(same => same)).All(same => same);

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
        /// It takes the pad, so any tile menu goes. Without <paramref name="showsPanel"/>, only a dialog shows.
        /// </summary>
        private void OpenGuide(bool showsPanel = true)
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

            // The clock at once; the battery as last read, at most a minute ago, and afresh once the Guide is up.
            RefreshTime();
            guideBatteryTimer?.Dispose();
            guideBatteryTimer = clock.After(BatteryReadAfterGuideOpens, () =>
            {
                guideBatteryTimer = null;
                Status.Battery = controller.Battery;
            });
            DropToasts();
            ShowTab(GuideTabKind.Landing);
            Guide.IsOverlayShown = true;
            Guide.ShowsPanel = showsPanel;
            Guide.IsOpen = true;

            // Taking the foreground can fail; the Overlay is Topmost and the Guide follows Playnite's callback either way.
            windowSystem.TakeForeground();
            FreezeRunningGame();
        }

        /// <summary>
        /// Freezes the running game under the Guide, so a game that reads the pad in the background can't take its
        /// input. Not one that is quitting, which must be free to close, nor one whose processes can't be found.
        /// </summary>
        private void FreezeRunningGame()
        {
            var directory = GameById(runningGame)?.InstallDirectory;
            if (gameFrozen || quittingGame != null || string.IsNullOrEmpty(directory))
            {
                return;
            }

            windowSystem.FreezeGame(directory, RunningGameWindow());
            gameFrozen = true;
        }

        /// <summary>Thaws the game frozen under the Guide, if it is, so it can run, take the foreground or close again.</summary>
        private void ThawGame()
        {
            if (gameFrozen)
            {
                gameFrozen = false;
                windowSystem.ThawGame();
            }
        }

        /// <summary>
        /// Selects <paramref name="item"/>. Most entries close the Guide through <paramref name="dismiss"/>, handing
        /// focus on as the entry says; a recent game while another game runs asks "Quit X and start Y?" instead.
        /// </summary>
        private void Select(GuideItem item, Action<Action> dismiss)
        {
            if (item == null || item.Kind == GuideItemKind.Profile || item.Kind == GuideItemKind.RecentAchievement || item.Kind == GuideItemKind.Notification || item.Kind == GuideItemKind.Volume
                || item.Kind == GuideItemKind.DiscordAccount || item.Kind == GuideItemKind.DiscordIdle || item.Kind == GuideItemKind.DiscordUnavailable
                || item.Kind == GuideItemKind.DiscordChannel || item.Kind == GuideItemKind.DiscordParticipant
                || item.Kind == GuideItemKind.NowPlaying || item.Kind == GuideItemKind.NothingPlaying || IsEmptyDropdown(item) || !item.IsEnabled)
            {
                return;
            }

            sounds.Play(UiSound.Enter);
            if (SelectDiscordAction(item) || SelectMediaControl(item))
            {
                return;
            }

            if (item.Kind == GuideItemKind.Power)
            {
                ShowPower();
                return;
            }

            if (item.Kind == GuideItemKind.SwitchProfile)
            {
                SwitchProfile();
                return;
            }

            if (item.Kind == GuideItemKind.Notifications || item.Kind == GuideItemKind.Audio)
            {
                StopRepeat();
                OpenFlyout(item.Kind);
                return;
            }

            if (IsDropdown(item))
            {
                StopRepeat();
                OpenDeviceList(item, null);
                return;
            }

            if (item.Kind == GuideItemKind.AudioDevice)
            {
                ChooseDevice(item);
                return;
            }

            if (item.Kind == GuideItemKind.Mute)
            {
                audio.SetMuted(!audio.IsMuted);
                item.IsOn = audio.IsMuted;
                return;
            }

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
                        CloseSettings();
                        CloseSearch();
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
                case GuideItemKind.Settings:
                    return () =>
                    {
                        playnite.BringToFront(DashboardPage.Settings);
                        ShowSettings(SettingsOrigin.Guide);
                    };
                case GuideItemKind.Search:
                    return () =>
                    {
                        playnite.BringToFront(DashboardPage.Search);
                        ShowSearch(SearchOrigin.Guide);
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
        private void Repeat(ControllerButton direction)
        {
            MoveFocus(direction);
            repeatTimer = clock.After(RepeatRate, () => Repeat(direction));
        }

        private void StopRepeat()
        {
            heldDirection = null;
            repeatTimer?.Dispose();
            repeatTimer = null;
        }

        /// <summary>
        /// Moves focus through the menu or prompt when one is showing, otherwise up and down the tab. Only a move that
        /// lands somewhere new makes a sound: none at either end.
        /// </summary>
        private void MoveFocus(ControllerButton direction)
        {
            var dialog = ActiveDialog;
            if (dialog != null)
            {
                var next = dialog.Towards(direction);
                if (next != dialog.FocusedOption)
                {
                    sounds.Play(UiSound.Navigate);
                    dialog.FocusedOption = next;
                }

                return;
            }

            if (!Guide.IsOpen || Guide.FocusedItem == null)
            {
                return;
            }

            var vertical = direction == ControllerButton.DPadUp || direction == ControllerButton.DPadDown;
            var by = direction == ControllerButton.DPadUp || direction == ControllerButton.DPadLeft ? -1 : 1;
            var entries = Guide.Flyout?.Items ?? Guide.Items;
            if (Guide.Flyout == null && Guide.Utilities.Contains(Guide.FocusedItem))
            {
                // The utilities row: left and right along it, up back to the tab's last entry.
                if (!vertical)
                {
                    Guide.FocusedItem = Step(Guide.Utilities, Guide.FocusedItem, by);
                }
                else if (by < 0)
                {
                    sounds.Play(UiSound.Navigate);
                    Guide.FocusedItem = Guide.Items.Last();
                }
            }
            else if (Guide.Flyout == null && vertical && by > 0 && Guide.FocusedItem == Guide.Items.Last() && Guide.Utilities.Count > 0)
            {
                sounds.Play(UiSound.Navigate);
                Guide.FocusedItem = Guide.Utilities[0];
            }
            else if (Guide.Flyout == null && MoveAmongShortcuts(direction))
            {
                // Among the Discord tab's shortcut tiles, or onto them.
            }
            else if (OpenDropdown != null)
            {
                // An open list has the pad: up and down along it, left and right nothing.
                if (vertical)
                {
                    Guide.FocusedItem = Step(OpenDropdown.Options, Guide.FocusedItem, by);
                }
            }
            else if (Guide.Flyout != null && MoveAmongMediaControls(direction))
            {
                // Onto, along or off the Now Playing card's row of controls.
            }
            else if (vertical)
            {
                Guide.FocusedItem = Step(entries, Guide.FocusedItem, by);
            }
            else if (Guide.FocusedItem.Kind == GuideItemKind.Volume)
            {
                TurnVolume(by);
            }
        }

        /// <summary>
        /// Left or right on the volume slider: Windows' master volume a step down or up, held at 0 and 100. The slider
        /// shows what Windows then reports, so a change it refuses doesn't move it.
        /// </summary>
        private void TurnVolume(int by)
        {
            var before = audio.Volume;
            audio.SetVolume(Math.Max(0, Math.Min(100, before + by * VolumeStep)));
            var after = audio.Volume;
            if (after != before)
            {
                sounds.Play(UiSound.Navigate);
            }

            Guide.FocusedItem.Level = after;
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

        /// <summary>
        /// The menu or prompt that has the pad: the Guide's while the Guide is open, otherwise the tile menu's More options
        /// flyout over the tile menu, or the tile menu itself.
        /// </summary>
        private Dialog ActiveDialog => Guide.IsOpen ? Guide.Dialog : Home.TileMenuFlyout ?? Home.TileMenu;

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
            if (Guide.IsOpen && Guide.Dialog == null && IsShowing(item))
            {
                Guide.FocusedItem = item;
            }
        }

        /// <summary>
        /// Whether the mouse can reach <paramref name="item"/>: an open dropdown's devices, otherwise an open flyout's
        /// entries and the Now Playing card's controls, otherwise the tab's and its utilities.
        /// </summary>
        private bool IsShowing(GuideItem item) =>
            OpenDropdown != null ? OpenDropdown.Options.Contains(item)
            : Guide.Flyout != null ? Guide.Flyout.Items.Contains(item) || IsMediaControl(item)
            : Guide.Items.Contains(item) || Guide.Utilities.Contains(item);

        /// <summary>A click on an entry. No pad button is held, so focus goes on at once.</summary>
        public void ChooseItem(GuideItem item)
        {
            if (!Guide.IsOpen || Guide.Dialog != null || !IsShowing(item))
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
                AfterAnswer(DismissAtOnce);
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
            answerHandBack = null;
            Guide.IsOpen = false;
        }

        /// <summary>Menu on an entry: the running game's menu, with Quit, or a Discord shortcut's, with Remove shortcut.</summary>
        private void ShowMenuFor(GuideItem item)
        {
            if (item == null || !item.HasMenu)
            {
                return;
            }

            if (item.Kind == GuideItemKind.DiscordShortcut)
            {
                ShowShortcutMenu(item);
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

        /// <summary>
        /// Shows a menu or prompt over the Guide, opening the Guide first if it is closed; with the Guide off, on its own
        /// over the dim, as Power from a hold is, and answering it hands focus back to the window in front.
        /// </summary>
        private void ShowDialog(Dialog dialog)
        {
            if (!Guide.IsOpen)
            {
                OpenGuide(showsPanel: Settings.Guide.IsGuideOn);
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

        /// <summary>
        /// Starts a game through Playnite, holding off other launches until Playnite reports it started or didn't. Its
        /// Launch screen shows as Playnite's starting event for it, finding it already starting, lets it through.
        /// </summary>
        private void Launch(Guid gameId)
        {
            // A game not installed goes to Playnite's install dialog, which never begins, starts or cancels the launch,
            // so it holds off nothing.
            if (GameById(gameId)?.Installed != false)
            {
                startingGame = gameId;
                StopLaunchBeginTimer();
                launchBeginTimer = clock.After(LaunchBeginTimeout, () =>
                {
                    launchBeginTimer = null;
                    if (startingGame == gameId)
                    {
                        startingGame = null;
                    }
                });
            }

            playnite.StartGame(gameId);
        }

        private void StopLaunchBeginTimer()
        {
            launchBeginTimer?.Dispose();
            launchBeginTimer = null;
        }

        private string NameOf(Guid gameId) => GameById(gameId)?.Name ?? "the game";

        private LibraryGame GameById(Guid? gameId) =>
            gameId != null && gamesById.TryGetValue(gameId.Value, out var game) ? game : null;

        /// <summary>Games that have been played, newest first, never hidden ones.</summary>
        private IEnumerable<LibraryGame> PlayedGames() =>
            library.Games.Where(g => g.LastActivity != null && !g.Hidden).OrderByDescending(g => g.LastActivity);

        /// <summary>
        /// A game went with the Guide open: rebuild the entries, keeping focus at the same place in the list, or in the
        /// flyout open over them.
        /// </summary>
        private void RefreshLandingTab()
        {
            var focused = Guide.FocusedItem;
            var index = Guide.Items.ToList().IndexOf(focused);
            ShowLandingTab();
            if (Guide.Flyout != null)
            {
                Guide.FocusedItem = focused;
                return;
            }

            Guide.FocusedItem = Guide.Utilities.FirstOrDefault(u => u.Kind == focused.Kind) ?? Guide.Items[Clamp(index, Guide.Items.Count)];
        }

        /// <summary>The landing tab, where the Guide always opens, focus on Home.</summary>
        private void ShowLandingTab()
        {
            var running = GameById(runningGame);
            var recent = PlayedGames().Where(g => g != running);
            var games = (running == null ? recent : new[] { running }.Concat(recent)).Take(RecentGameCount);

            // A divider separates the games from Home and My games & apps.
            var home = new GuideItem(GuideItemKind.Home, "Home");
            Guide.Items = new[] { home, new GuideItem(GuideItemKind.MyGamesAndApps, "My games & apps") }
                .Concat(games.Select((g, index) => new GuideItem(
                    g == running ? GuideItemKind.RunningGame : GuideItemKind.RecentGame, g.Name, g.Id, hasDividerAbove: index == 0, image: g.CoverImage, source: g.Source)))
                .ToList();
            Guide.Utilities = new[]
            {
                new GuideItem(GuideItemKind.Notifications, "Notifications"),
                new GuideItem(GuideItemKind.Audio, "Audio"),
                new GuideItem(GuideItemKind.Search, "Search"),
            };
            Guide.FocusedItem = home;
        }

        /// <summary>
        /// The Guide has gone: hands focus on as it was closed. Every close comes through here, so this is where the
        /// game frozen under it thaws, first, so it can take the foreground again.
        /// </summary>
        private void HandFocusBack()
        {
            var handBack = pendingHandBack;
            CancelReleaseWait();
            ThawGame();
            handBack();
            homeOwed = false;
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

        /// <summary>Back to the window that was in front when the Guide opened, or to Playnite if it has gone; to Home when a game exited under the Guide.</summary>
        private void ReturnToRecordedWindow()
        {
            if (homeOwed)
            {
                homeOwed = false;
                playnite.BringToFront(DashboardPage.Home);
            }
            else if (windowSystem.Exists(recordedForeground))
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

        /// <summary>
        /// A Toast waiting its turn: a Playnite notification's, or a game joining the queue's, which isn't one of
        /// Playnite's and so has no notification to stand for it.
        /// </summary>
        private sealed class WaitingToast
        {
            public WaitingToast(
                string text, ToastKind kind, string image, Guid? gameId = null, string detail = null, int? score = null,
                double? rarity = null, string description = null)
            {
                Text = text;
                Kind = kind;
                Image = image;
                GameId = gameId;
                Detail = detail;
                Score = score;
                Rarity = rarity;
                Description = description;
            }

            public string Text { get; }
            public ToastKind Kind { get; }

            /// <summary>The image the tile shows in place of its kind's own; null for the kind's own.</summary>
            public string Image { get; }

            /// <summary>The game a Ready to play Toast tells of, which its Guide press starts; null for any other.</summary>
            public Guid? GameId { get; }

            /// <summary>An Achievement unlocked Toast's achievement name, under its line; null for any other.</summary>
            public string Detail { get; }

            /// <summary>An Achievement unlocked Toast's achievement's Score; null for any other.</summary>
            public int? Score { get; }

            /// <summary>A rare achievement's rarity, the percentage of players who have it; null for any other Toast.</summary>
            public double? Rarity { get; }

            /// <summary>A rare achievement's description, its Toast's second page; null for none and for any other Toast.</summary>
            public string Description { get; }
        }
    }
}
