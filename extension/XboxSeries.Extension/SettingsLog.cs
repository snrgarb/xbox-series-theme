using System.Windows;
using System.Windows.Input;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Settings in extensions.log, in lines beginning "[Settings]": the page opening, with the category and page it opens
    /// on, each page of Theme settings it then shows, and closing; the rail item or box that gets focus, by its name; and
    /// Background delay as it is at the start and whenever it is turned over. A line each time, so they stay; the
    /// end-to-end test background-follows-selection turns Background delay off with the pad and reads them to know
    /// where it is before each press.
    /// </summary>
    internal static class SettingsLog
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static void Follow(ExtensionCore core)
        {
            var settings = core.Settings;
            LogBackgroundDelay(settings.StartUp, "is");
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
            settings.StartUp.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(StartUpSettingsState.BackgroundDelay))
                {
                    LogBackgroundDelay(settings.StartUp, "turned");
                }
            };
        }

        /// <summary>
        /// Says which of <paramref name="page"/>'s named rail items and boxes gets focus, as "[Settings] focus on
        /// BackgroundDelayBox". A choice in a list, made from a template, has no name and no line.
        /// </summary>
        public static void FollowFocus(UIElement page)
        {
            page.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((s, e) =>
            {
                if (e.NewFocus is FrameworkElement focused && !string.IsNullOrEmpty(focused.Name))
                {
                    logger.Info($"[Settings] focus on {focused.Name}");
                }
            }), handledEventsToo: true);
        }

        private static void LogBackgroundDelay(StartUpSettingsState state, string verb) =>
            logger.Info($"[Settings] Background delay {verb} {(state.BackgroundDelay ? "on" : "off")}");
    }
}
