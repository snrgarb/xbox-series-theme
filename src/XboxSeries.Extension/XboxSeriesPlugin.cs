using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Reflection;
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
        private readonly Win32WindowSystem windows = new Win32WindowSystem();
        private readonly HomeScreen homeScreen = new HomeScreen();
        private readonly PlayniteSounds sounds;
        private readonly PlayniteHost host;
        private readonly GamingInputController controller = new GamingInputController();
        private readonly WindowsAudio audio = new WindowsAudio();
        private readonly DiscordPipe discordPipe = new DiscordPipe();
        private readonly DiscordOAuth discordOAuth = new DiscordOAuth();
        private bool libraryPushed;
        private ToastWindow toastWindow;
        private LaunchScreenWindow launchScreenWindow;
        private StartupWindow startupWindow;
#if DEBUG
        private DebugGuideHotkey debugGuideHotkey;
#endif

        // Set once, as PluginGuid in the csproj, which also builds the manifest Id from it.
        public override Guid Id { get; } = Guid.Parse(typeof(XboxSeriesPlugin).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "PluginGuid").Value);

        public XboxSeriesPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = false };
            sounds = new PlayniteSounds(api);
            host = new PlayniteHost(api, GetPluginUserDataPath());
            core = new ExtensionCore(new DispatcherClock(), windows, host, sounds, new WindowsDrives(), new WindowsUserAccount(GetPluginUserDataPath()), audio, controller,
                discordPipe, discordOAuth, new DpapiSecretStore(GetPluginUserDataPath()));

            // Home's elements, its top bar's status area, My games & apps and Settings, placed by the Dashboard as ContentControls named XboxSeries_<element>.
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = "XboxSeries",
                ElementList = new List<string> { "Hero", "RecentRow", "Groups", "TileMenu", "MyGames", "Settings", "Status" },
            });
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            // The first of Home's elements is the earliest the extension hears from Playnite on its UI thread with its
            // window made, seconds before OnApplicationStarted: the Start-up sequence covers the screen from here, so
            // Home is never seen before it.
            BeginStartup();

            // Playnite builds Home before it raises OnApplicationStarted, and can take seconds between the two. The
            // database is already loaded by then, so Home's elements start from it rather than showing empty until then.
            if (!libraryPushed && PlayniteApi.Database.IsOpen)
            {
                PushLibrary();
            }

            switch (args.Name)
            {
                case "Hero": return new HeroControl(core.Home);
                case "RecentRow": return new RecentRowControl(core, homeScreen);
                case "Groups": return new GroupsControl(core, homeScreen);
                case "TileMenu": return new TileMenuControl(core);
                case "MyGames": return new MyGamesControl(core);
                case "Settings": return new SettingsControl(core);
                case "Status": return new StatusControl(core);
                default: return null;
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Created here, on the UI thread, which owns the Overlay and runs the core. The Toast's may be already, for
            // the Start-up sequence's greeting.
            var overlay = new OverlayWindow(core);
            windows.Overlay = overlay;

            // Made and laid out once the UI thread is idle, so the Guide's first open is as quick as the rest.
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(overlay.Prewarm));
            toastWindow = toastWindow ?? new ToastWindow(core.Toast);

            // Beneath the Toast's and the Overlay, which it keeps above it as it shows, so Toasts and the Guide show over it.
            launchScreenWindow = launchScreenWindow ?? new LaunchScreenWindow(core.LaunchScreen, toastWindow, overlay);
#if DEBUG
            debugGuideHotkey = new DebugGuideHotkey(core);
#endif
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                host.RestoreLeftUserFilter();

                // Begun with Home's first element, unless the theme has none of them.
                BeginStartup();
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
            }
            else
            {
                // In Fullscreen the Start-up sequence plays it as a profile signs in.
                sounds.Play(UiSound.Login);
            }

            PushLibrary();
            GameHubFocusSounds.Register(sounds);

            // Database events can arrive on background threads; the core only runs on the UI thread.
            PlayniteApi.Database.Games.ItemCollectionChanged += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.Games.ItemUpdated += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.FilterPresets.ItemCollectionChanged += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.FilterPresets.ItemUpdated += (_, __) => OnUiThread(PushLibrary);
            PushNotifications();
            controller.Changed += () => OnUiThread(core.OnControllersChanged);
            audio.DevicesChanged += () => OnUiThread(core.OnAudioDevicesChanged);
            PlayniteApi.Notifications.Messages.CollectionChanged += (_, __) => OnUiThread(PushNotifications);

            // Discord's frames and token answers arrive on background threads. Subscribed before the core signs in with
            // the tokens saved from before, so no early frame is lost.
            discordPipe.FrameReceived += (opcode, json) => OnUiThread(() => core.OnDiscordFrame(opcode, json));
            discordPipe.Closed += () => OnUiThread(core.OnDiscordClosed);
            discordOAuth.TokenResponded += (status, body) => OnUiThread(() => core.OnDiscordTokenResponse(status, body));
            core.OnApplicationStarted();
            logger.Info($"Xbox Series extension started in {PlayniteApi.ApplicationInfo.Mode} mode");
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
            startupWindow?.Shutdown();
            discordPipe.Close();
            audio.Dispose();

            // The user's own filter and sort go back. Playnite has already saved its settings, so they are saved again.
            PlayniteApi.MainView.UIDispatcher.Invoke(core.OnApplicationStopped);

            // Playnite releases its audio engine only after this returns, so the sound gets to finish.
            sounds.PlayToEnd(UiSound.PowerOff);
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            var id = args.Game.Id;
            OnUiThread(() => core.OnGameStarted(id));
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
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
            var id = args.Game.Id;
            OnUiThread(() => core.OnGameStartupCancelled(id));
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            var id = args.Game.Id;
            OnUiThread(() => core.OnGameStopped(id));
        }

        public override void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args)
        {
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
            }

            OnUiThread(() => core.OnControllerButton(button.Value, state, at));
        }

        /// <summary>
        /// The Start-up sequence, once, in Fullscreen only: its window, and the Toast's for its greeting, made before the
        /// core begins it, so they show its first phase.
        /// </summary>
        private void BeginStartup()
        {
            if (startupWindow != null || PlayniteApi.ApplicationInfo.Mode != ApplicationMode.Fullscreen)
            {
                return;
            }

            startupWindow = new StartupWindow(core, sounds.SilenceThemeSounds);
            toastWindow = toastWindow ?? new ToastWindow(core.Toast);
            core.OnStartup();
            startupWindow.Follow();
        }

        /// <summary>Windows woke from sleep: Who's playing today? again. Raised on a thread of its own.</summary>
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                OnUiThread(core.OnResumedFromSleep);
            }
        }

        private void OnUiThread(Action action)
        {
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(action);
        }

        private void PushNotifications()
        {
            core.OnNotificationsChanged(PlayniteApi.Notifications.Messages.Select(m => new Notification(m.Id, m.Text)).ToList());
        }

        private void PushLibrary()
        {
            libraryPushed = true;
            core.OnLibraryChanged(PlayniteLibrary.Snapshot(PlayniteApi));
        }
    }
}
