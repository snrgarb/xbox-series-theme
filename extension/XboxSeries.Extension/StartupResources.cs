using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The theme's resources the Start-up window uses, copied on the UI thread for the Start-up thread. Playnite's
    /// application resources belong to the UI thread, and its theme's brushes take their colours from other resources,
    /// so they can't be frozen and throw when the Start-up thread draws with them, as do the implicit styles of the
    /// theme's controls (its ScrollBar's, inside the profiles' ScrollViewer). The window never looks there; it has these
    /// copies instead, frozen, or sealed for a style, which every thread may use.
    /// </summary>
    internal static class StartupResources
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>Every theme resource the window, its swirl and its spinner ask for.</summary>
        private static readonly string[] Keys =
        {
            "GlyphLightBrush",
            "XboxGreenBrush",
            "XboxFont",
            "XboxHeaderFont",
            "XboxIconFont",
            "XboxFocusGlowRound",
            "XboxFocusGlowCache",
        };

        /// <summary>The copies, made on the UI thread; a resource the theme lacks, or that can't be copied, is left out.</summary>
        public static Dictionary<string, object> Copy()
        {
            var copies = new Dictionary<string, object>();
            foreach (var key in Keys)
            {
                var copy = CopyOf(Application.Current?.TryFindResource(key));
                if (copy != null)
                {
                    copies[key] = copy;
                }
                else
                {
                    logger.Warn($"Start-up goes without the theme's {key}");
                }
            }

            return copies;
        }

        private static object CopyOf(object value)
        {
            switch (value)
            {
                case Freezable freezable:
                    var frozen = freezable.CloneCurrentValue();
                    return frozen.CanFreeze ? Frozen(frozen) : null;
                case Style style:
                    return CopyOf(style);
                case DispatcherObject _:
                    return null;
                default:
                    // A font family, a number: nothing a thread owns.
                    return value;
            }
        }

        /// <summary>
        /// A style's setters, each value frozen, in a style sealed so any thread may use it; a template, as the focus
        /// glow's, is sealed with it, which frees it to any thread too. A resource a setter asks for is looked up where
        /// the style is used, among the window's copies.
        /// </summary>
        private static Style CopyOf(Style style)
        {
            if (style.BasedOn != null || style.Triggers.Count > 0 || style.Resources.Count > 0)
            {
                return null;
            }

            var copy = new Style(style.TargetType);
            foreach (var setterBase in style.Setters)
            {
                if (!(setterBase is Setter setter))
                {
                    return null;
                }

                var value = setter.Value is Freezable freezable ? freezable.CloneCurrentValue() : setter.Value;
                if (value is Freezable unfrozen && !unfrozen.CanFreeze)
                {
                    return null;
                }

                copy.Setters.Add(new Setter(setter.Property, value is Freezable f ? Frozen(f) : value, setter.TargetName));
            }

            copy.Seal();
            return copy;
        }

        private static Freezable Frozen(Freezable freezable)
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
