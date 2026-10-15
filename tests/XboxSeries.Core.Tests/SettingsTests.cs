using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class SettingsTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public SettingsTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore());
        }

        [Fact]
        public void Home_starts_without_the_page()
        {
            Assert.False(core.Settings.IsShown);
        }

        [Fact]
        public void The_top_bars_Settings_opens_the_page_on_the_rails_first_category()
        {
            core.OpenSettings();

            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.StartUp, core.Settings.Category);
            Assert.Equal(SettingsOrigin.TopBar, core.Settings.OpenedFrom);
        }

        [Theory]
        [InlineData(SettingsCategory.Guide)]
        [InlineData(SettingsCategory.Discord)]
        public void Focus_moving_down_the_rail_shows_that_category(SettingsCategory category)
        {
            core.OpenSettings();

            core.FocusSettingsCategory(category);

            Assert.Equal(category, core.Settings.Category);
        }

        [Fact]
        public void The_rail_shows_nothing_new_with_the_page_gone()
        {
            core.FocusSettingsCategory(SettingsCategory.Discord);

            Assert.Equal(SettingsCategory.StartUp, core.Settings.Category);
        }

        [Fact]
        public void B_leaves_the_page_for_the_top_bars_Settings_button()
        {
            core.OpenSettings();

            core.LeaveSettings();

            Assert.False(core.Settings.IsShown);
            Assert.Equal(SettingsOrigin.TopBar, core.Settings.OpenedFrom);
        }

        [Fact]
        public void B_with_the_Guide_over_the_page_answers_the_Guide_without_leaving_the_page()
        {
            core.OpenSettings();
            Tap(ControllerButton.Guide);

            // Playnite's own B reaches the page before the pad's B reaches the core.
            core.LeaveSettings();
            Tap(ControllerButton.B);

            Assert.True(core.Settings.IsShown);
            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void The_page_opens_on_the_rails_first_category_every_time()
        {
            core.OpenSettings();
            core.FocusSettingsCategory(SettingsCategory.Discord);
            core.LeaveSettings();

            core.OpenSettings();

            Assert.Equal(SettingsCategory.StartUp, core.Settings.Category);
        }

        [Fact]
        public void Playnite_settings_opens_Playnites_own_Settings_window_over_the_page()
        {
            core.OpenSettings();

            core.OpenPlayniteSettings();

            Assert.Equal(1, playnite.PlayniteSettingsOpened);
            Assert.True(core.Settings.IsShown);
        }

        [Fact]
        public void More_Playnite_options_opens_Playnites_own_main_menu_over_the_page()
        {
            core.OpenSettings();

            core.OpenMorePlayniteOptions();

            Assert.Equal(1, playnite.MainMenuOpened);
            Assert.True(core.Settings.IsShown);
        }

        [Fact]
        public void Opening_the_page_from_My_games_and_apps_takes_its_place_with_the_users_own_filter_back()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.OpenSettings();

            Assert.False(core.MyGames.IsShown);
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Opening_My_games_and_apps_takes_the_pages_place()
        {
            core.OpenSettings();

            core.OpenMyGames(MyGamesItem.Games);

            Assert.False(core.Settings.IsShown);
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
