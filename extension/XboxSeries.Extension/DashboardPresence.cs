using System;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Whether Playnite is showing the Dashboard, the only place the extension runs: Fullscreen mode, with the Dashboard
    /// theme's resources loaded. Desktop mode, or Fullscreen on another theme (Default, or the Dashboard falling back to
    /// Default as Playnite refused it), leaves the extension dormant: no Start-up, Guide, Toasts, Launch screen or Idle
    /// screen over Playnite's own windows, and every launch left to Playnite. Outside Fullscreen the Guide button is the
    /// Boot shell's, which brings Fullscreen back.
    /// </summary>
    internal static class DashboardPresence
    {
        /// <summary>
        /// A resource only the Dashboard defines (Constants.xaml), which Playnite merges into its application resources
        /// with the rest of the theme. The extension adds none of its own there, so finding it means the Dashboard loaded.
        /// </summary>
        public const string ResourceKey = "XboxFont";

        /// <param name="fullscreen">Whether Playnite runs in Fullscreen mode.</param>
        /// <param name="findResource">Looks a key up in Playnite's application resources; null when none has it.</param>
        public static bool IsShowing(bool fullscreen, Func<string, object> findResource) =>
            fullscreen && findResource(ResourceKey) != null;
    }
}
