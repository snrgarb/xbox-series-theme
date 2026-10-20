using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// A text naming pad buttons in braces, as an Idle screen tip's "Press {Guide} anywhere", split into its words and
    /// its buttons, for the view to draw each button as its icon (<see cref="ButtonGlyphs"/>). A token is a
    /// <see cref="ControllerButton"/>'s exact name; anything else in braces, "{Foo}" or "{1}", stays as it is, words.
    /// Plain C#, with no WPF, so it is checked on its own.
    /// </summary>
    internal static class ButtonTokens
    {
        private static readonly Regex Token = new Regex(@"\{([A-Za-z]+)\}");

        /// <summary>A run of words, or one button; <see cref="Button"/> is null for words.</summary>
        internal struct Part
        {
            internal Part(string text, ControllerButton? button)
            {
                Text = text;
                Button = button;
            }

            internal string Text { get; }

            internal ControllerButton? Button { get; }
        }

        /// <summary>The parts of <paramref name="text"/> in order, words run together; none for null or empty.</summary>
        internal static IReadOnlyList<Part> Parse(string text)
        {
            var parts = new List<Part>();
            if (string.IsNullOrEmpty(text))
            {
                return parts;
            }

            var words = "";
            var at = 0;
            foreach (Match match in Token.Matches(text))
            {
                words += text.Substring(at, match.Index - at);
                at = match.Index + match.Length;
                var name = match.Groups[1].Value;
                if (Enum.IsDefined(typeof(ControllerButton), name))
                {
                    if (words.Length > 0)
                    {
                        parts.Add(new Part(words, null));
                        words = "";
                    }

                    parts.Add(new Part(null, (ControllerButton)Enum.Parse(typeof(ControllerButton), name)));
                }
                else
                {
                    words += match.Value;
                }
            }

            words += text.Substring(at);
            if (words.Length > 0)
            {
                parts.Add(new Part(words, null));
            }

            return parts;
        }
    }
}
