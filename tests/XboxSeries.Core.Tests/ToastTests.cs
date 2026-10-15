using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class ToastTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly ExtensionCore core;

        public ToastTests()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(),
                new FakeUserAccount(), new FakeAudio(), new FakeController());
        }

        [Fact]
        public void Notifications_there_from_the_start_show_no_Toast()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_new_notification_shows_a_Toast_for_five_seconds()
        {
            core.OnNotificationsChanged(new Notification[0]);

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Assert.True(core.Toast.IsShown);
            Assert.Equal("Library updated", core.Toast.Text);

            clock.Advance(TimeSpan.FromSeconds(4.9));
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Several_new_notifications_show_one_Toast_after_another_oldest_first()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            core.OnNotificationsChanged(new[]
            {
                new Notification("a", "Library updated"),
                new Notification("b", "Steam import failed"),
                new Notification("c", "Update available"),
            });

            Assert.Equal("Steam import failed", core.Toast.Text);

            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.5));
            Assert.True(core.Toast.IsShown);
            Assert.Equal("Update available", core.Toast.Text);

            clock.Advance(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_notification_arriving_during_a_Toast_waits_its_turn()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            clock.Advance(TimeSpan.FromSeconds(2));

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Update available") });

            Assert.Equal("Library updated", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(3.5));
            Assert.Equal("Update available", core.Toast.Text);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void Pressing_the_Guide_button_during_a_Toast_opens_the_Guide_on_Notifications_and_the_Toast_goes()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            TapGuide();

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.Utility);
            Assert.Equal("Library updated", core.Guide.FocusedItem.Title);
            Assert.False(core.Toast.IsShown);

            core.OnControllerButton(ControllerButton.B, ButtonState.Pressed);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void The_Guide_opened_without_a_Toast_opens_on_the_landing_tab()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            clock.Advance(TimeSpan.FromSeconds(5));

            TapGuide();

            Assert.Null(core.Guide.Utility);
            Assert.Equal("Home", core.Guide.FocusedItem.Title);
        }

        [Fact]
        public void Opening_the_Guide_drops_Toasts_still_waiting()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Update available") });

            TapGuide();
            TapGuide();
            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_notification_arriving_while_the_Guide_shows_Notifications_shows_no_Toast()
        {
            core.OnNotificationsChanged(new Notification[0]);
            TapGuide();
            foreach (var _ in core.Guide.Items)
            {
                core.OnControllerButton(ControllerButton.DPadDown, ButtonState.Pressed);
                core.OnControllerButton(ControllerButton.DPadDown, ButtonState.Released);
            }

            core.OnControllerButton(ControllerButton.A, ButtonState.Pressed);
            Assert.Equal(GuideItemKind.Notifications, core.Guide.Utility);

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Assert.False(core.Toast.IsShown);
            Assert.Equal(new[] { "Library updated" }, core.Guide.Items.Select(i => i.Title));
        }

        private void TapGuide()
        {
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));
        }
    }
}
