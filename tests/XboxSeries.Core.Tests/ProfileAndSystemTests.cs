using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class ProfileAndSystemTests
    {
        private const string HaloDirectory = @"C:\Games\Halo Infinite";

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeUserAccount account = new FakeUserAccount { Name = "Gabe", Picture = @"C:\pics\gabe.png" };
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public ProfileAndSystemTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, sounds, new FakeDrives(), account, new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), installDirectory: HaloDirectory, coverImage: @"C:\covers\halo.jpg");
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void The_Guide_has_the_landing_tab_then_Profile_and_system_and_opens_on_the_landing_tab()
        {
            Tap(ControllerButton.Guide);

            Assert.Equal(new[] { GuideTabKind.Landing, GuideTabKind.ProfileAndSystem }, core.Guide.Tabs.Select(t => t.Kind));
            Assert.Equal(GuideTabKind.Landing, SelectedTab());
            Assert.Equal(new[] { true, false }, core.Guide.Tabs.Select(t => t.IsSelected));
        }

        [Fact]
        public void RB_and_LB_move_between_tabs_and_stop_at_the_ends_with_a_sound_only_when_they_move()
        {
            Tap(ControllerButton.Guide);
            sounds.Played.Clear();

            Tap(ControllerButton.LeftShoulder);
            Assert.Equal(GuideTabKind.Landing, SelectedTab());

            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.ProfileAndSystem, SelectedTab());

            Tap(ControllerButton.RightShoulder);
            Assert.Equal(GuideTabKind.ProfileAndSystem, SelectedTab());

            Tap(ControllerButton.LeftShoulder);
            Assert.Equal(GuideTabKind.Landing, SelectedTab());
            Assert.Equal("Home", core.Guide.FocusedItem.Title);
            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate }, sounds.Played);
        }

        [Fact]
        public void The_Guide_always_reopens_on_the_landing_tab()
        {
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.RightShoulder);
            Tap(ControllerButton.B);

            Tap(ControllerButton.Guide);

            Assert.Equal(GuideTabKind.Landing, SelectedTab());
            Assert.Equal("Home", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void Profile_and_system_shows_the_profile_card_with_the_Windows_user_then_Settings_and_Power()
        {
            OpenProfileTab();

            var items = core.Guide.Items;
            Assert.Equal(new[] { GuideItemKind.Profile, GuideItemKind.Settings, GuideItemKind.Power }, items.Select(i => i.Kind));
            Assert.Equal(new[] { "Gabe", "Settings", "Power" }, items.Select(i => i.Title));
            Assert.Equal(@"C:\pics\gabe.png", items[0].Image);
            Assert.Equal(new[] { false, true, true }, items.Select(i => i.HasDividerAbove));
            Assert.Equal(GuideItemKind.Profile, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void The_profile_tab_shows_the_account_picture_and_follows_a_change_of_it()
        {
            Tap(ControllerButton.Guide);
            Assert.Equal(@"C:\pics\gabe.png", core.Guide.Tabs.Last().Image);

            account.Picture = @"C:\pics\new.png";
            Tap(ControllerButton.RightShoulder);

            Assert.Equal(@"C:\pics\new.png", core.Guide.Tabs.Last().Image);
        }

        [Fact]
        public void Recent_games_on_the_landing_tab_show_their_cover()
        {
            Tap(ControllerButton.Guide);

            Assert.Equal(@"C:\covers\halo.jpg", core.Guide.Items.Single(i => i.Title == "Halo Infinite").Image);
        }

        [Fact]
        public void A_on_the_profile_card_does_nothing()
        {
            OpenProfileTab();
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.True(core.Guide.IsOpen);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Settings_brings_Playnite_to_the_front_on_the_Settings_page_once_A_is_released()
        {
            windows.OpenWindow();
            OpenProfileTab();
            Tap(ControllerButton.DPadDown);

            Press(ControllerButton.A);

            Assert.False(core.Guide.IsOpen);
            Assert.False(core.Settings.IsShown);

            Release(ControllerButton.A);

            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.Personalisation, core.Settings.Category);
            Assert.Equal(SettingsOrigin.Guide, core.Settings.OpenedFrom);
            Assert.Equal(DashboardPage.Settings, playnite.Page);
            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.Equal(0, playnite.PlayniteSettingsOpened);
        }

        [Fact]
        public void Settings_from_the_Guide_takes_My_games_and_apps_place_with_the_users_own_filter_back()
        {
            core.OpenMyGames(MyGamesItem.Games);
            OpenProfileTab();
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.True(core.Settings.IsShown);
            Assert.False(core.MyGames.IsShown);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void The_Guides_Home_entry_leaves_the_Settings_page()
        {
            core.OpenSettings();
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.A);

            Assert.False(core.Settings.IsShown);
            Assert.Equal(DashboardPage.Home, playnite.Page);
        }

        [Fact]
        public void The_Guides_My_games_and_apps_entry_takes_the_Settings_pages_place()
        {
            core.OpenSettings();
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.False(core.Settings.IsShown);
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(DashboardPage.MyGamesAndApps, playnite.Page);
        }

        [Fact]
        public void Power_asks_what_to_do_over_the_Guide_with_focus_on_Close()
        {
            OpenPowerFromTheGuide();

            var dialog = core.Guide.Dialog;
            Assert.Equal("What do you want to do?", dialog.Title);
            Assert.True(dialog.IsWide);
            Assert.Equal(new[] { "Turn off PC", "Restart PC", "Sleep", "Exit Playnite" }, dialog.Tiles.Select(o => o.Label));
            Assert.Equal("Close", dialog.CloseOption.Label);
            Assert.Same(dialog.CloseOption, dialog.FocusedOption);
            Assert.True(core.Guide.ShowsPanel);
        }

        [Fact]
        public void Close_just_takes_Power_away_leaving_the_Guide_on_Power()
        {
            OpenPowerFromTheGuide();

            Tap(ControllerButton.A);

            Assert.Null(core.Guide.Dialog);
            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideItemKind.Power, core.Guide.FocusedItem.Kind);
            Assert.Empty(playnite.PowerCommands);
        }

        [Fact]
        public void B_on_Power_takes_it_away_too()
        {
            OpenPowerFromTheGuide();

            Tap(ControllerButton.B);

            Assert.Null(core.Guide.Dialog);
            Assert.True(core.Guide.IsOpen);
        }

        [Theory]
        [InlineData("Turn off PC", PowerCommand.TurnOffPc)]
        [InlineData("Restart PC", PowerCommand.RestartPc)]
        [InlineData("Exit Playnite", PowerCommand.ExitPlaynite)]
        public void Turn_off_restart_and_exit_bring_Playnite_to_the_front_and_carry_out_once_A_is_released(string label, PowerCommand command)
        {
            windows.OpenWindow();
            OpenPowerFromTheGuide();
            FocusOption(label);

            Press(ControllerButton.A);

            Assert.False(core.Guide.IsOpen);
            Assert.Empty(playnite.PowerCommands);

            Release(ControllerButton.A);

            Assert.Equal(new[] { command }, playnite.PowerCommands);
            Assert.Equal(playnite.Window, windows.Foreground);
            Assert.False(core.Guide.IsOverlayShown);
        }

        [Fact]
        public void Sleep_hands_focus_back_to_the_window_in_front_so_the_PC_wakes_into_it()
        {
            var game = windows.OpenWindow();
            OpenPowerFromTheGuide();
            FocusOption("Sleep");

            Tap(ControllerButton.A);

            Assert.Equal(new[] { PowerCommand.Sleep }, playnite.PowerCommands);
            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void Clicking_a_Power_option_carries_it_out_at_once()
        {
            OpenPowerFromTheGuide();

            core.ChooseOption(core.Guide.Dialog.Tiles[0]);

            Assert.Equal(new[] { PowerCommand.TurnOffPc }, playnite.PowerCommands);
            Assert.False(core.Guide.IsOverlayShown);
        }

        [Fact]
        public void The_D_pad_moves_through_Power_as_a_grid_of_three_with_Close_below()
        {
            OpenPowerFromTheGuide();

            Tap(ControllerButton.DPadUp);
            Assert.Equal("Exit Playnite", FocusedOption());

            Tap(ControllerButton.DPadRight);
            Assert.Equal("Exit Playnite", FocusedOption());

            Tap(ControllerButton.DPadUp);
            Assert.Equal("Turn off PC", FocusedOption());

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadRight);
            Assert.Equal("Sleep", FocusedOption());

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadUp);
            Assert.Equal("Sleep", FocusedOption());

            Tap(ControllerButton.DPadLeft);
            Assert.Equal("Restart PC", FocusedOption());

            // Nothing below Restart PC on the second row: down goes on to the row below it.
            Tap(ControllerButton.DPadDown);
            Assert.Equal("Exit Playnite", FocusedOption());

            Tap(ControllerButton.DPadDown);
            Assert.Equal("Close", FocusedOption());

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadLeft);
            Assert.Equal("Close", FocusedOption());
        }

        [Fact]
        public void A_tap_opens_the_Guide_300_ms_after_the_press()
        {
            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Release(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(199));

            Assert.False(core.Guide.IsOverlayShown);
            Assert.Empty(sounds.Played);

            clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.True(core.Guide.IsOpen);
            Assert.True(core.Guide.ShowsPanel);
            Assert.Equal(new[] { UiSound.OpenMenu }, sounds.Played);
        }

        [Fact]
        public void A_press_let_go_after_300_ms_but_before_the_hold_opens_the_Guide_on_release()
        {
            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(500));

            Assert.False(core.Guide.IsOverlayShown);

            Release(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);
            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void Pressing_again_before_a_tap_has_opened_the_Guide_starts_over()
        {
            Tap(ControllerButton.Guide, settle: false);
            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("What do you want to do?", core.Guide.Dialog?.Title);
            Assert.False(core.Guide.ShowsPanel);
        }

        [Fact]
        public void A_short_press_of_the_Guide_button_still_opens_and_closes_the_Guide_without_Power()
        {
            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(699));
            Release(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.True(core.Guide.IsOpen);
            Assert.Null(core.Guide.Dialog);

            Tap(ControllerButton.Guide);

            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void Holding_the_Guide_button_for_700_ms_opens_Power_on_its_own_over_the_dim()
        {
            var game = windows.OpenWindow();

            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(699));

            // Not the Guide first: until the hold is known, nothing opens.
            Assert.False(core.Guide.IsOverlayShown);
            Assert.Empty(sounds.Played);

            clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal("What do you want to do?", core.Guide.Dialog?.Title);
            Assert.True(core.Guide.IsOpen);
            Assert.False(core.Guide.ShowsPanel);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Release(ControllerButton.Guide);

            Assert.NotNull(core.Guide.Dialog);
            Assert.True(core.Guide.IsOverlayShown);
        }

        [Fact]
        public void A_hold_still_opens_Power_when_a_busy_UI_thread_delivers_the_release_before_the_timer_fires()
        {
            // Opening the Guide can keep the UI thread busy past the hold, so the release, queued first, is handled
            // before the hold timer's tick. The buttons' own times decide it.
            windows.OpenWindow();
            var pressedAt = clock.Now;

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed, pressedAt);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released, pressedAt + TimeSpan.FromSeconds(1.2));

            Assert.Equal("What do you want to do?", core.Guide.Dialog?.Title);
            Assert.False(core.Guide.ShowsPanel);
        }

        [Fact]
        public void A_tap_handled_late_by_a_busy_UI_thread_is_still_a_tap()
        {
            windows.OpenWindow();
            var pressedAt = clock.Now;
            clock.Advance(TimeSpan.FromSeconds(2));

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed, pressedAt);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released, pressedAt + TimeSpan.FromSeconds(0.2));
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Null(core.Guide.Dialog);
            Assert.True(core.Guide.ShowsPanel);
        }

        [Fact]
        public void Holding_the_Guide_button_with_the_Guide_open_swaps_the_Guide_for_Power_and_keeps_the_window_to_return_to()
        {
            var game = windows.OpenWindow();
            Tap(ControllerButton.Guide);

            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(1));
            Release(ControllerButton.Guide);

            Assert.False(core.Guide.ShowsPanel);
            Assert.NotNull(core.Guide.Dialog);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Tap(ControllerButton.B);

            Assert.Equal(game, windows.Foreground);
        }

        [Theory]
        [InlineData(ControllerButton.A)]
        [InlineData(ControllerButton.B)]
        public void Closing_Power_opened_by_holding_hands_focus_back_once_the_button_is_released(ControllerButton button)
        {
            var game = windows.OpenWindow();
            HoldGuide();

            Press(button);

            Assert.Null(core.Guide.Dialog);
            Assert.False(core.Guide.IsOpen);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            Release(button);

            Assert.False(core.Guide.IsOverlayShown);
            Assert.Equal(game, windows.Foreground);
        }

        [Fact]
        public void The_Guide_opened_after_Power_on_its_own_shows_its_panel_again()
        {
            HoldGuide();
            Tap(ControllerButton.B);

            Tap(ControllerButton.Guide);

            Assert.True(core.Guide.ShowsPanel);
            Assert.Null(core.Guide.Dialog);
        }

        [Fact]
        public void LB_and_RB_do_nothing_while_Power_is_up()
        {
            OpenPowerFromTheGuide();

            Tap(ControllerButton.LeftShoulder);

            Assert.Equal(GuideTabKind.ProfileAndSystem, SelectedTab());
        }

        [Fact]
        public void A_game_stopping_leaves_Power_up()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            HoldGuide();

            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.NotNull(core.Guide.Dialog);
        }

        [Fact]
        public void The_hints_offer_power_options_always_and_more_options_on_the_running_game()
        {
            core.OnGameStarted(library.Id("Halo Infinite"));
            Tap(ControllerButton.Guide);

            Assert.Equal(new[] { (ControllerButton.Guide, "Hold for power options") }, Hints());

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);

            Assert.Equal(
                new[] { (ControllerButton.Guide, "Hold for power options"), (ControllerButton.Start, "More options") },
                Hints());

            Tap(ControllerButton.RightShoulder);

            Assert.Equal(new[] { (ControllerButton.Guide, "Hold for power options") }, Hints());
        }

        [Fact]
        public void The_profile_on_Homes_top_bar_opens_the_Guide_on_Profile_and_system_and_B_goes_back_to_Playnite()
        {
            playnite.BringToFront();

            core.OpenProfile();

            Assert.True(core.Guide.IsOpen);
            Assert.True(core.Guide.ShowsPanel);
            Assert.Equal(GuideTabKind.ProfileAndSystem, SelectedTab());
            Assert.Equal(GuideItemKind.Profile, core.Guide.FocusedItem.Kind);
            Assert.Equal(new[] { UiSound.OpenMenu }, sounds.Played);
            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);

            // The A that chose the profile reaches the core too, after the Guide opened; on the profile card it does nothing.
            Tap(ControllerButton.A);
            Assert.True(core.Guide.IsOpen);

            Tap(ControllerButton.B);

            Assert.Equal(playnite.Window, windows.Foreground);
        }

        private void OpenProfileTab()
        {
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.RightShoulder);
        }

        private void OpenPowerFromTheGuide()
        {
            OpenProfileTab();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);
        }

        private void HoldGuide()
        {
            Press(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(1));
            Release(ControllerButton.Guide);
        }

        private void FocusOption(string label) => core.FocusOption(core.Guide.Dialog.Options.Single(o => o.Label == label));

        private string FocusedOption() => core.Guide.Dialog.FocusedOption.Label;

        private GuideTabKind SelectedTab() => core.Guide.Tabs.Single(t => t.IsSelected).Kind;

        private (ControllerButton, string)[] Hints() => core.Guide.Hints.Select(h => (h.Button, h.Text)).ToArray();

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        private void Release(ControllerButton button) => core.OnControllerButton(button, ButtonState.Released);

        /// <summary>Pressed and let go. A Guide tap opens the Guide 0.3 s after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button, bool settle = true)
        {
            Press(button);
            Release(button);
            if (button == ControllerButton.Guide && settle)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
