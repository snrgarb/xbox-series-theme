using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Achievements from PlayniteAchievements: the Achievement unlocked Toast, only for the game running now or the one
    /// that just exited and never for the first read or an older session's, several recorded together as one, a rare one alone as the
    /// rare Toast, and the Profile's totals and recent unlocks on Profile &amp; system.
    /// </summary>
    public class AchievementsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 20, 20, 0, 0);

        private readonly FakeClock clock = new FakeClock(Start);
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeAchievements achievements = new FakeAchievements();
        private readonly FakeLibrary library = new FakeLibrary();
        private readonly ExtensionCore core;
        private readonly Guid hades = Guid.NewGuid();
        private readonly Guid celeste = Guid.NewGuid();

        /// <summary>Each game's achievements as the fake holds them, by game, so a test can add to one.</summary>
        private readonly Dictionary<Guid, List<Achievement>> recorded = new Dictionary<Guid, List<Achievement>>();

        public AchievementsTests()
        {
            core = new ExtensionCore(
                clock, windows, new FakePlayniteHost(windows), sounds, new FakeDrives(), new FakeUserAccount { Name = "Gabe" }, new FakeAudio(),
                new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads(),
                achievements: achievements);
            library.WithGame(hades, "Hades", coverImage: "hades.jpg").WithGame(celeste, "Celeste", coverImage: "celeste.jpg");
            core.OnLibraryChanged(library.Snapshot());
        }

        [Fact]
        public void An_unlock_heard_live_in_the_running_game_shows_an_Achievement_unlocked_Toast_with_its_icon_name_and_Score()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 20, icon: "blessed.png"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal("Achievement unlocked", core.Toast.Text);
            Assert.Equal("blessed.png", core.Toast.Image);
            Assert.Equal("Blessed", core.Toast.Detail);
            Assert.Equal(20, core.Toast.Score);
        }

        [Fact]
        public void A_live_unlock_waits_a_second_for_others_found_with_it_before_its_Toast_shows()
        {
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed"));
            clock.Advance(TimeSpan.FromMilliseconds(999));

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Several_live_unlocks_heard_together_show_one_Toast_with_their_count_and_no_icon_name_or_Score()
        {
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", icon: "blessed.png"));
            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", icon: "escaped.png"));
            clock.Advance(TimeSpan.FromMilliseconds(500));
            core.OnAchievementUnlocked(hades, Unlocked("pet", "Petted Cerberus", icon: "pet.png"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal("3 achievements unlocked", core.Toast.Text);
            Assert.Null(core.Toast.Image);
            Assert.Null(core.Toast.Detail);
            Assert.Null(core.Toast.Score);

            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Live_unlocks_heard_apart_show_a_Toast_each_in_turn()
        {
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed"));
            clock.Advance(TimeSpan.FromSeconds(2));
            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped"));

            Assert.Equal("Blessed", core.Toast.Detail);

            clock.Advance(TimeSpan.FromSeconds(8.5 + 1.12 + 0.5 - 1));
            Assert.Equal("Escaped", core.Toast.Detail);
            Assert.Equal("Achievement unlocked", core.Toast.Text);
        }

        [Fact]
        public void The_first_read_after_start_up_announces_nothing_even_in_the_running_games_session()
        {
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            Record(hades, Unlocked("boon", "Blessed"));

            core.OnAchievementsChanged();
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_read_finding_a_new_unlock_in_the_running_games_session_shows_its_Toast()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            Record(hades, Unlocked("boon", "Blessed", rarity: 30, icon: "blessed.png"));

            core.OnAchievementsChanged();

            Assert.True(core.Toast.IsShown);
            Assert.Equal("Blessed", core.Toast.Detail);
            Assert.Equal("blessed.png", core.Toast.Image);
            Assert.Equal(15, core.Toast.Score);
        }

        [Fact]
        public void A_read_finding_several_new_unlocks_shows_one_Toast_with_their_count()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            Record(hades, Unlocked("boon", "Blessed"), Unlocked("escape", "Escaped"));

            core.OnAchievementsChanged();

            Assert.Equal("2 achievements unlocked", core.Toast.Text);
            Assert.Null(core.Toast.Detail);
        }

        [Fact]
        public void A_read_after_the_game_exits_announces_what_it_unlocked_in_that_session()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            var unlockedAt = clock.Now;
            clock.Advance(TimeSpan.FromMinutes(25));
            core.OnGameStopped(hades);
            clock.Advance(TimeSpan.FromSeconds(10));
            Record(hades, Unlocked("boon", "Blessed", at: unlockedAt));

            core.OnAchievementsChanged();

            Assert.True(core.Toast.IsShown);
            Assert.Equal("Blessed", core.Toast.Detail);
        }

        [Fact]
        public void A_resync_of_an_older_session_announces_nothing()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            Record(hades, Unlocked("boon", "Blessed", at: Start.AddDays(-3)));

            core.OnAchievementsChanged();

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Unlocks_of_a_game_neither_running_nor_just_exited_are_not_announced()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            Record(celeste, Unlocked("summit", "Summit"));

            core.OnAchievementsChanged();
            core.OnAchievementUnlocked(celeste, Unlocked("strawberry", "Strawberry"));
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Once_another_game_starts_the_game_that_exited_before_it_is_no_longer_announced()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            var unlockedAt = clock.Now;
            core.OnGameStopped(hades);
            core.OnGameStarted(celeste);
            Record(hades, Unlocked("boon", "Blessed", at: unlockedAt));

            core.OnAchievementsChanged();

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Nothing_is_announced_before_any_game_has_run()
        {
            Baseline();
            Record(hades, Unlocked("boon", "Blessed"));

            core.OnAchievementsChanged();

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void An_unlock_announced_live_is_not_announced_again_when_a_read_shows_it()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            var blessed = Unlocked("boon", "Blessed");
            core.OnAchievementUnlocked(hades, blessed);
            core.OnAchievementUnlocked(hades, blessed);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal("Blessed", core.Toast.Detail);
            clock.Advance(TimeSpan.FromSeconds(10));

            core.OnGameStopped(hades);
            Record(hades, blessed);
            core.OnAchievementsChanged();
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_live_unlock_and_a_read_within_the_same_second_show_one_Toast_for_both()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            var blessed = Unlocked("boon", "Blessed");

            core.OnAchievementUnlocked(hades, blessed);
            Record(hades, blessed, Unlocked("escape", "Escaped"));
            core.OnAchievementsChanged();
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("2 achievements unlocked", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_Toast_waits_its_turn_behind_a_Toast_already_showing_and_the_next_Toast_has_no_name_or_Score()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromSeconds(1));

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 30));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.Null(core.Toast.Detail);

            clock.Advance(TimeSpan.FromSeconds(3.5));
            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal(15, core.Toast.Score);

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Update available") });
            clock.Advance(TimeSpan.FromSeconds(8.5 + 1.12 + 0.5));

            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.Null(core.Toast.Detail);
            Assert.Null(core.Toast.Score);
        }

        [Fact]
        public void The_Toast_never_takes_focus_from_the_game()
        {
            var game = windows.OpenWindow(@"C:\Games\Hades\Hades.exe");
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.True(core.Toast.IsShown);
            Assert.Equal(game, windows.Foreground);
        }

        [Theory]
        [InlineData(0.5, 100)]
        [InlineData(1.0, 100)]
        [InlineData(1.1, 50)]
        [InlineData(5.0, 50)]
        [InlineData(7.0, 30)]
        [InlineData(10.0, 30)]
        [InlineData(25.0, 20)]
        [InlineData(50.0, 15)]
        [InlineData(50.1, 10)]
        [InlineData(97.0, 10)]
        [InlineData(null, 10)]
        public void An_achievements_Score_is_rarer_for_more(double? rarity, int score)
        {
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: rarity));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(score, core.Toast.Score);
        }

        [Fact]
        public void A_live_unlock_with_its_platforms_own_points_shows_those_points_as_its_Score()
        {
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 0.5, points: 40));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(40, core.Toast.Score);
        }

        [Fact]
        public void A_live_unlock_heard_with_no_time_keeps_its_points_for_its_Toast_and_the_totals()
        {
            Baseline();
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, new Achievement("boon", "Blessed", "boon.png", 0.5, null, null, 40));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(40, core.Toast.Score);
            Assert.Equal(40, core.Guide.Achievements.Score);
        }

        [Fact]
        public void The_Score_total_counts_each_platforms_own_points_where_recorded_and_the_rarity_Score_elsewhere()
        {
            Record(
                hades,
                Unlocked("boon", "Blessed", rarity: 0.5, points: 40, at: Start.AddDays(-5)),
                Unlocked("escape", "Escaped", rarity: 4, points: null, at: Start.AddDays(-4)),
                Unlocked("pet", "Petted Cerberus", rarity: 0.5, points: 0, at: Start.AddDays(-3)));
            Record(celeste, Unlocked("summit", "Summit", rarity: 60, points: 15, at: Start.AddDays(-2)));
            core.OnAchievementsChanged();

            Assert.Equal(40 + 50 + 0 + 15, core.Guide.Achievements.Score);
            Assert.Equal(40 + 50 + 0 + 15, core.Status.Achievements.Score);
        }

        [Fact]
        public void The_Guide_button_during_the_Toast_opens_the_Guide_at_Profile_and_system()
        {
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Tap(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.ProfileAndSystem, core.Guide.Tab);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Profile_and_system_shows_the_Score_total_the_unlocked_count_and_the_last_three_unlocks_newest_first()
        {
            Record(
                hades,
                Unlocked("boon", "Blessed", rarity: 4, icon: "blessed.png", at: Start.AddDays(-5)),
                Unlocked("escape", "Escaped", rarity: 0.2, icon: "escaped.png", at: Start.AddDays(-1)),
                Locked("pet", "Petted Cerberus"));
            Record(
                celeste,
                Unlocked("summit", "Summit", rarity: 60, icon: "summit.png", at: Start.AddDays(-2)),
                Unlocked("strawberry", "Strawberry", icon: "strawberry.png", at: Start.AddHours(-3)));
            core.OnAchievementsChanged();

            OpenProfileTab();

            Assert.Equal(50 + 100 + 10 + 20, core.Guide.Achievements.Score);
            Assert.Equal(4, core.Guide.Achievements.Unlocked);
            var items = core.Guide.Items;
            Assert.Equal(
                new[] { GuideItemKind.Profile, GuideItemKind.SwitchProfile, GuideItemKind.RecentAchievement, GuideItemKind.RecentAchievement, GuideItemKind.RecentAchievement, GuideItemKind.Settings, GuideItemKind.Power },
                items.Select(i => i.Kind));
            var recent = items.Where(i => i.Kind == GuideItemKind.RecentAchievement).ToList();
            Assert.Equal(new[] { "Strawberry", "Escaped", "Summit" }, recent.Select(i => i.Title));
            Assert.Equal(new[] { "Celeste", "Hades", "Celeste" }, recent.Select(i => i.Detail));
            Assert.Equal(new[] { "strawberry.png", "escaped.png", "summit.png" }, recent.Select(i => i.Image));
            Assert.Equal(new[] { true, false, false }, recent.Select(i => i.HasDividerAbove));
            Assert.True(items.Single(i => i.Kind == GuideItemKind.Settings).HasDividerAbove);
            Assert.Equal(GuideItemKind.Profile, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Home_top_bar_shows_the_same_Score_total_under_the_gamertag_and_none_without_totals()
        {
            Assert.Null(core.Status.Achievements);

            Record(hades, Unlocked("boon", "Blessed", rarity: 4, at: Start.AddDays(-5)), Locked("escape", "Escaped"));
            Record(celeste, Unlocked("summit", "Summit", rarity: 60, at: Start.AddDays(-2)));
            core.OnAchievementsChanged();

            Assert.Equal(50 + 10, core.Status.Achievements.Score);
            Assert.Same(core.Guide.Achievements, core.Status.Achievements);

            achievements.IsAvailable = false;
            core.OnAchievementsChanged();

            Assert.Null(core.Status.Achievements);
        }

        [Fact]
        public void A_live_unlock_counts_in_the_totals_and_leads_the_recent_unlocks_before_any_read_shows_it()
        {
            Record(hades, Unlocked("boon", "Blessed", rarity: 4, at: Start.AddDays(-5)), Locked("escape", "Escaped"));
            core.OnAchievementsChanged();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(1));

            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 0.2));
            Record(hades, Unlocked("boon", "Blessed", rarity: 4, at: Start.AddDays(-5)), Locked("escape", "Escaped"));
            core.OnAchievementsChanged();
            OpenProfileTab();

            Assert.Equal(150, core.Guide.Achievements.Score);
            Assert.Equal(2, core.Guide.Achievements.Unlocked);
            Assert.Equal("Escaped", core.Guide.Items.First(i => i.Kind == GuideItemKind.RecentAchievement).Title);
        }

        [Fact]
        public void Before_the_first_read_a_live_unlock_shows_its_Toast_but_no_totals_built_from_it_alone()
        {
            Record(hades, Unlocked("boon", "Blessed", at: Start.AddDays(-5)));
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));

            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped"));
            clock.Advance(TimeSpan.FromSeconds(1));
            OpenProfileTab();

            Assert.Equal("Escaped", core.Toast.Detail);
            Assert.Null(core.Guide.Achievements);
            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.RecentAchievement);

            core.OnAchievementsChanged();
            Assert.Equal(2, core.Guide.Achievements.Unlocked);
        }

        [Fact]
        public void A_read_with_Profile_and_system_showing_updates_its_totals_and_entries_in_place_focus_kept()
        {
            Record(hades, Unlocked("boon", "Blessed", at: Start.AddDays(-5)));
            core.OnAchievementsChanged();
            OpenProfileTab();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.Settings, core.Guide.FocusedItem.Kind);

            Record(hades, Unlocked("boon", "Blessed", at: Start.AddDays(-5)), Unlocked("escape", "Escaped", at: Start.AddDays(-1)));
            core.OnAchievementsChanged();

            Assert.Equal(2, core.Guide.Achievements.Unlocked);
            Assert.Equal(2, core.Guide.Items.Count(i => i.Kind == GuideItemKind.RecentAchievement));
            Assert.Equal(GuideItemKind.Settings, core.Guide.FocusedItem.Kind);
            Assert.True(core.Guide.FocusedItem.IsFocused);
        }

        [Fact]
        public void A_on_a_recent_unlock_does_nothing()
        {
            Record(hades, Unlocked("boon", "Blessed", at: Start.AddDays(-5)));
            core.OnAchievementsChanged();
            OpenProfileTab();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.RecentAchievement, core.Guide.FocusedItem.Kind);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideItemKind.RecentAchievement, core.Guide.FocusedItem.Kind);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Without_PlayniteAchievements_there_are_no_totals_no_recent_unlocks_and_no_Toast()
        {
            achievements.IsAvailable = false;
            Record(hades, Unlocked("boon", "Blessed", at: Start.AddDays(-5)));
            core.OnAchievementsChanged();
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped"));
            clock.Advance(TimeSpan.FromSeconds(1));
            OpenProfileTab();

            Assert.False(core.Toast.IsShown);
            Assert.Null(core.Guide.Achievements);
            Assert.Equal(new[] { GuideItemKind.Profile, GuideItemKind.SwitchProfile, GuideItemKind.Settings, GuideItemKind.Power }, core.Guide.Items.Select(i => i.Kind));
        }

        [Fact]
        public void With_no_game_having_any_achievements_there_are_no_totals()
        {
            achievements.Recorded.Add(new GameAchievements(hades, new Achievement[0]));
            core.OnAchievementsChanged();

            OpenProfileTab();

            Assert.Null(core.Guide.Achievements);
            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.RecentAchievement);
        }

        [Fact]
        public void With_achievements_but_none_unlocked_the_totals_are_zero_with_no_recent_unlocks()
        {
            Record(hades, Locked("boon", "Blessed"));
            core.OnAchievementsChanged();

            OpenProfileTab();

            Assert.Equal(0, core.Guide.Achievements.Score);
            Assert.Equal(0, core.Guide.Achievements.Unlocked);
            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.RecentAchievement);
        }

        [Theory]
        [InlineData(0.5, true)]
        [InlineData(9.9, true)]
        [InlineData(10.0, false)]
        [InlineData(40.0, false)]
        [InlineData(null, false)]
        public void An_unlock_fewer_than_10_percent_of_players_have_is_rare_and_one_of_unknown_rarity_never(double? rarity, bool rare)
        {
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: rarity));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(rare ? ToastKind.RareAchievementUnlocked : ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal(rare ? "Rare Achievement unlocked" : "Achievement unlocked", core.Toast.Text);
            Assert.Equal(rare ? rarity : null, core.Toast.Rarity);
        }

        [Fact]
        public void A_rare_unlock_shows_the_rare_Toast_with_its_rarity_icon_name_Score_and_description()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));

            core.OnAchievementUnlocked(hades, Unlocked("waves", "Two's Good, but I Want Three", rarity: 1, icon: "three.png", description: "Earned at least 2 stars on 15 different maps"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.RareAchievementUnlocked, core.Toast.Kind);
            Assert.Equal("Rare Achievement unlocked", core.Toast.Text);
            Assert.Equal(1, core.Toast.Rarity);
            Assert.Equal("three.png", core.Toast.Image);
            Assert.Equal("Two's Good, but I Want Three", core.Toast.Detail);
            Assert.Equal(100, core.Toast.Score);
            Assert.Equal("Earned at least 2 stars on 15 different maps", core.Toast.Description);
        }

        [Fact]
        public void A_rare_unlock_found_by_a_read_shows_the_rare_Toast_too()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            Record(hades, Unlocked("escape", "Escaped", rarity: 3, description: "Escape the Underworld"));

            core.OnAchievementsChanged();

            Assert.Equal(ToastKind.RareAchievementUnlocked, core.Toast.Kind);
            Assert.Equal(3, core.Toast.Rarity);
            Assert.Equal("Escape the Underworld", core.Toast.Description);
        }

        [Fact]
        public void An_ordinary_unlock_has_its_description_on_its_Toast_but_no_rarity()
        {
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 40, description: "Get a boon"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Null(core.Toast.Rarity);
            Assert.Equal("Get a boon", core.Toast.Description);
        }

        [Fact]
        public void An_ordinary_unlock_found_by_a_read_has_its_description_too()
        {
            Baseline();
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            Record(hades, Unlocked("boon", "Blessed", rarity: 30, description: "Get a boon"));

            core.OnAchievementsChanged();

            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal("Get a boon", core.Toast.Description);
            Assert.Null(core.Toast.Rarity);
        }

        [Fact]
        public void An_ordinary_unlock_without_a_description_has_none_on_its_Toast()
        {
            core.OnGameStarted(hades);

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 40));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Null(core.Toast.Description);
        }

        [Fact]
        public void The_ordinary_Toast_plays_the_notification_sound()
        {
            core.OnGameStarted(hades);
            sounds.Played.Clear();

            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 40));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { UiSound.Notification }, sounds.Played);
        }

        [Fact]
        public void A_description_longer_than_a_page_keeps_the_Toast_up_three_seconds_more_for_each_page_after_the_first()
        {
            // 200 characters: three pages of about 80, two more than the console's one.
            var description = new string('a', 199) + ".";
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 40, description: description));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.FromSeconds(8.5 + 2 * 3), core.Toast.ShownFor);

            clock.Advance(TimeSpan.FromSeconds(14.49));
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.01));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_description_of_a_page_or_none_keeps_the_console_s_8_and_a_half_seconds_and_other_Toasts_say_nothing_of_it()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 40, description: new string('a', 80)));
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(TimeSpan.FromSeconds(8.5), core.Toast.ShownFor);

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            clock.Advance(TimeSpan.FromSeconds(8.5 + 1.12 + 0.5));
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.Equal(TimeSpan.Zero, core.Toast.ShownFor);
        }

        [Fact]
        public void The_ordinary_Toast_shows_for_8_and_a_half_seconds_then_the_next_after_its_outro_and_the_usual_gap()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 40));
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            clock.Advance(TimeSpan.FromSeconds(8.49));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);

            clock.Advance(TimeSpan.FromSeconds(0.01));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(1.12 + 0.5) - TimeSpan.FromMilliseconds(1));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
        }

        [Fact]
        public void The_several_unlocks_Toast_shows_for_8_and_a_half_seconds_then_the_next_after_its_outro_and_the_usual_gap()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed"));
            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped"));
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            clock.Advance(TimeSpan.FromSeconds(8.49));
            Assert.Equal("2 achievements unlocked", core.Toast.Text);
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.01));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(1.12 + 0.5) - TimeSpan.FromMilliseconds(1));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
        }

        [Fact]
        public void A_notification_Toast_after_an_achievement_one_still_shows_for_5_seconds_then_the_next_after_the_usual_gap()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed"));
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Update available") });

            clock.Advance(TimeSpan.FromSeconds(8.5 + 1.12 + 0.5));
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1));
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.5));
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void The_rare_Toast_plays_the_rare_sound_in_place_of_the_notification_sound()
        {
            core.OnGameStarted(hades);
            sounds.Played.Clear();

            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 2));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { UiSound.RareAchievement }, sounds.Played);
        }

        [Fact]
        public void The_rare_Toast_plays_its_closing_sound_as_its_pill_collapses_after_every_page_of_a_long_description()
        {
            core.OnGameStarted(hades);
            sounds.Played.Clear();
            var threePages = new string('x', 170);

            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 2, description: threePages));
            clock.Advance(TimeSpan.FromSeconds(1));
            clock.Advance(TimeSpan.FromSeconds(8.5 + 2 * 3) - TimeSpan.FromMilliseconds(1));
            Assert.Equal(new[] { UiSound.RareAchievement }, sounds.Played);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.False(core.Toast.IsShown);
            Assert.Equal(new[] { UiSound.RareAchievement, UiSound.RareAchievementClose }, sounds.Played);
        }

        [Fact]
        public void The_rare_Toast_cut_short_by_the_Guide_never_plays_its_closing_sound()
        {
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 2));
            clock.Advance(TimeSpan.FromSeconds(1));
            sounds.Played.Clear();

            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.DoesNotContain(UiSound.RareAchievementClose, sounds.Played);
        }

        [Fact]
        public void The_rare_Toast_shows_for_8_and_a_half_seconds_then_the_next_after_its_outro_and_the_usual_gap()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 2));
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            clock.Advance(TimeSpan.FromSeconds(8.49));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.RareAchievementUnlocked, core.Toast.Kind);

            clock.Advance(TimeSpan.FromSeconds(0.01));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(1.12 + 0.5) - TimeSpan.FromMilliseconds(1));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
        }

        [Fact]
        public void Several_unlocks_together_are_one_ordinary_Toast_with_their_count_even_with_a_rare_one_among_them()
        {
            core.OnGameStarted(hades);
            clock.Advance(TimeSpan.FromMinutes(5));
            sounds.Played.Clear();

            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 0.2, description: "Escape the Underworld"));
            core.OnAchievementUnlocked(hades, Unlocked("boon", "Blessed", rarity: 40));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(ToastKind.AchievementUnlocked, core.Toast.Kind);
            Assert.Equal("2 achievements unlocked", core.Toast.Text);
            Assert.Null(core.Toast.Rarity);
            Assert.Null(core.Toast.Description);
            Assert.Equal(new[] { UiSound.Notification }, sounds.Played);

            clock.Advance(TimeSpan.FromSeconds(8.49));
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.01));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_next_Toast_after_a_rare_one_has_no_rarity_or_description()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 2, description: "Escape the Underworld"));
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            clock.Advance(TimeSpan.FromSeconds(10.2));

            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.Null(core.Toast.Rarity);
            Assert.Null(core.Toast.Description);
            Assert.Null(core.Toast.Detail);
            Assert.Null(core.Toast.Score);
        }

        [Fact]
        public void The_Guide_button_during_the_rare_Toast_opens_the_Guide_at_Profile_and_system()
        {
            core.OnGameStarted(hades);
            core.OnAchievementUnlocked(hades, Unlocked("escape", "Escaped", rarity: 2));
            clock.Advance(TimeSpan.FromSeconds(1));

            Tap(ControllerButton.Guide);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.ProfileAndSystem, core.Guide.Tab);
            Assert.False(core.Toast.IsShown);
        }

        /// <summary>The first read after start-up, with nothing recorded: the baseline later reads are told against.</summary>
        private void Baseline() => core.OnAchievementsChanged();

        /// <summary>Sets a game's achievements as PlayniteAchievements' records hold them now.</summary>
        private void Record(Guid gameId, params Achievement[] list)
        {
            recorded[gameId] = list.ToList();
            achievements.Recorded.Clear();
            achievements.Recorded.AddRange(recorded.Select(r => new GameAchievements(r.Key, r.Value)));
        }

        private Achievement Unlocked(string key, string name, double? rarity = 20, string icon = null, DateTime? at = null, string description = null, int? points = null) =>
            new Achievement(key, name, icon ?? key + ".png", rarity, at ?? clock.Now, description, points);

        private static Achievement Locked(string key, string name) => new Achievement(key, name, key + ".png", 20, null);

        private void OpenProfileTab()
        {
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.RightShoulder);
        }

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
