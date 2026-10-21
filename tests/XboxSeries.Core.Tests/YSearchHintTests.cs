using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // "Y Search" on the hint bar, right of More options, wherever Y opens Search: Home's Group tiles, beside More options,
    // Home's top bar, alone, and a game in My games & apps' Games grid, beside More options (#69).
    public class YSearchHintTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 18, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public YSearchHintTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), favorite: true)
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10), favorite: true);
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void Homes_top_bar_with_focus_shows_Y_Search_alone()
        {
            core.FocusTopBar();

            var hint = Assert.Single(core.Home.Hints);
            Assert.Equal(ControllerButton.Y, hint.Button);
            Assert.Equal("Search", hint.Text);
        }

        [Fact]
        public void A_Group_tile_with_focus_shows_Y_Search_right_of_More_options()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            Assert.Equal(new[] { ControllerButton.Start, ControllerButton.Y }, core.Home.Hints.Select(h => h.Button));
            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        [Fact]
        public void Search_opened_from_the_top_bar_takes_Y_Search_away_with_Home()
        {
            core.FocusTopBar();

            core.OpenSearch();

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Settings_opened_from_the_top_bar_takes_Y_Search_away_with_Home()
        {
            core.FocusTopBar();

            core.OpenSettings();

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void My_games_and_apps_opened_from_the_top_bar_takes_Y_Search_away_with_Home()
        {
            core.FocusTopBar();

            core.OpenMyGames(MyGamesItem.Apps);

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Back_on_Home_the_top_bar_shows_Y_Search_again()
        {
            core.FocusTopBar();
            core.OpenSettings();

            core.LeaveSettings();

            Assert.Equal(new[] { "Search" }, HintTexts());
        }

        [Fact]
        public void Focus_leaving_the_top_bar_for_Homes_rows_hides_Y_Search()
        {
            core.FocusTopBar();

            core.LeaveTopBar();

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Focus_moving_from_the_top_bar_down_to_a_Group_tile_shows_both()
        {
            core.FocusTopBar();

            core.LeaveTopBar();
            core.FocusHomeTile(GroupTile("Tunic"));

            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        [Fact]
        public void The_Recent_row_shows_no_hint()
        {
            core.FocusHomeTile(RecentTile("Hades"));

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_Guide_over_the_top_bar_hides_Y_Search_until_it_has_handed_focus_back()
        {
            core.FocusTopBar();

            Tap(ControllerButton.Guide);

            Assert.Empty(core.Home.Hints);

            Tap(ControllerButton.B);

            Assert.Equal(new[] { "Search" }, HintTexts());
        }

        [Fact]
        public void A_prompt_over_the_top_bar_hides_Y_Search()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            core.FocusTopBar();

            core.PlayGame(library.Id("Hades"));

            Assert.NotNull(core.Guide.Dialog);
            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Start_up_hides_Y_Search()
        {
            core.FocusTopBar();

            core.OnStartup();

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void A_game_in_Games_grid_shows_Y_Search_right_of_More_options()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.FocusGameGrid();

            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        [Fact]
        public void A_game_in_Full_librarys_grid_shows_More_options_alone_as_Y_opens_its_name_box()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);

            core.FocusGameGrid();

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Fact]
        public void Other_My_games_tiles_show_More_options_alone()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);

            core.FocusHomeTile(core.MyGames.PlayHistory[0].Entries[0].Tile);

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Fact]
        public void The_Game_hub_shows_More_options_alone()
        {
            core.FocusGameHub();

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Fact]
        public void Search_opened_from_Games_takes_Y_Search_away()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusGameGrid();

            core.SearchFromMyGames(library.Id("Hades"));

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Moving_along_a_Group_shows_the_same_hints_rather_than_a_new_list_each_tile()
        {
            core.FocusHomeTile(GroupTile("Tunic"));
            var hints = core.Home.Hints;

            core.LeaveHomeTile(GroupTile("Tunic"));
            core.FocusHomeTile(GroupTile("Hades"));

            Assert.Same(hints, core.Home.Hints);
        }

        [Fact]
        public void Y_Search_shows_and_hides_with_a_change_the_bar_can_bind_to()
        {
            var changed = new List<string>();
            core.Home.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            core.FocusTopBar();
            core.LeaveTopBar();

            Assert.Equal(2, changed.Count(n => n == nameof(HomeState.Hints)));
        }

        private string[] HintTexts() => core.Home.Hints.Select(h => h.Text).ToArray();

        private HomeTile RecentTile(string title) => core.Home.RecentRow.Single(t => t.Title == title);

        // With no preset flagged, the Favourites Group stands in, holding Tunic.
        private HomeTile GroupTile(string title) => core.Home.Groups.SelectMany(g => g.Tiles).Single(t => t.Title == title);

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        /// <summary>Pressed and let go. A Guide tap opens the Guide 0.3 s after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button)
        {
            Press(button);
            Release(button);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
