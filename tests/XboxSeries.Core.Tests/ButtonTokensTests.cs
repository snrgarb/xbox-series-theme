using System.Linq;
using XboxSeries.Extension;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // A tip's text naming pad buttons in braces, "{Y}", split into its words and its buttons for the card to draw each
    // button as its icon; plain C# (ButtonTokens.cs, compiled in here).
    public class ButtonTokensTests
    {
        private static string Show(string text) =>
            string.Join("|", ButtonTokens.Parse(text).Select(p => p.Button.HasValue ? "<" + p.Button.Value + ">" : p.Text));

        [Fact]
        public void Each_button_named_in_braces_is_a_button_between_the_words_around_it()
        {
            Assert.Equal("Press |<Guide>| anywhere, even in a game, to open the Guide.",
                Show("Press {Guide} anywhere, even in a game, to open the Guide."));
            Assert.Equal("On the keyboard, |<X>| is backspace, |<Y>| is space, |<Start>| is Enter.",
                Show("On the keyboard, {X} is backspace, {Y} is space, {Start} is Enter."));
        }

        [Fact]
        public void A_button_at_either_end_or_next_to_another_has_no_empty_words_around_it()
        {
            Assert.Equal("<LeftShoulder>|<RightShoulder>| to cycle and |<Y>", Show("{LeftShoulder}{RightShoulder} to cycle and {Y}"));
            Assert.Equal("<DPadUp>", Show("{DPadUp}"));
        }

        [Fact]
        public void Anything_else_in_braces_stays_as_words()
        {
            Assert.Equal("Press {Nope}, {y}, {1}, { Y } or {} and |<A>|.", Show("Press {Nope}, {y}, {1}, { Y } or {} and {A}."));
            Assert.Equal("Plain words, no buttons.", Show("Plain words, no buttons."));
        }

        [Fact]
        public void No_text_has_no_parts()
        {
            Assert.Empty(ButtonTokens.Parse(null));
            Assert.Empty(ButtonTokens.Parse(""));
        }
    }
}
