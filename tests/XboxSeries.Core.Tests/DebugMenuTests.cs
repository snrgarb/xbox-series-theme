#if DEBUG
using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Debug builds' Debug menu in Settings, for trying Toasts on the TV.</summary>
    public class DebugMenuTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeAchievements achievements = new FakeAchievements();
        private readonly ExtensionCore core;

        public DebugMenuTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, sounds, new FakeDrives(),
                new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads(),
                achievements: achievements);
            core.OnNotificationsChanged(new Notification[0]);
        }

        [Fact]
        public void The_Debug_category_can_be_focused_on_the_rail()
        {
            core.OpenSettings();

            core.FocusSettingsCategory(SettingsCategory.Debug);

            Assert.Equal(SettingsCategory.Debug, core.Settings.Category);
        }

        [Fact]
        public void A_test_notification_is_posted_to_Playnite_with_a_fresh_id_each_time()
        {
            core.DebugNotify("Library updated");
            core.DebugNotify("Screenshot saved");

            Assert.Equal(new[] { "Library updated", "Screenshot saved" }, playnite.PostedNotifications.Select(n => n.Text));
            Assert.All(playnite.PostedNotifications, n => Assert.StartsWith("xbox-series-debug-", n.Id));
            Assert.Equal(2, playnite.PostedNotifications.Select(n => n.Id).Distinct().Count());
        }

        [Fact]
        public void A_test_ring_shows_the_call_Toast_with_the_callers_name_and_image()
        {
            core.DebugRing("ana", "https://cdn.example/ana.png");

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);
            Assert.Equal("ana is calling", core.Toast.Text);
            Assert.Equal("https://cdn.example/ana.png", core.Toast.Image);
        }

        [Fact]
        public void A_test_rings_Toast_shows_for_30_seconds()
        {
            core.DebugRing("ana", null);

            clock.Advance(TimeSpan.FromSeconds(29.9));
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_test_ring_cuts_in_before_a_notification_which_shows_after_it()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            clock.Advance(TimeSpan.FromSeconds(2));

            core.DebugRing("ana", null);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);

            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.False(core.Toast.IsShown);
            clock.Advance(TimeSpan.FromSeconds(0.5));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.Equal("Library updated", core.Toast.Text);
        }

        [Fact]
        public void A_Guide_tap_during_a_test_rings_Toast_opens_the_Guide_on_the_landing_tab_without_Answer()
        {
            core.DebugRing("ana", null);

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.DiscordIncomingCall);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_test_unlock_shows_the_Achievement_unlocked_Toast_with_a_made_up_name_description_and_Score_with_no_game_running()
        {
            core.DebugAchievementsUnlocked(1, rare: false);

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal("Achievement unlocked", core.Toast.Text);
            Assert.Equal("Two's Good, but I Want Three", core.Toast.Detail);
            Assert.Equal(15, core.Toast.Score);
            Assert.Null(core.Toast.Image);
            Assert.Null(core.Toast.Rarity);
            Assert.Equal("Earned at least 2 stars on 15 different maps in Frontier Defense", core.Toast.Description);
            Assert.Equal(UiSound.Notification, sounds.Played.Last());
        }

        [Fact]
        public void The_Toast_after_an_ordinary_test_unlock_waits_for_its_outro_before_the_usual_gap()
        {
            core.DebugAchievementsUnlocked(1, rare: false);
            core.DebugAchievementsUnlocked(1, rare: true);

            clock.Advance(TimeSpan.FromSeconds(8.5 + 1.12 + 0.5) - TimeSpan.FromMilliseconds(1));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(ToastKind.RareAchievementUnlocked, core.Toast.Kind);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void The_Toast_after_a_rare_one_waits_for_its_outro_before_the_usual_gap()
        {
            core.DebugAchievementsUnlocked(1, rare: true);
            core.DebugAchievementsUnlocked(1, rare: false);

            clock.Advance(TimeSpan.FromSeconds(8.45 + 1.12 + 0.5) - TimeSpan.FromMilliseconds(1));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void A_test_rare_unlock_shows_the_rare_Toast_at_1_percent_with_its_description_and_sound()
        {
            core.DebugAchievementsUnlocked(1, rare: true);

            Assert.Equal(ToastKind.RareAchievementUnlocked, core.Toast.Kind);
            Assert.Equal("Rare Achievement unlocked", core.Toast.Text);
            Assert.Equal(1, core.Toast.Rarity);
            Assert.Equal("Two's Good, but I Want Three", core.Toast.Detail);
            Assert.Equal(100, core.Toast.Score);
            Assert.Equal("Earned at least 2 stars on 15 different maps in Frontier Defense", core.Toast.Description);
            Assert.Null(core.Toast.Image);
            Assert.Equal(UiSound.RareAchievement, sounds.Played.Last());

            clock.Advance(TimeSpan.FromSeconds(8.5));
            Assert.False(core.Toast.IsShown);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Several_test_unlocks_show_one_Toast_with_their_count(bool rare)
        {
            core.DebugAchievementsUnlocked(3, rare);

            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal("3 achievements unlocked", core.Toast.Text);
            Assert.Null(core.Toast.Detail);
            Assert.Null(core.Toast.Score);
        }

        [Fact]
        public void A_test_unlock_waits_its_turn_behind_a_Toast_showing()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            core.DebugAchievementsUnlocked(1, rare: true);
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);

            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal(ToastKind.RareAchievementUnlocked, core.Toast.Kind);
        }

        [Fact]
        public void Test_unlocks_never_count_in_the_Profiles_totals_or_its_last_unlocks()
        {
            var game = Guid.NewGuid();
            achievements.Recorded.Add(new GameAchievements(game, new[] { new Achievement("boon", "Blessed", null, 20, clock.Now.AddDays(-1)) }));
            core.OnAchievementsChanged();

            core.DebugAchievementsUnlocked(1, rare: true);
            core.DebugAchievementsUnlocked(2, rare: false);
            clock.Advance(TimeSpan.FromSeconds(20));
            core.OnAchievementsChanged();

            Assert.Equal(20, core.Guide.Achievements.Score);
            Assert.Equal(1, core.Guide.Achievements.Unlocked);
        }
    }
}
#endif
