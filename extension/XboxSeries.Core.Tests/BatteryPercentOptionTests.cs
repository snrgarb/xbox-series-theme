using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Settings' Show battery percentages: whether the Status area, on Home's top bar and in the Guide, writes the PC's and
    // the pad's percent beside their batteries. On until set; off, the batteries show alone. In force at once, saved at
    // once, shared by every profile.
    public class BatteryPercentOptionTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 11, 12, 20, 0, 0));
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private ExtensionCore core;

        public BatteryPercentOptionTests()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same store, as when Playnite starts again.</summary>
        private void NewCore()
        {
            var windows = new FakeWindowSystem();
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, new FakeDownloads());
        }

        [Fact]
        public void With_nothing_saved_it_is_on_in_settings_and_the_Status_area_and_nothing_goes_to_the_store()
        {
            Assert.True(StartUpSettingsState.ShowBatteryPercentByDefault);
            Assert.True(core.Settings.StartUp.ShowBatteryPercent);
            Assert.True(core.Status.ShowBatteryPercent);
            Assert.False(secrets.Values.ContainsKey("StartUp"));
        }

        [Fact]
        public void Off_the_Status_area_hides_the_percents_at_once()
        {
            core.SetShowBatteryPercent(false);

            Assert.False(core.Settings.StartUp.ShowBatteryPercent);
            Assert.False(core.Status.ShowBatteryPercent);
        }

        [Fact]
        public void Turned_on_again_the_percents_come_back()
        {
            core.SetShowBatteryPercent(false);
            core.SetShowBatteryPercent(true);

            Assert.True(core.Status.ShowBatteryPercent);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_is_saved_at_once_and_kept_across_a_restart(bool on)
        {
            core.SetShowBatteryPercent(on);

            Assert.Contains(on ? "\"ShowBatteryPercent\":true" : "\"ShowBatteryPercent\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.Equal(on, core.Settings.StartUp.ShowBatteryPercent);
            Assert.Equal(on, core.Status.ShowBatteryPercent);
        }

        [Fact]
        public void It_is_shared_by_every_profile()
        {
            core.AddProfile("Player Two");
            core.SetShowBatteryPercent(false);

            NewCore();

            Assert.Equal(2, core.Settings.StartUp.Profiles.Count);
            Assert.False(core.Status.ShowBatteryPercent);
        }

        [Fact]
        public void Saving_another_Personalisation_option_leaves_it_unset_so_the_default_stands()
        {
            core.SetReduceMotion(true);

            Assert.DoesNotContain("\"ShowBatteryPercent\":true", secrets.Values["StartUp"]);
            Assert.DoesNotContain("\"ShowBatteryPercent\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.True(core.Status.ShowBatteryPercent);
        }

        [Fact]
        public void It_is_kept_when_another_Personalisation_option_is_saved_after_it()
        {
            core.SetShowBatteryPercent(false);
            core.SetShowPlayHistory(false);

            NewCore();

            Assert.False(core.Status.ShowBatteryPercent);
            Assert.False(core.Settings.StartUp.ShowPlayHistory);
        }

        [Fact]
        public void Setting_it_tells_the_views_once_and_saves_nothing_when_it_is_set_to_what_it_is()
        {
            var settings = new List<string>();
            core.Settings.StartUp.PropertyChanged += (_, e) => settings.Add(e.PropertyName);
            var status = new List<string>();
            core.Status.PropertyChanged += (_, e) => status.Add(e.PropertyName);

            core.SetShowBatteryPercent(false);
            var writes = secrets.Writes;
            core.SetShowBatteryPercent(false);

            Assert.Equal(new[] { nameof(StartUpSettingsState.ShowBatteryPercent) }, settings);
            Assert.Equal(1, status.Count(p => p == nameof(StatusState.ShowBatteryPercent)));
            Assert.Equal(writes, secrets.Writes);
        }
    }
}
