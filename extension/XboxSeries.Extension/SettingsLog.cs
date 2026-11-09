using System.ComponentModel;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Settings in extensions.log, in lines beginning "[Settings]": the page opening, with the category and page it opens
    /// on, its page changing, and closing. A line each time, so they stay; the end-to-end test see-all reads them to know
    /// that the Tile menu's Customise Home lands on Personalisation and stays there.
    /// </summary>
    internal static class SettingsLog
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static void Follow(ExtensionCore core)
        {
            var settings = core.Settings;
            settings.Opened += (s, e) => logger.Info($"[Settings] opened on {settings.Category}, page {settings.ThemePage}");
            settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsState.IsShown) && !settings.IsShown)
                {
                    logger.Info("[Settings] closed");
                }
                else if (e.PropertyName == nameof(SettingsState.ThemePage) && settings.IsShown)
                {
                    logger.Info($"[Settings] page {settings.ThemePage}");
                }
            };
        }
    }
}
