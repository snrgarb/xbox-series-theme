using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class GuideUtilityTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeAudio audio = new FakeAudio { Volume = 50 };
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public GuideUtilityTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, sounds, new FakeDrives(), new FakeUserAccount(), audio, new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            core.OnLibraryChanged(new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .Snapshot());
        }

        [Fact]
        public void The_landing_tab_has_the_Notifications_and_Audio_utilities_and_Profile_and_system_has_none()
        {
            Tap(ControllerButton.Guide);

            Assert.Equal(new[] { GuideItemKind.Notifications, GuideItemKind.Audio }, core.Guide.Utilities.Select(u => u.Kind));

            Tap(ControllerButton.RightShoulder);

            Assert.Empty(core.Guide.Utilities);
        }

        [Fact]
        public void Down_from_the_last_entry_reaches_the_utilities_which_left_and_right_move_along_and_up_leaves()
        {
            Tap(ControllerButton.Guide);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Equal("Halo Infinite", core.Guide.FocusedItem.Title);

            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.FocusedItem.Kind);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadLeft);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.FocusedItem.Kind);

            Tap(ControllerButton.DPadRight);
            Assert.Equal(GuideItemKind.Audio, core.Guide.FocusedItem.Kind);

            Tap(ControllerButton.DPadRight);
            Assert.Equal(GuideItemKind.Audio, core.Guide.FocusedItem.Kind);

            Tap(ControllerButton.DPadUp);
            Assert.Equal("Halo Infinite", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void Notifications_opens_in_a_flyout_listing_Playnite_notifications_newest_first_with_focus_on_the_first()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Steam import failed") });

            OpenUtility(GuideItemKind.Notifications);

            Assert.Equal(GuideItemKind.Notifications, core.Guide.Flyout.Kind);
            Assert.Equal("Notifications", core.Guide.Flyout.Title);
            Assert.Equal(new[] { "Steam import failed", "Library updated" }, Flyout.Select(i => i.Title));
            Assert.All(Flyout, i => Assert.Equal(GuideItemKind.Notification, i.Kind));
            Assert.Same(Flyout[0], core.Guide.FocusedItem);

            Tap(ControllerButton.DPadDown);
            Assert.Equal("Library updated", core.Guide.FocusedItem.Title);
        }

        [Theory]
        [InlineData(GuideItemKind.Notifications)]
        [InlineData(GuideItemKind.Audio)]
        public void The_landing_tab_stays_behind_a_utility_flyout(GuideItemKind utility)
        {
            OpenUtility(utility);

            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.Equal("Home", core.Guide.Items[0].Title);
            Assert.Equal(new[] { GuideItemKind.Notifications, GuideItemKind.Audio }, core.Guide.Utilities.Select(u => u.Kind));
            Assert.DoesNotContain(core.Guide.FocusedItem, core.Guide.Items);
            Assert.DoesNotContain(core.Guide.FocusedItem, core.Guide.Utilities);
        }

        [Fact]
        public void Audio_opens_in_a_flyout_titled_Audio()
        {
            OpenUtility(GuideItemKind.Audio);

            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);
            Assert.Equal("Audio", core.Guide.Flyout.Title);
        }

        [Fact]
        public void The_D_pad_keeps_focus_inside_the_flyout()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Steam import failed") });
            OpenUtility(GuideItemKind.Notifications);

            Tap(ControllerButton.DPadUp);
            Assert.Same(Flyout[0], core.Guide.FocusedItem);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Same(Flyout[1], core.Guide.FocusedItem);

            Tap(ControllerButton.DPadLeft);
            Assert.Same(Flyout[1], core.Guide.FocusedItem);
        }

        [Fact]
        public void Notifications_with_none_shows_an_empty_flyout_the_D_pad_and_A_leave_alone()
        {
            OpenUtility(GuideItemKind.Notifications);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Assert.Empty(Flyout);
            Assert.Null(core.Guide.FocusedItem);
            Assert.True(core.Guide.IsOpen);
            Assert.NotNull(core.Guide.Flyout);
        }

        [Fact]
        public void Menu_in_an_empty_flyout_does_nothing()
        {
            OpenUtility(GuideItemKind.Notifications);

            Tap(ControllerButton.Start);

            Assert.Null(core.Guide.Dialog);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.Flyout.Kind);
        }

        [Fact]
        public void Notifications_follows_Playnite_while_its_flyout_is_open()
        {
            OpenUtility(GuideItemKind.Notifications);

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Assert.Equal(new[] { "Library updated" }, Flyout.Select(i => i.Title));
            Assert.Same(Flyout[0], core.Guide.FocusedItem);
        }

        [Fact]
        public void A_on_a_notification_does_nothing()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            OpenUtility(GuideItemKind.Notifications);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.Flyout.Kind);
            Assert.Empty(sounds.Played);
        }

        [Theory]
        [InlineData(GuideItemKind.Notifications)]
        [InlineData(GuideItemKind.Audio)]
        public void B_closes_the_flyout_with_focus_back_on_the_utility_that_opened_it(GuideItemKind utility)
        {
            OpenUtility(utility);
            var opener = core.Guide.Utilities.Single(u => u.Kind == utility);

            Tap(ControllerButton.B);

            Assert.True(core.Guide.IsOpen);
            Assert.Null(core.Guide.Flyout);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.Same(opener, core.Guide.FocusedItem);
            Assert.True(opener.IsFocused);
        }

        [Fact]
        public void B_on_the_utility_after_the_flyout_closes_the_Guide()
        {
            OpenUtility(GuideItemKind.Audio);
            Tap(ControllerButton.B);

            Tap(ControllerButton.B);

            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void The_Guide_reopens_on_the_landing_tab_with_no_flyout()
        {
            OpenUtility(GuideItemKind.Notifications);
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.Guide);

            Assert.Null(core.Guide.Flyout);
            Assert.Equal("Home", core.Guide.FocusedItem.Title);
        }

        [Theory]
        [InlineData(ControllerButton.LeftShoulder)]
        [InlineData(ControllerButton.RightShoulder)]
        public void LB_and_RB_leave_an_open_flyout_and_its_tab_alone(ControllerButton shoulder)
        {
            OpenUtility(GuideItemKind.Audio);
            var focused = core.Guide.FocusedItem;

            Tap(shoulder);

            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.Same(focused, core.Guide.FocusedItem);
        }

        [Fact]
        public void Clicking_a_utility_opens_its_flyout()
        {
            Tap(ControllerButton.Guide);

            core.ChooseItem(core.Guide.Utilities[1]);

            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);
        }

        [Fact]
        public void The_mouse_reaches_the_flyout_but_not_the_tab_behind_it()
        {
            OpenUtility(GuideItemKind.Audio);

            core.FocusItem(core.Guide.Items[0]);
            core.ChooseItem(core.Guide.Utilities[0]);
            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);
            Assert.Same(Flyout[0], core.Guide.FocusedItem);

            core.FocusItem(Flyout[1]);
            Assert.Same(Flyout[1], core.Guide.FocusedItem);
        }

        [Fact]
        public void A_game_stopping_keeps_focus_on_the_utility_it_was_on()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            Tap(ControllerButton.Guide);
            foreach (var _ in core.Guide.Items)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.DPadRight);

            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.Equal(GuideItemKind.Audio, core.Guide.FocusedItem.Kind);
            Assert.Contains(core.Guide.FocusedItem, core.Guide.Utilities);
        }

        [Fact]
        public void A_game_stopping_behind_an_open_flyout_updates_the_tab_and_leaves_focus_in_the_flyout()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12));
            core.OnLibraryChanged(library.Snapshot());
            core.OnGameStarted(library.Id("Halo Infinite"));
            OpenUtility(GuideItemKind.Audio);
            var focused = core.Guide.FocusedItem;

            core.OnGameStopped(library.Id("Halo Infinite"));

            Assert.Equal(GuideItemKind.RecentGame, core.Guide.Items.Single(i => i.Title == "Halo Infinite").Kind);
            Assert.Same(focused, core.Guide.FocusedItem);

            Tap(ControllerButton.B);
            Assert.Same(core.Guide.Utilities.Single(u => u.Kind == GuideItemKind.Audio), core.Guide.FocusedItem);
        }

        [Fact]
        public void Audio_shows_Windows_volume_and_mute_as_they_are_when_it_opens_under_Output_and_Input()
        {
            audio.Volume = 36;
            audio.IsMuted = true;

            OpenUtility(GuideItemKind.Audio);

            Assert.Equal(new[] { GuideItemKind.AudioOutput, GuideItemKind.AudioInput, GuideItemKind.Volume, GuideItemKind.Mute }, Flyout.Select(i => i.Kind));
            Assert.Equal(36, Flyout[2].Level);
            Assert.True(Flyout[3].IsOn);
            Assert.Equal(GuideItemKind.AudioOutput, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Right_and_left_on_the_volume_turn_Windows_volume_up_and_down_two_at_a_time_held_at_0_and_100()
        {
            audio.Volume = 97;
            OpenUtility(GuideItemKind.Audio);
            FocusOn(GuideItemKind.Volume);
            sounds.Played.Clear();

            Tap(ControllerButton.DPadRight);
            Assert.Equal(99, audio.Volume);
            Assert.Equal(99, core.Guide.FocusedItem.Level);

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadRight);
            Assert.Equal(100, audio.Volume);
            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate }, sounds.Played);

            Tap(ControllerButton.DPadLeft);
            Assert.Equal(98, audio.Volume);

            audio.Volume = 1;
            Tap(ControllerButton.DPadLeft);
            Assert.Equal(0, audio.Volume);
            Assert.Equal(0, core.Guide.FocusedItem.Level);
        }

        [Fact]
        public void A_volume_Windows_refuses_to_change_stays_put_on_the_slider_without_a_sound()
        {
            audio.Volume = 0;
            audio.Refuses = true;
            OpenUtility(GuideItemKind.Audio);
            FocusOn(GuideItemKind.Volume);
            sounds.Played.Clear();

            Tap(ControllerButton.DPadRight);

            Assert.Equal(0, core.Guide.FocusedItem.Level);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Holding_left_on_the_volume_keeps_turning_it_down()
        {
            audio.Volume = 50;
            OpenUtility(GuideItemKind.Audio);
            FocusOn(GuideItemKind.Volume);

            Press(ControllerButton.DPadLeft);
            clock.Advance(TimeSpan.FromMilliseconds(700 + 80 * 4));
            Release(ControllerButton.DPadLeft);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(50 - 2 * 6, audio.Volume);
        }

        [Fact]
        public void A_on_Mute_mutes_and_unmutes_Windows()
        {
            OpenUtility(GuideItemKind.Audio);
            FocusOn(GuideItemKind.Mute);

            Tap(ControllerButton.A);
            Assert.True(audio.IsMuted);
            Assert.True(core.Guide.FocusedItem.IsOn);
            Assert.True(core.Guide.IsOpen);

            Tap(ControllerButton.A);
            Assert.False(audio.IsMuted);
            Assert.False(core.Guide.FocusedItem.IsOn);
        }

        [Fact]
        public void Left_and_right_on_Mute_leave_the_volume_alone()
        {
            audio.Volume = 50;
            OpenUtility(GuideItemKind.Audio);
            FocusOn(GuideItemKind.Mute);

            Tap(ControllerButton.DPadRight);

            Assert.Equal(50, audio.Volume);
        }

        private IReadOnlyList<GuideItem> Flyout => core.Guide.Flyout.Items;

        private void OpenUtility(GuideItemKind utility)
        {
            Tap(ControllerButton.Guide);
            foreach (var _ in core.Guide.Items)
            {
                Tap(ControllerButton.DPadDown);
            }

            if (utility == GuideItemKind.Audio)
            {
                Tap(ControllerButton.DPadRight);
            }

            Tap(ControllerButton.A);
        }

        /// <summary>Down through the flyout until focus is on the entry of <paramref name="kind"/>.</summary>
        private void FocusOn(GuideItemKind kind)
        {
            while (core.Guide.FocusedItem.Kind != kind)
            {
                Tap(ControllerButton.DPadDown);
            }
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
