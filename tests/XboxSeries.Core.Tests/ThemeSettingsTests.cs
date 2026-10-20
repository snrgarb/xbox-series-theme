using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Settings' Theme settings: its boxes (Personalisation, Guide and Discord), the page each opens in the pane, the routes
    // in that land on one, and B stepping back one level at a time to where Settings was opened from.
    public class ThemeSettingsTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 20, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;

        public ThemeSettingsTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, sounds, new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void Customise_Home_opens_Theme_settings_on_Personalisation_focus_in_its_boxes()
        {
            CustomiseHomeOn("Hades");

            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.Personalisation, core.Settings.ThemePage);
            Assert.True(core.Settings.OpensInPane);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);
            Assert.Equal(library.Id("Hades"), core.Settings.OpenedFromGame);
        }

        [Fact]
        public void Customise_games_and_apps_opens_Theme_settings_on_Personalisation_in_the_pages_place()
        {
            CustomiseGamesAndAppsOn("Hades");

            Assert.True(core.Settings.IsShown);
            Assert.False(core.MyGames.IsShown);
            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.Personalisation, core.Settings.ThemePage);
            Assert.True(core.Settings.OpensInPane);
            Assert.Equal(SettingsOrigin.MyGamesTile, core.Settings.OpenedFrom);
        }

        [Fact]
        public void Homes_Discord_tile_before_a_Discord_tab_opens_Theme_settings_on_Discord_focus_in_its_options()
        {
            core.OpenDiscordFromHome();

            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.Discord, core.Settings.ThemePage);
            Assert.True(core.Settings.OpensInPane);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);
        }

        [Fact]
        public void The_top_bar_opens_Theme_settings_on_its_boxes_focus_on_the_rail()
        {
            core.OpenSettings();

            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
            Assert.False(core.Settings.OpensInPane);
        }

        [Fact]
        public void The_top_bar_after_Customise_opens_on_Theme_settings_boxes_again()
        {
            CustomiseHomeOn("Hades");
            core.LeaveThemePage();
            core.LeaveSettings();

            core.OpenSettings();

            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
            Assert.False(core.Settings.OpensInPane);
        }

        [Fact]
        public void The_top_bar_after_Homes_Discord_tile_opens_on_Theme_settings_boxes_again()
        {
            core.OpenDiscordFromHome();
            core.LeaveSettings();
            core.LeaveSettings();

            core.OpenSettings();

            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
            Assert.False(core.Settings.OpensInPane);
        }

        [Theory]
        [InlineData(ThemeSettingsPage.Personalisation)]
        [InlineData(ThemeSettingsPage.Guide)]
        [InlineData(ThemeSettingsPage.Discord)]
        public void A_on_a_box_opens_its_page_in_the_pane(ThemeSettingsPage page)
        {
            core.OpenSettings();

            core.OpenThemePage(page);

            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(page, core.Settings.ThemePage);
        }

        [Fact]
        public void A_box_opening_its_page_plays_nothing_of_its_own()
        {
            core.OpenSettings();

            core.OpenThemePage(ThemeSettingsPage.Guide);

            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void A_page_opens_nothing_with_Settings_gone()
        {
            core.OpenThemePage(ThemeSettingsPage.Guide);

            Assert.False(core.Settings.IsShown);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
        }

        [Fact]
        public void A_page_opens_nothing_on_another_category()
        {
            core.OpenSettings();
            core.FocusSettingsCategory(SettingsCategory.PlayniteSettings);

            core.OpenThemePage(ThemeSettingsPage.Discord);

            Assert.Equal(SettingsCategory.PlayniteSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
        }

        [Fact]
        public void B_on_a_page_goes_back_to_Theme_settings_boxes()
        {
            core.OpenSettings();
            core.OpenThemePage(ThemeSettingsPage.Guide);

            core.LeaveThemePage();

            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.ThemeSettings, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
        }

        [Fact]
        public void B_on_a_page_a_tile_opened_leaves_focus_to_the_boxes_from_then_on()
        {
            core.OpenDiscordFromHome();

            core.LeaveThemePage();

            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
            Assert.False(core.Settings.OpensInPane);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);
        }

        [Fact]
        public void Leaving_a_page_does_nothing_with_Settings_gone()
        {
            core.LeaveThemePage();

            Assert.False(core.Settings.IsShown);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
        }

        [Fact]
        public void Settings_closing_on_a_page_leaves_no_page_showing()
        {
            core.OpenSettings();
            core.OpenThemePage(ThemeSettingsPage.Discord);

            core.OpenMyGames(MyGamesItem.Games);

            Assert.False(core.Settings.IsShown);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
        }

        [Theory]
        [InlineData(SettingsCategory.ThemeSettings)]
        [InlineData(SettingsCategory.PlayniteSettings)]
        public void Focus_on_the_rail_leaves_Theme_settings_on_its_boxes(SettingsCategory category)
        {
            core.OpenSettings();
            core.OpenThemePage(ThemeSettingsPage.Personalisation);

            core.FocusSettingsCategory(category);

            Assert.Equal(category, core.Settings.Category);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);
        }

        [Fact]
        public void B_on_a_page_with_Settings_own_B_steps_back_to_the_boxes_the_page_staying()
        {
            core.OpenSettings();
            core.OpenThemePage(ThemeSettingsPage.Guide);

            core.LeaveSettings();

            Assert.True(core.Settings.IsShown);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);

            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
        }

        [Fact]
        public void B_on_a_page_steps_back_nothing_with_text_entry_over_it()
        {
            core.OpenDiscordFromHome();
            core.TypeDiscordClientId();

            core.LeaveSettings();

            Assert.True(core.TextEntry.IsShown);
            Assert.Equal(ThemeSettingsPage.Discord, core.Settings.ThemePage);
        }

        [Fact]
        public void B_from_Customise_games_and_apps_steps_back_to_the_boxes_then_to_the_page_and_its_tile()
        {
            CustomiseGamesAndAppsOn("Hades");

            core.LeaveThemePage();

            Assert.True(core.Settings.IsShown);
            Assert.False(core.MyGames.IsShown);
            Assert.Equal(ThemeSettingsPage.None, core.Settings.ThemePage);

            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.PlayHistory, core.MyGames.Item);
            Assert.Equal(library.Id("Hades"), core.MyGames.ReturnsToGame);
        }

        [Fact]
        public void B_from_Customise_Home_with_Settings_own_B_steps_back_twice_to_the_tile()
        {
            CustomiseHomeOn("Hades");

            core.LeaveSettings();
            Assert.True(core.Settings.IsShown);

            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.False(core.MyGames.IsShown);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);
            Assert.Equal(library.Id("Hades"), core.Settings.OpenedFromGame);
        }

        [Fact]
        public void B_from_Homes_Discord_tile_steps_back_to_the_boxes_then_to_the_tile()
        {
            core.OpenDiscordFromHome();

            core.LeaveThemePage();
            Assert.True(core.Settings.IsShown);

            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);
        }

        [Fact]
        public void A_box_opened_from_the_top_bar_steps_back_to_the_boxes_then_to_the_top_bar()
        {
            core.OpenSettings();
            core.OpenThemePage(ThemeSettingsPage.Discord);

            core.LeaveThemePage();
            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.Equal(SettingsOrigin.TopBar, core.Settings.OpenedFrom);
        }

        private void CustomiseHomeOn(string title)
        {
            core.FocusHomeTile(core.Home.RecentRow.Single(t => t.Title == title));
            Tap(ControllerButton.Start);
            Choose("Customise Home");
        }

        private void CustomiseGamesAndAppsOn(string title)
        {
            core.OpenMyGames(MyGamesItem.PlayHistory);
            core.FocusHomeTile(core.MyGames.PlayHistory.SelectMany(s => s.Entries).Single(e => e.Tile.Title == title).Tile);
            Tap(ControllerButton.Start);
            Choose("Customise games & apps");
        }

        private void Choose(string label)
        {
            while (core.Home.TileMenu.FocusedOption.Label != label)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.A);
        }

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }
    }
}
