using System;
using System.Globalization;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class QueueTests
    {
        private const long MB = 1L << 20;
        private const long GB = 1L << 30;

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 16, 23, 52, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDrives drives = new FakeDrives();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public QueueTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), drives, new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
        }

        [Fact]
        public void The_queue_holds_every_game_Playnite_is_installing()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithGame("33 Immortals", installed: false, installing: true)
                .WithGame("Not Installed", installed: false)
                .Snapshot());

            Assert.Equal(new[] { "33 Immortals" }, QueueTitles());
            Assert.Equal(1, core.MyGames.QueueCount);
        }

        [Fact]
        public void The_current_download_comes_first_then_the_rest_in_the_order_they_started_installing()
        {
            var library = new FakeLibrary()
                .WithGame("33 Immortals", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
            library.WithGame("Hades", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
            library.WithGame("1000xRESIST", installed: false, installing: true);
            downloads
                .With(library.Id("33 Immortals"), isActive: false, 0, 3 * GB)
                .With(library.Id("Hades"), isActive: false, 0, 2 * GB)
                .With(library.Id("1000xRESIST"), isActive: true, 530 * MB, 8 * GB);

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(new[] { "1000xRESIST", "33 Immortals", "Hades" }, QueueTitles());
        }

        [Fact]
        public void A_game_that_stops_installing_leaves_the_queue_and_the_rest_keep_their_order()
        {
            var halo = Guid.NewGuid();
            var hades = Guid.NewGuid();
            var forza = Guid.NewGuid();
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", installed: false, installing: true)
                .WithGame(hades, "Hades", installed: false, installing: true)
                .WithGame(forza, "Forza Horizon 5", installed: false, installing: true)
                .Snapshot());

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(forza, "Forza Horizon 5", installed: false, installing: true)
                .WithGame(hades, "Hades", installed: false, installing: true)
                .WithGame(halo, "Halo Infinite", installed: true)
                .Snapshot());

            Assert.Equal(new[] { "Hades", "Forza Horizon 5" }, QueueTitles());
        }

        [Fact]
        public void The_current_download_shows_how_much_is_down_of_the_whole()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            downloads.With(library.Id("33 Immortals"), isActive: true, (long)(67.8 * MB), (long)(3.55 * GB));

            core.OnLibraryChanged(library.Snapshot());

            var entry = core.MyGames.Queue.Single();
            Assert.True(entry.IsCurrent);
            Assert.Same(entry, core.MyGames.CurrentDownload);
            Assert.Equal("Installing...", entry.State);
            Assert.Equal($"{Num(67.8, "0.00")} MB of {Num(3.55, "0.00")} GB", entry.Amount);
            Assert.Equal($"{Num(1.9, "0.0")}%", entry.Percent);
            Assert.Equal(67.8 / (3.55 * 1024), entry.Fraction, 4);
        }

        [Fact]
        public void A_game_next_up_is_queued_with_no_speed_or_amount_and_an_empty_bar()
        {
            var library = new FakeLibrary()
                .WithGame("1000xRESIST", installed: false, installing: true)
                .WithGame("33 Immortals", installed: false, installing: true);
            downloads
                .With(library.Id("1000xRESIST"), isActive: true, 530 * MB, 8 * GB)
                .With(library.Id("33 Immortals"), isActive: false, 0, (long)(3.55 * GB));

            core.OnLibraryChanged(library.Snapshot());

            var next = core.MyGames.Queue[1];
            Assert.False(next.IsCurrent);
            Assert.Equal("Queued", next.State);
            Assert.Null(next.Speed);
            Assert.Null(next.Amount);
            Assert.Equal($"{Num(0, "0.0")}%", next.Percent);
            Assert.Equal(0, next.Fraction);
        }

        [Fact]
        public void A_game_queued_with_some_of_it_already_down_still_reads_0_percent_with_an_empty_bar()
        {
            var library = new FakeLibrary()
                .WithGame("1000xRESIST", installed: false, installing: true)
                .WithGame("33 Immortals", installed: false, installing: true);
            downloads
                .With(library.Id("1000xRESIST"), isActive: true, 530 * MB, 8 * GB)
                .With(library.Id("33 Immortals"), isActive: false, 1 * GB, 4 * GB);

            core.OnLibraryChanged(library.Snapshot());

            var next = core.MyGames.Queue[1];
            Assert.Equal("Queued", next.State);
            Assert.Equal($"{Num(0, "0.0")}%", next.Percent);
            Assert.Equal(0, next.Fraction);
        }

        [Fact]
        public void A_Steam_download_is_the_current_one_ahead_of_a_game_whose_library_cant_say_how_far_it_has_got()
        {
            var library = new FakeLibrary()
                .WithGame("Forza Horizon 5", source: "Xbox", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
            library
                .WithGame("33 Immortals", installed: false, installing: true)
                .WithGame("Hades", installed: false, installing: true);
            downloads
                .With(library.Id("33 Immortals"), isActive: false, 0, 3 * GB)
                .With(library.Id("Hades"), isActive: true, 1 * GB, 2 * GB);

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(new[] { "Hades", "Forza Horizon 5", "33 Immortals" }, QueueTitles());
            Assert.Equal("Hades", core.MyGames.CurrentDownload.Tile.Title);
        }

        [Fact]
        public void Starting_an_install_closes_only_that_games_Game_hub()
        {
            var halo = Guid.NewGuid();
            var hades = Guid.NewGuid();
            var library = new FakeLibrary()
                .WithGame(halo, "Halo Infinite", installed: false)
                .WithGame(hades, "Hades", installed: false);
            core.OnLibraryChanged(library.Snapshot());

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", installed: false)
                .WithGame(hades, "Hades", installed: false, installing: true)
                .Snapshot());

            Assert.Equal(new[] { hades }, playnite.ClosedGameHubs);
        }

        [Fact]
        public void A_game_whose_library_cant_say_how_far_it_has_got_shows_Installing_without_numbers()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Forza Horizon 5", source: "Xbox", installed: false, installing: true).Snapshot());

            var entry = core.MyGames.Queue.Single();
            Assert.True(entry.IsCurrent);
            Assert.Equal("Installing...", entry.State);
            Assert.Null(entry.Speed);
            Assert.Null(entry.Amount);
            Assert.Null(entry.Percent);
            Assert.Equal(0, entry.Fraction);
        }

        [Fact]
        public void With_only_queued_games_there_is_no_current_download()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            downloads.With(library.Id("33 Immortals"), isActive: false, 0, 0);

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal("Queued", core.MyGames.Queue.Single().State);
            Assert.Null(core.MyGames.CurrentDownload);
        }

        [Fact]
        public void A_download_whose_size_isnt_known_yet_shows_no_amount()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            downloads.With(library.Id("33 Immortals"), isActive: true, 0, 0);

            core.OnLibraryChanged(library.Snapshot());

            var entry = core.MyGames.Queue.Single();
            Assert.Null(entry.Amount);
            Assert.Equal($"{Num(0, "0.0")}%", entry.Percent);
            Assert.Equal(0, entry.Fraction);
        }

        [Theory]
        [InlineData(512L * 1024, "0.50 MB")]
        [InlineData(1000L * 1024 * 1024, "1000.00 MB")]
        [InlineData(1L << 30, "1.00 GB")]
        [InlineData(1L << 40, "1.00 TB")]
        public void Sizes_are_in_MB_below_a_GB_and_in_TB_from_1024_GB(long bytes, string size)
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            downloads.With(library.Id("33 Immortals"), isActive: true, bytes, 2L << 40);

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal($"{size.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)} of {Num(2, "0.00")} TB", core.MyGames.Queue.Single().Amount);
        }

        [Fact]
        public void The_queue_follows_the_download_every_second()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: false, 0, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());

            downloads.With(id, isActive: true, 1 * GB, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("Installing...", core.MyGames.Queue.Single().State);
            Assert.Equal($"{Num(25, "0.0")}%", core.MyGames.Queue.Single().Percent);
        }

        [Fact]
        public void The_speed_shows_once_a_second_sample_is_in_in_whole_Mbps()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: true, 100_000_000, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());
            Assert.Null(core.MyGames.Queue.Single().Speed);

            downloads.With(id, isActive: true, 100_000_000 + 13_250_000, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("106 Mbps", core.MyGames.Queue.Single().Speed);
        }

        [Fact]
        public void A_speed_below_1_Mbps_shows_in_Kbps()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: true, 0, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());

            downloads.With(id, isActive: true, 40_375, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("323 Kbps", core.MyGames.Queue.Single().Speed);
        }

        [Fact]
        public void The_speed_is_over_the_time_since_the_last_sample()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: true, 0, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());

            clock.Advance(TimeSpan.FromSeconds(0.5));
            downloads.With(id, isActive: true, 25_000_000, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal("400 Mbps", core.MyGames.Queue.Single().Speed);
        }

        [Fact]
        public void A_queued_games_tile_stays_the_same_tile_while_it_reads_the_same_so_its_focus_and_art_stay()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: true, 0, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());
            var tile = core.MyGames.Queue.Single().Tile;

            downloads.With(id, isActive: true, 1 * GB, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Same(tile, core.MyGames.Queue.Single().Tile);

            downloads.With(id, isActive: false, 1 * GB, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal("Queued", core.MyGames.Queue.Single().Tile.Status);
        }

        [Fact]
        public void Steam_writing_its_progress_every_few_seconds_keeps_the_last_speed_and_measures_over_the_whole_gap()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: true, 0, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());
            downloads.With(id, isActive: true, 12_500_000, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal("100 Mbps", core.MyGames.Queue.Single().Speed);

            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal("100 Mbps", core.MyGames.Queue.Single().Speed);

            downloads.With(id, isActive: true, 12_500_000 + 3 * 25_000_000, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal("200 Mbps", core.MyGames.Queue.Single().Speed);
        }

        [Fact]
        public void A_download_whose_progress_hasnt_moved_for_over_10_seconds_shows_0_Kbps()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: true, 0, 4 * GB);
            core.OnLibraryChanged(library.Snapshot());
            downloads.With(id, isActive: true, 12_500_000, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));

            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal("100 Mbps", core.MyGames.Queue.Single().Speed);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal("0 Kbps", core.MyGames.Queue.Single().Speed);
        }

        [Fact]
        public void While_the_page_is_up_the_storage_ring_follows_the_download()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", installDirectory: @"C:\Games\Halo")
                .WithGame("33 Immortals", installed: false, installing: true);
            drives.WithDrive(@"C:\", 190 * GB, 500 * GB);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);

            drives.WithDrive(@"C:\", 186 * GB, 500 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal($"{Num(186, "0.0")} GB free", core.MyGames.Storage.Free);
        }

        [Fact]
        public void With_nothing_installing_nothing_is_followed()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", installDirectory: @"C:\Games\Halo");
            drives.WithDrive(@"C:\", 190 * GB, 500 * GB);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);

            drives.WithDrive(@"C:\", 186 * GB, 500 * GB);
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal($"{Num(190, "0.0")} GB free", core.MyGames.Storage.Free);
        }

        [Fact]
        public void Following_stops_when_the_last_game_finishes_and_starts_again_with_the_next()
        {
            var halo = Guid.NewGuid();
            var forza = Guid.NewGuid();
            drives.WithDrive(@"C:\", 190 * GB, 500 * GB);
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", installDirectory: @"C:\Games\Halo", installing: true)
                .Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", installDirectory: @"C:\Games\Halo")
                .Snapshot());

            drives.WithDrive(@"C:\", 186 * GB, 500 * GB);
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal($"{Num(190, "0.0")} GB free", core.MyGames.Storage.Free);

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", installDirectory: @"C:\Games\Halo")
                .WithGame(forza, "Forza Horizon 5", installed: false, installing: true)
                .Snapshot());
            drives.WithDrive(@"C:\", 180 * GB, 500 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal($"{Num(180, "0.0")} GB free", core.MyGames.Storage.Free);
        }

        [Fact]
        public void Tiles_of_games_installing_read_Queued_or_Installing()
        {
            var library = new FakeLibrary()
                .WithGame("1000xRESIST", installed: false, installing: true, favorite: true)
                .WithGame("33 Immortals", installed: false, installing: true, lastActivity: new DateTime(2026, 10, 16))
                .WithGame("Halo Infinite", favorite: true);
            downloads
                .With(library.Id("1000xRESIST"), isActive: true, 530 * MB, 8 * GB)
                .With(library.Id("33 Immortals"), isActive: false, 0, 3 * GB);

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal("Installing...", RecentRowTile("1000xRESIST").Status);
            Assert.Equal("Queued", RecentRowTile("33 Immortals").Status);
            Assert.Null(RecentRowTile("Halo Infinite").Status);
            Assert.Equal("Installing...", core.Home.Groups.Single().Tiles.Single(t => t.Title == "1000xRESIST").Status);
            Assert.Equal("Queued", core.MyGames.PlayHistory.SelectMany(s => s.Entries).Single().Tile.Status);
            Assert.Equal(new[] { "Installing...", "Queued" }, core.MyGames.Queue.Select(e => e.Tile.Status));
        }

        [Fact]
        public void A_tile_reads_Installing_once_its_download_starts()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: false, 0, 3 * GB);
            core.OnLibraryChanged(library.Snapshot());
            Assert.Equal("Queued", RecentRowTile("33 Immortals").Status);

            downloads.With(id, isActive: true, 0, 3 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("Installing...", RecentRowTile("33 Immortals").Status);
        }

        [Fact]
        public void Home_keeps_its_rows_while_no_tiles_state_changes()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            var id = library.Id("33 Immortals");
            downloads.With(id, isActive: true, 0, 3 * GB);
            core.OnLibraryChanged(library.Snapshot());
            var row = core.Home.RecentRow;
            var playHistory = core.MyGames.PlayHistory;

            downloads.With(id, isActive: true, 1 * GB, 3 * GB);
            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Same(row, core.Home.RecentRow);
            Assert.Same(playHistory, core.MyGames.PlayHistory);
        }

        [Fact]
        public void Starting_an_install_returns_from_the_Game_hub()
        {
            var id = Guid.NewGuid();
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "33 Immortals", installed: false).Snapshot());

            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "33 Immortals", installed: false, installing: true).Snapshot());
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "33 Immortals", installed: false, installing: true).Snapshot());

            Assert.Equal(new[] { id }, playnite.ClosedGameHubs);
        }

        [Fact]
        public void Games_already_installing_as_Playnite_starts_leave_the_Game_hub_alone()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true).Snapshot());

            Assert.Empty(playnite.ClosedGameHubs);
        }

        [Fact]
        public void Manage_and_Manage_Queue_show_their_own_content_and_leave_the_main_list_alone()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite").Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            var gamesList = playnite.MainListFilter;

            core.OpenMyGames(MyGamesItem.Manage);
            Assert.Equal(MyGamesItem.Manage, core.MyGames.Item);
            Assert.False(core.MyGames.ShowsGameGrid);

            core.OpenMyGames(MyGamesItem.ManageQueue);
            Assert.Equal(MyGamesItem.ManageQueue, core.MyGames.Item);
            Assert.False(core.MyGames.ShowsGameGrid);
            Assert.Same(gamesList, playnite.MainListFilter);
        }

        [Fact]
        public void Manage_and_Manage_Queue_opened_first_take_no_hold_of_the_main_list()
        {
            core.OpenMyGames(MyGamesItem.Manage);
            core.OpenMyGames(MyGamesItem.ManageQueue);
            Assert.True(playnite.ShowsUsersOwnFilter);

            core.LeaveMyGames();
            core.LeaveMyGames();

            Assert.False(core.MyGames.IsShown);
            Assert.Equal(0, playnite.UserFilterRestores);
        }

        [Fact]
        public void Manage_Queue_opens_with_nothing_queued()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite").Snapshot());

            core.OpenMyGames(MyGamesItem.ManageQueue);

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.ManageQueue, core.MyGames.Item);
            Assert.False(core.MyGames.HasQueue);
        }

        [Fact]
        public void B_on_Manage_Queue_goes_back_to_Manage_and_B_on_Manage_leaves_the_page()
        {
            core.OpenMyGames(MyGamesItem.Manage);
            core.OpenMyGames(MyGamesItem.ManageQueue);

            core.LeaveMyGames();
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Manage, core.MyGames.Item);

            core.LeaveMyGames();
            Assert.False(core.MyGames.IsShown);
        }

        [Fact]
        public void Recently_installed_lists_up_to_8_installed_games_newest_added_first_without_hidden_ones()
        {
            var library = new FakeLibrary()
                .WithGame("Hidden Game", added: new DateTime(2026, 10, 15), hidden: true)
                .WithGame("Not Installed", added: new DateTime(2026, 10, 15), installed: false);
            for (var day = 1; day <= 12; day++)
            {
                library.WithGame("Game " + day, added: new DateTime(2026, 10, day));
            }

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(
                Enumerable.Range(5, 8).Reverse().Select(day => "Game " + day),
                core.MyGames.RecentlyInstalled.Select(t => t.Title));
        }

        [Fact]
        public void Recently_installed_orders_games_by_when_they_were_installed_before_when_they_were_added()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Left 4 Dead 2", added: new DateTime(2026, 10, 16), installedAt: new DateTime(2026, 10, 6))
                .WithGame("Half-Life 2", added: new DateTime(2020, 1, 1), installedAt: new DateTime(2026, 10, 17, 2, 16, 0))
                .WithGame("Fall Guys", added: new DateTime(2024, 1, 1), installedAt: new DateTime(2026, 10, 17, 2, 1, 0))
                .WithGame("No Folder", added: new DateTime(2026, 10, 10))
                .Snapshot());

            Assert.Equal(new[] { "Half-Life 2", "Fall Guys", "No Folder", "Left 4 Dead 2" }, core.MyGames.RecentlyInstalled.Select(t => t.Title));
        }

        [Fact]
        public void Games_that_finish_installing_head_Recently_installed_the_latest_first()
        {
            var halo = Guid.NewGuid();
            var hades = Guid.NewGuid();
            var forza = Guid.NewGuid();
            var added = new DateTime(2020, 1, 1);
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", added: added, installed: false, installing: true)
                .WithGame(hades, "Hades", added: added, installed: false, installing: true)
                .WithGame(forza, "Forza Horizon 5", added: new DateTime(2026, 10, 1))
                .Snapshot());

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", added: added, installed: true)
                .WithGame(hades, "Hades", added: added, installed: false, installing: true)
                .WithGame(forza, "Forza Horizon 5", added: new DateTime(2026, 10, 1))
                .Snapshot());
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(halo, "Halo Infinite", added: added, installed: true)
                .WithGame(hades, "Hades", added: added, installed: true)
                .WithGame(forza, "Forza Horizon 5", added: new DateTime(2026, 10, 1))
                .Snapshot());

            Assert.Equal(new[] { "Hades", "Halo Infinite", "Forza Horizon 5" }, core.MyGames.RecentlyInstalled.Select(t => t.Title));
        }

        [Fact]
        public void View_on_a_queued_game_on_Manage_Queue_opens_Manage_game()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.ManageQueue);
            core.FocusHomeTile(core.MyGames.Queue.Single().Tile);

            Tap(ControllerButton.Back);

            Assert.Equal(new[] { library.Id("33 Immortals") }, playnite.OpenedGameMenus);
        }

        [Fact]
        public void View_on_a_Recently_installed_cover_opens_Manage_game()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.ManageQueue);
            core.FocusHomeTile(core.MyGames.RecentlyInstalled.Single());

            Tap(ControllerButton.Back);

            Assert.Equal(new[] { library.Id("Halo Infinite") }, playnite.OpenedGameMenus);
        }

        [Fact]
        public void View_on_a_tile_anywhere_else_does_nothing()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite").Snapshot());
            core.FocusHomeTile(core.Home.RecentRow.Single());

            Tap(ControllerButton.Back);

            Assert.Empty(playnite.OpenedGameMenus);
        }

        [Fact]
        public void Menu_on_a_queued_game_opens_its_tile_menu_and_A_its_Game_hub()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.ManageQueue);
            core.FocusHomeTile(core.MyGames.Queue.Single().Tile);

            Tap(ControllerButton.Start);
            Assert.Equal("33 Immortals", core.Home.TileMenu.Title);
            Tap(ControllerButton.B);

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(new[] { library.Id("33 Immortals") }, playnite.OpenedHubs);
        }

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }

        private HomeTile RecentRowTile(string title) => core.Home.RecentRow.Single(t => t.Title == title);

        private string[] QueueTitles() => core.MyGames.Queue.Select(e => e.Tile.Title).ToArray();

        /// <summary>A number to <paramref name="format"/>, as the user's culture writes it.</summary>
        private static string Num(double value, string format) => value.ToString(format, CultureInfo.CurrentCulture);
    }
}
