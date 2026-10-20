using System.Linq;
using XboxSeries.Extension;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The achievement Toast's description, seen whole: split into pages of two lines and paged through within the 8.5 s
    // the core shows it, the header's and the prompt's pages around them. The paging is plain C# (AchievementPages.cs,
    // compiled in here); how the pill wraps a page's text is stood in for by a fake of so many words to a line.
    public class AchievementPagesTests
    {
        // Six words to a line.
        private static int Lines(string text) => (text.Split(' ').Length + 5) / 6;

        private static string Words(int count) => string.Join(" ", Enumerable.Range(1, count).Select(i => "w" + i));

        [Fact]
        public void A_description_is_split_two_lines_to_a_page_in_order_with_nothing_cut_or_lost()
        {
            var pages = AchievementPages.Split(Words(30), Lines);

            Assert.Equal(new[] { 12, 12, 6 }, pages.Select(p => p.Split(' ').Length));
            Assert.All(pages, p => Assert.True(Lines(p) <= 2));
            Assert.Equal(Words(30), string.Join(" ", pages));
            Assert.DoesNotContain(pages, p => p.Contains("…") || p.EndsWith("..."));
        }

        [Fact]
        public void A_short_description_is_one_page_and_its_spacing_is_tidied()
        {
            Assert.Equal(new[] { "Win a match." }, AchievementPages.Split("  Win a\r\nmatch. ", Lines));
        }

        [Fact]
        public void A_word_too_long_for_a_page_is_a_page_of_its_own_rather_than_stopping_the_paging()
        {
            int Long(string text) => text.Contains("Supercalifragilistic") ? 3 : 1;

            Assert.Equal(new[] { "a b", "Supercalifragilistic", "c" }, AchievementPages.Split("a b Supercalifragilistic c", Long));
        }

        [Fact]
        public void No_description_is_no_pages()
        {
            Assert.Empty(AchievementPages.Split(null, Lines));
            Assert.Empty(AchievementPages.Split("   ", Lines));
        }

        [Fact]
        public void One_page_of_description_keeps_the_consoles_own_timings()
        {
            var times = AchievementPages.Plan(1);

            Assert.Equal(4.122, Assert.Single(times.Pushes), 3);
            Assert.Equal(7.091, times.Cut, 3);
            Assert.Equal(7.158, times.PromptAt, 3);
            Assert.Equal(7.341, times.PromptEnd, 3);
        }

        [Fact]
        public void Without_a_description_the_header_holds_until_the_prompt_as_it_did()
        {
            var times = AchievementPages.Plan(0);

            Assert.Empty(times.Pushes);
            Assert.Equal((7.091, 7.158, 7.341), (times.Cut, times.PromptAt, times.PromptEnd));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(6)]
        public void More_pages_are_each_held_for_less_in_proportion_the_prompt_still_in_before_the_Toast_goes(int pages)
        {
            var times = AchievementPages.Plan(pages);
            const double push = AchievementPages.PushInAt + AchievementPages.PushIn - AchievementPages.PushAt;

            // Every page of description held alike, each push at the console's pace.
            var steps = times.Pushes.Zip(times.Pushes.Skip(1).Concat(new[] { times.Cut }), (from, to) => to - from).ToArray();
            Assert.Equal(pages, steps.Length);
            Assert.All(steps, s => Assert.Equal(steps[0], s, 6));
            var pageHold = steps[0] - push;
            Assert.True(pageHold > 0);

            // The header, the pages and the prompt all shortened by the same proportion of the console's holds.
            var headerHold = times.Pushes[0] - AchievementPages.TextInEnd;
            var promptHold = AchievementPages.ShownFor - times.PromptEnd;
            var scale = pageHold / (7.091 - 4.68);
            Assert.True(scale < 1);
            Assert.Equal(4.122 - 2.386, headerHold / scale, 3);
            Assert.Equal(8.5 - 7.341, promptHold / scale, 3);
            Assert.Equal(7.341 - 7.091, times.PromptEnd - times.Cut, 3);
        }

        [Fact]
        public void Three_pages_a_five_line_description_are_still_each_held_about_a_second()
        {
            var times = AchievementPages.Plan(3);

            Assert.True(times.Pushes[1] - times.Pushes[0] - 0.558 > 0.95);
        }
    }
}
