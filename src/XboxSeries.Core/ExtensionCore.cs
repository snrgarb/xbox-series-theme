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
        /// How many games a Group on Home shows before its See all tile: as many as the Recent row, so that every row
        /// on Home is the same width.
        /// </summary>
        private const int GroupTileCount = RecentRowCount;

        /// <summary>How soon a second A on the same Home tile counts as a double-tap, which launches the game.</summary>
        private static readonly TimeSpan DoubleTapWindow = TimeSpan.FromMilliseconds(300);

        /// <summary>How long focus has to stay on a Recent-row tile before the hero follows it, so scrolling past doesn't flicker it.</summary>
        private static readonly TimeSpan HeroSettle = TimeSpan.FromSeconds(1.5);

        /// <summary>
        /// How long the hero waits, once focus has left a Recent-row tile, for it to land on another before the swirl
        /// comes back: WPF tells of the tile left before the one focused.
        /// </summary>
        private static readonly TimeSpan HeroLeave = TimeSpan.FromMilliseconds(100);

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

        /// <summary>How far one press of left or right turns the volume, as Windows' own volume keys do.</summary>
        private const int VolumeStep = 2;

        /// <summary>How long a Toast shows, and the pause before the next one waiting.</summary>
        private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ToastGap = TimeSpan.FromSeconds(0.5);

        /// <summary>
        /// How long the Launch screen waits for the game to start before giving up, so a launcher waiting for the user
        /// (a sign-in, an update, an agreement) isn't hidden for good.
        /// </summary>
        private static readonly TimeSpan LaunchScreenTimeout = TimeSpan.FromSeconds(90);

        private readonly IClock clock;
        private readonly IWindowSystem windowSystem;
        private readonly IPlayniteHost playnite;
        private readonly ISoundPlayer sounds;
        private readonly IDrives drives;
        private readonly IUserAccount account;
        private readonly IAudio audio;
        private readonly IController controller;
        private LibrarySnapshot library = LibrarySnapshot.Empty;
        private IReadOnlyList<Notification> notifications = new Notification[0];

        /// <summary>The ids of the notifications last seen, so only new ones get a Toast; null until the first are seen.</summary>
        private HashSet<string> seenNotifications;
        private readonly Queue<Notification> waitingToasts = new Queue<Notification>();
        private IDisposable toastTimer;

        /// <summary>The notification whose Toast is showing, which the call's Toast puts back in line; null for none.</summary>
        private Notification shownNotification;

        /// <summary>
        /// What the Toast showing as the Guide button went down told of, which the tap opens the Guide at: Notifications,
        /// the Discord tab's Answer for a call, or Profile &amp; system for the greeting; null for no Toast.
        /// </summary>
        private GuideItemKind? toastAtGuidePress;
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
        private Guid? heroGame;
        private Guid? tappedGame;
        private IDisposable tapTimer;
        private IDisposable heroTimer;
        private HomeTile focusedTile;

        /// <summary>Whether A, still held, answered the tile menu: Playnite's own Enter for it must not choose the tile.</summary>
        private bool tileMenuHoldsA;

        /// <summary>Whether My games &amp; apps has put its own filter on the main list, with the user's kept to give back.</summary>
        private bool ownsMainList;

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
            ISecretStore secrets)
        {
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
            LoadDiscordApp();
            LoadShortcuts();
            LoadRecentServers();
            LoadStartUp();
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
            library = snapshot;
            RefreshHome();
            RefreshFullLibraryCount();
            RefreshApps(snapshot.Apps);

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
                waitingToasts.Enqueue(notification);
            }

            if (toastTimer == null)
            {
                ShowNextToast();
            }
        }

        /// <summary>
        /// The next notification's Toast, with the notification sound, unless the call's or the greeting is showing, which
        /// they wait behind, or Start-up covers the screen, which they wait for.
        /// </summary>
        private void ShowNextToast()
        {
            toastTimer = null;
            if (waitingToasts.Count == 0 || ringToastTimer != null || greetingTimer != null || Startup.IsShown)
            {
                return;
            }

            shownNotification = waitingToasts.Dequeue();
            Toast.Text = shownNotification.Text;
            Toast.Kind = shownNotification.Kind;
            Toast.Image = null;
            Toast.IsShown = true;
            sounds.Play(UiSound.Notification);
            toastTimer = clock.After(ToastDuration, () =>
            {
                shownNotification = null;
                Toast.IsShown = false;
                toastTimer = waitingToasts.Count == 0 ? null : clock.After(ToastGap, ShowNextToast);
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
            shownNotification = null;
            waitingToasts.Clear();
            Toast.IsShown = false;
        }

        /// <summary>
        /// A notification's Toast showing, or the gap after one, is cut short: the notification goes back to the front of
        /// those waiting, to show again in its turn.
        /// </summary>
        private void PutBackShownNotification()
        {
            if (toastTimer == null)
            {
                return;
            }

            toastTimer.Dispose();
            toastTimer = null;
            if (Toast.IsShown && Toast.Kind != ToastKind.Call && shownNotification != null)
            {
                var waiting = waitingToasts.ToList();
                waitingToasts.Clear();
                waitingToasts.Enqueue(shownNotification);
                foreach (var notification in waiting)
                {
                    waitingToasts.Enqueue(notification);
                }
            }

            shownNotification = null;
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

        private void RefreshStatus()
        {
            Status.Time = clock.Now.ToString("t", CultureInfo.CurrentCulture);
            Status.Battery = controller.Battery;
        }

        /// <summary>Playnite's main list changed its games. On Full library, that's the user switching tabs, so its count follows.</summary>
        public void OnMainListChanged()
        {
            if (MyGames.ShowsFullLibrary)
            {
                RefreshFullLibraryCount();
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
            HideLaunchScreenFor(gameId);
            RefreshHome();
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

            if (Guide.IsOpen && Guide.Tab == GuideTabKind.Landing)
            {
                RefreshLandingTab();
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
            else if (!Startup.IsShown)
            {
                playnite.BringToFront(DashboardPage.Home);
            }

            Home.ReturnFromGame(gameId);
        }

        /// <summary>Takes away a menu or prompt about a game. Power is about the PC, so it stays.</summary>
        private void CloseGameDialog()
        {
            if (Guide.Dialog != null && !Guide.Dialog.IsWide)
            {
                Guide.Dialog = null;
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
            // Start-up has the pad to itself, the Guide button too; a button held from before it lets go as usual.
            if (Startup.IsShown && (state == ButtonState.Pressed || button == ControllerButton.Guide))
            {
                OnStartupButton(button, state);
                return;
            }

            if (button == ControllerButton.Guide)
            {
                OnGuideButton(state, at);
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

                if (button == ControllerButton.A)
                {
                    tileMenuHoldsA = false;
                }

                return;
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
            else if (button == ControllerButton.A && dialog != null)
            {
                tileMenuHoldsA = dialog == Home.TileMenu;
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
            else if (button == ControllerButton.Start && dialog == null && focusedTile != null && !Guide.IsOverlayShown)
            {
                ShowTileMenu(focusedTile);
            }
            else if (button == ControllerButton.A && dialog == null && focusedTile != null && !Guide.IsOverlayShown)
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
                toastAtGuidePress = !Toast.IsShown ? (GuideItemKind?)null
                    : Toast.Kind == ToastKind.Call ? GuideItemKind.DiscordIncomingCall
                    : Toast.Kind == ToastKind.Greeting ? GuideItemKind.Profile
                    : GuideItemKind.Notifications;
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
            else if (Guide.IsOpen && toastAtGuidePress == GuideItemKind.DiscordIncomingCall && ringingChannelId != null && Settings.Discord.HasDiscordTab)
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

        /// <summary>The profile on Home's top bar: the Guide, on Profile &amp; system.</summary>
        public void OpenProfile()
        {
            if (Guide.IsOpen)
            {
                return;
            }

            sounds.Play(UiSound.OpenMenu);
            OpenGuide();
            ShowTab(GuideTabKind.ProfileAndSystem);
        }

        /// <summary>
        /// A tap opens the Guide at the subject of the Toast showing, as the console does: Notifications for a
        /// notification's, the Discord tab, focus on Answer, for a call's, and Profile &amp; system for the greeting.
        /// </summary>
        private void OpenGuideFromTap()
        {
            guideTapTimer = null;
            sounds.Play(UiSound.OpenMenu);
            OpenGuide();
            if (toastAtGuidePress == GuideItemKind.Notifications)
            {
                OpenFlyout(GuideItemKind.Notifications);
            }
            else if (toastAtGuidePress == GuideItemKind.DiscordIncomingCall && Settings.Discord.HasDiscordTab)
            {
                ShowTab(GuideTabKind.Discord);
            }
            else if (toastAtGuidePress == GuideItemKind.Profile)
            {
                ShowTab(GuideTabKind.ProfileAndSystem);
            }
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
            var handBack = answerHandBack;
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
        /// in or not (<see cref="DiscordSettingsState.HasDiscordTab"/>).
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
        /// sign-in, then Settings and Power, each under a divider.
        /// </summary>
        private void ShowProfileTab()
        {
            Guide.Items = new[]
            {
                new GuideItem(GuideItemKind.Profile, Status.Profile?.Gamertag ?? account.Name, image: ProfilePicture),
                new GuideItem(GuideItemKind.Settings, "Settings", hasDividerAbove: true),
                new GuideItem(GuideItemKind.Power, "Power", hasDividerAbove: true),
            };
            Guide.FocusedItem = Guide.Items[0];
        }

        /// <summary>
        /// A game tile on Home got focus: Menu now opens its tile menu. The hero shows a Recent-row tile's game art at once
        /// coming into the row onto the swirl, and along the row once focus has stayed on a tile for a moment, the last
        /// art up meanwhile. Any other tile, as a Group's, has the swirl behind Home.
        /// </summary>
        public void FocusHomeTile(HomeTile tile)
        {
            focusedTile = tile;
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

            heroTimer = clock.After(HeroSettle, () =>
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
        /// Opens My games &amp; apps on <paramref name="item"/>, in place of Settings if that is up, or moves it there. Full
        /// library is the main list under the user's own filter and sort. Games and Play history take the main list over,
        /// keeping the user's filter and sort first (as Full library last left them), to give back on Full library or when
        /// the page goes. Apps shows its own tiles and leaves the main list as it is, hidden behind them; with no Apps to
        /// show (Software tools couldn't be read), it opens nothing.
        /// </summary>
        public void OpenMyGames(MyGamesItem item)
        {
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

            // On opening, for today's sections, the drive's space now, and a Full library count Playnite couldn't give
            // before its main view was up. Moving through the nav keeps Play history: rebuilding it rebuilds every row and its art.
            if (!MyGames.IsShown)
            {
                RefreshPlayHistory();
                RefreshFullLibraryCount();
                RefreshStorage();
            }

            // The two pages are never up together: My games & apps takes Settings' place.
            CloseSettings();
            MyGames.Item = item;
            MyGames.IsShown = true;
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
            if (!MyGames.IsShown || MyGames.Item != MyGamesItem.Apps || ActiveDialog != null || Guide.IsOverlayShown)
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

        /// <summary>Opens Settings from Home's top bar, with the rail's first category showing; B goes back to the top bar.</summary>
        public void OpenSettings() => ShowSettings(SettingsOrigin.TopBar);

        /// <summary>
        /// Puts Settings up on the rail's first category, every time it opens. It takes My games &amp; apps' place, which
        /// gives the user's own filter and sort back.
        /// </summary>
        private void ShowSettings(SettingsOrigin from)
        {
            CloseMyGames();
            RefreshGamerpics();
            Settings.Category = SettingsCategory.Personalisation;
            Settings.OpenedFrom = from;
            Settings.IsShown = true;
        }

        /// <summary>The rail's focus moved to <paramref name="category"/>: the page shows it.</summary>
        public void FocusSettingsCategory(SettingsCategory category)
        {
            if (Settings.IsShown)
            {
                Settings.Category = category;
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
        /// B on the page: back to where it was opened from (<see cref="SettingsState.OpenedFrom"/>). Playnite's own B
        /// reaches the page before the pad's reaches the core, so a menu or prompt still showing answers the B instead.
        /// </summary>
        public void LeaveSettings()
        {
            if (Settings.IsShown && ActiveDialog == null && !Guide.IsOverlayShown)
            {
                CloseSettings();
            }
        }

        private void CloseSettings()
        {
            Settings.IsShown = false;
        }

        /// <summary>Playnite is leaving Fullscreen, or closing: the user's own filter and sort go back, and a game frozen under the Guide thaws.</summary>
        public void OnApplicationStopped()
        {
            ThawGame();
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

            RefreshStatus();
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
            if (item == null || item.Kind == GuideItemKind.Profile || item.Kind == GuideItemKind.Notification || item.Kind == GuideItemKind.Volume
                || item.Kind == GuideItemKind.DiscordAccount || item.Kind == GuideItemKind.DiscordIdle || item.Kind == GuideItemKind.DiscordUnavailable
                || item.Kind == GuideItemKind.DiscordChannel || item.Kind == GuideItemKind.DiscordParticipant || IsEmptyDropdown(item))
            {
                return;
            }

            sounds.Play(UiSound.Enter);
            if (SelectDiscordAction(item))
            {
                return;
            }

            if (item.Kind == GuideItemKind.Power)
            {
                ShowPower();
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
            if (Guide.IsOpen && Guide.Dialog == null && IsShowing(item))
            {
                Guide.FocusedItem = item;
            }
        }

        /// <summary>
        /// Whether the mouse can reach <paramref name="item"/>: an open dropdown's devices, otherwise an open flyout's
        /// entries, otherwise the tab's and its utilities.
        /// </summary>
        private bool IsShowing(GuideItem item) =>
            OpenDropdown != null ? OpenDropdown.Options.Contains(item)
            : Guide.Flyout != null ? Guide.Flyout.Items.Contains(item)
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

        /// <summary>
        /// Starts a game through Playnite, holding off other launches until Playnite reports it started or didn't. Its
        /// Launch screen shows as Playnite's starting event for it, finding it already starting, lets it through.
        /// </summary>
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
    }
}
