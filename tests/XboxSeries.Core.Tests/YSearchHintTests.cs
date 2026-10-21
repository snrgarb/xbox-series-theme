using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // "Y Search" on the hint bar, right of More options, on the tiles with a menu where Y opens Search: Home's Group tiles
    // and a game in My games & apps' Games grid (#69). Never alone: elsewhere on Home, Y opens Search with no hint.
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
        public void A_Group_tile_with_focus_shows_Y_Search_right_of_More_options()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            Assert.Equal(new[] { ControllerButton.Start, ControllerButton.Y }, core.Home.Hints.Select(h => h.Button));
            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        [Fact]
        public void The_Idle_screens_dim_hides_the_hints_as_Y_there_opens_the_cards_game_until_the_wake_press()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.True(core.Idle.IsDimmed);
            Assert.Empty(core.Home.Hints);

            Tap(ControllerButton.A);

            Assert.False(core.Idle.IsDimmed);
            Assert.Equal(new[] { "More options", "Search" }, core.Home.Hints.Select(h => h.Text));
        }

        [Fact]
        public void The_Recent_row_shows_no_hint()
        {
            core.FocusHomeTile(RecentTile("Hades"));

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
