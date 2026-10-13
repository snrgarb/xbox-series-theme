using System;
using System.Linq;
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

        // Set once, as PluginGuid in the csproj, which also builds the manifest Id from it.
        public override Guid Id { get; } = Guid.Parse(typeof(XboxSeriesPlugin).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "PluginGuid").Value);

        public XboxSeriesPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = false };
            core = new ExtensionCore(new DispatcherClock());
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            PushLibrary();

            // Database events can arrive on background threads; the core only runs on the UI thread.
            PlayniteApi.Database.Games.ItemCollectionChanged += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.Games.ItemUpdated += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.FilterPresets.ItemCollectionChanged += (_, __) => OnUiThread(PushLibrary);
            PlayniteApi.Database.FilterPresets.ItemUpdated += (_, __) => OnUiThread(PushLibrary);
            logger.Info($"Xbox Series extension started in {PlayniteApi.ApplicationInfo.Mode} mode");
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
            core.OnLibraryChanged(PlayniteLibrary.Snapshot(PlayniteApi.Database));
        }
    }
}
