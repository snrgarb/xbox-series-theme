using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Idle screen's cards: at Showing things a pool of the nearly-finished games, the last unlocks, the last games
    /// played and the tips, shown one at a time in shuffled turn, each for 10 s, reshuffled when it runs out; waking hides
    /// the card.
    /// </summary>
    public class IdleCardsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 20, 20, 0, 0);

        private readonly FakeClock clock = new FakeClock(Start);
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeAchievements achievements = new FakeAchievements();
        private readonly FakeLibrary library = new FakeLibrary();
        private ExtensionCore core;

        public IdleCardsTests()
        {
            core = Core(new Random(1));
        }

        private ExtensionCore Core(Random random) =>
            new ExtensionCore(
                clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(),
                new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads(),
                achievements: achievements, random: random);

        /// <summary>No input until the Idle screen shows things, at its default 5 min.</summary>
        private void ShowThings()
        {
            clock.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        private void NextCard() => clock.Advance(TimeSpan.FromSeconds(10));

        /// <summary>The cards shown from now, the one showing first, <paramref name="count"/> of them.</summary>
        private List<IdleCard> Cards(int count)
        {
            var cards = new List<IdleCard>();
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    NextCard();
                }

                cards.Add(core.Idle.Card);
            }

            return cards;
        }

        [Fact]
        public void No_card_shows_while_awake_or_only_dimmed()
        {
            Assert.Null(core.Idle.Card);

            clock.Advance(TimeSpan.FromMinutes(4.99));

            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            Assert.Null(core.Idle.Card);
        }

        [Fact]
        public void A_card_shows_the_moment_Showing_things_starts()
        {
            ShowThings();

            Assert.NotNull(core.Idle.Card);
        }

        [Fact]
        public void Each_card_shows_for_10_seconds_then_the_next()
        {
            ShowThings();
            var first = core.Idle.Card;

            clock.Advance(TimeSpan.FromSeconds(9.99));
            Assert.Same(first, core.Idle.Card);

            clock.Advance(TimeSpan.FromSeconds(0.01));
            Assert.NotSame(first, core.Idle.Card);
            Assert.NotNull(core.Idle.Card);
        }

        [Fact]
        public void Without_achievements_or_games_the_twelve_tips_cycle_text_only()
        {
            ShowThings();

            var cards = Cards(12);

            Assert.All(cards, c => Assert.Equal(IdleCardKind.Tip, c.Kind));
            Assert.All(cards, c => Assert.Null(c.Detail));
            Assert.All(cards, c => Assert.Null(c.Art));
            Assert.All(cards, c => Assert.Null(c.Icon));
            Assert.Equal(12, cards.Select(c => c.Title).Distinct().Count());
            Assert.Contains("Press Y on Home to search your games and apps.", cards.Select(c => c.Title));
            Assert.Contains("Pick a boot video, original Xbox to Series X, in Personalisation › Start up.", cards.Select(c => c.Title));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Every_card_shows_once_a_round_then_the_pool_is_reshuffled_without_repeating_the_last_first(int seed)
        {
            core = Core(new Random(seed));
            ShowThings();

            var cards = Cards(36);

            var rounds = Enumerable.Range(0, 3).Select(r => cards.Skip(r * 12).Take(12).Select(c => c.Title).ToList()).ToList();
            Assert.All(rounds, round => Assert.Equal(12, round.Distinct().Count()));
            Assert.All(rounds, round => Assert.Equal(rounds[0].OrderBy(t => t), round.OrderBy(t => t)));
            Assert.NotEqual(rounds[0].Last(), rounds[1].First());
            Assert.NotEqual(rounds[1].Last(), rounds[2].First());
        }

        [Fact]
        public void The_order_is_shuffled_by_the_cores_Random()
        {
            var orders = new[] { 1, 2, 3 }.Select(seed =>
            {
                core = Core(new Random(seed));
                ShowThings();
                var order = string.Join("|", Cards(12).Select(c => c.Title));
                core.OnUserInput();
                return order;
            }).ToList();

            Assert.True(orders.Distinct().Count() > 1);
        }

        [Fact]
        public void Waking_hides_the_card_and_stops_the_turn()
        {
            ShowThings();

            core.OnUserInput();

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Null(core.Idle.Card);
            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.Null(core.Idle.Card);
        }

        [Fact]
        public void A_pause_hides_the_card()
        {
            ShowThings();

            core.OnGameStarted(Guid.NewGuid());

            Assert.Null(core.Idle.Card);
        }

        [Fact]
        public void Showing_things_again_after_waking_shows_a_card_again()
        {
            ShowThings();
            core.OnUserInput();

            ShowThings();

            Assert.NotNull(core.Idle.Card);
        }
    }
}
