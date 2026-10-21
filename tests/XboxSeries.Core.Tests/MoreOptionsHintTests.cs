using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The hint bar at the Dashboard's bottom right: "More options" with the Menu glyph whenever what has focus has a menu,
    // a Group's tile on Home, a game on My games & apps or the Game hub, and never while a menu, the Guide or a prompt is up.
    public class MoreOptionsHintTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 18, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public MoreOptionsHintTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), favorite: true)
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10), favorite: true)
                .WithGame("33 Immortals", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
        }

        // Home (#66)

        [Fact]
        public void Nothing_on_Home_with_focus_shows_no_hint()
        {
            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void A_Group_tile_with_focus_shows_More_options_with_the_Menu_button()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            // Y Search follows it to the right, as Y on Home opens Search (#69).
            var hint = core.Home.Hints[0];
            Assert.Equal(ControllerButton.Start, hint.Button);
            Assert.Equal("More options", hint.Text);
        }

        [Fact]
        public void The_hint_shows_and_hides_with_a_change_the_bar_can_bind_to()
        {
            var changed = new List<string>();
            core.Home.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            core.FocusHomeTile(GroupTile("Tunic"));
            core.LeaveHomeTile(GroupTile("Tunic"));

            Assert.Equal(2, changed.Count(n => n == nameof(HomeState.Hints)));
        }

        [Fact]
        public void Moving_along_a_Group_shows_the_same_hint_rather_than_a_new_one_each_tile()
        {
            core.FocusHomeTile(GroupTile("Tunic"));
            var hints = core.Home.Hints;

            core.LeaveHomeTile(GroupTile("Tunic"));
            core.FocusHomeTile(GroupTile("Hades"));

            Assert.Same(hints, core.Home.Hints);
        }

        [Fact]
        public void A_Recent_row_tile_shows_no_hint()
        {
            core.FocusHomeTile(RecentTile("Hades"));

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_Recent_row_rebuilt_under_a_focused_tile_still_shows_no_hint()
        {
            core.FocusHomeTile(RecentTile("Hades"));

            core.OnLibraryChanged(library.WithGame("Forza Horizon 5").Snapshot());

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Focus_moving_from_a_Group_up_to_the_Recent_row_hides_the_hint()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            core.LeaveHomeTile(GroupTile("Tunic"));
            core.FocusHomeTile(RecentTile("Hades"));

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Focus_leaving_a_Group_tile_for_the_top_bar_or_the_tiles_under_the_Recent_row_hides_the_hint()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            core.LeaveHomeTile(GroupTile("Tunic"));

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_Tile_menu_hides_the_hint_and_closing_it_brings_it_back_with_focus_still_on_the_Group_tile()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            Tap(ControllerButton.Start);

            Assert.NotNull(core.Home.TileMenu);
            Assert.Empty(core.Home.Hints);

            Tap(ControllerButton.B);

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        [Fact]
        public void Menu_on_a_Group_tile_still_opens_its_Tile_menu()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            Tap(ControllerButton.Start);

            Assert.Equal("Tunic", core.Home.TileMenu.Title);
        }

        [Fact]
        public void The_Guide_hides_the_hint_until_it_has_handed_focus_back()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            Tap(ControllerButton.Guide);

            Assert.Empty(core.Home.Hints);

            Press(ControllerButton.B);

            // Closed, but still handing focus back.
            Assert.Empty(core.Home.Hints);

            Release(ControllerButton.B);

            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        [Fact]
        public void A_prompt_hides_the_hint_until_it_is_answered()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            core.FocusHomeTile(GroupTile("Tunic"));

            core.PlayGame(library.Id("Hades"));

            Assert.NotNull(core.Guide.Dialog);
            Assert.Empty(core.Home.Hints);

            Tap(ControllerButton.B);
            Tap(ControllerButton.B);

            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        [Fact]
        public void Start_up_hides_the_hint()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            core.OnStartup();

            Assert.True(core.IsStartupShown);
            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_hint_comes_back_once_Signing_you_in_has_shown()
        {
            core.FocusHomeTile(GroupTile("Tunic"));
            core.OnStartupSignedIn(core.OnStartup().LastProfile);

            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.False(core.IsStartupShown);
            Assert.Equal(new[] { "More options", "Search" }, HintTexts());
        }

        // My games & apps (#67)

        [Fact]
        public void A_Play_history_game_with_focus_shows_the_hint()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);

            core.FocusHomeTile(core.MyGames.PlayHistory[0].Entries[0].Tile);

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Fact]
        public void A_Play_history_game_shows_the_hint_even_when_it_is_also_in_Homes_Recent_row()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);

            core.FocusHomeTile(RecentTile("Hades"));

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Fact]
        public void A_Manage_Queue_row_or_Recently_installed_cover_with_focus_shows_the_hint()
        {
            core.OpenMyGames(MyGamesItem.ManageQueue);

            core.FocusHomeTile(core.MyGames.Queue.Single().Tile);

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Theory]
        [InlineData(MyGamesItem.Games)]
        [InlineData(MyGamesItem.FullLibrary)]
        public void A_game_in_the_grid_with_focus_shows_the_hint(MyGamesItem item)
        {
            core.OpenMyGames(item);

            core.FocusGameGrid();

            Assert.Equal("More options", HintTexts()[0]);
        }

        [Fact]
        public void Focus_leaving_the_grid_for_the_nav_hides_the_hint()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusGameGrid();

            core.LeaveGameGrid();
            core.FocusMyGamesNav(MyGamesItem.Games);

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_nav_Apps_and_Manages_boxes_show_no_hint()
        {
            core.OpenMyGames(MyGamesItem.Apps);
            core.FocusMyGamesNav(MyGamesItem.Apps);

            Assert.Empty(core.Home.Hints);

            core.OpenMyGames(MyGamesItem.Manage);

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Playnites_main_list_with_focus_away_from_the_grid_shows_no_hint()
        {
            // On Home, as a game stops, Playnite's focus goes to its main list, hidden there.
            core.FocusGameGrid();

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_grid_keeps_the_hint_only_while_it_is_showing()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.FocusGameGrid();

            core.LeaveMyGames();

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_Tile_menu_on_My_games_and_apps_hides_the_hint()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(core.MyGames.PlayHistory[0].Entries[0].Tile);

            Tap(ControllerButton.Start);

            Assert.NotNull(core.Home.TileMenu);
            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void The_Guide_over_the_grid_hides_the_hint()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.FocusGameGrid();

            Tap(ControllerButton.Guide);

            Assert.Empty(core.Home.Hints);
        }

        // Game hub (#68)

        [Fact]
        public void The_Game_hub_with_the_pad_shows_the_hint()
        {
            core.FocusGameHub();

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Fact]
        public void Manage_game_or_a_dialog_taking_the_pad_from_the_hub_hides_the_hint_and_it_comes_back_after()
        {
            core.FocusGameHub();

            core.LeaveGameHub();

            Assert.Empty(core.Home.Hints);

            core.FocusGameHub();

            Assert.Equal(new[] { "More options" }, HintTexts());
        }

        [Fact]
        public void The_Guide_over_the_hub_hides_the_hint()
        {
            core.FocusGameHub();

            Tap(ControllerButton.Guide);

            Assert.Empty(core.Home.Hints);
        }

        [Fact]
        public void Menu_on_the_hub_comes_through_its_own_binding_not_the_pad()
        {
            core.FocusGameHub();

            Tap(ControllerButton.Start);

            // The hub's Menu binding asks for the Tile menu itself (OpenTileMenu); the pad alone opens nothing more over it.
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void A_Group_tile_opening_its_hub_keeps_the_hint_as_focus_moves_onto_the_hub()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            core.LeaveHomeTile(GroupTile("Tunic"));
            core.FocusGameHub();

            Assert.Equal(new[] { "More options" }, HintTexts());
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
