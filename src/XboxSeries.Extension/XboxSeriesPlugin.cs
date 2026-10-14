using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Reflection;
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
        private bool libraryPushed;
#if DEBUG
        private DebugGuideHotkey debugGuideHotkey;
#endif

        // Set once, as PluginGuid in the csproj, which also builds the manifest Id from it.
        public override Guid Id { get; } = Guid.Parse(typeof(XboxSeriesPlugin).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "PluginGuid").Value);

        public XboxSeriesPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = false };
            core = new ExtensionCore(new DispatcherClock(), windows, new PlayniteHost(api));

            // Home's elements, placed by the Dashboard as ContentControls named XboxSeries_<element>.
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = "XboxSeries",
                ElementList = new List<string> { "Hero", "RecentRow" },
            });
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            // Playnite builds Home before it raises OnApplicationStarted, and can take seconds between the two. The
            // database is already loaded by then, so Home's elements start from it rather than showing empty until then.
            if (!libraryPushed && PlayniteApi.Database.IsOpen)
            {
                PushLibrary();
            }

            switch (args.Name)
            {
                case "Hero": return new HeroControl(core.Home);
                case "RecentRow": return new RecentRowControl(core);
                default: return null;
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Created here, on the UI thread, which owns the Overlay and runs the core.
            windows.Overlay = new OverlayWindow(core);
#if DEBUG
            debugGuideHotkey = new DebugGuideHotkey(core);
#endif
            PushLibrary();

            // Database events can arrive on background threads; the core only runs on the UI thread.
            PlayniteApi.Database.Games.ItemCollectionChanged += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.Games.ItemUpdated += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.FilterPresets.ItemCollectionChanged += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.FilterPresets.ItemUpdated += (_, __) => OnUiThread(PushLibrary);
            logger.Info($"Xbox Series extension started in {PlayniteApi.ApplicationInfo.Mode} mode");
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
#if DEBUG
            debugGuideHotkey?.Dispose();
#endif
            windows.Overlay?.Close();
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

            var state = PlayniteInput.ToState(args.State);
            OnUiThread(() => core.OnControllerButton(button.Value, state));
        }

        private void OnUiThread(Action action)
        {
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(action);
        }

        private void PushLibrary()
        {
            libraryPushed = true;
            core.OnLibraryChanged(PlayniteLibrary.Snapshot(PlayniteApi));
        }
    }
}
