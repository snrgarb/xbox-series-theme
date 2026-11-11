namespace XboxSeries.Core
{
    /// <summary>
    /// What the Dashboard's XAML reads of the extension, through Playnite's {PluginSettings Plugin=XboxSeries, Path=...}
    /// markup: the plugin hands Playnite this as its DashboardSettings, and each path starts from here. Renaming a
    /// property breaks the theme of the same version, which falls back as if there were no extension.
    /// </summary>
    public sealed class DashboardBindings
    {
        public DashboardBindings(StartUpSettingsState startUp, HubBackdropState hubBackdrop)
        {
            StartUp = startUp;
            HubBackdrop = hubBackdrop;
        }

        /// <summary>Personalisation's settings: the theme reads StartUp.ReduceMotion, which tells it when it changes.</summary>
        public StartUpSettingsState StartUp { get; }

        /// <summary>The Game hub's backdrop: HubBackdrop.Snapshot, shown while HubBackdrop.HasSnapshot.</summary>
        public HubBackdropState HubBackdrop { get; }
    }
}
