using System;
using System.Collections.Generic;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Settings' Reduce motion (#156): whether the Dashboard's animations are turned off, for a PC they cost frames on.
    // Off until set, in force at once (the views and the theme follow the state), saved at once.
    public class ReduceMotionTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 11, 10, 20, 0, 0));
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private ExtensionCore core;

        public ReduceMotionTests()
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

        private bool ReduceMotion => core.Settings.StartUp.ReduceMotion;

        [Fact]
        public void With_nothing_saved_Reduce_motion_is_off_and_nothing_goes_to_the_store()
        {
            Assert.False(StartUpSettingsState.ReduceMotionByDefault);
            Assert.False(ReduceMotion);
            Assert.False(secrets.Values.ContainsKey("StartUp"));
        }

        [Fact]
        public void Turned_on_and_off_again_it_is_in_force_at_once_without_a_restart()
        {
            core.SetReduceMotion(true);

            Assert.True(ReduceMotion);

            core.SetReduceMotion(false);

            Assert.False(ReduceMotion);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_is_saved_at_once_and_kept_across_a_restart(bool on)
        {
            core.SetReduceMotion(on);

            Assert.Equal(on, ReduceMotion);
            Assert.Contains(on ? "\"ReduceMotion\":true" : "\"ReduceMotion\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.Equal(on, ReduceMotion);
        }

        [Fact]
        public void Saving_another_Personalisation_option_leaves_it_unset_so_the_default_stands()
        {
            core.SetAskWhosPlaying(false);
            core.SetBackgroundDelay(false);

            Assert.DoesNotContain("\"ReduceMotion\":true", secrets.Values["StartUp"]);
            Assert.DoesNotContain("\"ReduceMotion\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.Equal(StartUpSettingsState.ReduceMotionByDefault, ReduceMotion);
        }

        [Fact]
        public void It_is_kept_when_another_Personalisation_option_is_saved_after_it()
        {
            core.SetReduceMotion(true);
            core.SetBootVideo(BootVideo.Xbox360);
            core.SetBackgroundDelay(false);

            NewCore();

            Assert.True(ReduceMotion);
            Assert.Equal(BootVideo.Xbox360, core.Settings.StartUp.BootVideo);
            Assert.False(core.Settings.StartUp.BackgroundDelay);
        }

        [Fact]
        public void It_leaves_Background_delay_as_it_was()
        {
            core.SetReduceMotion(true);

            Assert.Equal(StartUpSettingsState.BackgroundDelayByDefault, core.Settings.StartUp.BackgroundDelay);
            Assert.DoesNotContain("\"BackgroundDelay\":true", secrets.Values["StartUp"]);
            Assert.DoesNotContain("\"BackgroundDelay\":false", secrets.Values["StartUp"]);
        }

        [Fact]
        public void Options_saved_before_there_was_a_Reduce_motion_load_with_it_off()
        {
            core.SetBootVideo(BootVideo.XboxOne);
            Assert.DoesNotContain("\"ReduceMotion\":true", secrets.Values["StartUp"]);

            NewCore();

            Assert.False(ReduceMotion);
            Assert.Equal(BootVideo.XboxOne, core.Settings.StartUp.BootVideo);
        }

        [Fact]
        public void Setting_it_tells_the_view_and_the_theme_once_and_not_when_it_is_set_to_what_it_is()
        {
            var changed = new List<string>();
            core.Settings.StartUp.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            core.SetReduceMotion(true);
            core.SetReduceMotion(true);

            Assert.Equal(new[] { nameof(StartUpSettingsState.ReduceMotion) }, changed);
        }
    }
}
