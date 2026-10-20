using System;
using System.Collections.Generic;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The press that wakes the Idle screen, swallowed whole, apart from WPF so it can be checked on its own. The core
    /// says whether a press only woke the screen; this keeps the rest of that press from acting: its repeats and its
    /// release, as a held key or Playnite's resent D-pad, and its twin, the same pad press Playnite also sends as a key
    /// (the D-pad as an arrow key, A as Enter), which can come just before or just after it. An input is anything that
    /// tells a press apart, as "a key" or "a pad button".
    /// </summary>
    internal sealed class WakeSwallow<TInput>
    {
        /// <summary>How long after the wake its twin may still come: Playnite sends both within a frame or two.</summary>
        public static readonly TimeSpan TwinWindow = TimeSpan.FromMilliseconds(250);

        /// <summary>The wake's inputs held down, swallowed until each is released.</summary>
        private readonly HashSet<TInput> held = new HashSet<TInput>();

        /// <summary>The wake's twins yet to come, and until when they may.</summary>
        private readonly Dictionary<TInput, DateTime> twins = new Dictionary<TInput, DateTime>();

        /// <summary>Whether any of the wake's inputs is still held down that <paramref name="matches"/>.</summary>
        public bool Holds(Func<TInput, bool> matches)
        {
            foreach (var input in held)
            {
                if (matches(input))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A press of <paramref name="input"/>, or its repeat: true to swallow it. A repeat of the wake's press, or its
        /// twin, is swallowed without asking; any other asks <paramref name="wake"/>, which tells the core of the input,
        /// and is swallowed, its <paramref name="twinsOf"/> with it, when that only woke the screen. A press that acts
        /// ends the wake's, so a key whose release was lost is swallowed no longer.
        /// </summary>
        public bool Press(TInput input, DateTime now, Func<bool> wake, IEnumerable<TInput> twinsOf)
        {
            if (held.Contains(input))
            {
                return true;
            }

            if (twins.TryGetValue(input, out var until))
            {
                twins.Remove(input);
                if (now <= until)
                {
                    held.Add(input);
                    return true;
                }
            }

            var woke = wake();
            held.Clear();
            twins.Clear();
            if (woke)
            {
                held.Add(input);
                foreach (var twin in twinsOf ?? Array.Empty<TInput>())
                {
                    if (!held.Contains(twin))
                    {
                        twins[twin] = now + TwinWindow;
                    }
                }
            }

            return woke;
        }

        /// <summary>A release of <paramref name="input"/>: true to swallow it, as one of the wake's presses let go.</summary>
        public bool Release(TInput input) => held.Remove(input);
    }
}
