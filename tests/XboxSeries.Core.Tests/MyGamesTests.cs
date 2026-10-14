using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class MyGamesTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public MyGamesTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer());
        }

        [Fact]
        public void Games_shows_installed_games_A_to_Z_without_hidden_ones()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithGame("Not Installed", installed: false)
                .WithGame("Hidden Game", hidden: true)
                .WithGame("Forza Horizon 5")
                .WithGame("The Witness", sortingName: "Witness")
                .WithGame("Hades");
            core.OnLibraryChanged(library.Snapshot());

            core.OpenMyGames(MyGamesItem.Games);

            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, MainList(library));
        }

        [Fact]
        public void Play_history_lists_played_games_newest_first_grouped_by_when_they_were_last_played()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Tonight", lastActivity: new DateTime(2026, 10, 14, 18, 0, 0))
                .WithGame("This Morning", lastActivity: new DateTime(2026, 10, 14, 8, 0, 0))
                .WithGame("Last Night", lastActivity: new DateTime(2026, 10, 13, 23, 0, 0))
                .WithGame("Monday", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Last Tuesday", lastActivity: new DateTime(2026, 10, 6))
                .WithGame("Early October", lastActivity: new DateTime(2026, 10, 2))
                .WithGame("Last Month", lastActivity: new DateTime(2026, 9, 20))
                .WithGame("Spring", lastActivity: new DateTime(2026, 4, 1))
                .WithGame("Long Ago", lastActivity: new DateTime(2024, 1, 1))
                .WithGame("Never Played")
                .WithGame("Hidden Game", lastActivity: new DateTime(2026, 10, 14, 19, 0, 0), hidden: true)
                .Snapshot());

            core.OpenMyGames(MyGamesItem.PlayHistory);

            Assert.Equal(
                new[]
                {
                    "Today: Tonight, This Morning",
                    "Yesterday: Last Night",
                    "This week: Monday",
                    "Last week: Last Tuesday",
                    "This month: Early October",
                    "Last month: Last Month",
                    "This year: Spring",
                    "Older: Long Ago",
                },
                core.MyGames.PlayHistory.Select(s => $"{s.Title}: {string.Join(", ", s.Entries.Select(e => e.Tile.Title))}"));
        }

        [Fact]
        public void A_Play_history_entry_shows_its_art_last_played_date_and_platform()
        {
            var played = new DateTime(2026, 10, 14, 18, 0, 0);
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: played, coverImage: @"C:\covers\halo.jpg", platform: "PC (Windows)", source: "Steam")
                .Snapshot());

            core.OpenMyGames(MyGamesItem.PlayHistory);

            var entry = core.MyGames.PlayHistory.Single().Entries.Single();
            Assert.Equal(@"C:\covers\halo.jpg", entry.Tile.CoverImage);
            Assert.Equal(played, entry.LastPlayed);
            Assert.Equal("PC (Windows)", entry.Platform);
        }

        [Fact]
        public void Play_history_puts_every_game_it_lists_in_the_main_list_so_A_can_open_its_hub()
        {
            var library = new FakeLibrary()
                .WithGame("Not Installed", lastActivity: new DateTime(2026, 10, 12), installed: false)
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());

            core.OpenMyGames(MyGamesItem.PlayHistory);

            Assert.Equal(new[] { "Halo Infinite", "Not Installed" }, MainList(library));
        }

        [Fact]
        public void A_on_a_Play_history_entry_opens_its_Game_hub()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(core.MyGames.PlayHistory[0].Entries[0].Tile);

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Halo Infinite") }, playnite.OpenedHubs);
        }

        [Fact]
        public void The_top_bar_opens_the_page_on_Games()
        {
            core.OpenMyGames(MyGamesItem.Games);

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
            Assert.True(core.MyGames.ShowsGameGrid);
        }

        [Fact]
        public void The_Play_history_tile_opens_the_page_on_Play_history_without_the_grid()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.PlayHistory, core.MyGames.Item);
            Assert.False(core.MyGames.ShowsGameGrid);
        }

        [Fact]
        public void The_Guides_My_games_entry_brings_Playnite_to_the_front_on_Games()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite").WithGame("Not Installed", installed: false);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.PlayHistory);
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
            Assert.Equal(new[] { "Halo Infinite" }, MainList(library));
        }

        [Fact]
        public void Home_starts_without_the_page()
        {
            Assert.False(core.MyGames.IsShown);
        }

        [Fact]
        public void B_returns_to_Home_with_the_users_own_filter_and_sort()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.LeaveMyGames();

            Assert.False(core.MyGames.IsShown);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void The_users_filter_is_saved_only_when_the_page_first_takes_over_the_main_list()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.OpenMyGames(MyGamesItem.Games);

            core.LeaveMyGames();

            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void The_users_filter_is_restored_when_Playnite_leaves_Fullscreen_on_the_page()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.OnApplicationStopped();

            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void The_users_filter_is_left_alone_when_the_page_never_took_over()
        {
            core.OnApplicationStopped();
            core.LeaveMyGames();

            Assert.Equal(0, playnite.UserFilterRestores);
        }

        [Fact]
        public void The_users_filter_is_given_back_only_once()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.LeaveMyGames();
            var restores = playnite.UserFilterRestores;

            core.OnApplicationStopped();
            core.LeaveMyGames();

            Assert.Equal(restores, playnite.UserFilterRestores);
        }

        [Fact]
        public void The_Guides_Home_entry_leaves_the_page()
        {
            core.OpenMyGames(MyGamesItem.Games);
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.A);

            Assert.False(core.MyGames.IsShown);
            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.Equal(DashboardPage.Home, playnite.Page);
        }

        [Fact]
        public void B_answers_a_tile_menu_on_the_page_without_leaving_it()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(core.MyGames.PlayHistory[0].Entries[0].Tile);
            Tap(ControllerButton.Start);

            // Playnite's own B reaches the page before the pad's B reaches the core.
            core.LeaveMyGames();
            Tap(ControllerButton.B);

            Assert.True(core.MyGames.IsShown);
            Assert.Null(core.Home.TileMenu);
        }

        /// <summary>The games Playnite's main list shows under the filter the core last applied.</summary>
        private string[] MainList(FakeLibrary library) =>
            playnite.MainListFilter.Apply(library.Snapshot().Games).Select(g => g.Name).ToArray();

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }
    }
}
