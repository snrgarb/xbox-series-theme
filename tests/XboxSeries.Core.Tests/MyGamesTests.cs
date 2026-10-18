using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class MyGamesTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDrives drives = new FakeDrives();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public MyGamesTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), drives, new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
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
        public void The_nav_counts_the_games_Games_shows()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithGame("Not Installed", installed: false)
                .WithGame("Hidden Game", hidden: true)
                .WithGame("Hades")
                .Snapshot());

            Assert.Equal(2, core.MyGames.GameCount);
        }

        [Fact]
        public void The_nav_counts_the_games_Play_history_lists()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13))
                .WithGame("Hades", lastActivity: new DateTime(2025, 1, 1), installed: false)
                .WithGame("Never Played")
                .WithGame("Hidden Game", lastActivity: new DateTime(2026, 10, 14), hidden: true)
                .Snapshot());

            Assert.Equal(2, core.MyGames.PlayHistoryCount);
        }

        [Fact]
        public void The_nav_counts_the_games_the_users_own_filter_shows_for_Full_library()
        {
            playnite.UserFilterGameCount = 7;

            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite").Snapshot());

            Assert.Equal(7, core.MyGames.FullLibraryCount);
        }

        [Fact]
        public void Full_librarys_count_follows_the_tab_the_user_switches_to()
        {
            playnite.UserFilterGameCount = 7;
            core.OpenMyGames(MyGamesItem.FullLibrary);

            playnite.UserFilterGameCount = 3;
            core.OnMainListChanged();

            Assert.Equal(3, core.MyGames.FullLibraryCount);
        }

        [Fact]
        public void The_storage_ring_shows_the_drive_holding_most_installed_games()
        {
            const long GB = 1L << 30;
            drives.WithDrive(@"C:\", 100 * GB, 200 * GB).WithDrive(@"D:\", 412 * GB, 1843 * GB).WithDrive(@"E:\", 1 * GB, 2 * GB);
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", installDirectory: @"D:\Games\Halo Infinite")
                .WithGame("Hades", installDirectory: @"d:\SteamLibrary\Hades")
                .WithGame("Forza Horizon 5", installDirectory: @"C:\XboxGames\Forza Horizon 5")
                .WithGame("Uninstalled 1", installDirectory: @"E:\Old\1", installed: false)
                .WithGame("Uninstalled 2", installDirectory: @"E:\Old\2", installed: false)
                .WithGame("Uninstalled 3", installDirectory: @"E:\Old\3", installed: false)
                .Snapshot());

            core.OpenMyGames(MyGamesItem.Games);

            var storage = core.MyGames.Storage;
            Assert.Equal($"{Tenths(412)} GB free", storage.Free);
            Assert.Equal($"{Tenths(77.6)}%", storage.UsedPercent);
            Assert.Equal(1431.0 / 1843, storage.UsedFraction, 3);
        }

        [Fact]
        public void The_storage_ring_shows_a_drives_space_again_when_the_page_opens()
        {
            const long GB = 1L << 30;
            drives.WithDrive(@"C:\", 100 * GB, 200 * GB);
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", installDirectory: @"C:\Games\Halo").Snapshot());

            drives.WithDrive(@"C:\", 50 * GB, 200 * GB);
            core.OpenMyGames(MyGamesItem.Games);

            Assert.Equal($"{Tenths(50)} GB free", core.MyGames.Storage.Free);
            Assert.Equal($"{Tenths(75)}%", core.MyGames.Storage.UsedPercent);
        }

        [Fact]
        public void The_storage_ring_shows_free_space_that_rounds_to_1024_GB_in_TB()
        {
            drives.WithDrive(@"C:\", (1L << 40) - (1L << 25), 2L << 40);
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", installDirectory: @"C:\Games\Halo").Snapshot());

            core.OpenMyGames(MyGamesItem.Games);

            Assert.Equal($"{Tenths(1)} TB free", core.MyGames.Storage.Free);
        }

        [Fact]
        public void There_is_no_storage_ring_without_an_installed_game_on_a_drive_that_can_be_read()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("No Directory")
                .WithGame("On A Share", installDirectory: @"\\nas\games\Halo")
                .WithGame("Unreadable", installDirectory: @"Z:\Games\Hades")
                .Snapshot());

            core.OpenMyGames(MyGamesItem.Games);

            Assert.Null(core.MyGames.Storage);
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
        public void A_on_a_Play_history_entry_puts_every_game_it_lists_in_the_main_list_for_its_hub()
        {
            var library = new FakeLibrary()
                .WithGame("Not Installed", lastActivity: new DateTime(2026, 10, 12), installed: false)
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(EntryOf("Not Installed"));

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Not Installed") }, playnite.OpenedHubs);
            Assert.Equal(new[] { "Halo Infinite", "Not Installed" }, MainList(library));
        }

        [Fact]
        public void Manage_game_on_a_Play_history_entry_puts_every_game_it_lists_in_the_main_list_for_its_menu()
        {
            var library = new FakeLibrary()
                .WithGame("Not Installed", lastActivity: new DateTime(2026, 10, 12), installed: false)
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(EntryOf("Not Installed"));
            Tap(ControllerButton.Start);
            while (core.Home.TileMenu.FocusedOption.Label != "Manage game")
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.A);

            Assert.Equal(new[] { library.Id("Not Installed") }, playnite.OpenedGameMenus);
            Assert.Equal(new[] { "Halo Infinite", "Not Installed" }, MainList(library));
        }

        [Fact]
        public void Play_history_leaves_the_main_list_as_it_was_until_one_of_its_games_opens()
        {
            var library = new FakeLibrary()
                .WithGame("Not Installed", lastActivity: new DateTime(2026, 10, 12), installed: false)
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            var shows = playnite.MainListShows;

            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.OpenMyGames(MyGamesItem.Games);

            Assert.Equal(shows, playnite.MainListShows);
            Assert.Equal(new[] { "Halo Infinite" }, MainList(library));
        }

        [Fact]
        public void Play_history_opened_from_Home_takes_no_hold_of_the_main_list()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13)).Snapshot());

            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.LeaveMyGames();

            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.Equal(0, playnite.MainListShows);
            Assert.Equal(0, playnite.UserFilterRestores);
        }

        [Fact]
        public void The_users_filter_comes_back_after_a_Play_history_game_opened_from_Home_held_the_main_list()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13)).Snapshot());
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(EntryOf("Halo Infinite"));
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            core.LeaveMyGames();

            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.Equal(1, playnite.UserFilterRestores);
        }

        [Fact]
        public void A_single_step_along_the_nav_opens_the_item_at_once()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.Games);
            clock.Advance(TimeSpan.FromSeconds(1));

            core.FocusMyGamesNav(MyGamesItem.FullLibrary);

            Assert.Equal(MyGamesItem.FullLibrary, core.MyGames.Item);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Focus_moving_on_along_the_nav_opens_an_item_once_it_has_stayed_there()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.Games);

            core.FocusMyGamesNav(MyGamesItem.FullLibrary);
            clock.Advance(TimeSpan.FromMilliseconds(149));
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(MyGamesItem.FullLibrary, core.MyGames.Item);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Focus_passing_through_the_nav_opens_only_the_item_it_stops_on()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13));
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            var opened = new List<MyGamesItem>();
            core.MyGames.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MyGamesState.Item))
                {
                    opened.Add(core.MyGames.Item);
                }
            };
            core.FocusMyGamesNav(MyGamesItem.Games);

            core.FocusMyGamesNav(MyGamesItem.PlayHistory);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            core.FocusMyGamesNav(MyGamesItem.FullLibrary);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            core.FocusMyGamesNav(MyGamesItem.Manage);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { MyGamesItem.Manage }, opened);
            Assert.Equal(new[] { "Halo Infinite" }, MainList(library));
            Assert.Equal(0, playnite.UserFilterRestores);
        }

        [Fact]
        public void Focus_back_on_the_item_showing_drops_the_one_it_passed()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.Games);

            core.FocusMyGamesNav(MyGamesItem.FullLibrary);
            core.FocusMyGamesNav(MyGamesItem.Games);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
            Assert.Equal(0, playnite.UserFilterRestores);
        }

        [Fact]
        public void A_or_Right_on_the_nav_opens_the_focused_item_at_once_and_only_once()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.FullLibrary);

            core.OpenMyGames(MyGamesItem.FullLibrary);
            Assert.Equal(MyGamesItem.FullLibrary, core.MyGames.Item);
            core.FocusMyGamesNav(MyGamesItem.FullLibrary);
            core.OpenMyGames(MyGamesItem.Games);

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
        }

        [Fact]
        public void B_while_focus_settles_on_the_nav_leaves_the_page_without_opening_the_item()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.PlayHistory);

            core.LeaveMyGames();
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.False(core.MyGames.IsShown);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Focus_leaving_the_nav_before_it_settles_leaves_the_item_showing()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.Games);
            core.FocusMyGamesNav(MyGamesItem.FullLibrary);

            core.LeaveMyGamesNav();
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
        }

        [Fact]
        public void The_nav_taking_focus_with_the_page_down_opens_it_at_once()
        {
            core.FocusMyGamesNav(MyGamesItem.Games);

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
        }

        [Fact]
        public void Moving_through_the_nav_keeps_Play_history_as_it_is()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13)).Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            var history = core.MyGames.PlayHistory;

            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.OpenMyGames(MyGamesItem.Games);

            Assert.Same(history, core.MyGames.PlayHistory);
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
        public void Full_library_is_the_main_list_under_the_users_own_filter_and_presets()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.OpenMyGames(MyGamesItem.FullLibrary);

            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.True(core.MyGames.ShowsGameGrid);
            Assert.True(core.MyGames.ShowsFullLibrary);
        }

        [Fact]
        public void Full_library_opened_first_leaves_the_users_filter_alone()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.LeaveMyGames();

            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.Equal(0, playnite.UserFilterRestores);
        }

        [Fact]
        public void Games_after_Full_library_shows_installed_games_and_keeps_the_filter_chosen_there_for_Home()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite").WithGame("Not Installed", installed: false);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.FullLibrary);

            core.OpenMyGames(MyGamesItem.Games);

            Assert.Equal(new[] { "Halo Infinite" }, MainList(library));
            core.LeaveMyGames();
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Only_Full_library_lets_LB_and_RB_switch_the_users_presets()
        {
            Assert.False(core.MyGames.ShowsFullLibrary);
            core.OpenMyGames(MyGamesItem.Games);
            Assert.False(core.MyGames.ShowsFullLibrary);
            core.OpenMyGames(MyGamesItem.PlayHistory);
            Assert.False(core.MyGames.ShowsFullLibrary);
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.LeaveMyGames();
            Assert.False(core.MyGames.ShowsFullLibrary);
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
        public void Moving_between_Games_and_Full_library_keeps_hold_of_the_users_filter_until_the_page_goes()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.OpenMyGames(MyGamesItem.FullLibrary);
            Assert.Equal(0, playnite.UserFilterReleases);

            core.LeaveMyGames();

            Assert.True(playnite.UserFilterReleases > 0);
            Assert.False(playnite.ReleasedUnderOwnFilter);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Playnite_stopping_on_Full_library_lets_go_of_the_users_filter()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.FullLibrary);

            core.OnApplicationStopped();

            Assert.True(playnite.UserFilterReleases > 0);
            Assert.False(playnite.ReleasedUnderOwnFilter);
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

        /// <summary>The Play history entry's tile of the game named <paramref name="name"/>.</summary>
        private HomeTile EntryOf(string name) =>
            core.MyGames.PlayHistory.SelectMany(s => s.Entries).Single(e => e.Tile.Title == name).Tile;

        /// <summary>The games Playnite's main list shows under the filter the core last applied.</summary>
        private string[] MainList(FakeLibrary library) =>
            playnite.MainListFilter.Apply(library.Snapshot().Games).Select(g => g.Name).ToArray();

        /// <summary>A number to a tenth, as the user's culture writes it.</summary>
        private static string Tenths(double value) => value.ToString("0.0", CultureInfo.CurrentCulture);

        /// <summary>Pressed and let go. A Guide tap opens the Guide 0.3 s after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
