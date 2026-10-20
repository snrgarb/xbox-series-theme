using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Idle screen's cards: at Showing things a pool of the nearly-finished games, the last unlocks, the last games
    /// played and the tips, shown one at a time in shuffled turn, each for 20 s, reshuffled when it runs out; waking hides
    /// the card.
    /// </summary>
    public class IdleCardsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 20, 20, 0, 0);

        private readonly FakeClock clock = new FakeClock(Start);
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeAchievements achievements = new FakeAchievements();
        private readonly FakeLibrary library = new FakeLibrary();
        private FakePlayniteHost playnite;
        private ExtensionCore core;

        public IdleCardsTests()
        {
            core = Core(new Random(1));
        }

        private ExtensionCore Core(Random random) =>
            new ExtensionCore(
                clock, windows, playnite = new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(),
                new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads(),
                achievements: achievements, random: random);

        /// <summary>No input until the Idle screen shows things, at its default 5 min.</summary>
        private void ShowThings()
        {
            clock.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        private void NextCard() => clock.Advance(TimeSpan.FromSeconds(20));

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

        /// <summary>The game a card's title names, before its achievement's: "Hades" of "Hades - Blessed".</summary>
        private static string GameOf(IdleCard card)
        {
            var dash = card.Title.IndexOf(" - ", StringComparison.Ordinal);
            return dash < 0 ? card.Title : card.Title.Substring(0, dash);
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
        public void Each_card_shows_for_20_seconds_then_the_next()
        {
            ShowThings();
            var first = core.Idle.Card;

            clock.Advance(TimeSpan.FromSeconds(19.99));
            Assert.Same(first, core.Idle.Card);

            clock.Advance(TimeSpan.FromSeconds(0.01));
            Assert.NotSame(first, core.Idle.Card);
            Assert.NotNull(core.Idle.Card);
        }

        [Fact]
        public void Without_achievements_or_games_the_twelve_tips_cycle_headed_Tip_text_only_and_Y_got_it()
        {
            ShowThings();

            var cards = Cards(12);

            Assert.All(cards, c => Assert.Equal(IdleCardKind.Tip, c.Kind));
            Assert.All(cards, c => Assert.Null(c.Description));
            Assert.All(cards, c => Assert.Null(c.Art));
            Assert.All(cards, c => Assert.Null(c.Icon));
            Assert.All(cards, c => Assert.Equal("Tip", c.Header));
            Assert.All(cards, c => Assert.Null(c.Label));
            Assert.All(cards, c => Assert.Null(c.Score));
            Assert.All(cards, c => Assert.Null(c.Progress));
            Assert.All(cards, c => Assert.False(c.IsRare));
            Assert.All(cards, c => Assert.Equal("Got it", c.Prompt));
            Assert.All(cards, c => Assert.Null(c.GameId));
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
        [InlineData(12 * 3600 + 1800, 1, "Played yesterday", "12 h played")]
        [InlineData(3600, 0, "Played today", "1 h played")]
        [InlineData(45 * 60 + 30, 0, "Played today", "45 min played")]
        [InlineData(0, 1, "Played yesterday", null)]
        [InlineData(30, 3, "Played 3 days ago", null)]
        public void A_recently_played_cards_header_is_when_it_was_last_played_and_its_description_its_playtime(
            ulong playtime, int daysAgo, string header, string description)
        {
            library.WithGame("Hades", lastActivity: Start.Date.AddDays(-daysAgo).AddHours(9), playtime: playtime);
            LibraryChanged();

            var card = FirstRound(IdleCardKind.RecentlyPlayed).Single();

            Assert.Equal(header, card.Header);
            Assert.Equal(description, card.Description);
        }

        [Fact]
        public void A_recently_played_card_is_labelled_so_with_no_Score_or_progress_and_Y_sees_the_game()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades", lastActivity: Start, playtime: 7200);
            LibraryChanged();

            var card = FirstRound(IdleCardKind.RecentlyPlayed).Single();

            Assert.Equal("Hades", card.Title);
            Assert.Equal("Recently played", card.Label);
            Assert.Null(card.Score);
            Assert.Null(card.Progress);
            Assert.False(card.IsRare);
            Assert.Equal("See game", card.Prompt);
            Assert.Equal(id, card.GameId);
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

            var header = FirstRound(IdleCardKind.RecentlyPlayed).Single().Header;

            Assert.Equal("Played " + when, header);
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

            Assert.Equal(new[] { "Eight", "Four", "Nine", "Seven", "Twenty" }, close.Select(GameOf).OrderBy(t => t));
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

            var titles = FirstRound(IdleCardKind.CloseAchievements).Select(GameOf).ToList();

            Assert.Contains("Zeta five of six", titles);
            Assert.DoesNotContain("Five of fifty", titles);
            Assert.DoesNotContain("Alpha five of ten", titles);

            achievements.Recorded.RemoveAll(g => library.Snapshot().Games.Single(x => x.Id == g.GameId).Name == "Zeta five of six");
            core.OnUserInput();
            AchievementsRead();
            titles = FirstRound(IdleCardKind.CloseAchievements).Select(GameOf).ToList();

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

            Assert.Equal("Close", GameOf(Assert.Single(close)));
        }

        [Fact]
        public void A_close_achievements_card_is_the_game_and_the_locked_achievement_most_players_have_with_its_Score_and_the_games_progress()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades", coverImage: "hades.jpg");
            achievements.Recorded.Add(new GameAchievements(id, new[]
            {
                new Achievement("a", "Blessed", null, 80, Start.AddDays(-2)),
                new Achievement("b", "Escaped", null, 90, Start.AddDays(-1)),
                new Achievement("c", "Unknown", null, null, null),
                new Achievement("d", "Petted Cerberus", null, 40, null),
                new Achievement("e", "Nameless", null, 60, null, "Clear an escape without a name."),
                new Achievement("f", "Rare", null, 2, null),
            }));
            AchievementsRead();

            var card = FirstRound(IdleCardKind.CloseAchievements).Single();

            Assert.Equal("Hades - Nameless", card.Title);
            Assert.Equal("Close achievement", card.Label);
            Assert.Equal("Clear an escape without a name.", card.Description);
            Assert.Equal(10, card.Score);
            Assert.False(card.IsRare);
            Assert.Equal(2.0 / 6, card.Progress.Value, 6);
            Assert.Null(card.Header);
            Assert.Equal("hades.jpg", card.Art.Image);
            Assert.True(card.Art.IsCover);
            Assert.Null(card.Icon);
            Assert.Equal("See game", card.Prompt);
            Assert.Equal(id, card.GameId);
        }

        [Fact]
        public void A_close_achievements_card_shows_its_next_ones_Score_and_whether_it_is_rare()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Halo 5: Guardians");
            achievements.Recorded.Add(new GameAchievements(id, new[]
            {
                new Achievement("a", "Blessed", null, 80, Start),
                new Achievement("k", "Kraken Lackin'", null, 4, null, null, 30),
            }));
            AchievementsRead();

            var card = FirstRound(IdleCardKind.CloseAchievements).Single();

            Assert.Equal("Halo 5: Guardians - Kraken Lackin'", card.Title);
            Assert.Equal(30, card.Score);
            Assert.True(card.IsRare);
            Assert.Null(card.Description);
            Assert.Equal(0.5, card.Progress);
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

            Assert.Equal("Hades - Common", FirstRound(IdleCardKind.CloseAchievements).Single().Title);
        }

        [Fact]
        public void With_no_locked_achievement_named_the_card_is_the_game_alone_with_its_progress()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades");
            achievements.Recorded.Add(new GameAchievements(id, new[]
            {
                new Achievement("a", "Blessed", null, 80, Start),
                new Achievement("b", "", null, 50, null),
                new Achievement("c", null, null, null, null),
                new Achievement("d", "Escaped", null, 90, Start),
            }));
            AchievementsRead();

            var card = FirstRound(IdleCardKind.CloseAchievements).Single();

            Assert.Equal("Hades", card.Title);
            Assert.Null(card.Description);
            Assert.Null(card.Score);
            Assert.False(card.IsRare);
            Assert.Equal(0.5, card.Progress);
            Assert.Equal("Close achievement", card.Label);
        }

        [Fact]
        public void An_unlock_heard_live_counts_as_unlocked_before_a_read_shows_it()
        {
            var id = GameWithAchievements("Hades", 1, 3);
            AchievementsRead();

            core.OnAchievementUnlocked(id, new Achievement("Hades1", "Hades 1", null, 50, Start));

            var card = FirstRound(IdleCardKind.CloseAchievements).Single();
            Assert.Equal("Hades - Hades 2", card.Title);
            Assert.Equal(2.0 / 3, card.Progress.Value, 6);
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

            Assert.Equal(
                new[] { "Celeste - Celeste 1", "Celeste - Celeste 2", "Hades - Hades 1", "Hades - Hades 2", "Hades - Hades 3" },
                unlocks.Select(c => c.Title).OrderBy(t => t));
            var celeste2 = unlocks.Single(c => c.Title == "Celeste - Celeste 2");
            Assert.Equal("c2.png", celeste2.Icon);
            Assert.Equal("Unlocked 5 days ago", celeste2.Header);
            Assert.Equal("Unlocked 2 days ago", unlocks.Single(c => c.Title == "Hades - Hades 1").Header);
        }

        [Theory]
        [InlineData(0.5, null, 100, true)]
        [InlineData(60.0, null, 10, false)]
        [InlineData(60.0, 30, 30, false)]
        public void A_recent_unlock_card_is_its_game_and_name_its_description_its_Score_and_rarity_with_the_games_art_and_Y_seeing_it(
            double rarity, int? points, int score, bool rare)
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Halo 5: Guardians", backgroundImage: "halo.jpg");
            achievements.Recorded.Add(new GameAchievements(id, new[]
            {
                new Achievement("k", "Kraken Lackin'", "k.png", rarity, Start.AddDays(-1), "Complete the mission without dying.", points),
            }));
            AchievementsRead();

            var card = FirstRound(IdleCardKind.RecentUnlock).Single();

            Assert.Equal("Halo 5: Guardians - Kraken Lackin'", card.Title);
            Assert.Equal("Unlocked yesterday", card.Header);
            Assert.Equal("Achievement unlocked", card.Label);
            Assert.Equal("Complete the mission without dying.", card.Description);
            Assert.Equal(score, card.Score);
            Assert.Equal(rare, card.IsRare);
            Assert.Null(card.Progress);
            Assert.Equal("k.png", card.Icon);
            Assert.Equal("halo.jpg", card.Art.Image);
            Assert.Equal("See game", card.Prompt);
            Assert.Equal(id, card.GameId);
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
            Assert.Equal("Hades - Hades 0", unlock.Title);
            Assert.Equal("Unlocked today", unlock.Header);
        }

        [Fact]
        public void An_unlock_heard_live_before_the_first_read_is_no_recent_unlock_as_it_makes_no_totals()
        {
            var hades = GameWithAchievements("Hades", 0, 2);
            LibraryChanged();

            core.OnAchievementUnlocked(hades, new Achievement("Hades0", "Hades 0", "h.png", 50, Start));

            Assert.Empty(FirstRound(IdleCardKind.RecentUnlock));
        }

        [Fact]
        public void Waking_takes_the_card_once_the_screen_is_awake_so_the_view_hides_it_with_the_dim()
        {
            ShowThings();
            var dimmedAsTheCardWent = (bool?)null;
            core.Idle.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(IdleState.Card) && core.Idle.Card == null)
                {
                    dimmedAsTheCardWent = core.Idle.IsDimmed;
                }
            };

            core.OnUserInput();

            Assert.False(dimmedAsTheCardWent);
        }

        /// <summary>Showing things, on to the first card of <paramref name="kind"/>.</summary>
        private IdleCard ShowThingsOn(IdleCardKind kind)
        {
            ShowThings();
            for (var i = 0; i < 100 && core.Idle.Card.Kind != kind; i++)
            {
                NextCard();
            }

            Assert.Equal(kind, core.Idle.Card.Kind);
            return core.Idle.Card;
        }

        /// <summary>A recently played game, whose card Y sees, read by the core.</summary>
        private Guid RecentlyPlayedGame()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades", lastActivity: Start);
            LibraryChanged();
            return id;
        }

        private void Press(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }

        [Fact]
        public void Y_routed_then_reported_on_a_games_card_wakes_the_screen_swallowed_and_opens_its_Game_hub_once()
        {
            var id = RecentlyPlayedGame();
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            Assert.True(core.OnUserInput(ControllerButton.Y));
            Press(ControllerButton.Y);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
        }

        [Fact]
        public void Y_reported_then_routed_on_a_games_card_opens_its_Game_hub_once_too()
        {
            var id = RecentlyPlayedGame();
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            core.OnControllerButton(ControllerButton.Y, ButtonState.Pressed);
            Assert.True(core.OnUserInput(ControllerButton.Y));
            core.OnControllerButton(ControllerButton.Y, ButtonState.Released);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
        }

        [Fact]
        public void Y_on_an_achievements_card_opens_its_games_Game_hub()
        {
            var id = GameWithAchievements("Hades", 3, 10);
            AchievementsRead();
            ShowThingsOn(IdleCardKind.CloseAchievements);

            core.OnUserInput(ControllerButton.Y);
            Press(ControllerButton.Y);

            Assert.Equal(new[] { id }, playnite.OpenedHubs);
        }

        [Fact]
        public void Y_on_a_tip_only_wakes_the_screen()
        {
            ShowThingsOn(IdleCardKind.Tip);

            Assert.True(core.OnUserInput(ControllerButton.Y));
            Press(ControllerButton.Y);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Empty(playnite.OpenedHubs);
        }

        [Theory]
        [InlineData(ControllerButton.A)]
        [InlineData(ControllerButton.B)]
        [InlineData(ControllerButton.X)]
        public void Another_button_on_a_games_card_only_wakes_the_screen(ControllerButton button)
        {
            RecentlyPlayedGame();
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            Assert.True(core.OnUserInput(button));
            Press(button);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void A_key_or_the_mouse_on_a_games_card_only_wakes_the_screen()
        {
            RecentlyPlayedGame();
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            Assert.True(core.OnUserInput());

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void Y_while_only_dimmed_only_wakes_the_screen()
        {
            RecentlyPlayedGame();
            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            Assert.True(core.OnUserInput(ControllerButton.Y));
            Press(ControllerButton.Y);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void Y_on_a_games_card_over_the_Guide_closes_the_Guide_and_opens_the_Game_hub_on_the_Dashboard()
        {
            var id = RecentlyPlayedGame();
            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(6));
            Assert.True(core.Guide.IsOpen);
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            core.OnUserInput(ControllerButton.Y);
            Press(ControllerButton.Y);

            Assert.False(core.Guide.IsOpen);
            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
        }

        [Fact]
        public void Y_on_a_games_card_over_a_Tile_menu_closes_the_menu_and_opens_the_Game_hub()
        {
            var id = RecentlyPlayedGame();
            core.FocusHomeTile(core.Home.RecentRow[0]);
            Press(ControllerButton.Start);
            Assert.NotNull(core.Home.TileMenu);
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            core.OnUserInput(ControllerButton.Y);
            Press(ControllerButton.Y);

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
        }

        [Fact]
        public void Y_on_a_games_card_over_the_Search_page_opens_the_Game_hub_with_every_game_in_the_main_list()
        {
            var id = RecentlyPlayedGame();
            core.OpenSearch();
            core.LeaveSearch();
            Assert.True(core.Search.IsShown);
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            core.OnUserInput(ControllerButton.Y);
            Press(ControllerButton.Y);

            Assert.True(core.Search.IsShown);
            Assert.False(playnite.ShowsUsersOwnFilter);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
            core.LeaveSearch();
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Y_on_a_games_card_over_Settings_and_the_Console_keyboard_takes_both_down_and_opens_the_Game_hub()
        {
            var id = RecentlyPlayedGame();
            core.OpenSettings();
            core.TypeNewProfile();
            Assert.True(core.TextEntry.IsShown);
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            core.OnUserInput(ControllerButton.Y);
            Press(ControllerButton.Y);

            Assert.False(core.TextEntry.IsShown);
            Assert.False(core.Settings.IsShown);
            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
        }

        [Fact]
        public void Y_on_an_uninstalled_games_card_over_My_games_Games_lists_it_in_the_main_list_for_its_Game_hub()
        {
            var id = Guid.NewGuid();
            library.WithGame(id, "Hades", lastActivity: Start, installed: false);
            LibraryChanged();
            core.OpenMyGames(MyGamesItem.Games);
            Assert.DoesNotContain(playnite.MainListFilter.Apply(library.Snapshot().Games), g => g.Id == id);
            ShowThingsOn(IdleCardKind.RecentlyPlayed);

            core.OnUserInput(ControllerButton.Y);
            Press(ControllerButton.Y);

            Assert.Contains(playnite.MainListFilter.Apply(library.Snapshot().Games), g => g.Id == id);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
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
