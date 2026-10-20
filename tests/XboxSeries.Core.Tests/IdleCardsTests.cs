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

        /// <summary>Showing things starts, and the cards of its first round: each card once, in the order shown.</summary>
        private List<IdleCard> FirstRound()
        {
            ShowThings();
            var cards = new List<IdleCard> { core.Idle.Card };
            for (var i = 0; i < 100; i++)
            {
                NextCard();
                if (cards.Contains(core.Idle.Card))
                {
                    break;
                }

                cards.Add(core.Idle.Card);
            }

            return cards;
        }

        private List<IdleCard> FirstRound(IdleCardKind kind) => FirstRound().Where(c => c.Kind == kind).ToList();

        private void LibraryChanged() => core.OnLibraryChanged(library.Snapshot());

        /// <summary>
        /// A game in the library with <paramref name="total"/> achievements, the first <paramref name="unlocked"/> of them
        /// unlocked, recorded but not yet read: <see cref="AchievementsRead"/> tells the core.
        /// </summary>
        private Guid GameWithAchievements(string name, int unlocked, int total, bool hidden = false, bool inLibrary = true)
        {
            var id = Guid.NewGuid();
            if (inLibrary)
            {
                library.WithGame(id, name, hidden: hidden, backgroundImage: name + ".jpg");
            }

            achievements.Recorded.Add(new GameAchievements(
                id,
                Enumerable.Range(0, total)
                    .Select(i => new Achievement(name + i, name + " " + i, null, 50, i < unlocked ? Start.AddDays(-30 - i) : (DateTime?)null))
                    .ToList()));
            return id;
        }

        private void AchievementsRead()
        {
            LibraryChanged();
            core.OnAchievementsChanged();
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

        [Fact]
        public void Recently_played_holds_the_last_five_games_played_never_hidden_ones_with_their_art_and_name()
        {
            for (var i = 1; i <= 7; i++)
            {
                library.WithGame("Game " + i, lastActivity: Start.AddHours(-i), backgroundImage: "bg" + i + ".jpg");
            }

            library.WithGame("Hidden", lastActivity: Start, hidden: true).WithGame("Never played");
            LibraryChanged();

            var played = FirstRound(IdleCardKind.RecentlyPlayed);

            Assert.Equal(new[] { "Game 1", "Game 2", "Game 3", "Game 4", "Game 5" }, played.Select(c => c.Title).OrderBy(t => t));
            Assert.Equal("bg1.jpg", played.Single(c => c.Title == "Game 1").Art.Image);
            Assert.All(played, c => Assert.Null(c.Icon));
        }

        [Fact]
        public void A_recently_played_games_art_falls_back_to_its_cover()
        {
            library.WithGame("Celeste", lastActivity: Start, coverImage: "celeste.jpg");
            LibraryChanged();

            var card = FirstRound(IdleCardKind.RecentlyPlayed).Single();

            Assert.Equal("celeste.jpg", card.Art.Image);
            Assert.True(card.Art.IsCover);
        }

        [Theory]
        [InlineData(12 * 3600 + 1800, 1, "12 h played · yesterday")]
        [InlineData(3600, 0, "1 h played · today")]
        [InlineData(45 * 60 + 30, 0, "45 min played · today")]
        [InlineData(0, 1, "Yesterday")]
        [InlineData(30, 3, "3 days ago")]
        public void A_recently_played_cards_detail_is_its_playtime_and_when_it_was_last_played(ulong playtime, int daysAgo, string detail)
        {
            library.WithGame("Hades", lastActivity: Start.Date.AddDays(-daysAgo).AddHours(9), playtime: playtime);
            LibraryChanged();

            Assert.Equal(detail, FirstRound(IdleCardKind.RecentlyPlayed).Single().Detail);
        }

        [Theory]
        [InlineData("2026-10-20 23:59", "today")]
        [InlineData("2026-10-20 00:00", "today")]
        [InlineData("2026-10-19 23:59", "yesterday")]
        [InlineData("2026-10-18 12:00", "2 days ago")]
        [InlineData("2026-10-14 12:00", "6 days ago")]
        [InlineData("2026-10-13 12:00", "a week ago")]
        [InlineData("2026-10-06 12:00", "2 weeks ago")]
        [InlineData("2026-09-22 12:00", "4 weeks ago")]
        [InlineData("2026-09-20 12:00", "a month ago")]
        [InlineData("2026-08-21 12:00", "a month ago")]
        [InlineData("2026-08-20 12:00", "2 months ago")]
        [InlineData("2025-10-21 12:00", "11 months ago")]
        [InlineData("2025-10-20 12:00", "a year ago")]
        [InlineData("2023-01-01 12:00", "3 years ago")]
        public void When_is_worded_by_calendar_day_from_now(string at, string when)
        {
            library.WithGame("Hades", lastActivity: DateTime.Parse(at, System.Globalization.CultureInfo.InvariantCulture));
            LibraryChanged();

            var detail = FirstRound(IdleCardKind.RecentlyPlayed).Single().Detail;

            Assert.Equal(char.ToUpperInvariant(when[0]) + when.Substring(1), detail);
        }

        [Fact]
        public void Close_achievements_holds_the_five_games_with_the_most_unlocked_without_all_of_them()
        {
            GameWithAchievements("Nine", 9, 50);
            GameWithAchievements("Three", 3, 4);
            GameWithAchievements("Twenty", 20, 21);
            GameWithAchievements("Eight", 8, 9);
            GameWithAchievements("Two", 2, 3);
            GameWithAchievements("Seven", 7, 100);
            GameWithAchievements("Four", 4, 40);
            AchievementsRead();

            var close = FirstRound(IdleCardKind.CloseAchievements);

            Assert.Equal(new[] { "Eight", "Four", "Nine", "Seven", "Twenty" }, close.Select(c => c.Title).OrderBy(t => t));
        }

        [Fact]
        public void Among_games_with_as_many_unlocked_the_one_nearer_finished_goes_first_then_by_name()
        {
            for (var i = 0; i < 4; i++)
            {
                GameWithAchievements("Ten " + i, 10, 20);
            }

            GameWithAchievements("Five of fifty", 5, 50);
            GameWithAchievements("Zeta five of six", 5, 6);
            GameWithAchievements("Beta five of ten", 5, 10);
            GameWithAchievements("Alpha five of ten", 5, 10);
            AchievementsRead();

            var titles = FirstRound(IdleCardKind.CloseAchievements).Select(c => c.Title).ToList();

            Assert.Contains("Zeta five of six", titles);
            Assert.DoesNotContain("Five of fifty", titles);
            Assert.DoesNotContain("Alpha five of ten", titles);

            achievements.Recorded.RemoveAll(g => library.Snapshot().Games.Single(x => x.Id == g.GameId).Name == "Zeta five of six");
            core.OnUserInput();
            AchievementsRead();
            titles = FirstRound(IdleCardKind.CloseAchievements).Select(c => c.Title).ToList();

            Assert.Contains("Alpha five of ten", titles);
            Assert.DoesNotContain("Beta five of ten", titles);
        }

        [Fact]
        public void Fully_finished_untouched_hidden_and_unknown_games_are_left_out_of_close_achievements()
        {
            GameWithAchievements("Finished", 30, 30);
            GameWithAchievements("Untouched", 0, 30);
            GameWithAchievements("Hidden", 20, 30, hidden: true);
            GameWithAchievements("Not in the library", 20, 30, inLibrary: false);
            GameWithAchievements("Close", 1, 30);
            AchievementsRead();

            var close = FirstRound(IdleCardKind.CloseAchievements);

            Assert.Equal("Close", Assert.Single(close).Title);
        }

        [Fact]
        public void A_close_achievements_card_shows_the_games_art_how_many_are_unlocked_and_the_locked_one_most_players_have()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades", coverImage: "hades.jpg");
            achievements.Recorded.Add(new GameAchievements(id, new[]
            {
                new Achievement("a", "Blessed", null, 80, Start.AddDays(-2)),
                new Achievement("b", "Escaped", null, 90, Start.AddDays(-1)),
                new Achievement("c", "Unknown", null, null, null),
                new Achievement("d", "Petted Cerberus", null, 40, null),
                new Achievement("e", "Nameless", null, 60, null),
                new Achievement("f", "Rare", null, 2, null),
            }));
            AchievementsRead();

            var card = FirstRound(IdleCardKind.CloseAchievements).Single();

            Assert.Equal("Hades", card.Title);
            Assert.Equal("2 of 6 unlocked · Next: Nameless", card.Detail);
            Assert.Equal("hades.jpg", card.Art.Image);
            Assert.True(card.Art.IsCover);
            Assert.Null(card.Icon);
        }

        [Fact]
        public void A_locked_achievement_of_unknown_rarity_is_named_only_when_no_other_is_known()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades");
            achievements.Recorded.Add(new GameAchievements(id, new[]
            {
                new Achievement("a", "Blessed", null, 80, Start),
                new Achievement("b", "Unknown", null, null, null),
                new Achievement("c", "Common", null, 1, null),
            }));
            AchievementsRead();

            Assert.Equal("1 of 3 unlocked · Next: Common", FirstRound(IdleCardKind.CloseAchievements).Single().Detail);
        }

        [Fact]
        public void With_no_locked_achievement_named_the_card_only_counts()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades");
            achievements.Recorded.Add(new GameAchievements(id, new[]
            {
                new Achievement("a", "Blessed", null, 80, Start),
                new Achievement("b", "", null, 50, null),
                new Achievement("c", null, null, null, null),
            }));
            AchievementsRead();

            Assert.Equal("1 of 3 unlocked", FirstRound(IdleCardKind.CloseAchievements).Single().Detail);
        }

        [Fact]
        public void An_unlock_heard_live_counts_as_unlocked_before_a_read_shows_it()
        {
            var id = GameWithAchievements("Hades", 1, 3);
            AchievementsRead();

            core.OnAchievementUnlocked(id, new Achievement("Hades1", "Hades 1", null, 50, Start));

            Assert.Equal("2 of 3 unlocked · Next: Hades 2", FirstRound(IdleCardKind.CloseAchievements).Single().Detail);
        }

        [Fact]
        public void Recent_unlocks_holds_the_last_five_unlocks_with_their_icon_name_game_and_when()
        {
            var hades = Guid.NewGuid();
            var celeste = Guid.NewGuid();
            library.WithGame(hades, "Hades").WithGame(celeste, "Celeste");
            achievements.Recorded.Add(new GameAchievements(hades, Enumerable.Range(1, 4)
                .Select(i => new Achievement("h" + i, "Hades " + i, "h" + i + ".png", 50, Start.AddDays(-i * 2)))
                .ToList()));
            achievements.Recorded.Add(new GameAchievements(celeste, Enumerable.Range(1, 3)
                .Select(i => new Achievement("c" + i, "Celeste " + i, "c" + i + ".png", 50, Start.AddDays(-i * 2 - 1)))
                .Concat(new[] { new Achievement("locked", "Locked", null, 50, null) })
                .ToList()));
            AchievementsRead();

            var unlocks = FirstRound(IdleCardKind.RecentUnlock);

            Assert.Equal(new[] { "Celeste 1", "Celeste 2", "Hades 1", "Hades 2", "Hades 3" }, unlocks.Select(c => c.Title).OrderBy(t => t));
            var celeste2 = unlocks.Single(c => c.Title == "Celeste 2");
            Assert.Equal("c2.png", celeste2.Icon);
            Assert.Equal("Celeste · Unlocked 5 days ago", celeste2.Detail);
            Assert.Null(celeste2.Art);
            Assert.Equal("Hades · Unlocked 2 days ago", unlocks.Single(c => c.Title == "Hades 1").Detail);
        }

        [Fact]
        public void An_unlock_heard_live_is_a_recent_unlock_and_one_of_a_hidden_or_unknown_game_is_not()
        {
            var hades = GameWithAchievements("Hades", 0, 2);
            var hidden = GameWithAchievements("Hidden", 1, 2, hidden: true);
            GameWithAchievements("Unknown", 1, 2, inLibrary: false);
            AchievementsRead();

            core.OnAchievementUnlocked(hades, new Achievement("Hades0", "Hades 0", "h.png", 50, Start));

            var unlock = Assert.Single(FirstRound(IdleCardKind.RecentUnlock));
            Assert.Equal("Hades 0", unlock.Title);
            Assert.Equal("Hades · Unlocked today", unlock.Detail);
        }

        [Fact]
        public void Without_PlayniteAchievements_only_recently_played_and_tips_cycle()
        {
            GameWithAchievements("Hades", 3, 10);
            library.WithGame("Celeste", lastActivity: Start);
            AchievementsRead();
            achievements.IsAvailable = false;

            var kinds = FirstRound().Select(c => c.Kind).Distinct().OrderBy(k => k);

            Assert.Equal(new[] { IdleCardKind.RecentlyPlayed, IdleCardKind.Tip }, kinds);
        }

        [Fact]
        public void With_achievements_the_pool_holds_every_kind()
        {
            GameWithAchievements("Hades", 3, 10);
            library.WithGame("Celeste", lastActivity: Start);
            AchievementsRead();

            var kinds = FirstRound().Select(c => c.Kind).Distinct().OrderBy(k => k);

            Assert.Equal(new[] { IdleCardKind.CloseAchievements, IdleCardKind.RecentUnlock, IdleCardKind.RecentlyPlayed, IdleCardKind.Tip }, kinds);
        }
    }
}
