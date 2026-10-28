using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Tile menu's More options: a flyout over the dimmed menu holding what the console keeps off the menu itself,
    /// B stepping back one level at a time.
    /// </summary>
    public class TileMenuFlyoutTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 20, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public TileMenuFlyoutTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            library = new FakeLibrary()
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11))
                .WithGame("Skyrim", lastActivity: new DateTime(2026, 10, 10), otherActions: new[] { "Launch config tool", "Open mods folder" }, hdr: true);
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void More_options_opens_the_flyout_over_the_menu_which_stays_on_More_options()
        {
            OpenMoreOptionsOn("Hades");

            Assert.NotNull(core.Home.TileMenuFlyout);
            Assert.Equal("Hades", core.Home.TileMenu.Title);
            Assert.Equal("More options", core.Home.TileMenu.FocusedOption.Label);
        }

        [Fact]
        public void A_game_with_no_other_actions_on_a_display_without_HDR_offers_Hide_Extensions_and_Remove()
        {
            OpenMoreOptionsOn("Hades");

            Assert.Equal(new[] { "Hide this game", "Extensions", "Remove" }, Flyout());
            Assert.Equal(
                new[] { DialogIcon.Hide, DialogIcon.Extensions, DialogIcon.RemoveGame },
                core.Home.TileMenuFlyout.Options.Select(o => o.Icon));
            Assert.Equal("Hide this game", core.Home.TileMenuFlyout.FocusedOption.Label);
        }

        [Fact]
        public void The_games_other_actions_come_first_and_HDR_shows_on_an_HDR_display()
        {
            playnite.IsHdrSupported = true;

            OpenMoreOptionsOn("Skyrim");

            Assert.Equal(new[] { "Launch config tool", "Open mods folder", "Hide this game", "Disable HDR", "Extensions", "Remove" }, Flyout());
            Assert.Equal(DialogIcon.GameAction, core.Home.TileMenuFlyout.Options[0].Icon);
            Assert.Equal(DialogIcon.Hdr, core.Home.TileMenuFlyout.Options[3].Icon);
        }

        [Fact]
        public void An_other_action_runs_that_action_and_closes_the_menu()
        {
            OpenMoreOptionsOn("Skyrim");

            Choose("Open mods folder");

            Assert.Equal(new[] { (library.Id("Skyrim"), 1) }, playnite.RanActions);
            Assert.Null(core.Home.TileMenuFlyout);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Hide_this_game_hides_it()
        {
            OpenMoreOptionsOn("Hades");

            Choose("Hide this game");

            Assert.Equal(new[] { (library.Id("Hades"), true) }, playnite.HiddenChanges);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void A_hidden_game_offers_Unhide_this_game_which_shows_it_again()
        {
            OpenMenuOn("Hades");
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(library.Id("Hades"), "Hades", lastActivity: new DateTime(2026, 10, 11), hidden: true)
                .Snapshot());
            ChooseOnMenu("More options");

            Assert.Equal("Unhide this game", Flyout()[0]);
            Assert.Equal(DialogIcon.Unhide, core.Home.TileMenuFlyout.Options[0].Icon);

            Choose("Unhide this game");

            Assert.Equal(new[] { (library.Id("Hades"), false) }, playnite.HiddenChanges);
        }

        [Fact]
        public void Enable_HDR_turns_it_on_for_a_game_without_it()
        {
            playnite.IsHdrSupported = true;
            OpenMoreOptionsOn("Hades");

            Choose("Enable HDR");

            Assert.Equal(new[] { (library.Id("Hades"), true) }, playnite.HdrChanges);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Disable_HDR_turns_it_off_for_a_game_with_it()
        {
            playnite.IsHdrSupported = true;
            OpenMoreOptionsOn("Skyrim");

            Choose("Disable HDR");

            Assert.Equal(new[] { (library.Id("Skyrim"), false) }, playnite.HdrChanges);
        }

        [Fact]
        public void Extensions_opens_Playnites_Extensions_picker_for_the_game()
        {
            OpenMoreOptionsOn("Hades");

            Choose("Extensions");

            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedExtensionMenus);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Remove_asks_first_with_a_prompt()
        {
            OpenMoreOptionsOn("Hades");

            Choose("Remove");

            Assert.Equal("Remove Hades?", core.Home.TileMenuFlyout.Title);
            Assert.True(core.Home.TileMenuFlyout.IsPrompt);
            Assert.Equal(new[] { "Remove", "Cancel" }, Flyout());
            Assert.Equal(DialogIcon.RemoveGame, core.Home.TileMenuFlyout.Options[0].Icon);
            Assert.Empty(playnite.RemovedGames);
        }

        [Fact]
        public void Confirming_Remove_removes_the_game_and_closes_the_menu()
        {
            OpenMoreOptionsOn("Hades");
            Choose("Remove");

            Choose("Remove");

            Assert.Equal(new[] { library.Id("Hades") }, playnite.RemovedGames);
            Assert.Null(core.Home.TileMenuFlyout);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Cancel_on_the_Remove_prompt_goes_back_to_the_flyout_on_Remove()
        {
            OpenMoreOptionsOn("Hades");
            Choose("Remove");

            Choose("Cancel");

            Assert.Equal(new[] { "Hide this game", "Extensions", "Remove" }, Flyout());
            Assert.Equal("Remove", core.Home.TileMenuFlyout.FocusedOption.Label);
            Assert.Empty(playnite.RemovedGames);
        }

        [Fact]
        public void B_steps_back_one_level_at_a_time_from_the_Remove_prompt_to_the_flyout_the_menu_and_out()
        {
            OpenMoreOptionsOn("Hades");
            Choose("Remove");

            Tap(ControllerButton.B);

            Assert.Equal("Remove", core.Home.TileMenuFlyout.FocusedOption.Label);
            Assert.False(core.Home.TileMenuFlyout.IsPrompt);

            Tap(ControllerButton.B);

            Assert.Null(core.Home.TileMenuFlyout);
            Assert.Equal("More options", core.Home.TileMenu.FocusedOption.Label);

            Tap(ControllerButton.B);

            Assert.Null(core.Home.TileMenu);
            Assert.Empty(playnite.RemovedGames);
        }

        [Fact]
        public void Up_and_down_move_through_the_flyout_and_left_and_right_dont_leave_it()
        {
            OpenMoreOptionsOn("Hades");

            Tap(ControllerButton.DPadDown);
            Assert.Equal("Extensions", core.Home.TileMenuFlyout.FocusedOption.Label);

            Tap(ControllerButton.DPadLeft);
            Tap(ControllerButton.DPadRight);
            Assert.Equal("Extensions", core.Home.TileMenuFlyout.FocusedOption.Label);
            Assert.Equal("More options", core.Home.TileMenu.FocusedOption.Label);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal("Remove", core.Home.TileMenuFlyout.FocusedOption.Label);

            Tap(ControllerButton.DPadUp);
            Assert.Equal("Extensions", core.Home.TileMenuFlyout.FocusedOption.Label);
        }

        [Fact]
        public void The_mouse_points_at_and_picks_the_flyouts_rows_not_the_menus_behind_it()
        {
            OpenMoreOptionsOn("Hades");
            var extensions = core.Home.TileMenuFlyout.Options[1];

            core.FocusOption(core.Home.TileMenu.Options[0]);
            Assert.Equal("More options", core.Home.TileMenu.FocusedOption.Label);

            core.ChooseOption(extensions);
            Assert.Equal(new[] { library.Id("Hades") }, playnite.OpenedExtensionMenus);
        }

        [Fact]
        public void Closing_the_menu_from_the_Dashboard_takes_the_flyout_with_it()
        {
            OpenMoreOptionsOn("Hades");

            core.CloseTileMenu();

            Assert.Null(core.Home.TileMenuFlyout);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void The_A_that_chose_from_the_flyout_does_not_also_choose_the_tile_under_it()
        {
            var tile = RecentTile("Hades");
            OpenMoreOptionsOn("Hades");

            // Playnite's own Enter for the same press reaches the tile once the menu has gone.
            Press(ControllerButton.A);
            core.ChooseHomeTile(tile);
            Release(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { (library.Id("Hades"), true) }, playnite.HiddenChanges);
            Assert.Empty(playnite.OpenedHubs);
        }

        private HomeTile RecentTile(string title) => core.Home.RecentRow.Single(t => t.Title == title);

        private string[] Flyout() => core.Home.TileMenuFlyout.Options.Select(o => o.Label).ToArray();

        private void OpenMenuOn(string title)
        {
            core.FocusHomeTile(RecentTile(title));
            Tap(ControllerButton.Start);
        }

        private void OpenMoreOptionsOn(string title)
        {
            OpenMenuOn(title);
            ChooseOnMenu("More options");
        }

        private void ChooseOnMenu(string label)
        {
            while (core.Home.TileMenu.FocusedOption.Label != label)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.A);
        }

        /// <summary>Chooses <paramref name="label"/> on the flyout, or the prompt in its place.</summary>
        private void Choose(string label)
        {
            while (core.Home.TileMenuFlyout.FocusedOption.Label != label)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.A);
        }

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        private void Tap(ControllerButton button)
        {
            Press(button);
            Release(button);
        }
    }
}
