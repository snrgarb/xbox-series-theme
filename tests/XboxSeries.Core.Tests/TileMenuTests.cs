using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class TileMenuTests
    {
        private const string HaloDirectory = @"C:\Games\Halo Infinite";

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public TileMenuTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), installDirectory: HaloDirectory)
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades.jpg")
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10), favorite: true, backgroundImage: @"C:\art\tunic.jpg");
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void Menu_on_a_Recent_row_tile_opens_the_tile_menu_on_Play_game()
        {
            core.FocusHomeTile(RecentTile("Hades"));

            Tap(ControllerButton.Start);

            Assert.Equal("Hades", core.Home.TileMenu.Title);
            Assert.Equal(new[] { "Play game", "Add to Favourites", "Manage game" }, Options());
            Assert.Equal("Play game", core.Home.TileMenu.FocusedOption.Label);
        }

        [Fact]
        public void Menu_on_a_Group_tile_opens_the_tile_menu()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            Tap(ControllerButton.Start);

            Assert.Equal("Tunic", core.Home.TileMenu.Title);
        }

        [Fact]
        public void Focus_on_a_Group_tile_leaves_the_hero_alone()
        {
            core.FocusHomeTile(RecentTile("Hades"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            core.FocusHomeTile(GroupTile("Tunic"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.Equal(library.Id("Hades"), core.Home.Hero.GameId);
        }

        [Fact]
        public void Focus_moving_to_a_Group_tile_before_it_settles_leaves_the_hero()
        {
            var hero = core.Home.Hero?.GameId;
            core.FocusHomeTile(RecentTile("Hades"));

            core.FocusHomeTile(GroupTile("Tunic"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.Equal(hero, core.Home.Hero?.GameId);
        }

        [Fact]
        public void Menu_once_focus_has_left_the_tiles_does_nothing()
        {
            var tile = RecentTile("Hades");
            core.FocusHomeTile(tile);
            core.LeaveHomeTile(tile);

            Tap(ControllerButton.Start);

            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void The_pads_A_on_a_focused_Recent_row_tile_opens_its_Game_hub()
        {
            core.FocusHomeTile(RecentTile("Hades"));

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedHubs);
        }

        [Fact]
        public void The_pads_A_on_a_focused_Group_tile_opens_its_Game_hub()
        {
            core.FocusHomeTile(GroupTile("Tunic"));

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Tunic") }, playnite.OpenedHubs);
        }

        [Fact]
        public void A_double_tap_of_the_pads_A_on_a_focused_tile_plays_the_game()
        {
            core.FocusHomeTile(RecentTile("Hades"));

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void The_pads_A_once_focus_has_left_the_tiles_does_nothing()
        {
            var tile = RecentTile("Hades");
            core.FocusHomeTile(tile);
            core.LeaveHomeTile(tile);

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void The_pads_A_while_the_Guide_is_still_handing_focus_back_does_not_choose_the_tile()
        {
            core.FocusHomeTile(RecentTile("Hades"));
            Tap(ControllerButton.Guide);
            Press(ControllerButton.B);

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void Menu_while_the_Guide_is_open_does_not_open_the_tile_menu()
        {
            core.FocusHomeTile(RecentTile("Hades"));
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.Start);

            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Menu_while_the_Guide_is_still_handing_focus_back_does_not_open_the_tile_menu()
        {
            core.FocusHomeTile(RecentTile("Hades"));
            Tap(ControllerButton.Guide);
            Press(ControllerButton.B);

            Tap(ControllerButton.Start);

            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Each_item_has_its_icon()
        {
            OpenMenuOn("Hades");

            Assert.Equal(
                new[] { DialogIcon.Play, DialogIcon.Favourite, DialogIcon.Manage },
                core.Home.TileMenu.Options.Select(o => o.Icon));
        }

        [Fact]
        public void Play_game_closes_the_menu_and_launches_the_game()
        {
            OpenMenuOn("Hades");

            Tap(ControllerButton.A);

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { library.Id("Hades") }, playnite.StartedGames);
        }

        [Fact]
        public void Play_game_while_another_runs_asks_Quit_X_and_start_Y()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            OpenMenuOn("Hades");

            Tap(ControllerButton.A);

            Assert.Null(core.Home.TileMenu);
            Assert.Equal("Quit Halo Infinite and start Hades?", core.Guide.Dialog.Title);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void The_running_games_menu_reads_Quit_with_the_quit_icon()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));

            OpenMenuOn("Halo Infinite");

            Assert.Equal(new[] { "Quit", "Add to Favourites", "Manage game" }, Options());
            Assert.Equal(DialogIcon.Quit, core.Home.TileMenu.Options[0].Icon);
        }

        [Fact]
        public void Quit_runs_the_shared_quit_flow()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            var halo = windows.OpenWindow(HaloDirectory + @"\HaloInfinite.exe");
            OpenMenuOn("Halo Infinite");

            Tap(ControllerButton.A);

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { halo }, windows.CloseRequests);

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal("Force quit Halo Infinite?", core.Guide.Dialog.Title);
        }

        [Fact]
        public void Manage_game_opens_Playnites_game_menu()
        {
            OpenMenuOn("Hades");

            Choose("Manage game");

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedGameMenus);
        }

        [Fact]
        public void Add_to_Favourites_makes_the_game_a_favourite()
        {
            OpenMenuOn("Hades");

            Choose("Add to Favourites");

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { (library.Id("Hades"), true) }, playnite.FavouriteChanges);
        }

        [Fact]
        public void A_favourite_offers_Remove_from_Favourites_which_unmarks_it()
        {
            OpenMenuOn("Tunic");

            Assert.Equal(new[] { "Play game", "Remove from Favourites", "Manage game" }, Options());
            Assert.Equal(DialogIcon.Unfavourite, core.Home.TileMenu.Options[1].Icon);

            Choose("Remove from Favourites");

            Assert.Equal(new[] { (library.Id("Tunic"), false) }, playnite.FavouriteChanges);
        }

        [Fact]
        public void The_Favourites_label_follows_the_library()
        {
            var hades = library.Id("Hades");
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(hades, "Hades", lastActivity: new DateTime(2026, 10, 11), favorite: true)
                .Snapshot());

            OpenMenuOn("Hades");

            Assert.Equal("Remove from Favourites", Options()[1]);
        }

        [Fact]
        public void The_D_pad_moves_between_rows_and_stops_at_the_ends()
        {
            OpenMenuOn("Hades");

            Tap(ControllerButton.DPadUp);
            Assert.Equal("Play game", core.Home.TileMenu.FocusedOption.Label);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Assert.Equal("Manage game", core.Home.TileMenu.FocusedOption.Label);
            Assert.True(core.Home.TileMenu.FocusedOption.IsFocused);
            Assert.False(core.Home.TileMenu.Options[0].IsFocused);
        }

        [Fact]
        public void Holding_the_D_pad_repeats_through_the_rows()
        {
            OpenMenuOn("Hades");

            Press(ControllerButton.DPadDown);
            clock.Advance(TimeSpan.FromMilliseconds(700));

            Assert.Equal("Manage game", core.Home.TileMenu.FocusedOption.Label);

            Release(ControllerButton.DPadDown);
        }

        [Fact]
        public void The_A_that_chose_from_the_menu_does_not_also_choose_the_tile_under_it()
        {
            var tile = RecentTile("Hades");
            OpenMenuOn("Hades");
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            // Playnite's own Enter for the same press reaches the tile once the menu has gone.
            Press(ControllerButton.A);
            core.ChooseHomeTile(tile);
            Release(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedGameMenus);
            Assert.Empty(playnite.OpenedHubs);

            core.ChooseHomeTile(tile);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedHubs);
        }

        [Fact]
        public void B_closes_the_menu_without_doing_anything()
        {
            OpenMenuOn("Hades");

            Tap(ControllerButton.B);

            Assert.Null(core.Home.TileMenu);
            Assert.Empty(playnite.StartedGames);
            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void The_Guide_button_closes_the_menu_and_opens_the_Guide()
        {
            OpenMenuOn("Hades");

            Tap(ControllerButton.Guide);

            Assert.Null(core.Home.TileMenu);
            Assert.True(core.Guide.IsOpen);
        }

        [Fact]
        public void Closing_the_menu_from_the_Dashboard_does_nothing_else()
        {
            OpenMenuOn("Hades");

            core.CloseTileMenu();

            Assert.Null(core.Home.TileMenu);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void The_mouse_points_at_and_picks_items()
        {
            OpenMenuOn("Hades");
            var manage = core.Home.TileMenu.Options[2];

            core.FocusOption(manage);
            Assert.Same(manage, core.Home.TileMenu.FocusedOption);

            core.ChooseOption(manage);
            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedGameMenus);
        }

        private HomeTile RecentTile(string title) => core.Home.RecentRow.Single(t => t.Title == title);

        // With no preset flagged, the Favourites Group stands in, holding Tunic.
        private HomeTile GroupTile(string title) => core.Home.Groups.SelectMany(g => g.Tiles).Single(t => t.Title == title);

        private string[] Options() => core.Home.TileMenu.Options.Select(o => o.Label).ToArray();

        private void OpenMenuOn(string title)
        {
            core.FocusHomeTile(RecentTile(title));
            Tap(ControllerButton.Start);
        }

        private void Choose(string label)
        {
            while (core.Home.TileMenu.FocusedOption.Label != label)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.A);
        }

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
