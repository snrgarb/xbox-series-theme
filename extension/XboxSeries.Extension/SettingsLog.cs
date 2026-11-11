using System.Windows;
using System.Windows.Input;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Settings in extensions.log, in lines beginning "[Settings]": the page opening, with the category and page it
    /// opens on, each page of Theme settings it then shows, and closing; the rail item or box that gets focus, by its
    /// name; and Background delay, Reduce motion, Show Play history, Xbox store art and the Guide as they are at the start
    /// and whenever one is turned over. A line each time, so they stay; the end-to-end tests background-follows-selection,
    /// reduce-motion, guide-off and play-history-off turn theirs over with the pad and read them to know where Settings
    /// is before each press.
    /// </summary>
    internal static class SettingsLog
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static void Follow(ExtensionCore core)
        {
            var settings = core.Settings;
            LogBackgroundDelay(settings.StartUp, "is");
            LogReduceMotion(settings.StartUp, "is");
            LogPlayHistory(settings.StartUp, "is");
            LogXboxArt(settings.StartUp, "is");
            LogGuide(settings.Guide, "is");
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
            settings.Guide.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GuideSettingsState.IsGuideOn))
                {
                    LogGuide(settings.Guide, "turned");
                }
            };
            settings.StartUp.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(StartUpSettingsState.BackgroundDelay))
                {
                    LogBackgroundDelay(settings.StartUp, "turned");
                }
                else if (e.PropertyName == nameof(StartUpSettingsState.ReduceMotion))
                {
                    LogReduceMotion(settings.StartUp, "turned");
                }
                else if (e.PropertyName == nameof(StartUpSettingsState.ShowPlayHistory))
                {
                    LogPlayHistory(settings.StartUp, "turned");
                }
                else if (e.PropertyName == nameof(StartUpSettingsState.XboxArt))
                {
                    LogXboxArt(settings.StartUp, "turned");
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

        private static void LogGuide(GuideSettingsState state, string verb) =>
            logger.Info($"[Settings] Guide {verb} {(state.IsGuideOn ? "on" : "off")}");

        private static void LogPlayHistory(StartUpSettingsState state, string verb) =>
            logger.Info($"[Settings] Play history {verb} {(state.ShowPlayHistory ? "on" : "off")}");

        private static void LogXboxArt(StartUpSettingsState state, string verb) =>
            logger.Info($"[Settings] Xbox art {verb} {(state.XboxArt ? "on" : "off")}");

        private static void LogReduceMotion(StartUpSettingsState state, string verb) =>
            logger.Info($"[Settings] Reduce motion {verb} {(state.ReduceMotion ? "on" : "off")}");
    }
}
