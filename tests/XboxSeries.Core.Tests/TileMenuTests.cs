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
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public TileMenuTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
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
            Assert.Equal(new[] { "Play game", "Add to Favourites", "Manage game", "Uninstall", "Customise Home", "More options" }, Options());
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
        public void Focus_on_a_Group_tile_puts_the_swirl_behind_Home()
        {
            core.FocusHomeTile(RecentTile("Hades"));

            core.FocusHomeTile(GroupTile("Tunic"));

            Assert.Null(core.Home.Hero);
        }

        [Fact]
        public void Focus_moving_to_a_Group_tile_before_it_settles_keeps_the_swirl()
        {
            core.FocusHomeTile(RecentTile("Halo Infinite"));
            core.LeaveHomeTile(RecentTile("Halo Infinite"));
            core.FocusHomeTile(RecentTile("Hades"));

            core.FocusHomeTile(GroupTile("Tunic"));
            clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.Null(core.Home.Hero);
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
                new[] { DialogIcon.Play, DialogIcon.Favourite, DialogIcon.Manage, DialogIcon.Uninstall, DialogIcon.Customise, DialogIcon.MoreOptions },
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

            Assert.Equal(new[] { "Quit", "Add to Favourites", "Manage game", "Uninstall", "Customise Home", "More options" }, Options());
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
        public void Manage_game_opens_the_games_Game_hub()
        {
            OpenMenuOn("Hades");

            Choose("Manage game");

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedHubs);
        }

        [Fact]
        public void An_uninstalled_game_offers_Install_in_place_of_Play_game_and_no_Uninstall()
        {
            AddGame("Celeste", installed: false);

            OpenMenuOn("Celeste");

            Assert.Equal(new[] { "Install", "Add to Favourites", "Manage game", "Customise Home", "More options" }, Options());
            Assert.Equal(DialogIcon.Install, core.Home.TileMenu.Options[0].Icon);
        }

        [Fact]
        public void Install_installs_through_the_client_without_its_windows_when_it_can()
        {
            AddGame("Celeste", installed: false);
            downloads.Installable.Add(library.Id("Celeste"));
            OpenMenuOn("Celeste");

            Choose("Install");

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { library.Id("Celeste") }, downloads.InstallsAsked);
            Assert.Empty(playnite.PlayniteInstalls);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void Install_falls_back_on_Playnites_own_Install_for_a_game_its_client_cant_install_silently()
        {
            AddGame("Celeste", installed: false);
            OpenMenuOn("Celeste");

            Choose("Install");

            Assert.Equal(new[] { library.Id("Celeste") }, playnite.PlayniteInstalls);
            Assert.Empty(downloads.InstallsAsked);
        }

        [Fact]
        public void Install_on_a_game_already_installing_asks_nothing_more()
        {
            AddGame("Celeste", installed: false, installing: true);
            OpenMenuOn("Celeste");

            Choose("Install");

            Assert.Empty(playnite.PlayniteInstalls);
            Assert.Empty(downloads.InstallsAsked);
        }

        [Fact]
        public void A_custom_game_has_no_Uninstall()
        {
            AddGame("Doom", custom: true);

            OpenMenuOn("Doom");

            Assert.Equal(new[] { "Play game", "Add to Favourites", "Manage game", "Customise Home", "More options" }, Options());
        }

        [Fact]
        public void Uninstall_asks_first_with_a_prompt()
        {
            OpenMenuOn("Hades");

            Choose("Uninstall");

            Assert.Equal("Uninstall Hades?", core.Home.TileMenu.Title);
            Assert.True(core.Home.TileMenu.IsPrompt);
            Assert.Equal(new[] { "Uninstall", "Cancel" }, Options());
            Assert.Empty(downloads.UninstallsAsked);
            Assert.Empty(playnite.PlayniteUninstalls);
        }

        [Fact]
        public void Confirming_Uninstall_uninstalls_through_the_client_without_its_windows()
        {
            downloads.Uninstallable.Add(library.Id("Hades"));
            OpenMenuOn("Hades");
            Choose("Uninstall");

            Choose("Uninstall");

            Assert.Null(core.Home.TileMenu);
            Assert.Equal(new[] { library.Id("Hades") }, downloads.UninstallsAsked);
            Assert.Empty(playnite.PlayniteUninstalls);
        }

        [Fact]
        public void Confirming_Uninstall_falls_back_on_Playnites_own_for_a_game_its_client_cant_uninstall_silently()
        {
            OpenMenuOn("Hades");
            Choose("Uninstall");

            Choose("Uninstall");

            Assert.Equal(new[] { library.Id("Hades") }, playnite.PlayniteUninstalls);
            Assert.Empty(downloads.UninstallsAsked);
        }

        [Fact]
        public void Confirming_Uninstall_of_a_game_already_being_uninstalled_asks_nothing_more()
        {
            downloads.Uninstallable.Add(library.Id("Hades"));
            core.UninstallGame(library.Id("Hades"));
            OpenMenuOn("Hades");
            Choose("Uninstall");

            Choose("Uninstall");

            Assert.Equal(new[] { library.Id("Hades") }, downloads.UninstallsAsked);
            Assert.Empty(playnite.PlayniteUninstalls);
        }

        [Fact]
        public void Cancel_on_the_Uninstall_prompt_goes_back_to_the_menu_on_Uninstall()
        {
            downloads.Uninstallable.Add(library.Id("Hades"));
            OpenMenuOn("Hades");
            Choose("Uninstall");

            Choose("Cancel");

            Assert.Equal("Hades", core.Home.TileMenu.Title);
            Assert.Equal("Uninstall", core.Home.TileMenu.FocusedOption.Label);
            Assert.Empty(downloads.UninstallsAsked);
            Assert.Empty(playnite.PlayniteUninstalls);
        }

        [Fact]
        public void B_on_the_Uninstall_prompt_goes_back_to_the_menu_and_B_again_closes_it()
        {
            downloads.Uninstallable.Add(library.Id("Hades"));
            OpenMenuOn("Hades");
            Choose("Uninstall");

            Tap(ControllerButton.B);

            Assert.Equal("Hades", core.Home.TileMenu.Title);
            Assert.Equal("Uninstall", core.Home.TileMenu.FocusedOption.Label);

            Tap(ControllerButton.B);

            Assert.Null(core.Home.TileMenu);
            Assert.Empty(downloads.UninstallsAsked);
        }

        [Fact]
        public void Focus_steps_from_Uninstall_over_the_gap_to_Customise_and_back()
        {
            OpenMenuOn("Hades");
            Choose("Uninstall");
            Tap(ControllerButton.B);

            Tap(ControllerButton.DPadDown);
            Assert.Equal("Customise Home", core.Home.TileMenu.FocusedOption.Label);

            Tap(ControllerButton.DPadUp);
            Assert.Equal("Uninstall", core.Home.TileMenu.FocusedOption.Label);
        }

        [Fact]
        public void Customise_Home_opens_Settings_on_Personalisation()
        {
            OpenMenuOn("Hades");

            Choose("Customise Home");

            Assert.Null(core.Home.TileMenu);
            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.Personalisation, core.Settings.ThemePage);
        }

        [Fact]
        public void B_from_Settings_opened_by_Customise_Home_goes_back_to_the_tile_that_opened_it()
        {
            OpenMenuOn("Hades");
            Choose("Customise Home");

            core.LeaveThemePage();
            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.False(core.MyGames.IsShown);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);
            Assert.Equal(library.Id("Hades"), core.Settings.OpenedFromGame);
        }

        [Fact]
        public void Customise_Home_from_a_Group_tile_goes_back_to_that_tile()
        {
            core.FocusHomeTile(GroupTile("Tunic"));
            Tap(ControllerButton.Start);
            Choose("Customise Home");

            core.LeaveThemePage();
            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.Equal(library.Id("Tunic"), core.Settings.OpenedFromGame);
        }

        [Fact]
        public void The_tile_menu_on_My_games_and_apps_offers_Customise_games_and_apps()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(EntryOf("Hades"));

            Tap(ControllerButton.Start);

            Assert.Equal(new[] { "Play game", "Add to Favourites", "Manage game", "Uninstall", "Customise games & apps", "More options" }, Options());
            Assert.Equal(DialogIcon.Customise, core.Home.TileMenu.Options[4].Icon);
        }

        [Fact]
        public void Customise_games_and_apps_opens_Settings_on_Personalisation_in_the_pages_place()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(EntryOf("Hades"));
            Tap(ControllerButton.Start);

            Choose("Customise games & apps");

            Assert.Null(core.Home.TileMenu);
            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.Personalisation, core.Settings.ThemePage);
            Assert.False(core.MyGames.IsShown);
            Assert.Equal(SettingsOrigin.MyGamesTile, core.Settings.OpenedFrom);
        }

        [Fact]
        public void B_from_Settings_opened_by_Customise_games_and_apps_goes_back_to_the_page_and_its_tile()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(EntryOf("Hades"));
            Tap(ControllerButton.Start);
            Choose("Customise games & apps");

            core.LeaveThemePage();
            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.PlayHistory, core.MyGames.Item);
            Assert.Equal(library.Id("Hades"), core.MyGames.ReturnsToGame);
        }

        [Fact]
        public void B_from_Settings_opened_by_Customise_games_and_apps_on_Manage_Queue_goes_back_to_Manage_Queue()
        {
            var queued = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            core.OnLibraryChanged(queued.Snapshot());
            core.OpenMyGames(MyGamesItem.ManageQueue);
            core.FocusHomeTile(core.MyGames.Queue.Single().Tile);
            Tap(ControllerButton.Start);
            Choose("Customise games & apps");

            core.LeaveThemePage();
            core.LeaveSettings();

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.ManageQueue, core.MyGames.Item);
            Assert.Equal(queued.Id("33 Immortals"), core.MyGames.ReturnsToGame);
        }

        [Fact]
        public void My_games_and_apps_opened_any_other_way_starts_at_its_first_tile()
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(EntryOf("Hades"));
            Tap(ControllerButton.Start);
            Choose("Customise games & apps");
            core.LeaveSettings();
            core.LeaveMyGames();

            core.OpenMyGames(MyGamesItem.PlayHistory);

            Assert.Null(core.MyGames.ReturnsToGame);
        }

        [Fact]
        public void Settings_from_the_top_bar_after_Customise_goes_back_to_the_top_bar()
        {
            OpenMenuOn("Hades");
            Choose("Customise Home");
            core.LeaveSettings();

            core.OpenSettings();

            Assert.Equal(SettingsOrigin.TopBar, core.Settings.OpenedFrom);
            Assert.Null(core.Settings.OpenedFromGame);
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

            Assert.Equal(new[] { "Play game", "Remove from Favourites", "Manage game", "Uninstall", "Customise Home", "More options" }, Options());
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

            for (var i = 0; i < 6; i++)
            {
                Tap(ControllerButton.DPadDown);
            }

            Assert.Equal("More options", core.Home.TileMenu.FocusedOption.Label);
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

            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedHubs);

            core.ChooseHomeTile(tile);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades"), library.Id("Hades") }, playnite.OpenedHubs);
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
            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedHubs);
        }

        [Fact]
        public void Opened_from_the_Game_hub_the_menu_has_no_Manage_game()
        {
            core.OpenTileMenu(library.Id("Hades"), fromGameHub: true);

            Assert.Equal("Hades", core.Home.TileMenu.Title);
            Assert.Equal(new[] { "Play game", "Add to Favourites", "Uninstall", "Customise Home", "More options" }, Options());
            Assert.Equal("Play game", core.Home.TileMenu.FocusedOption.Label);
        }

        [Fact]
        public void Opened_from_Playnites_own_grid_the_menu_has_Manage_game()
        {
            core.OpenTileMenu(library.Id("Hades"), fromGameHub: false);

            Assert.Equal(new[] { "Play game", "Add to Favourites", "Manage game", "Uninstall", "Customise Home", "More options" }, Options());
        }

        [Fact]
        public void The_Game_hubs_menu_opens_nothing_over_the_Guide_or_a_menu_already_up()
        {
            Tap(ControllerButton.Guide);
            core.OpenTileMenu(library.Id("Hades"), fromGameHub: true);
            Assert.Null(core.Home.TileMenu);

            Tap(ControllerButton.B);
            clock.Advance(TimeSpan.FromSeconds(1));
            OpenMenuOn("Tunic");
            core.OpenTileMenu(library.Id("Hades"), fromGameHub: true);
            Assert.Equal("Tunic", core.Home.TileMenu.Title);
        }

        [Fact]
        public void The_Game_hubs_menu_for_a_game_no_longer_in_the_library_opens_nothing()
        {
            core.OpenTileMenu(Guid.NewGuid(), fromGameHub: true);

            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void The_menu_holds_every_button_while_it_is_up()
        {
            core.OpenTileMenu(library.Id("Hades"), fromGameHub: true);

            Assert.True(core.TileMenuHolds(ControllerButton.A));
            Assert.True(core.TileMenuHolds(ControllerButton.B));
            Assert.True(core.TileMenuHolds(ControllerButton.Start));
        }

        [Fact]
        public void The_A_that_chose_from_the_menu_is_held_until_it_is_released()
        {
            core.OpenTileMenu(library.Id("Hades"), fromGameHub: true);

            // Play game: the menu closes on the press, and the hub's own Play under it mustn't get the same press.
            Press(ControllerButton.A);

            Assert.Null(core.Home.TileMenu);
            Assert.True(core.TileMenuHolds(ControllerButton.A));
            Assert.False(core.TileMenuHolds(ControllerButton.B));

            Release(ControllerButton.A);

            Assert.False(core.TileMenuHolds(ControllerButton.A));
        }

        [Fact]
        public void The_B_that_closed_the_menu_is_held_until_it_is_released_so_the_hub_stays_open()
        {
            core.OpenTileMenu(library.Id("Hades"), fromGameHub: true);

            Press(ControllerButton.B);

            Assert.Null(core.Home.TileMenu);
            Assert.True(core.TileMenuHolds(ControllerButton.B));

            Release(ControllerButton.B);

            Assert.False(core.TileMenuHolds(ControllerButton.B));
        }

        [Fact]
        public void With_no_menu_up_nothing_is_held()
        {
            Press(ControllerButton.A);

            Assert.False(core.TileMenuHolds(ControllerButton.A));
        }

        private HomeTile RecentTile(string title) => core.Home.RecentRow.Single(t => t.Title == title);

        // With no preset flagged, the Favourites Group stands in, holding Tunic.
        private HomeTile GroupTile(string title) => core.Home.Groups.SelectMany(g => g.Tiles).Single(t => t.Title == title);

        /// <summary>The Play history entry's tile of the game named <paramref name="title"/>.</summary>
        private HomeTile EntryOf(string title) =>
            core.MyGames.PlayHistory.SelectMany(s => s.Entries).Single(e => e.Tile.Title == title).Tile;

        /// <summary>Another game on the Recent row, played most recently of all.</summary>
        private void AddGame(string name, bool installed = true, bool installing = false, bool custom = false)
        {
            library.WithGame(name, lastActivity: new DateTime(2026, 10, 13), installed: installed, installing: installing, custom: custom);
            core.OnLibraryChanged(library.Snapshot());
        }

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
