using System;
using System.Collections.Generic;

namespace XboxSeries.Extension
{
    /// <summary>
    /// When the achievement Toast's pages change, as times from the Toast showing (see <see cref="AchievementPages.Plan"/>):
    /// the pushes up from each page to the next, the header's first, and the cut from the last to the prompt.
    /// </summary>
    internal sealed class PageTimes
    {
        /// <summary>When each page but the last starts to leave, pushed up for the next: the header's first.</summary>
        public double[] Pushes;

        /// <summary>When the last page is cut for the prompt.</summary>
        public double Cut;

        /// <summary>When the prompt starts to fade in, and when it is in.</summary>
        public double PromptAt, PromptEnd;
    }

    /// <summary>
    /// The achievement Toast's pages, apart from WPF so they can be checked on their own: its description split into pages
    /// of two lines, so it is seen whole rather than cut short, and when each page comes and goes. With one page of
    /// description, or none, the times are the console's own, from a capture of its rare achievement Toast; with more,
    /// every page is held for less, in proportion, so the prompt is still in before the core hides the Toast.
    /// </summary>
    internal static class AchievementPages
    {
        /// <summary>How long the core shows an achievement's Toast before it goes.</summary>
        public const double ShownFor = 8.5;

        /// <summary>When the header's page is in, faded in as the pill unfurled.</summary>
        public const double TextInEnd = 2.386;

        /// <summary>
        /// The console's push from the header to the description: the header leaving up over <see cref="PushOut"/>, and
        /// the description coming up from below at <see cref="PushInAt"/> over <see cref="PushIn"/>.
        /// </summary>
        public const double PushAt = 4.122, PushOut = 0.18, PushInAt = 4.30, PushIn = 0.38;

        /// <summary>The console's cut from the description to the prompt, and the prompt's fade in.</summary>
        public const double CutAt = 7.091, PromptAt = 7.158, PromptEnd = 7.341;

        /// <summary>The lines on a page of description, as many as the pill's 128 holds at 45 apart.</summary>
        public const int LinesPerPage = 2;

        /// <summary>A push, from the old page starting to leave to the new one in its place.</summary>
        private const double PushSpan = PushInAt + PushIn - PushAt;

        /// <summary>How long the console holds the header, a page of description, and the prompt, still.</summary>
        private const double HeaderHold = PushAt - TextInEnd, PageHold = CutAt - PushInAt - PushIn, PromptHold = ShownFor - PromptEnd;

        /// <summary>
        /// <paramref name="text"/> in pages of at most <see cref="LinesPerPage"/> lines, as <paramref name="linesOf"/>
        /// wraps a page's text: each page as many of its words as fit, in order, so nothing is cut or lost. A word too
        /// long for a page alone is a page of its own, wrapped within the word. None for no text.
        /// </summary>
        public static List<string> Split(string text, Func<string, int> linesOf)
        {
            var pages = new List<string>();
            var words = (text ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var start = 0;
            while (start < words.Length)
            {
                var end = start + 1;
                while (end < words.Length && linesOf(string.Join(" ", words, start, end - start + 1)) <= LinesPerPage)
                {
                    end++;
                }

                pages.Add(string.Join(" ", words, start, end - start));
                start = end;
            }

            return pages;
        }

        /// <summary>
        /// When the pages change, for <paramref name="descriptionPages"/> pages of description after the header. With
        /// none, the header holds until the prompt; with one, the console's own timings; with more, each push takes the
        /// console's time but the header, every page of description and the prompt are held for less, all in the same
        /// proportion, so the prompt is in with time to read it before the Toast goes at <see cref="ShownFor"/>.
        /// </summary>
        public static PageTimes Plan(int descriptionPages)
        {
            if (descriptionPages <= 0)
            {
                return new PageTimes { Pushes = new double[0], Cut = CutAt, PromptAt = PromptAt, PromptEnd = PromptEnd };
            }

            var holds = HeaderHold + descriptionPages * PageHold + PromptHold;
            var room = ShownFor - TextInEnd - descriptionPages * PushSpan - (PromptEnd - CutAt);
            var scale = Math.Min(1, room / holds);
            var pushes = new double[descriptionPages];
            var at = TextInEnd + scale * HeaderHold;
            for (var page = 0; page < descriptionPages; page++)
            {
                pushes[page] = at;
                at += PushSpan + scale * PageHold;
            }

            return new PageTimes { Pushes = pushes, Cut = at, PromptAt = at + (PromptAt - CutAt), PromptEnd = at + (PromptEnd - CutAt) };
        }
    }
}
