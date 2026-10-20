using Xunit;

namespace XboxSeries.Core.Tests
{
    // Where the four tiles under Home's Recent row go: Discord, Playnite settings, Achievements and Downloads, each back
    // to Home on B.
    public class HomePromoTilesTests : DiscordTestsBase
    {
        [Fact]
        public void Discord_opens_the_Guide_on_the_Discord_tab_once_connected()
        {
            NotInCall();

            core.OpenDiscordFromHome();

            Assert.True(Guide.IsOpen);
            Assert.Equal(GuideTabKind.Discord, Guide.Tab);
            Assert.False(core.Settings.IsShown);
        }

        [Fact]
        public void Discord_never_connected_opens_Settings_on_Discord_focus_in_its_options()
        {
            core.OpenDiscordFromHome();

            Assert.False(Guide.IsOpen);
            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.Discord, core.Settings.Category);
            Assert.True(core.Settings.OpensInPane);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);

            core.LeaveSettings();
            Assert.False(core.Settings.IsShown);
        }

        [Fact]
        public void Playnite_settings_opens_Settings_on_Playnite_settings_focus_in_its_boxes()
        {
            core.OpenPlayniteSettingsFromHome();

            Assert.True(core.Settings.IsShown);
            Assert.Equal(SettingsCategory.PlayniteSettings, core.Settings.Category);
            Assert.True(core.Settings.OpensInPane);
            Assert.Equal(SettingsOrigin.HomeTile, core.Settings.OpenedFrom);
        }

        [Fact]
        public void Settings_opened_from_the_top_bar_after_a_tile_focuses_its_rail_again()
        {
            core.OpenPlayniteSettingsFromHome();
            core.LeaveSettings();

            core.OpenSettings();

            Assert.Equal(SettingsCategory.Personalisation, core.Settings.Category);
            Assert.False(core.Settings.OpensInPane);
        }

        [Fact]
        public void Downloads_opens_Manage_Queue_and_B_goes_back_to_Home()
        {
            core.OpenQueueFromHome();
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.ManageQueue, core.MyGames.Item);

            core.LeaveMyGames();

            Assert.False(core.MyGames.IsShown);
        }

        [Fact]
        public void Manage_Queue_reached_from_Manage_after_the_tile_goes_back_to_Manage_again()
        {
            core.OpenQueueFromHome();
            core.OpenMyGames(MyGamesItem.Manage);
            core.OpenMyGames(MyGamesItem.ManageQueue);

            core.LeaveMyGames();

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Manage, core.MyGames.Item);
        }
    }
}
