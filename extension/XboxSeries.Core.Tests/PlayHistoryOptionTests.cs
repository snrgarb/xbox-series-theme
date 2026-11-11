using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Settings' Show Play history (#210): whether Home's Recent row ends in the Play history tile. On until set; off, the
    // row holds eight games and no tile, as the console lets the tile be hidden. In force at once, saved at once, shared
    // by every profile.
    public class PlayHistoryOptionTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 11, 11, 20, 0, 0));
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private ExtensionCore core;

        public PlayHistoryOptionTests()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same store, as when Playnite starts again, with twelve played games.</summary>
        private void NewCore()
        {
            var windows = new FakeWindowSystem();
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, new FakeDownloads());
            var library = new FakeLibrary();
            for (var day = 1; day <= 12; day++)
            {
                library.WithGame($"Game {day}", lastActivity: new DateTime(2026, 10, day));
            }

            core.OnLibraryChanged(library.Snapshot());
        }

        private bool ShowPlayHistory => core.Settings.StartUp.ShowPlayHistory;

        private IEnumerable<string> RecentRow() => core.Home.RecentRow.Select(t => t.Title);

        /// <summary>The newest <paramref name="count"/> of the twelve, newest first.</summary>
        private static IEnumerable<string> Newest(int count) => Enumerable.Range(13 - count, count).Reverse().Select(day => $"Game {day}");

        [Fact]
        public void With_nothing_saved_it_is_on_and_nothing_goes_to_the_store()
        {
            Assert.True(StartUpSettingsState.ShowPlayHistoryByDefault);
            Assert.True(ShowPlayHistory);
            Assert.False(secrets.Values.ContainsKey("StartUp"));
        }

        [Fact]
        public void On_the_Recent_row_has_seven_games_and_the_Play_history_tile()
        {
            Assert.Equal(Newest(7), RecentRow());
            Assert.True(core.Home.HasPlayHistoryTile);
            Assert.Equal(new[] { "Game 5", "Game 4", "Game 3", "Game 2" }, core.Home.PlayHistory.Select(t => t.Title));
        }

        [Fact]
        public void Off_the_Recent_row_has_eight_games_and_no_Play_history_tile_at_once()
        {
            core.SetShowPlayHistory(false);

            Assert.False(ShowPlayHistory);
            Assert.Equal(Newest(8), RecentRow());
            Assert.False(core.Home.HasPlayHistoryTile);
        }

        [Fact]
        public void Turned_on_again_the_tile_comes_back_and_the_eighth_game_goes_without_a_restart()
        {
            core.SetShowPlayHistory(false);
            core.SetShowPlayHistory(true);

            Assert.Equal(Newest(7), RecentRow());
            Assert.True(core.Home.HasPlayHistoryTile);
        }

        [Fact]
        public void Off_the_Play_history_collage_is_not_built_and_on_again_it_is_built_afresh()
        {
            core.SetShowPlayHistory(false);
            var changed = new List<string>();
            core.Home.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            var library = new FakeLibrary();
            for (var day = 1; day <= 13; day++)
            {
                library.WithGame($"Game {day}", lastActivity: new DateTime(2026, 10, day));
            }

            core.OnLibraryChanged(library.Snapshot());

            Assert.DoesNotContain(nameof(HomeState.PlayHistory), changed);

            core.SetShowPlayHistory(true);

            Assert.Equal(new[] { "Game 6", "Game 5", "Game 4", "Game 3" }, core.Home.PlayHistory.Select(t => t.Title));
        }

        [Fact]
        public void Set_again_to_what_the_player_chose_it_saves_nothing_more()
        {
            core.SetShowPlayHistory(false);
            var writes = secrets.Writes;

            core.SetShowPlayHistory(false);

            Assert.Equal(writes, secrets.Writes);
        }

        [Fact]
        public void Set_on_while_on_only_by_default_it_is_saved_as_chosen()
        {
            core.SetShowPlayHistory(true);

            Assert.Contains("\"ShowPlayHistory\":true", secrets.Values["StartUp"]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_is_saved_at_once_and_kept_across_a_restart(bool on)
        {
            core.SetShowPlayHistory(on);

            Assert.Contains(on ? "\"ShowPlayHistory\":true" : "\"ShowPlayHistory\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.Equal(on, ShowPlayHistory);
            Assert.Equal(Newest(on ? 7 : 8), RecentRow());
            Assert.Equal(on, core.Home.HasPlayHistoryTile);
        }

        [Fact]
        public void It_is_shared_by_every_profile()
        {
            var first = core.Settings.StartUp.Profiles[0];
            var second = core.AddProfile("Player Two");
            core.SetShowPlayHistory(false);

            NewCore();

            Assert.Equal(new[] { first.Id, second.Id }, core.Settings.StartUp.Profiles.Select(p => p.Id));
            Assert.False(ShowPlayHistory);
        }

        [Fact]
        public void Saving_another_Personalisation_option_leaves_it_unset_so_the_default_stands()
        {
            core.SetReduceMotion(true);
            core.SetBackgroundDelay(false);

            Assert.DoesNotContain("\"ShowPlayHistory\":true", secrets.Values["StartUp"]);
            Assert.DoesNotContain("\"ShowPlayHistory\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.True(ShowPlayHistory);
        }

        [Fact]
        public void It_is_kept_when_another_Personalisation_option_is_saved_after_it()
        {
            core.SetShowPlayHistory(false);
            core.SetReduceMotion(true);

            NewCore();

            Assert.False(ShowPlayHistory);
            Assert.True(core.Settings.StartUp.ReduceMotion);
        }

        [Fact]
        public void Setting_it_tells_the_view_once_and_not_when_it_is_set_to_what_it_is()
        {
            var changed = new List<string>();
            core.Settings.StartUp.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            var home = new List<string>();
            core.Home.PropertyChanged += (_, e) => home.Add(e.PropertyName);

            core.SetShowPlayHistory(false);
            core.SetShowPlayHistory(false);

            Assert.Equal(new[] { nameof(StartUpSettingsState.ShowPlayHistory) }, changed);
            Assert.Equal(1, home.Count(p => p == nameof(HomeState.HasPlayHistoryTile)));
            Assert.Equal(1, home.Count(p => p == nameof(HomeState.RecentRow)));
        }
    }
}
