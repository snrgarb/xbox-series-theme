using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // Settings' Background delay (#147): whether Home's hero waits for focus to rest 1.5 s on a Recent-row game before
    // showing its art, or follows it after 150 ms. On until set, in force from the next tile focused, saved at once.
    public class BackgroundDelayTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 11, 10, 20, 0, 0));
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private ExtensionCore core;

        public BackgroundDelayTests()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same store, as when Playnite starts again, with four games in the Recent row.</summary>
        private void NewCore()
        {
            var windows = new FakeWindowSystem();
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, new FakeDownloads());
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), backgroundImage: @"C:\art\halo-bg.jpg")
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11), backgroundImage: @"C:\art\hades-bg.jpg")
                .WithGame("Tunic", lastActivity: new DateTime(2026, 10, 10), backgroundImage: @"C:\art\tunic-bg.jpg")
                .WithGame("Celeste", lastActivity: new DateTime(2026, 10, 9), backgroundImage: @"C:\art\celeste-bg.jpg")
                .Snapshot());
        }

        private bool BackgroundDelay => core.Settings.StartUp.BackgroundDelay;

        private string Hero => core.Home.Hero?.Image;

        private HomeTile Tile(string title) => core.Home.RecentRow.Single(t => t.Title == title);

        /// <summary>Focus moving from one Recent-row tile to another, as WPF tells it: the one left, then the one focused.</summary>
        private void MoveFocus(string from, string to)
        {
            core.LeaveHomeTile(Tile(from));
            core.FocusHomeTile(Tile(to));
        }

        private void Wait(int milliseconds) => clock.Advance(TimeSpan.FromMilliseconds(milliseconds));

        [Fact]
        public void With_nothing_saved_Background_delay_is_on_and_nothing_goes_to_the_store()
        {
            Assert.True(StartUpSettingsState.BackgroundDelayByDefault);
            Assert.True(BackgroundDelay);
            Assert.False(secrets.Values.ContainsKey("StartUp"));
        }

        [Fact]
        public void On_the_hero_follows_focus_along_the_row_once_it_has_stayed_on_a_tile_for_1500_ms()
        {
            core.SetBackgroundDelay(true);
            core.FocusHomeTile(Tile("Halo Infinite"));

            MoveFocus("Halo Infinite", "Hades");
            Wait(1499);

            Assert.Equal(@"C:\art\halo-bg.jpg", Hero);

            Wait(1);

            Assert.Equal(@"C:\art\hades-bg.jpg", Hero);
        }

        [Fact]
        public void Off_the_hero_follows_focus_along_the_row_once_it_has_stayed_on_a_tile_for_150_ms()
        {
            core.SetBackgroundDelay(false);
            core.FocusHomeTile(Tile("Halo Infinite"));

            MoveFocus("Halo Infinite", "Hades");
            Wait(149);

            Assert.Equal(@"C:\art\halo-bg.jpg", Hero);

            Wait(1);

            Assert.Equal(@"C:\art\hades-bg.jpg", Hero);
        }

        [Fact]
        public void Off_a_held_d_pad_passes_over_tiles_without_the_hero_following_until_focus_stops()
        {
            core.SetBackgroundDelay(false);
            var shown = new List<string>();
            core.Home.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HomeState.Hero))
                {
                    shown.Add(Hero);
                }
            };
            core.FocusHomeTile(Tile("Halo Infinite"));

            // Playnite repeats a held direction every 80 ms; a slow frame can stretch a step.
            MoveFocus("Halo Infinite", "Hades");
            Wait(80);
            MoveFocus("Hades", "Tunic");
            Wait(120);
            MoveFocus("Tunic", "Celeste");
            Wait(149);

            Assert.Equal(new[] { @"C:\art\halo-bg.jpg" }, shown);

            Wait(1);

            Assert.Equal(new[] { @"C:\art\halo-bg.jpg", @"C:\art\celeste-bg.jpg" }, shown);
        }

        [Fact]
        public void On_or_off_the_art_shows_at_once_coming_into_the_row_onto_the_swirl()
        {
            foreach (var on in new[] { true, false })
            {
                core.SetBackgroundDelay(on);

                core.FocusHomeTile(Tile("Halo Infinite"));

                Assert.Equal(@"C:\art\halo-bg.jpg", Hero);

                core.LeaveHomeTile(Tile("Halo Infinite"));
                Wait(100);

                Assert.Null(Hero);
            }
        }

        [Fact]
        public void Turned_off_it_is_in_force_from_the_next_tile_focused_without_a_restart()
        {
            core.FocusHomeTile(Tile("Halo Infinite"));
            MoveFocus("Halo Infinite", "Hades");
            Wait(150);

            Assert.Equal(@"C:\art\halo-bg.jpg", Hero);

            core.SetBackgroundDelay(false);
            MoveFocus("Hades", "Tunic");
            Wait(150);

            Assert.Equal(@"C:\art\tunic-bg.jpg", Hero);
        }

        [Fact]
        public void Turned_on_again_it_is_in_force_from_the_next_tile_focused_without_a_restart()
        {
            core.SetBackgroundDelay(false);
            core.FocusHomeTile(Tile("Halo Infinite"));

            core.SetBackgroundDelay(true);
            MoveFocus("Halo Infinite", "Hades");
            Wait(1499);

            Assert.Equal(@"C:\art\halo-bg.jpg", Hero);

            Wait(1);

            Assert.Equal(@"C:\art\hades-bg.jpg", Hero);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_is_saved_at_once_and_kept_across_a_restart(bool on)
        {
            core.SetBackgroundDelay(on);

            Assert.Equal(on, BackgroundDelay);
            Assert.Contains(on ? "\"BackgroundDelay\":true" : "\"BackgroundDelay\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.Equal(on, BackgroundDelay);
            core.FocusHomeTile(Tile("Halo Infinite"));
            MoveFocus("Halo Infinite", "Hades");
            Wait(150);
            Assert.Equal(on ? @"C:\art\halo-bg.jpg" : @"C:\art\hades-bg.jpg", Hero);
        }

        [Fact]
        public void Saving_another_Personalisation_option_leaves_it_unset_so_the_default_stands()
        {
            core.SetAskWhosPlaying(false);

            Assert.DoesNotContain("\"BackgroundDelay\":true", secrets.Values["StartUp"]);
            Assert.DoesNotContain("\"BackgroundDelay\":false", secrets.Values["StartUp"]);

            NewCore();

            Assert.Equal(StartUpSettingsState.BackgroundDelayByDefault, BackgroundDelay);
        }

        [Fact]
        public void It_is_kept_when_another_Personalisation_option_is_saved_after_it()
        {
            core.SetBackgroundDelay(false);
            core.SetBootVideo(BootVideo.Xbox360);

            NewCore();

            Assert.False(BackgroundDelay);
            Assert.Equal(BootVideo.Xbox360, core.Settings.StartUp.BootVideo);
        }

        [Fact]
        public void Setting_it_tells_the_view()
        {
            var changed = new List<string>();
            core.Settings.StartUp.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            core.SetBackgroundDelay(false);

            Assert.Equal(new[] { nameof(StartUpSettingsState.BackgroundDelay) }, changed);
        }
    }
}
