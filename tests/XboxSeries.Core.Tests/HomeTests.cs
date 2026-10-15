using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class HomeTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public HomeTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore());
        }

        [Fact]
        public void Recent_row_lists_played_games_newest_first_without_hidden_games()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Forza Horizon 5", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hidden Game", lastActivity: new DateTime(2026, 10, 13), hidden: true)
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11))
                .Snapshot());

            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite" }, RecentRow());
        }

        [Fact]
        public void Recent_row_holds_at_most_7_games_so_it_fits_with_Play_history()
        {
            var library = new FakeLibrary();
            for (var day = 1; day <= 12; day++)
            {
                library.WithGame($"Game {day}", lastActivity: new DateTime(2026, 10, day));
            }

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(Enumerable.Range(6, 7).Reverse().Select(day => $"Game {day}"), RecentRow());
        }

        [Fact]
        public void Recent_row_is_padded_with_recently_added_games_when_fewer_than_7_have_been_played()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10), added: new DateTime(2026, 1, 1))
                .WithGame("Old Unplayed", added: new DateTime(2026, 2, 1))
                .WithGame("New Unplayed", added: new DateTime(2026, 10, 1))
                .WithGame("Hidden Unplayed", added: new DateTime(2026, 10, 2), hidden: true)
                .WithGame("Never Dated")
                .Snapshot());

            Assert.Equal(new[] { "Halo Infinite", "New Unplayed", "Old Unplayed", "Never Dated" }, RecentRow());
        }

        [Fact]
        public void The_running_game_comes_first_in_the_Recent_row()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());

            core.OnGameStarted(library.Id("Halo Infinite"));

            Assert.Equal(new[] { "Halo Infinite", "Hades" }, RecentRow());
            Assert.True(core.Home.RecentRow[0].IsRunning);
            Assert.False(core.Home.RecentRow[1].IsRunning);
        }

        [Fact]
        public void The_Recent_row_goes_back_to_last_played_order_when_the_game_stops()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));

            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.Equal(new[] { "Hades", "Halo Infinite" }, RecentRow());
            Assert.DoesNotContain(core.Home.RecentRow, t => t.IsRunning);
        }

        [Fact]
        public void Play_history_tile_shows_the_next_4_played_games_after_the_Recent_row()
        {
            var library = new FakeLibrary();
            for (var day = 1; day <= 14; day++)
            {
                library.WithGame($"Game {day}", lastActivity: new DateTime(2026, 10, day));
            }

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(new[] { "Game 7", "Game 6", "Game 5", "Game 4" }, core.Home.PlayHistory.Select(t => t.Title));
        }

        [Fact]
        public void Play_history_collage_is_empty_when_every_played_game_fits_in_the_Recent_row()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("New Unplayed", added: new DateTime(2026, 10, 1))
                .Snapshot());

            Assert.Empty(core.Home.PlayHistory);
        }

        [Fact]
        public void Hero_is_the_swirl_until_a_Recent_row_tile_has_focus()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg")
                .Snapshot());

            Assert.Null(core.Home.Hero);
        }

        [Fact]
        public void Focus_coming_into_the_Recent_row_shows_its_games_background_art_once_settled_as_along_it()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg")
                .Snapshot());

            core.FocusHomeTile(Tile("Hades"));
            clock.Advance(TimeSpan.FromSeconds(1.4));
            Assert.Null(core.Home.Hero);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.Equal(@"C:\art\hades-bg.jpg", core.Home.Hero.Image);
            Assert.False(core.Home.Hero.IsCover);
        }

        [Fact]
        public void Hero_waits_for_focus_to_settle_on_a_tile_before_following_it_along_the_row()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg")
                .Snapshot());
            core.FocusHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            MoveFocus("Halo Infinite", "Hades");
            clock.Advance(TimeSpan.FromMilliseconds(1499));

            Assert.Equal(@"C:\art\halo-bg.jpg", core.Home.Hero.Image);

            clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(@"C:\art\hades-bg.jpg", core.Home.Hero.Image);
        }

        [Fact]
        public void Scrolling_past_tiles_leaves_the_hero_until_focus_settles()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg")
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10), backgroundImage: @"C:\art\tunic-bg.jpg")
                .Snapshot());
            core.FocusHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            MoveFocus("Halo Infinite", "Hades");
            clock.Advance(TimeSpan.FromSeconds(1));
            MoveFocus("Hades", "Tunic");
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(@"C:\art\halo-bg.jpg", core.Home.Hero.Image);

            clock.Advance(TimeSpan.FromSeconds(0.5));

            Assert.Equal(@"C:\art\tunic-bg.jpg", core.Home.Hero.Image);
        }

        [Fact]
        public void Focus_leaving_the_Recent_row_goes_back_to_the_swirl()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .Snapshot());
            core.FocusHomeTile(Tile("Halo Infinite"));

            core.LeaveHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromMilliseconds(100));

            Assert.Null(core.Home.Hero);
        }

        [Fact]
        public void Focus_leaving_the_row_before_it_settles_goes_back_to_the_swirl()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg")
                .Snapshot());
            core.FocusHomeTile(Tile("Halo Infinite"));

            MoveFocus("Halo Infinite", "Hades");
            core.LeaveHomeTile(Tile("Hades"));
            Settle();

            Assert.Null(core.Home.Hero);
        }

        [Fact]
        public void Hero_falls_back_to_the_cover_when_a_game_has_no_background_art()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), coverImage: @"C:\art\hades-cover.jpg")
                .Snapshot());

            core.FocusHomeTile(Tile("Hades"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.Equal(@"C:\art\hades-cover.jpg", core.Home.Hero.Image);
            Assert.True(core.Home.Hero.IsCover);
        }

        [Fact]
        public void Hero_keeps_the_focused_games_art_while_the_row_is_rebuilt()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg");
            core.OnLibraryChanged(library.Snapshot());
            core.FocusHomeTile(Tile("Hades"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));

            Assert.Equal(@"C:\art\hades-bg.jpg", core.Home.Hero.Image);
        }

        [Fact]
        public void The_swirl_stays_while_the_row_is_rebuilt_with_focus_elsewhere()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg");
            core.OnLibraryChanged(library.Snapshot());
            core.FocusHomeTile(Tile("Halo Infinite"));
            core.LeaveHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromMilliseconds(100));

            core.OnLibraryChanged(library.Snapshot());

            Assert.Null(core.Home.Hero);
        }

        [Fact]
        public void Hero_picks_up_new_art_for_the_same_game()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), coverImage: @"C:\art\halo-cover.jpg");
            core.OnLibraryChanged(library.Snapshot());
            core.FocusHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(library.Id("Halo Infinite"), "Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .Snapshot());

            Assert.Equal(@"C:\art\halo-bg.jpg", core.Home.Hero.Image);
        }

        [Fact]
        public void A_game_with_no_art_at_all_leaves_the_swirl()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .Snapshot());

            core.FocusHomeTile(Tile("Halo Infinite"));

            Assert.Null(core.Home.Hero);
        }

        [Fact]
        public void A_on_a_tile_opens_its_Game_hub_once_a_double_tap_can_no_longer_follow()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12));
            core.OnLibraryChanged(library.Snapshot());

            core.ChooseHomeTile(Tile("Halo Infinite"));
            Assert.Empty(playnite.OpenedHubs);

            clock.Advance(TimeSpan.FromMilliseconds(300));

            Assert.Equal(new[] { library.Id("Halo Infinite") }, playnite.OpenedHubs);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void Double_tap_A_on_a_tile_launches_the_game_without_opening_its_Game_hub()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12));
            core.OnLibraryChanged(library.Snapshot());

            core.ChooseHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromMilliseconds(150));
            core.ChooseHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Halo Infinite") }, playnite.StartedGames);
            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void Taps_on_two_different_tiles_open_the_second_tiles_Game_hub()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());

            core.ChooseHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromMilliseconds(150));
            core.ChooseHomeTile(Tile("Hades"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedHubs);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void Double_tap_A_while_another_game_runs_asks_to_quit_it_first()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));

            core.ChooseHomeTile(Tile("Hades"));
            core.ChooseHomeTile(Tile("Hades"));

            Assert.Empty(playnite.StartedGames);
            Assert.Equal("Quit Halo Infinite and start Hades?", core.Guide.Dialog.Title);
        }

        [Fact]
        public void A_running_game_that_was_never_played_before_appears_once()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 10))
                .WithGame("Brand New", added: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());

            core.OnGameStarted(library.Id("Brand New"));

            Assert.Equal(new[] { "Brand New", "Halo Infinite" }, RecentRow());
        }

        [Fact]
        public void Focusing_a_game_with_no_art_keeps_the_last_art()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11))
                .Snapshot());
            core.FocusHomeTile(Tile("Halo Infinite"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            MoveFocus("Halo Infinite", "Hades");
            Settle();

            Assert.Equal(@"C:\art\halo-bg.jpg", core.Home.Hero.Image);
        }

        [Fact]
        public void Hero_goes_back_to_the_swirl_once_its_game_is_hidden()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg");
            core.OnLibraryChanged(library.Snapshot());
            core.FocusHomeTile(Tile("Hades"));

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame(library.Id("Hades"), "Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg", hidden: true)
                .Snapshot());

            Assert.Null(core.Home.Hero);
        }

        [Fact]
        public void Double_tap_A_on_the_running_games_tile_returns_to_the_game()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), installDirectory: @"C:\Games\Halo");
            core.OnLibraryChanged(library.Snapshot());
            var game = windows.OpenWindow(@"C:\Games\Halo\halo.exe");
            core.OnGameStarted(library.Id("Halo Infinite"));
            playnite.BringToFront();

            core.ChooseHomeTile(Tile("Halo Infinite"));
            core.ChooseHomeTile(Tile("Halo Infinite"));

            Assert.Equal(game, windows.Foreground);
            Assert.Empty(playnite.StartedGames);
            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void Tiles_carry_their_games_library_source()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), source: "Xbox")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), source: "Steam")
                .WithGame("Emulated", lastActivity: new DateTime(2026, 10, 10))
                .Snapshot());

            Assert.Equal(new[] { "Xbox", "Steam", null }, core.Home.RecentRow.Select(t => t.Source));
        }

        private string[] RecentRow() => core.Home.RecentRow.Select(t => t.Title).ToArray();

        /// <summary>Focus stays put long enough for the hero to follow it.</summary>
        private void Settle() => clock.Advance(TimeSpan.FromSeconds(1.5));

        private HomeTile Tile(string title) => core.Home.RecentRow.Single(t => t.Title == title);

        /// <summary>Focus moving from one Recent-row tile to another, as WPF tells it: the one left, then the one focused.</summary>
        private void MoveFocus(string from, string to)
        {
            core.LeaveHomeTile(Tile(from));
            core.FocusHomeTile(Tile(to));
        }
    }
}
