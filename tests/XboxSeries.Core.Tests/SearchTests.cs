using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Search: games and Apps found by name, typed on the console keyboard.</summary>
    public class SearchTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public SearchTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
        }

        [Fact]
        public void Home_starts_without_the_page()
        {
            Assert.False(core.Search.IsShown);
        }

        [Fact]
        public void The_top_bars_Search_opens_the_page_empty_with_the_keyboard_up_and_Shift_on()
        {
            core.OpenSearch();

            Assert.True(core.Search.IsShown);
            Assert.Equal(SearchOrigin.TopBar, core.Search.OpenedFrom);
            Assert.True(core.Search.IsKeyboardShown);
            Assert.Equal(string.Empty, Keyboard.Text);
            Assert.Equal(0, Keyboard.Cursor);
            Assert.True(Keyboard.IsShifted);
            Assert.False(Keyboard.ShowsSymbols);
        }

        [Fact]
        public void The_page_opens_empty_every_time()
        {
            core.OpenSearch();
            Type("hal");
            Press(Key(KeyboardKeyKind.Symbols));
            core.PressKey(Key(KeyboardKeyKind.Enter));
            core.LeaveSearch();

            core.OpenSearch();

            Assert.Equal(string.Empty, Keyboard.Text);
            Assert.Equal(0, Keyboard.Cursor);
            Assert.True(Keyboard.IsShifted);
            Assert.False(Keyboard.ShowsSymbols);
            Assert.True(core.Search.IsKeyboardShown);
            Assert.True(core.Search.ShowsRecentlyAdded);
        }

        [Fact]
        public void Opening_the_page_from_My_games_and_apps_takes_its_place_with_the_users_own_filter_back()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.OpenSearch();

            Assert.False(core.MyGames.IsShown);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Opening_the_page_from_Settings_takes_its_place()
        {
            core.OpenSettings();

            core.OpenSearch();

            Assert.False(core.Settings.IsShown);
            Assert.True(core.Search.IsShown);
        }

        [Fact]
        public void Opening_My_games_and_apps_or_Settings_takes_the_pages_place()
        {
            core.OpenSearch();
            core.OpenMyGames(MyGamesItem.Games);
            Assert.False(core.Search.IsShown);
            Assert.True(core.MyGames.IsShown);

            core.OpenSearch();
            core.OpenSettings();
            Assert.False(core.Search.IsShown);
            Assert.True(core.Settings.IsShown);
        }

        [Fact]
        public void The_Guides_Search_utility_brings_Playnite_to_the_front_on_the_page_once_A_is_released()
        {
            windows.OpenWindow();
            core.OpenSettings();
            FocusGuideSearch();

            Press(ControllerButton.A);

            Assert.False(core.Guide.IsOpen);
            Assert.False(core.Search.IsShown);

            Release(ControllerButton.A);

            Assert.True(core.Search.IsShown);
            Assert.False(core.Settings.IsShown);
            Assert.Equal(SearchOrigin.Guide, core.Search.OpenedFrom);
            Assert.True(core.Search.IsKeyboardShown);
            Assert.Equal(DashboardPage.Search, playnite.Page);
            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.Null(core.Guide.Flyout);
        }

        [Fact]
        public void The_Guides_Home_takes_the_page_away()
        {
            core.OpenSearch();
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.A);

            Assert.Equal(GuideItemKind.Home, core.Guide.Items[0].Kind);
            Assert.False(core.Search.IsShown);
            Assert.Equal(DashboardPage.Home, playnite.Page);
        }

        [Fact]
        public void The_Guides_My_games_and_apps_takes_the_pages_place()
        {
            core.OpenSearch();
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.False(core.Search.IsShown);
            Assert.True(core.MyGames.IsShown);
        }

        [Fact]
        public void A_game_exiting_goes_back_to_Home_without_the_page()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            core.OpenSearch();

            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.False(core.Search.IsShown);
        }

        [Fact]
        public void Empty_the_page_shows_the_games_most_recently_added_newest_first_never_hidden_ones()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Old", added: new DateTime(2025, 1, 1))
                .WithGame("Never Added")
                .WithGame("Newest", added: new DateTime(2026, 10, 1), installed: false)
                .WithGame("Hidden", added: new DateTime(2026, 10, 2), hidden: true)
                .WithGame("Middle", added: new DateTime(2026, 5, 1))
                .WithApp("Steam", @"C:\Steam\steam.exe")
                .Snapshot());

            core.OpenSearch();

            Assert.True(core.Search.ShowsRecentlyAdded);
            Assert.Equal(new[] { "Newest", "Middle", "Old", "Never Added" }, core.Search.RecentlyAdded.Select(t => t.Title));
            Assert.False(core.Search.ShowsGames);
            Assert.False(core.Search.ShowsApps);
        }

        [Fact]
        public void The_recently_added_row_holds_twelve_games()
        {
            var library = new FakeLibrary();
            for (var i = 1; i <= 15; i++)
            {
                library.WithGame("Game " + i, added: new DateTime(2026, 1, i));
            }

            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();

            Assert.Equal(12, core.Search.RecentlyAdded.Count);
            Assert.Equal("Game 15", core.Search.RecentlyAdded[0].Title);
        }

        [Fact]
        public void Typing_finds_games_whose_name_holds_the_text_whatever_the_case_and_accents_never_hidden_ones()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithGame("Pokémon Legends")
                .WithGame("Forza Horizon 5", installed: false)
                .WithGame("Hidden Halo", hidden: true)
                .Snapshot());
            core.OpenSearch();

            Type("HALO");
            Assert.Equal(new[] { "Halo Infinite" }, Titles(core.Search.Games));

            Clear();
            Type("pokemon");
            Assert.Equal(new[] { "Pokémon Legends" }, Titles(core.Search.Games));

            Clear();
            Type("izon");
            Assert.Equal(new[] { "Forza Horizon 5" }, Titles(core.Search.Games));
            Assert.True(core.Search.ShowsGames);
            Assert.False(core.Search.ShowsRecentlyAdded);
        }

        [Fact]
        public void Spaces_either_side_of_the_text_are_ignored_and_spaces_alone_are_nothing_typed()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite").Snapshot());
            core.OpenSearch();

            Type(" halo ");
            Assert.Equal(new[] { "Halo Infinite" }, Titles(core.Search.Games));

            Clear();
            Type("   ");
            Assert.True(core.Search.ShowsRecentlyAdded);
            Assert.Empty(core.Search.Games);
        }

        [Fact]
        public void Games_whose_name_starts_with_the_text_come_first_then_those_with_a_word_starting_with_it_each_by_sorting_name()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Unhalo")
                .WithGame("The Halo Collection", sortingName: "Halo Collection")
                .WithGame("Halo Wars")
                .WithGame("Halo Infinite")
                .WithGame("Ahalo")
                .WithGame("Super Halo")
                .Snapshot());
            core.OpenSearch();

            Type("halo");

            Assert.Equal(
                new[] { "Halo Infinite", "Halo Wars", "The Halo Collection", "Super Halo", "Ahalo", "Unhalo" },
                Titles(core.Search.Games));
        }

        [Fact]
        public void A_row_with_nothing_found_is_hidden()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithApp("Steam", @"C:\Steam\steam.exe")
                .Snapshot());
            core.OpenSearch();

            Type("steam");
            Assert.False(core.Search.ShowsGames);
            Assert.True(core.Search.ShowsApps);
            Assert.Equal(new[] { "Steam" }, core.Search.Apps.Select(a => a.Name));

            Clear();
            Type("halo");
            Assert.True(core.Search.ShowsGames);
            Assert.False(core.Search.ShowsApps);

            Clear();
            Type("zzz");
            Assert.False(core.Search.ShowsGames);
            Assert.False(core.Search.ShowsApps);
            Assert.False(core.Search.ShowsRecentlyAdded);
        }

        [Fact]
        public void Apps_are_found_by_name_as_games_are()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithApp("Discord", @"C:\Discord\Update.exe")
                .WithApp("Steam Link", @"C:\Steam\link.exe")
                .WithApp("Big Picture Steam", @"C:\Steam\steam.exe")
                .Snapshot());
            core.OpenSearch();

            Type("STEAM");

            Assert.Equal(new[] { "Steam Link", "Big Picture Steam" }, core.Search.Apps.Select(a => a.Name));
        }

        [Fact]
        public void With_Apps_unavailable_the_Apps_row_never_shows()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Steam Deck Tools").WithoutApps().Snapshot());
            core.OpenSearch();

            Type("steam");

            Assert.False(core.Search.ShowsApps);
            Assert.Empty(core.Search.Apps);
            Assert.True(core.Search.ShowsGames);
        }

        [Fact]
        public void Deleting_the_text_shows_the_recently_added_row_again()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", added: new DateTime(2026, 1, 1)).Snapshot());
            core.OpenSearch();
            Type("ha");

            Press(Key(KeyboardKeyKind.Backspace));
            Press(Key(KeyboardKeyKind.Backspace));

            Assert.True(core.Search.ShowsRecentlyAdded);
            Assert.Empty(core.Search.Games);
            Assert.Equal(new[] { "Halo Infinite" }, Titles(core.Search.RecentlyAdded));
        }

        [Fact]
        public void The_rows_follow_the_library_while_the_page_is_up()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");

            core.OnLibraryChanged(library.WithGame("Halo Wars").WithApp("Halo Mod Tool", @"C:\Halo\mod.exe").Snapshot());

            Assert.Equal(new[] { "Halo Infinite", "Halo Wars" }, Titles(core.Search.Games));
            Assert.Equal(new[] { "Halo Mod Tool" }, core.Search.Apps.Select(a => a.Name));
        }

        [Fact]
        public void A_found_game_running_is_marked_as_on_Home()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");

            core.OnGameStarted(library.Id("Halo Infinite"));

            Assert.True(Assert.Single(core.Search.Games).IsRunning);
        }

        [Fact]
        public void The_rows_are_not_rebuilt_when_nothing_in_them_changed()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite").WithApp("Steam", @"C:\Steam\steam.exe");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("a");
            var games = core.Search.Games;
            var apps = core.Search.Apps;

            core.OnLibraryChanged(library.Snapshot());

            Assert.Same(games, core.Search.Games);
            Assert.Same(apps, core.Search.Apps);
        }

        [Fact]
        public void A_letter_types_a_capital_with_Shift_on_then_Shift_goes_off_and_the_letters_read_small()
        {
            core.OpenSearch();
            Assert.Equal("Q", Keyboard.Keys.Single(k => k.Row == 1 && k.Column == 1).Text);

            Press(Key("H"));

            Assert.Equal("H", Keyboard.Text);
            Assert.Equal(1, Keyboard.Cursor);
            Assert.False(Keyboard.IsShifted);
            Assert.Equal("q", Keyboard.Keys.Single(k => k.Row == 1 && k.Column == 1).Text);

            Press(Key("a"));
            Press(Key("1"));

            Assert.Equal("Ha1", Keyboard.Text);
            Assert.Equal(3, Keyboard.Cursor);
        }

        [Fact]
        public void Shift_turns_capitals_on_and_off()
        {
            core.OpenSearch();

            Press(Key(KeyboardKeyKind.Shift));
            Assert.False(Keyboard.IsShifted);
            Assert.Equal("z", Keyboard.Keys.Single(k => k.Row == 3 && k.Column == 1).Text);

            Press(Key(KeyboardKeyKind.Shift));
            Assert.True(Keyboard.IsShifted);
            Assert.Equal("Z", Keyboard.Keys.Single(k => k.Row == 3 && k.Column == 1).Text);
        }

        [Fact]
        public void The_symbols_key_swaps_the_letter_rows_for_symbols_and_back_the_number_row_staying()
        {
            core.OpenSearch();
            var symbols = Key(KeyboardKeyKind.Symbols);
            var keys = Keyboard.Keys.ToList();

            Press(symbols);

            Assert.True(Keyboard.ShowsSymbols);
            Assert.Equal("abc", symbols.Text);
            Assert.Equal(keys, Keyboard.Keys);
            Assert.Equal("1234567890", RowText(0));
            Assert.Equal("!@#$%^&*()", RowText(1));
            Assert.Equal(10, RowText(2).Length);
            Assert.Equal(10, RowText(3).Length);
            Assert.Equal(30, RowText(1).Concat(RowText(2)).Concat(RowText(3)).Distinct().Count());

            Press(Key("@"));
            Assert.Equal("@", Keyboard.Text);
            Assert.True(Keyboard.ShowsSymbols);

            Press(symbols);

            Assert.False(Keyboard.ShowsSymbols);
            Assert.Equal("#+=", symbols.Text);
            Assert.Equal("qwertyuiop", RowText(1));
        }

        [Fact]
        public void Backspace_deletes_the_character_before_the_cursor_and_the_arrows_move_it_held_at_either_end()
        {
            core.OpenSearch();
            Type("halo");

            Press(Key(KeyboardKeyKind.Right));
            Assert.Equal(4, Keyboard.Cursor);

            Press(Key(KeyboardKeyKind.Left));
            Press(Key(KeyboardKeyKind.Backspace));
            Assert.Equal("hao", Keyboard.Text);
            Assert.Equal(2, Keyboard.Cursor);

            Press(Key(KeyboardKeyKind.Left));
            Press(Key(KeyboardKeyKind.Left));
            Press(Key(KeyboardKeyKind.Left));
            Assert.Equal(0, Keyboard.Cursor);

            Press(Key(KeyboardKeyKind.Backspace));
            Assert.Equal("hao", Keyboard.Text);
        }

        [Fact]
        public void Space_and_letters_go_in_at_the_cursor()
        {
            core.OpenSearch();
            Type("haloinfinite");
            for (var i = 0; i < 8; i++)
            {
                Press(Key(KeyboardKeyKind.Left));
            }

            Press(Key(KeyboardKeyKind.Space));

            Assert.Equal("halo infinite", Keyboard.Text);
            Assert.Equal(5, Keyboard.Cursor);
        }

        [Fact]
        public void A_physical_keyboard_types_as_typed_at_the_cursor_whatever_Shift_says()
        {
            core.OpenSearch();
            Type("ho");
            Press(Key(KeyboardKeyKind.Left));
            Press(Key(KeyboardKeyKind.Shift));

            Type("al");

            Assert.Equal("halo", Keyboard.Text);
            Assert.Equal(3, Keyboard.Cursor);
        }

        [Fact]
        public void Enter_puts_the_keyboard_away_for_the_results_the_text_kept()
        {
            core.OpenSearch();
            Type("halo");

            Press(Key(KeyboardKeyKind.Enter));

            Assert.False(core.Search.IsKeyboardShown);
            Assert.True(core.Search.IsShown);
            Assert.Equal("halo", Keyboard.Text);
        }

        [Fact]
        public void A_on_the_search_box_brings_the_keyboard_back_with_the_text_and_cursor_as_they_were()
        {
            core.OpenSearch();
            Type("halo");
            Press(Key(KeyboardKeyKind.Left));
            Press(Key(KeyboardKeyKind.Enter));

            core.ShowSearchKeyboard();

            Assert.True(core.Search.IsKeyboardShown);
            Assert.Equal("halo", Keyboard.Text);
            Assert.Equal(3, Keyboard.Cursor);
        }

        [Fact]
        public void Keys_do_nothing_with_the_keyboard_away_or_the_page_gone()
        {
            core.OpenSearch();
            var h = Key("H");
            Press(Key(KeyboardKeyKind.Enter));

            Press(h);
            Assert.Equal(string.Empty, Keyboard.Text);

            core.LeaveSearch();
            core.ShowSearchKeyboard();
            Assert.False(core.Search.IsKeyboardShown);
            Press(h);
            Assert.Equal(string.Empty, Keyboard.Text);
        }

        [Fact]
        public void The_pads_shortcuts_type_on_the_keyboard()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite").Snapshot());
            core.OpenSearch();
            Type("halox");

            Assert.True(core.PressKeyboardShortcut(ControllerButton.X));
            Assert.Equal("halo", Keyboard.Text);

            Assert.True(core.PressKeyboardShortcut(ControllerButton.Y));
            Assert.Equal("halo ", Keyboard.Text);
            Assert.Equal(5, Keyboard.Cursor);

            Assert.True(core.PressKeyboardShortcut(ControllerButton.LeftShoulder));
            Assert.Equal(4, Keyboard.Cursor);
            Assert.True(core.PressKeyboardShortcut(ControllerButton.RightShoulder));
            Assert.Equal(5, Keyboard.Cursor);

            Assert.False(Keyboard.IsShifted);
            Assert.True(core.PressKeyboardShortcut(ControllerButton.LeftStick));
            Assert.True(Keyboard.IsShifted);

            Assert.Equal(new[] { "Halo Infinite" }, Titles(core.Search.Games));
            Assert.True(core.PressKeyboardShortcut(ControllerButton.Start));
            Assert.False(core.Search.IsKeyboardShown);
        }

        [Fact]
        public void Other_buttons_are_not_the_keyboards()
        {
            core.OpenSearch();

            Assert.False(core.PressKeyboardShortcut(ControllerButton.A));
            Assert.False(core.PressKeyboardShortcut(ControllerButton.B));
            Assert.False(core.PressKeyboardShortcut(ControllerButton.DPadLeft));
            Assert.Equal(string.Empty, Keyboard.Text);
        }

        [Fact]
        public void The_shortcuts_do_nothing_with_the_keyboard_away_the_page_gone_or_the_Guide_over_it()
        {
            core.OpenSearch();
            Type("halo");
            core.PressKeyboardShortcut(ControllerButton.Start);

            Assert.False(core.PressKeyboardShortcut(ControllerButton.X));
            Assert.False(core.PressKeyboardShortcut(ControllerButton.Start));

            core.ShowSearchKeyboard();
            Tap(ControllerButton.Guide);
            Assert.False(core.PressKeyboardShortcut(ControllerButton.X));
            Tap(ControllerButton.B);

            core.LeaveSearch();
            core.LeaveSearch();
            Assert.False(core.PressKeyboardShortcut(ControllerButton.X));
            Assert.Equal("halo", Keyboard.Text);
        }

        [Fact]
        public void B_puts_the_keyboard_away_first_then_leaves_the_page_for_the_top_bar()
        {
            core.OpenSearch();
            Type("halo");

            core.LeaveSearch();

            Assert.True(core.Search.IsShown);
            Assert.False(core.Search.IsKeyboardShown);
            Assert.Equal("halo", Keyboard.Text);

            core.LeaveSearch();

            Assert.False(core.Search.IsShown);
            Assert.Equal(SearchOrigin.TopBar, core.Search.OpenedFrom);
        }

        [Fact]
        public void Y_on_My_games_and_apps_Games_opens_the_page_in_its_place_empty_with_the_keyboard_up()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);

            core.SearchFromMyGames(library.Id("Halo Infinite"));

            Assert.True(core.Search.IsShown);
            Assert.False(core.MyGames.IsShown);
            Assert.Equal(SearchOrigin.MyGames, core.Search.OpenedFrom);
            Assert.True(core.Search.IsKeyboardShown);
            Assert.Equal(string.Empty, Keyboard.Text);
        }

        [Fact]
        public void B_on_the_page_opened_from_Games_puts_the_keyboard_away_then_goes_back_to_Games_and_its_game()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);
            core.SearchFromMyGames(library.Id("Halo Infinite"));

            core.LeaveSearch();

            Assert.True(core.Search.IsShown);

            core.LeaveSearch();

            Assert.False(core.Search.IsShown);
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
            Assert.Equal(library.Id("Halo Infinite"), core.MyGames.ReturnsToGame);
        }

        [Theory]
        [InlineData(MyGamesItem.FullLibrary)]
        [InlineData(MyGamesItem.PlayHistory)]
        public void Y_elsewhere_on_My_games_and_apps_opens_nothing(MyGamesItem item)
        {
            core.OpenMyGames(item);

            core.SearchFromMyGames(null);

            Assert.False(core.Search.IsShown);
            Assert.True(core.MyGames.IsShown);
        }

        [Fact]
        public void Y_on_Games_with_text_entry_or_the_Guide_up_opens_nothing()
        {
            core.OpenMyGames(MyGamesItem.Games);
            Tap(ControllerButton.Guide);

            core.SearchFromMyGames(null);

            Assert.False(core.Search.IsShown);
            Assert.True(core.MyGames.IsShown);
        }

        [Fact]
        public void B_on_the_page_opened_from_the_Guide_goes_to_Home()
        {
            FocusGuideSearch();
            Tap(ControllerButton.A);

            core.LeaveSearch();
            core.LeaveSearch();

            Assert.False(core.Search.IsShown);
            Assert.Equal(SearchOrigin.Guide, core.Search.OpenedFrom);
        }

        [Fact]
        public void B_with_the_Guide_over_the_page_answers_the_Guide_without_touching_the_page()
        {
            core.OpenSearch();
            Tap(ControllerButton.Guide);

            // Playnite's own B reaches the page before the pad's B reaches the core.
            core.LeaveSearch();
            Tap(ControllerButton.B);

            Assert.True(core.Search.IsShown);
            Assert.True(core.Search.IsKeyboardShown);
            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void A_on_a_found_game_opens_its_Game_hub_at_once_with_the_game_in_the_main_list_the_page_staying()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", installed: false)
                .WithGame("Forza Horizon 5");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");

            core.ChooseSearchGame(library.Id("Halo Infinite"));

            Assert.Equal(new[] { library.Id("Halo Infinite") }, playnite.OpenedHubs);
            Assert.Contains("Halo Infinite", MainList(library));
            Assert.True(core.Search.IsShown);
            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void A_on_a_recently_added_game_opens_its_Game_hub()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", added: new DateTime(2026, 1, 1));
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();

            core.ChooseSearchGame(core.Search.RecentlyAdded[0].GameId);

            Assert.Equal(new[] { library.Id("Halo Infinite") }, playnite.OpenedHubs);
        }

        [Fact]
        public void A_twice_on_a_found_game_opens_its_hub_and_plays_nothing()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");

            core.ChooseSearchGame(library.Id("Halo Infinite"));
            core.ChooseSearchGame(library.Id("Halo Infinite"));

            Assert.Empty(playnite.StartedGames);
        }

        [Fact]
        public void A_Home_tile_focused_as_the_page_opened_takes_neither_the_pads_A_nor_Start()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 1));
            core.OnLibraryChanged(library.Snapshot());
            core.FocusHomeTile(core.Home.RecentRow[0]);
            core.OpenSearch();
            core.PressKeyboardShortcut(ControllerButton.Start);

            Tap(ControllerButton.A);
            Tap(ControllerButton.Start);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Empty(playnite.OpenedHubs);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Space_turns_Shift_off_as_any_character_does()
        {
            core.OpenSearch();

            core.PressKeyboardShortcut(ControllerButton.Y);

            Assert.False(core.Search.Keyboard.IsShifted);
        }

        [Fact]
        public void A_physical_Backspace_deletes_at_the_cursor_with_the_keyboard_up_or_away()
        {
            core.OpenSearch();
            Type("halo");

            core.DeleteText();
            Assert.Equal("hal", core.Search.Keyboard.Text);

            core.PressKeyboardShortcut(ControllerButton.Start);
            core.DeleteText();
            Assert.Equal("ha", core.Search.Keyboard.Text);
        }

        [Fact]
        public void A_game_the_page_doesnt_show_opens_nothing()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite").WithGame("Hidden", hidden: true);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");

            core.ChooseSearchGame(library.Id("Hidden"));
            core.ChooseSearchGame(Guid.NewGuid());

            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void A_reaching_the_page_under_the_Guide_or_with_the_page_gone_opens_nothing()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite").WithApp("Halo Mod Tool", @"C:\Halo\mod.exe");
            var snapshot = library.Snapshot();
            core.OnLibraryChanged(snapshot);
            core.OpenSearch();
            Type("halo");
            Tap(ControllerButton.Guide);

            core.ChooseSearchGame(library.Id("Halo Infinite"));
            core.StartSearchApp(snapshot.Apps[0].Id);

            Tap(ControllerButton.B);
            core.LeaveSearch();
            core.LeaveSearch();
            core.ChooseSearchGame(library.Id("Halo Infinite"));
            core.StartSearchApp(snapshot.Apps[0].Id);

            Assert.Empty(playnite.OpenedHubs);
            Assert.Empty(playnite.StartedApps);
        }

        [Fact]
        public void Leaving_the_page_gives_the_main_list_back_to_the_users_own_filter()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");
            core.ChooseSearchGame(library.Id("Halo Infinite"));

            core.LeaveSearch();
            core.LeaveSearch();

            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.Equal(1, playnite.UserFilterRestores);
        }

        [Fact]
        public void Leaving_a_page_that_opened_no_game_leaves_the_users_filter_alone()
        {
            core.OpenSearch();
            core.LeaveSearch();
            core.LeaveSearch();

            Assert.Equal(0, playnite.UserFilterRestores);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void My_games_and_apps_taking_the_pages_place_after_a_game_opened_shows_its_own_filter()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite").WithGame("Not Installed", installed: false);
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("i");
            core.ChooseSearchGame(library.Id("Not Installed"));

            core.OpenMyGames(MyGamesItem.Games);

            Assert.Equal(new[] { "Halo Infinite" }, MainList(library));
            core.LeaveMyGames();
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Settings_or_Home_taking_the_pages_place_gives_the_users_filter_back(bool settings)
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");
            core.ChooseSearchGame(library.Id("Halo Infinite"));

            if (settings)
            {
                core.OpenSettings();
            }
            else
            {
                Tap(ControllerButton.Guide);
                Tap(ControllerButton.A);
            }

            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Playnite_stopping_gives_the_users_filter_back()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenSearch();
            Type("halo");
            core.ChooseSearchGame(library.Id("Halo Infinite"));

            core.OnApplicationStopped();

            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.False(playnite.ReleasedUnderOwnFilter);
        }

        [Fact]
        public void A_on_a_found_App_starts_it_without_the_Launch_screen()
        {
            var snapshot = new FakeLibrary()
                .WithApp("Steam", @"C:\Steam\steam.exe", "-bigpicture")
                .WithApp("Discord", @"C:\Discord\Update.exe")
                .Snapshot();
            core.OnLibraryChanged(snapshot);
            core.OpenSearch();
            Type("steam");

            core.StartSearchApp(snapshot.Apps[0].Id);
            core.StartSearchApp(snapshot.Apps[1].Id);

            var started = Assert.Single(playnite.StartedApps);
            Assert.Equal(@"C:\Steam\steam.exe", started.Path);
            Assert.False(core.LaunchScreen.IsShown);
            Assert.True(core.Search.IsShown);
        }

        private ConsoleKeyboard Keyboard => core.Search.Keyboard;

        private string[] MainList(FakeLibrary library) =>
            playnite.MainListFilter.Apply(library.Snapshot().Games).Select(g => g.Name).ToArray();

        /// <summary>What the Character keys of <paramref name="row"/> read now, left to right.</summary>
        private string RowText(int row) =>
            string.Concat(Keyboard.Keys.Where(k => k.Kind == KeyboardKeyKind.Character && k.Row == row).OrderBy(k => k.Column).Select(k => k.Text));

        private static string[] Titles(IEnumerable<HomeTile> tiles) => tiles.Select(t => t.Title).ToArray();

        /// <summary>Deletes everything typed, from the end.</summary>
        private void Clear()
        {
            var length = Keyboard.Text.Length;
            for (var i = 0; i < length; i++)
            {
                core.PressKey(Key(KeyboardKeyKind.Right));
            }

            for (var i = 0; i < length; i++)
            {
                core.PressKey(Key(KeyboardKeyKind.Backspace));
            }

            Assert.Equal(string.Empty, Keyboard.Text);
        }

        /// <summary>Opens the Guide and moves focus along its utilities to Search, the last.</summary>
        private void FocusGuideSearch()
        {
            Tap(ControllerButton.Guide);
            foreach (var _ in core.Guide.Items)
            {
                Tap(ControllerButton.DPadDown);
            }

            foreach (var _ in core.Guide.Utilities.TakeWhile(u => u.Kind != GuideItemKind.Search))
            {
                Tap(ControllerButton.DPadRight);
            }

            Assert.Equal(GuideItemKind.Search, core.Guide.FocusedItem.Kind);
        }

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        /// <summary>The keyboard's key of <paramref name="kind"/>, of which there is one but for Character keys.</summary>
        private KeyboardKey Key(KeyboardKeyKind kind) => Keyboard.Keys.Single(k => k.Kind == kind);

        /// <summary>The Character key reading <paramref name="text"/> now.</summary>
        private KeyboardKey Key(string text) => Keyboard.Keys.Single(k => k.Kind == KeyboardKeyKind.Character && k.Text == text);

        private void Press(KeyboardKey key) => core.PressKey(key);

        /// <summary>Types on a physical keyboard.</summary>
        private void Type(string text) => core.TypeText(text);

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
