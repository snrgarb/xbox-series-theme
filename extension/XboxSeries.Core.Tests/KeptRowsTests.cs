using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // A library update pushes the library several times a second, mostly with nothing a tile shows changed. A new list
    // rebuilds every tile in its row and reads every cover again, so Home's rows, Play history and Recently installed
    // stay the same lists while they would show the same, and are replaced as soon as anything they show changes.
    public class KeptRowsTests
    {
        private static readonly DateTime LastPlayed = new DateTime(2026, 10, 12, 21, 0, 0);

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly Guid halo = Guid.NewGuid();
        private readonly Guid[] others = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        private readonly Guid shooters = Guid.NewGuid();
        private readonly ExtensionCore core;

        public KeptRowsTests()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
        }

        [Fact]
        public void A_push_with_nothing_changed_keeps_every_list()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();
            Assert.Equal(7, core.Home.RecentRow.Count);
            Assert.Equal(2, core.Home.PlayHistory.Count);
            Assert.Equal(3, core.Home.Groups.Single().SeeAll.Count);
            Assert.Equal(8, core.MyGames.RecentlyInstalled.Count);

            core.OnLibraryChanged(Library().Snapshot());

            AssertKept(before, "RecentRow", "Collage", "Groups", "PlayHistory", "RecentlyInstalled");
        }

        [Fact]
        public void A_push_with_nothing_changed_keeps_the_default_Favourites_Group()
        {
            core.OnLibraryChanged(Library(preset: null).Snapshot());
            var before = Lists();
            Assert.Equal("Favourites", core.Home.Groups.Single().Name);

            core.OnLibraryChanged(Library(preset: null).Snapshot());

            AssertKept(before, "RecentRow", "Collage", "Groups", "PlayHistory", "RecentlyInstalled");
        }

        [Fact]
        public void A_new_name_replaces_the_lists_showing_the_game()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(name: "Halo Infinite: Campaign").Snapshot());

            AssertKept(before, "Collage");
            Assert.Equal("Halo Infinite: Campaign", core.Home.RecentRow[0].Title);
            Assert.Contains("Halo Infinite: Campaign", core.Home.Groups.Single().SeeAll.Select(t => t.Title));
            Assert.Equal("Halo Infinite: Campaign", core.MyGames.PlayHistory[0].Entries[0].Tile.Title);
            Assert.Equal("Halo Infinite: Campaign", core.MyGames.RecentlyInstalled[0].Title);
        }

        [Fact]
        public void A_new_cover_replaces_the_lists_showing_the_game()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(cover: @"C:\covers\halo-new.jpg").Snapshot());

            AssertKept(before, "Collage");
            Assert.Equal(@"C:\covers\halo-new.jpg", core.Home.RecentRow[0].CoverImage);
            Assert.Contains(@"C:\covers\halo-new.jpg", core.Home.Groups.Single().SeeAll.Select(t => t.CoverImage));
            Assert.Equal(@"C:\covers\halo-new.jpg", core.MyGames.PlayHistory[0].Entries[0].Tile.CoverImage);
            Assert.Equal(@"C:\covers\halo-new.jpg", core.MyGames.RecentlyInstalled[0].CoverImage);
        }

        [Fact]
        public void A_new_source_replaces_the_lists_showing_the_game()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(source: "Xbox").Snapshot());

            AssertKept(before, "Collage");
            Assert.Equal("Xbox", core.Home.RecentRow[0].Source);
            Assert.Contains("Xbox", core.Home.Groups.Single().SeeAll.Select(t => t.Source));
            Assert.Equal("Xbox", core.MyGames.PlayHistory[0].Entries[0].Tile.Source);
            Assert.Equal("Xbox", core.MyGames.RecentlyInstalled[0].Source);
        }

        [Fact]
        public void A_game_starting_replaces_the_lists_marking_it_as_running()
        {
            core.OnLibraryChanged(Library(direction: SortDirection.Descending).Snapshot());
            var before = Lists();

            core.OnGameStarted(halo);

            // Recently installed is only made again by a library push.
            AssertKept(before, "Collage", "RecentlyInstalled");
            Assert.True(core.Home.RecentRow[0].IsRunning);
            Assert.True(core.Home.Groups.Single().Tiles[0].IsRunning);
            Assert.True(core.MyGames.PlayHistory[0].Entries[0].Tile.IsRunning);
        }

        [Fact]
        public void A_game_installing_replaces_the_lists_showing_it()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(installed: false, installing: true).Snapshot());

            AssertKept(before, "Collage");
            Assert.NotNull(core.Home.RecentRow[0].Status);
            Assert.Contains(core.Home.Groups.Single().SeeAll, t => t.Status != null);
            Assert.NotNull(core.MyGames.PlayHistory[0].Entries[0].Tile.Status);
            Assert.DoesNotContain("Halo Infinite", core.MyGames.RecentlyInstalled.Select(t => t.Title));
        }

        [Fact]
        public void A_game_played_again_without_changing_place_replaces_only_Play_history()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(played: LastPlayed.AddHours(1)).Snapshot());

            AssertKept(before, "RecentRow", "Collage", "Groups", "RecentlyInstalled");
            Assert.Equal(LastPlayed.AddHours(1), core.MyGames.PlayHistory[0].Entries[0].LastPlayed);
        }

        [Fact]
        public void A_game_changing_place_among_those_played_replaces_the_Recent_row_its_collage_and_Play_history()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(played: new DateTime(2026, 9, 1)).Snapshot());

            AssertKept(before, "Groups", "RecentlyInstalled");
            Assert.Equal("Game 8", core.Home.RecentRow[0].Title);
            Assert.Equal(new[] { "Game 1", "Halo Infinite" }, core.Home.PlayHistory.Select(t => t.Title));
            Assert.Equal("Halo Infinite", core.MyGames.PlayHistory.Last().Entries.Last().Tile.Title);
        }

        [Fact]
        public void A_new_platform_replaces_only_Play_history()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(platform: "Xbox Series").Snapshot());

            AssertKept(before, "RecentRow", "Collage", "Groups", "RecentlyInstalled");
            Assert.Equal("Xbox Series", core.MyGames.PlayHistory[0].Entries[0].Platform);
        }

        [Fact]
        public void A_new_day_replaces_Play_history_as_its_sections_change()
        {
            core.OnLibraryChanged(Library(played: new DateTime(2026, 10, 14, 9, 0, 0)).Snapshot());
            var before = Lists();
            Assert.Equal("Today", core.MyGames.PlayHistory[0].Title);

            clock.Advance(TimeSpan.FromHours(5));
            core.OnLibraryChanged(Library(played: new DateTime(2026, 10, 14, 9, 0, 0)).Snapshot());

            AssertKept(before, "RecentRow", "Collage", "Groups", "RecentlyInstalled");
            Assert.Equal("Yesterday", core.MyGames.PlayHistory[0].Title);
        }

        [Fact]
        public void A_game_no_longer_a_favourite_replaces_only_the_Favourites_Group()
        {
            core.OnLibraryChanged(Library(preset: null).Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(preset: null, favorite: false).Snapshot());

            AssertKept(before, "RecentRow", "Collage", "PlayHistory", "RecentlyInstalled");
            Assert.DoesNotContain("Halo Infinite", core.Home.Groups.Single().Tiles.Concat(core.Home.Groups.Single().SeeAll).Select(t => t.Title));
        }

        [Fact]
        public void A_Groups_new_name_replaces_only_the_Groups()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(preset: "First-person shooters").Snapshot());

            AssertKept(before, "RecentRow", "Collage", "PlayHistory", "RecentlyInstalled");
            Assert.Equal("First-person shooters", core.Home.Groups.Single().Name);
        }

        [Fact]
        public void A_Groups_new_order_replaces_only_the_Groups()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(direction: SortDirection.Descending).Snapshot());

            AssertKept(before, "RecentRow", "Collage", "PlayHistory", "RecentlyInstalled");
            Assert.Equal("Halo Infinite", core.Home.Groups.Single().Tiles[0].Title);
        }

        [Fact]
        public void Another_preset_becoming_the_Group_replaces_only_the_Groups()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var before = Lists();

            core.OnLibraryChanged(Library(presetId: Guid.NewGuid()).Snapshot());

            AssertKept(before, "RecentRow", "Collage", "PlayHistory", "RecentlyInstalled");
            Assert.NotEqual(shooters, core.Home.Groups.Single().FilterPresetId);
        }

        /// <summary>
        /// Halo Infinite, played last and installed last, as given; eight more games played on the eight days of October before it, so the
        /// Recent row is full and its collage has two; and a Group of all nine in name order, three of them under See all,
        /// or without <paramref name="preset"/> the default Favourites Group of the same.
        /// </summary>
        private FakeLibrary Library(
            string name = "Halo Infinite",
            DateTime? played = null,
            string cover = @"C:\covers\halo.jpg",
            string source = "Steam",
            string platform = "PC",
            bool favorite = true,
            bool installed = true,
            bool installing = false,
            string preset = "Shooters",
            Guid? presetId = null,
            SortDirection direction = SortDirection.Ascending)
        {
            var library = new FakeLibrary().WithGame(
                halo, name, lastActivity: played ?? LastPlayed, added: new DateTime(2026, 10, 1), installed: installed, source: source,
                coverImage: cover, favorite: favorite, platform: platform, installing: installing);
            for (var i = 0; i < others.Length; i++)
            {
                library.WithGame(
                    others[i], $"Game {i + 1}", lastActivity: new DateTime(2026, 10, i + 1), source: "Steam", coverImage: $@"C:\covers\{i + 1}.jpg",
                    favorite: true, platform: "PC");
            }

            if (preset != null)
            {
                library.WithFilterPreset(
                    presetId ?? shooters, preset, showInFullscreenQuickSelection: true,
                    games: new[] { name }.Concat(others.Select((_, i) => $"Game {i + 1}")).ToArray(), direction: direction);
            }

            return library;
        }

        /// <summary>The lists the views bind to, as they are now.</summary>
        private Dictionary<string, object> Lists() => new Dictionary<string, object>
        {
            ["RecentRow"] = core.Home.RecentRow,
            ["Collage"] = core.Home.PlayHistory,
            ["Groups"] = core.Home.Groups,
            ["PlayHistory"] = core.MyGames.PlayHistory,
            ["RecentlyInstalled"] = core.MyGames.RecentlyInstalled,
        };

        /// <summary>The lists named in <paramref name="kept"/> are the objects they were <paramref name="before"/>; every other is a new one.</summary>
        private void AssertKept(Dictionary<string, object> before, params string[] kept)
        {
            var now = Lists();
            foreach (var list in before)
            {
                Assert.True(
                    kept.Contains(list.Key) == ReferenceEquals(list.Value, now[list.Key]),
                    kept.Contains(list.Key) ? $"{list.Key} was replaced" : $"{list.Key} was kept");
            }
        }
    }
}
