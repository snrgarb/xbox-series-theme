using System;
using System.ComponentModel;
using System.Windows;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Settings' Reduce motion, as the views ask after it: on, what slides, flies, grows or scales is where it ends at
    /// once, and what only fades still fades. A view with a branch that shows without animating takes it while
    /// <see cref="Reduced"/>; one without hands its duration to <see cref="Of(Duration)"/>, and its animation, no time
    /// long, still ends and raises Completed, so whatever it does as it ends is done. Left moving whatever it says: the
    /// boot video, the loading spinners, the Idle screen's fades, the Launch screen's fade and the Achievement toast.
    /// </summary>
    internal static class Motion
    {
        // Read from the Start-up sequence's thread too, whose window slides Who's playing today?'s row.
        private static volatile bool reduced;
        private static StartUpSettingsState followed;

        /// <summary>Whether Reduce motion is on. Read from any thread.</summary>
        public static bool Reduced => reduced;

        /// <summary>
        /// Reduce motion was turned over. Raised on Playnite's UI thread, where Settings sets it: a view on another
        /// thread, as the Start-up sequence's, goes to its own before touching itself.
        /// </summary>
        public static event Action Changed;

        /// <summary>
        /// Takes Reduce motion as <paramref name="settings"/> has it now, and follows it from here on. Called as the
        /// core is made, which has read what was saved, so before any view or the Start-up sequence shows.
        /// </summary>
        public static void Follow(StartUpSettingsState settings)
        {
            if (followed != null)
            {
                followed.PropertyChanged -= OnSettingChanged;
            }

            followed = settings;
            reduced = settings.ReduceMotion;
            settings.PropertyChanged += OnSettingChanged;
        }

        /// <summary><paramref name="duration"/>, or no time at all while Reduce motion is on.</summary>
        public static Duration Of(Duration duration) => reduced ? new Duration(TimeSpan.Zero) : duration;

        /// <summary><paramref name="time"/>, or no time at all while Reduce motion is on.</summary>
        public static TimeSpan Of(TimeSpan time) => reduced ? TimeSpan.Zero : time;

        private static void OnSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(StartUpSettingsState.ReduceMotion))
            {
                return;
            }

            var now = ((StartUpSettingsState)sender).ReduceMotion;
            if (now != reduced)
            {
                reduced = now;
                Changed?.Invoke();
            }
        }
    }
}
