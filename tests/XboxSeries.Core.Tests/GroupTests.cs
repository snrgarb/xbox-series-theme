using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class GroupTests
    {
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly ExtensionCore core;

        public GroupTests()
        {
            core = new ExtensionCore(new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0)), windows, new FakePlayniteHost(windows));
        }

        [Fact]
        public void Each_preset_flagged_for_Fullscreen_quick_selection_is_a_Group_in_Playnites_order()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithFilterPreset("Shooters", showInFullscreenQuickSelection: true, games: new[] { "Halo Infinite" })
                .WithFilterPreset("Not flagged", games: new[] { "Halo Infinite" })
                .WithFilterPreset("Couch co-op", showInFullscreenQuickSelection: true)
                .Snapshot());

            Assert.Equal(new[] { "Shooters", "Couch co-op" }, core.Home.Groups.Select(g => g.Name));
        }

        [Fact]
        public void A_Group_shows_the_games_its_preset_matches()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithGame("Hades")
                .WithGame("Forza Horizon 5")
                .WithFilterPreset("Shooters", showInFullscreenQuickSelection: true, games: new[] { "Halo Infinite" });

            core.OnLibraryChanged(library.Snapshot());

            var group = core.Home.Groups.Single();
            Assert.Equal(library.PresetId("Shooters"), group.FilterPresetId);
            Assert.Equal(new[] { "Halo Infinite" }, group.Tiles.Select(t => t.Title));
        }

        [Fact]
        public void A_Group_without_a_sort_of_its_own_is_in_name_order()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Hades")
                .WithGame("forza Horizon 5")
                .WithGame("Halo Infinite")
                .WithFilterPreset("All", showInFullscreenQuickSelection: true, games: new[] { "Hades", "forza Horizon 5", "Halo Infinite" })
                .Snapshot());

            Assert.Equal(new[] { "forza Horizon 5", "Hades", "Halo Infinite" }, GroupTitles("All"));
        }

        [Fact]
        public void Name_order_follows_a_games_sorting_name_when_it_has_one()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("The Witcher 3", sortingName: "Witcher 3")
                .WithGame("Hades")
                .WithGame("Tetris Effect")
                .WithFilterPreset("All", showInFullscreenQuickSelection: true, games: new[] { "The Witcher 3", "Hades", "Tetris Effect" })
                .Snapshot());

            Assert.Equal(new[] { "Hades", "Tetris Effect", "The Witcher 3" }, GroupTitles("All"));
        }

        [Fact]
        public void A_Group_uses_its_presets_own_sort()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11))
                .WithGame("Never Played")
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Forza Horizon 5", lastActivity: new DateTime(2026, 10, 10))
                .WithFilterPreset(
                    "Recently played",
                    showInFullscreenQuickSelection: true,
                    games: new[] { "Hades", "Never Played", "Halo Infinite", "Forza Horizon 5" },
                    sort: GameSort.LastActivity,
                    direction: SortDirection.Descending)
                .Snapshot());

            Assert.Equal(new[] { "Halo Infinite", "Hades", "Forza Horizon 5", "Never Played" }, GroupTitles("Recently played"));
        }

        [Theory]
        [InlineData(GameSort.Added)]
        [InlineData(GameSort.Modified)]
        [InlineData(GameSort.ReleaseDate)]
        [InlineData(GameSort.Playtime)]
        [InlineData(GameSort.PlayCount)]
        public void A_Group_sorts_by_any_of_the_supported_fields_with_ties_in_name_order(GameSort sort)
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Small B", added: new DateTime(2026, 1, 1), modified: new DateTime(2026, 1, 1), releaseDate: new DateTime(2001, 1, 1), playtime: 10, playCount: 1)
                .WithGame("Big", added: new DateTime(2026, 6, 1), modified: new DateTime(2026, 6, 1), releaseDate: new DateTime(2020, 1, 1), playtime: 900, playCount: 9)
                .WithGame("Small A", added: new DateTime(2026, 1, 1), modified: new DateTime(2026, 1, 1), releaseDate: new DateTime(2001, 1, 1), playtime: 10, playCount: 1)
                .WithFilterPreset("Sorted", showInFullscreenQuickSelection: true, games: new[] { "Small B", "Big", "Small A" }, sort: sort)
                .Snapshot());

            Assert.Equal(new[] { "Small A", "Small B", "Big" }, GroupTitles("Sorted"));
        }

        [Fact]
        public void A_Group_shows_at_most_10_games_before_See_all()
        {
            var library = new FakeLibrary();
            var names = Enumerable.Range(1, 14).Select(i => $"Game {i:00}").ToArray();
            foreach (var name in names)
            {
                library.WithGame(name);
            }

            core.OnLibraryChanged(library.WithFilterPreset("Everything", showInFullscreenQuickSelection: true, games: names).Snapshot());

            Assert.Equal(names.Take(10), GroupTitles("Everything"));
        }

        [Fact]
        public void With_no_preset_flagged_Home_has_a_Favourites_Group_of_favourite_games_in_name_order_without_hidden_ones()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", favorite: true)
                .WithGame("Hades")
                .WithGame("Forza Horizon 5", favorite: true)
                .WithGame("Hidden Favourite", favorite: true, hidden: true)
                .WithFilterPreset("Not flagged", games: new[] { "Hades" })
                .Snapshot());

            var group = core.Home.Groups.Single();
            Assert.Equal("Favourites", group.Name);
            Assert.Null(group.FilterPresetId);
            Assert.Equal(new[] { "Forza Horizon 5", "Halo Infinite" }, group.Tiles.Select(t => t.Title));
        }

        [Fact]
        public void The_Favourites_Group_appears_even_before_anything_is_a_favourite()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Hades").Snapshot());

            var group = core.Home.Groups.Single();
            Assert.Equal("Favourites", group.Name);
            Assert.Empty(group.Tiles);
        }

        [Fact]
        public void The_Favourites_Group_holds_at_most_10_games()
        {
            var library = new FakeLibrary();
            for (var i = 1; i <= 12; i++)
            {
                library.WithGame($"Game {i:00}", favorite: true);
            }

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(10, core.Home.Groups.Single().Tiles.Count);
        }

        [Fact]
        public void Flagging_a_preset_replaces_the_Favourites_Group()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", favorite: true).Snapshot());

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", favorite: true)
                .WithFilterPreset("Shooters", showInFullscreenQuickSelection: true, games: new[] { "Halo Infinite" })
                .Snapshot());

            Assert.Equal(new[] { "Shooters" }, core.Home.Groups.Select(g => g.Name));
        }

        [Fact]
        public void A_running_game_is_marked_in_its_Groups()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", favorite: true)
                .WithGame("Hades", favorite: true);
            core.OnLibraryChanged(library.Snapshot());

            core.OnGameStarted(library.Id("Hades"));

            Assert.Equal(new[] { "Hades" }, core.Home.Groups.Single().Tiles.Where(t => t.IsRunning).Select(t => t.Title));
        }

        private string[] GroupTitles(string group) =>
            core.Home.Groups.Single(g => g.Name == group).Tiles.Select(t => t.Title).ToArray();
    }
}
