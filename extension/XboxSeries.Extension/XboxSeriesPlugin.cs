using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows.Controls;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    public class XboxSeriesPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly ExtensionCore core;
        private readonly Win32WindowSystem windows;
        private readonly HomeScreen homeScreen = new HomeScreen();
        private readonly PlayniteSounds sounds;
        private readonly PlayniteHost host;
        private readonly GamingInputController controller = new GamingInputController();
        private readonly WindowsAudio audio = new WindowsAudio();
        private readonly WindowsMediaSessions mediaSessions = new WindowsMediaSessions();
        private readonly PlayniteAchievementsSource achievements;
        private readonly DiscordPipe discordPipe = new DiscordPipe();
        private readonly DiscordOAuth discordOAuth = new DiscordOAuth();
        private readonly PopularGamerpics popularGamerpics;

        /// <summary>Input anywhere in Windows, a game's too, for the Idle screen, which dims over a game as over Playnite.</summary>
        private readonly WindowsInputActivity inputActivity = new WindowsInputActivity();
        private bool libraryPushed;

        /// <summary>
        /// Whether Playnite shows the Dashboard (<see cref="DashboardPresence"/>), settled on the UI thread by
        /// <see cref="IsOnDashboard"/>; false until then. Read from any thread: dormant, every callback leaves Playnite to itself.
        /// </summary>
        private volatile bool onDashboard;
        private bool dashboardSettled;

        /// <summary>1 while a push asked for by <see cref="PushLibrarySoon"/> is waiting to run.</summary>
        private int libraryPushPending;

        /// <summary>How long a database change waits for the rest of its burst before the library is pushed.</summary>
        private static readonly TimeSpan LibraryPushDelay = TimeSpan.FromMilliseconds(300);
        private ToastWindow toastWindow;
        private LaunchScreenWindow launchScreenWindow;
        private DimWindow dimWindow;
        private IdleInput idleInput;

        /// <summary>Whether Fullscreen's Start-up sequence has begun, which it does once.</summary>
        private bool startupBegun;

        /// <summary>The Start-up sequence running on its own thread, until its window has faded out; null otherwise.</summary>
        private StartupThread startup;

        // Home's build, for the Start-up timing: how many elements, when the first and last were asked for, and the time
        // spent making ours and pushing the library, in stopwatch ticks.
        private int homeElements;
        private long firstHomeElementAt;
        private long lastHomeElementAt;
        private long homeElementTicks;
        private long homePushTicks;
#if DEBUG
        private DebugGuideHotkey debugGuideHotkey;
        private SteamDownloads steamDownloads;
#endif

        // Set once, as PluginGuid in the csproj, which also builds the manifest Id from it.
        public override Guid Id { get; } = Guid.Parse(typeof(XboxSeriesPlugin).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "PluginGuid").Value);

        public XboxSeriesPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = false };

            // First, so a game a crashed Playnite left frozen runs again, and so a crash from here on doesn't leave one frozen.
            windows = new Win32WindowSystem(GetPluginUserDataPath());
            windows.ThawLeftoverGame();
            AppDomain.CurrentDomain.UnhandledException += (_, __) => windows.ThawGame();
            AppDomain.CurrentDomain.ProcessExit += (_, __) => windows.ThawGame();
            sounds = new PlayniteSounds(api);
            host = new PlayniteHost(api, GetPluginUserDataPath());
            var downloads = new SteamDownloads(api);
            popularGamerpics = new PopularGamerpics(new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                Path.Combine(GetPluginUserDataPath(), "Gamerpics", "Popular"));

            // PlayniteAchievements may load after us: the source finds it only once started.
            achievements = new PlayniteAchievementsSource(api);
#if DEBUG
            steamDownloads = downloads;
#endif
            core = new ExtensionCore(new DispatcherClock(), windows, host, sounds, new WindowsDrives(), new WindowsUserAccount(GetPluginUserDataPath(), popularGamerpics), audio, controller,
                discordPipe, discordOAuth, new DpapiSecretStore(GetPluginUserDataPath()), downloads, mediaSessions,
                achievements: achievements, inputActivity: inputActivity);
            core.StartupEnded += () => startup?.End();

            // The Guide's Switch profile: Who's playing today? over everything, as on waking. Raised on the UI thread
            // from the Guide's A press, and shown once that press has been handled.
            core.WhosPlayingAsked += plan => OnUiThread(() =>
            {
                StartupTiming.Begin("Start-up shown on Switch profile");
                ShowStartup(plan);
            });

            // Home's elements, its top bar's status area, My games & apps, Settings, the Game hub's Install, Search, text entry and the hint bar, placed by
            // the Dashboard as ContentControls named XboxSeries_<element>.
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = "XboxSeries",
                ElementList = new List<string> { "Hero", "RecentRow", "Groups", "TileMenu", "MyGames", "Settings", "Status", "InstallAction", "Search", "TextEntry", "Hints" },
            });
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            // Only the Dashboard places our elements, but another theme naming one gets nothing from a dormant extension.
            if (!IsOnDashboard())
            {
                return null;
            }

            // The first of Home's elements is the earliest the extension hears from Playnite on its UI thread with its
            // window made, seconds before OnApplicationStarted: the Start-up sequence covers the screen from here, so
            // Home is never seen before it.
            BeginStartup();
            lastHomeElementAt = StartupTiming.Now;
            if (homeElements++ == 0)
            {
                firstHomeElementAt = lastHomeElementAt;
                StartupTiming.Log($"Home's first element, {args.Name},");
            }

            // Playnite builds Home before it raises OnApplicationStarted, and can take seconds between the two. The
            // database is already loaded by then, so Home's elements start from it rather than showing empty until then.
            if (!libraryPushed && PlayniteApi.Database.IsOpen)
            {
                StartupTiming.Sum(ref homePushTicks, () =>
                {
                    PushLibrary();
                    return true;
                });
            }

            return StartupTiming.Sum(ref homeElementTicks, () => MakeElement(args.Name));
        }

        private Control MakeElement(string name)
        {
            switch (name)
            {
                case "Hero": return new HeroControl(core.Home);
                case "RecentRow": return new RecentRowControl(core, homeScreen);
                case "Groups": return new GroupsControl(core, homeScreen);
                case "TileMenu": return new TileMenuControl(core);
                case "MyGames": return new MyGamesControl(core);
                case "Settings":
                    var settings = new SettingsControl(core);
#if DEBUG
                    settings.SteamTrials = steamDownloads;
#endif
                    return settings;
                case "Status": return new StatusControl(core);
                case "InstallAction": return new InstallActionControl(core);
                case "Hints": return new HintBarControl(core);
                case "Search": return new SearchControl(core);
                case "TextEntry": return new TextEntryControl(core);
                default: return null;
            }
        }

        /// <summary>Home's build so far, for the Start-up timing.</summary>
        private void LogHomeBuild(string when)
        {
            StartupTiming.Log($"Home's build, {when}: {homeElements} elements, the first at {firstHomeElementAt} ms and the last at "
                + $"{lastHomeElementAt} ms, {StartupTiming.Ms(homeElementTicks)} ms making ours and "
                + $"{StartupTiming.Ms(homePushTicks)} ms pushing the library; now");
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            StartupTiming.Log("Our OnApplicationStarted began");
            if (!IsOnDashboard())
            {
                return;
            }

            LogHomeBuild("as OnApplicationStarted began");
            inputActivity.Start();

            // Created here, on the UI thread, which owns the Overlay and runs the core. The Toast's may be already, for
            // the Start-up sequence's greeting.
            OverlayWindow overlay = null;
            StartupTiming.Time("Making the Overlay", () => overlay = new OverlayWindow(core));
            windows.Overlay = overlay;

            // Made and laid out once the UI thread is idle, so the Guide's first open is as quick as the rest.
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                Guarded.Run("Prewarming the Overlay", () => StartupTiming.Time("Prewarming the Overlay", overlay.Prewarm))));
            toastWindow = toastWindow ?? new ToastWindow(core.Toast);

            // Beneath the Toast's and the Overlay, which it keeps above it as it shows, so Toasts and the Guide show over it.
            launchScreenWindow = launchScreenWindow ?? new LaunchScreenWindow(core.LaunchScreen, toastWindow, overlay);

            // The Idle screen's dim, over the Overlay and the Launch screen's, going back over them when either shows
            // while it is up, and beneath the Toast's, which it keeps above it, so Toasts show over it. Every input on
            // this thread is told to the core for its timers, and the press that only wakes the screen swallowed.
            dimWindow = dimWindow ?? new DimWindow(core.Idle, core.Status, new Window[] { overlay, launchScreenWindow }, toastWindow);
            idleInput = idleInput ?? new IdleInput(core, controller);

            // So work left for when the UI thread is idle runs at once, not when something else next wakes it.
            DispatcherHeartbeat.Start(PlayniteApi.MainView.UIDispatcher);
#if DEBUG
            debugGuideHotkey = new DebugGuideHotkey(core);
#endif
            host.RestoreLeftUserFilter();

            // Begun with Home's first element, unless the theme has none of them; Playnite's window may have had no
            // handle yet when it began.
            BeginStartup();
            startup?.SetPlayniteWindow(PlayniteWindow());
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // The game menu's Uninstall, Manage game's too, goes through the core for a game its client can uninstall
            // without its own windows.
            GameMenuUninstall.Register(core);

            StartupTiming.Time("Pushing the library", PushLibrary);
            GameHubFocusSounds.Register(sounds);

            // Database events can arrive on background threads; the core only runs on the UI thread. A metadata download
            // or an import raises one for each game, so a burst of them makes one snapshot, not one each (PushLibrarySoon).
            PlayniteApi.Database.Games.ItemCollectionChanged += (_, __) => PushLibrarySoon();
            PlayniteApi.Database.Games.ItemUpdated += (_, args) =>
            {
                PushLibrarySoon();
                Guarded.Run("Following launches", () => PushLaunchStates(args));
            };
            PlayniteApi.Database.FilterPresets.ItemCollectionChanged += (_, __) => PushLibrarySoon();
            PlayniteApi.Database.FilterPresets.ItemUpdated += (_, __) => PushLibrarySoon();

            // Apps follow Software tools added, renamed or removed in Playnite, on a build that tells when they change.
            if (PlayniteLibrary.SoftwareTools() is IItemCollection<Playnite.SDK.Models.AppSoftware> softwareTools)
            {
                softwareTools.ItemCollectionChanged += (_, __) => PushLibrarySoon();
                softwareTools.ItemUpdated += (_, __) => PushLibrarySoon();
            }

            PushNotifications();
            controller.Changed += () => OnUiThread(core.OnControllersChanged);
            audio.DevicesChanged += () => OnUiThread(core.OnAudioDevicesChanged);

            // Windows' media sessions, for the Audio flyout's Now Playing, followed from here: what it shows is ready
            // before the Guide first opens. Their changes arrive on threads of their own.
            mediaSessions.Changed += () => OnUiThread(core.OnMediaSessionChanged);
            mediaSessions.ArtFetched += (request, art) => OnUiThread(() => core.OnMediaArt(request, art));
            // Each optional part started guarded, so one failing leaves the rest, and what follows, started.
            Guarded.Run("Starting the media sessions", () => StartupTiming.Time("Starting the media sessions", mediaSessions.Start));

            // The popular gamerpics, fetched afresh on every start, in the background: those saved before are offered
            // meanwhile, and Settings reads them again each time it opens.
            _ = FetchPopularGamerpicsAsync();

            // PlayniteAchievements, every plugin loaded by now: its changes and live unlocks arrive on the UI thread, and
            // one change once its games are first read, which the core takes as where it starts announcing unlocks.
            achievements.Changed += () => Guarded.Run("Taking the achievements' changes", core.OnAchievementsChanged);
            achievements.Unlocked += (game, unlock) => Guarded.Run("Announcing an unlock", () => core.OnAchievementUnlocked(game, unlock));
            Guarded.Run("Starting the achievements",
                () => StartupTiming.Time("Starting the achievements", () => achievements.Start(PlayniteApi.MainView.UIDispatcher)));

            // Found or not, it has been looked for: without it, Home's Achievements tile says so. The log says what the
            // tile does, as the totals come and go: the only sign, off the screen, of which case a user is in.
            string said = null;
            core.Status.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(StatusState.Achievements) && e.PropertyName != nameof(StatusState.AchievementsNote))
                {
                    return;
                }

                var totals = core.Status.Achievements;
                var says = totals != null
                    ? $"Achievements: {totals.Score} Score, {totals.Unlocked} unlocked"
                    : $"Achievements: none ({core.Status.AchievementsNote ?? "not read yet"})";
                if (says != said)
                {
                    said = says;
                    logger.Info(says);
                }
            };
            Guarded.Run("Saying whether there are achievements", core.OnAchievementsLookedFor);

            // The Tile menu and its More options flyout, with their rows in order, as each opens and closes.
            core.Home.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HomeState.TileMenu) || e.PropertyName == nameof(HomeState.TileMenuFlyout))
                {
                    var menu = e.PropertyName == nameof(HomeState.TileMenu) ? core.Home.TileMenu : core.Home.TileMenuFlyout;
                    var what = e.PropertyName == nameof(HomeState.TileMenu) ? "Tile menu" : "Tile menu's flyout";
                    logger.Info(menu == null ? $"{what} closed" : $"{what} {menu.Title}: {string.Join(", ", menu.Options.Select(o => o.Label))}");
                }
            };
            PlayniteApi.Notifications.Messages.CollectionChanged += (_, __) => OnUiThread(PushNotifications);

            // Discord's frames and token answers arrive on background threads. Subscribed before the core signs in with
            // the tokens saved from before, so no early frame is lost.
            discordPipe.FrameReceived += (opcode, json) => OnUiThread(() => core.OnDiscordFrame(opcode, json));
            discordPipe.Closed += () => OnUiThread(core.OnDiscordClosed);
            discordOAuth.TokenResponded += (status, body) => OnUiThread(() => core.OnDiscordTokenResponse(status, body));
            Guarded.Run("Signing in to Discord",
                () => StartupTiming.Time("Signing in to Discord with the saved tokens", core.OnApplicationStarted));
            logger.Info("Xbox Series extension started on the Dashboard");
            StartupTiming.Log($"Our OnApplicationStarted took {started.ElapsedMilliseconds} ms, ending");
        }

        private async Task FetchPopularGamerpicsAsync()
        {
            try
            {
                await popularGamerpics.FetchAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.Warn(e, "Couldn't fetch the popular gamerpics");
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
#if DEBUG
            debugGuideHotkey?.Dispose();
#endif
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            windows.Overlay?.Close();
            toastWindow?.Close();
            launchScreenWindow?.Close();
            idleInput?.Dispose();
            inputActivity.Dispose();
            dimWindow?.Close();
            if (startup != null)
            {
                startup.Shutdown();
                startup = null;
                sounds.SilenceThemeSounds(false);
            }

            discordPipe.Close();
            audio.Dispose();
            mediaSessions.Dispose();
            achievements.Dispose();

            // The user's own filter and sort go back. Playnite has already saved its settings, so they are saved again.
            if (onDashboard)
            {
                PlayniteApi.MainView.UIDispatcher.Invoke(core.OnApplicationStopped);
            }

            // The core thaws a frozen game as it stops; this is in case it couldn't, or never ran, as for a game a
            // Dashboard session left frozen. Does nothing when none is frozen.
            windows.ThawGame();

            // Playnite releases its audio engine only after this returns, so the sound gets to finish.
            if (onDashboard)
            {
                sounds.PlayToEnd(UiSound.PowerOff);
            }
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            if (!onDashboard)
            {
                return;
            }

            var id = args.Game.Id;
            OnUiThread(() => core.OnGameStarted(id));
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Dormant, every launch is Playnite's to make.
            if (!onDashboard)
            {
                return;
            }

            // The answer is needed before this returns, so ask the core synchronously on the UI thread (inline if already there).
            var id = args.Game.Id;
            var allowed = PlayniteApi.MainView.UIDispatcher.Invoke(() => core.OnGameStarting(id));
            if (!allowed)
            {
                logger.Info($"Held back the launch of {args.Game.Name}: another game is running, quitting or launching");
                args.CancelStartup = true;
            }
        }

        public override void OnGameStartupCancelled(OnGameStartupCancelledEventArgs args)
        {
            if (!onDashboard)
            {
                return;
            }

            var id = args.Game.Id;
            OnUiThread(() => core.OnGameStartupCancelled(id));
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (!onDashboard)
            {
                return;
            }

            var id = args.Game.Id;
            OnUiThread(() => core.OnGameStopped(id));
        }

        public override void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args)
        {
            if (!onDashboard)
            {
                return;
            }

            var button = PlayniteInput.ToButton(args.Button);
            if (button == null)
            {
                return;
            }

            // Stamped here, as the pad reports it: the UI thread can be busy for longer than a hold.
            var state = PlayniteInput.ToState(args.State);
            var at = DateTime.Now;
            if (button == ControllerButton.Guide)
            {
                logger.Info($"Guide button {state} at {at:HH:mm:ss.fff}");

                // The Start-up sequence reads the pad itself, but XInputGetState never reads the Guide button. The
                // core, which it reaches too, swallows it meanwhile.
                startup?.Press(button.Value, state);
            }

            OnUiThread(() => core.OnControllerButton(button.Value, state, at));
        }

        /// <summary>
        /// The Start-up sequence, once, on the Dashboard only, on a thread of its own; the Toast's window, for its greeting,
        /// made here on the UI thread.
        /// </summary>
        private void BeginStartup()
        {
            if (startupBegun || !onDashboard)
            {
                return;
            }

            startupBegun = true;
            StartupTiming.Begin("Start-up shown");

            // When the UI thread first has nothing left to do: the end of the freeze Start-up used to share.
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                StartupTiming.Log("The UI thread's first idle after Start-up showed");
                LogHomeBuild("at the UI thread's first idle");
            }));
            toastWindow = toastWindow ?? new ToastWindow(core.Toast);
            ShowStartup(core.OnStartup());
        }

        /// <summary>Windows woke from sleep: Who's playing today? again, if it asks. Raised on a thread of its own.</summary>
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                OnUiThread(() =>
                {
                    var plan = core.OnResumedFromSleep();
                    if (plan != null)
                    {
                        StartupTiming.Begin("Start-up shown on waking from sleep");
                        ShowStartup(plan);
                    }
                });
            }
        }

        /// <summary>
        /// A Start-up sequence on a thread of its own, from the core's plan; none for no plan. Playnite's own navigation
        /// and activation sounds are silenced first, here on the UI thread, before Home takes its first focus behind it,
        /// and come back once its window has faded out. A sequence still fading out as another begins, as on waking just
        /// after one, finishes without bringing them back.
        /// </summary>
        private void ShowStartup(StartupPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            sounds.SilenceThemeSounds(true);
            StartupThread shown = null;
            shown = new StartupThread(
                plan,
                PlayniteApi.MainView.UIDispatcher,
                controller,
                sounds,
                PlayniteWindow(),
                () => core.IsStartupShown,
                core.OnStartupSignedIn,
                () =>
                {
                    if (startup == shown)
                    {
                        startup = null;
                        sounds.SilenceThemeSounds(false);
                    }
                });
            startup = shown;
            if (PlayniteWindow() == IntPtr.Zero && Application.Current?.MainWindow is Window main && !IsOurs(main))
            {
                // Made but without a handle yet: handed over as soon as it has one.
                main.SourceInitialized += (_, __) => shown.SetPlayniteWindow(new WindowInteropHelper(main).Handle);
            }
        }

        /// <summary>Playnite's own window, or <see cref="IntPtr.Zero"/> before it has one. On the UI thread.</summary>
        private static IntPtr PlayniteWindow()
        {
            var main = Application.Current?.MainWindow;
            return main == null || IsOurs(main) ? IntPtr.Zero : new WindowInteropHelper(main).Handle;
        }

        /// <summary>One of the extension's own windows, as the Toast's, which WPF makes its main window when it is the first made.</summary>
        private static bool IsOurs(Window window) => window.GetType().Assembly == typeof(XboxSeriesPlugin).Assembly;

        /// <summary>
        /// Whether Playnite shows the Dashboard, settled the first time it is asked, on the UI thread: by Home's first
        /// element on the Dashboard, which comes before OnApplicationStarted, or else by OnApplicationStarted. Playnite
        /// has applied its theme by either, and changes it only on a restart.
        /// </summary>
        private bool IsOnDashboard()
        {
            if (!dashboardSettled)
            {
                dashboardSettled = true;
                var fullscreen = PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen;
                onDashboard = DashboardPresence.IsShowing(fullscreen, key => Application.Current?.TryFindResource(key));
                if (!onDashboard)
                {
                    logger.Info(fullscreen
                        ? "Xbox Series extension stays dormant: Fullscreen isn't showing the Dashboard theme"
                        : $"Xbox Series extension stays dormant in {PlayniteApi.ApplicationInfo.Mode} mode");
                }
            }

            return onDashboard;
        }

        /// <summary>Runs <paramref name="action"/> on the UI thread, which runs the core, guarded (<see cref="Guarded"/>).</summary>
        private void OnUiThread(Action action, [CallerMemberName] string caller = null)
        {
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(new Action(() => Guarded.Run($"{caller}'s work on the UI thread", action)));
        }

        private void PushNotifications()
        {
            core.OnNotificationsChanged(PlayniteApi.Notifications.Messages.Select(m => new Notification(m.Id, m.Text)).ToList());
        }

        /// <summary>
        /// Pushes the library once <see cref="LibraryPushDelay"/> after the first of a burst of database changes, from
        /// any thread: every change in between is in that one snapshot, and one after it has begun asks for another.
        /// </summary>
        private void PushLibrarySoon()
        {
            if (Interlocked.Exchange(ref libraryPushPending, 1) == 1)
            {
                return;
            }

            OnUiThread(() =>
            {
                var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LibraryPushDelay };
                timer.Tick += (_, __) =>
                {
                    timer.Stop();
                    Interlocked.Exchange(ref libraryPushPending, 0);
                    PushLibrary();
                };
                timer.Start();
            });
        }

        /// <summary>
        /// Each game whose launching or running state changed, to the core, which sees in it a launch Playnite gave up
        /// on without raising cancelled or stopped. From any thread.
        /// </summary>
        private void PushLaunchStates(ItemUpdatedEventArgs<Playnite.SDK.Models.Game> args)
        {
            foreach (var update in args.UpdatedItems)
            {
                var before = update.OldData;
                var after = update.NewData;
                if (before == null || after == null
                    || (before.IsLaunching == after.IsLaunching && before.IsRunning == after.IsRunning))
                {
                    continue;
                }

                var id = after.Id;
                bool wasLaunching = before.IsLaunching, isLaunching = after.IsLaunching, isRunning = after.IsRunning;
                OnUiThread(() => core.OnGameStateChanged(id, wasLaunching, isLaunching, isRunning));
            }
        }

        /// <summary>The library to the core; should Playnite's database fail to be read, the core keeps the one it had.</summary>
        private void PushLibrary()
        {
            libraryPushed = true;
            Guarded.Run("Pushing the library", () => core.OnLibraryChanged(PlayniteLibrary.Snapshot(PlayniteApi)));
        }
    }
}
