using System;
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
            core = new ExtensionCore(clock, windows, playnite, sounds, new FakeDrives(), new FakeUserAccount(), audio, new FakeController());
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
        public void Notifications_lists_Playnite_notifications_newest_first_with_focus_on_the_first()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Steam import failed") });

            OpenUtility(GuideItemKind.Notifications);

            Assert.Equal(GuideItemKind.Notifications, core.Guide.Utility);
            Assert.Equal(new[] { "Steam import failed", "Library updated" }, core.Guide.Items.Select(i => i.Title));
            Assert.All(core.Guide.Items, i => Assert.Equal(GuideItemKind.Notification, i.Kind));
            Assert.Same(core.Guide.Items[0], core.Guide.FocusedItem);
            Assert.Empty(core.Guide.Utilities);

            Tap(ControllerButton.DPadDown);
            Assert.Equal("Library updated", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void Notifications_with_none_shows_an_empty_list_the_D_pad_and_A_leave_alone()
        {
            OpenUtility(GuideItemKind.Notifications);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            Assert.Empty(core.Guide.Items);
            Assert.Null(core.Guide.FocusedItem);
            Assert.True(core.Guide.IsOpen);
        }

        [Fact]
        public void Notifications_follows_Playnite_while_it_shows()
        {
            OpenUtility(GuideItemKind.Notifications);

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Assert.Equal(new[] { "Library updated" }, core.Guide.Items.Select(i => i.Title));
            Assert.Same(core.Guide.Items[0], core.Guide.FocusedItem);
        }

        [Fact]
        public void A_on_a_notification_does_nothing()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            OpenUtility(GuideItemKind.Notifications);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.Utility);
            Assert.Empty(sounds.Played);
        }

        [Theory]
        [InlineData(GuideItemKind.Notifications)]
        [InlineData(GuideItemKind.Audio)]
        public void B_leaves_a_utility_for_the_landing_tab_with_focus_back_on_it(GuideItemKind utility)
        {
            OpenUtility(utility);

            Tap(ControllerButton.B);

            Assert.True(core.Guide.IsOpen);
            Assert.Null(core.Guide.Utility);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.Equal(utility, core.Guide.FocusedItem.Kind);
            Assert.Contains(core.Guide.FocusedItem, core.Guide.Utilities);
        }

        [Fact]
        public void The_Guide_reopens_on_the_landing_tab_not_on_a_utility()
        {
            OpenUtility(GuideItemKind.Notifications);
            Tap(ControllerButton.Guide);

            Tap(ControllerButton.Guide);

            Assert.Null(core.Guide.Utility);
            Assert.Equal("Home", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void RB_from_a_utility_goes_on_to_Profile_and_system()
        {
            OpenUtility(GuideItemKind.Audio);

            Tap(ControllerButton.RightShoulder);

            Assert.Null(core.Guide.Utility);
            Assert.Equal(GuideTabKind.ProfileAndSystem, core.Guide.Tab);
        }

        [Fact]
        public void Clicking_a_utility_opens_it()
        {
            Tap(ControllerButton.Guide);

            core.ChooseItem(core.Guide.Utilities[1]);

            Assert.Equal(GuideItemKind.Audio, core.Guide.Utility);
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
        public void Audio_shows_Windows_volume_and_mute_as_they_are_when_it_opens_with_focus_on_the_volume()
        {
            audio.Volume = 36;
            audio.IsMuted = true;

            OpenUtility(GuideItemKind.Audio);

            Assert.Equal(new[] { GuideItemKind.Volume, GuideItemKind.Mute }, core.Guide.Items.Select(i => i.Kind));
            Assert.Equal(36, core.Guide.Items[0].Level);
            Assert.True(core.Guide.Items[1].IsOn);
            Assert.Equal(GuideItemKind.Volume, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Right_and_left_on_the_volume_turn_Windows_volume_up_and_down_two_at_a_time_held_at_0_and_100()
        {
            audio.Volume = 97;
            OpenUtility(GuideItemKind.Audio);
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
            Tap(ControllerButton.DPadDown);
            Assert.Equal(GuideItemKind.Mute, core.Guide.FocusedItem.Kind);

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
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.DPadRight);

            Assert.Equal(50, audio.Volume);
        }

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
