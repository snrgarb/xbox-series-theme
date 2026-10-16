using System;
using System.Globalization;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class StatusTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 30));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeController controller = new FakeController { Battery = ControllerBattery.Wireless(60) };
        private readonly ExtensionCore core;

        public StatusTests()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), controller, new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
        }

        [Fact]
        public void The_status_area_shows_the_time_and_the_controller_battery_from_the_start()
        {
            Assert.Equal(Time(20, 0), core.Status.Time);
            Assert.Equal(ControllerBattery.Wireless(60), core.Status.Battery);
        }

        [Fact]
        public void The_clock_turns_over_on_the_minute()
        {
            clock.Advance(TimeSpan.FromSeconds(29));
            Assert.Equal(Time(20, 0), core.Status.Time);

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(Time(20, 1), core.Status.Time);

            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Equal(Time(20, 3), core.Status.Time);
        }

        [Fact]
        public void The_battery_follows_the_controller_every_minute()
        {
            controller.Battery = ControllerBattery.Wireless(20);

            clock.Advance(TimeSpan.FromSeconds(30));

            Assert.Equal(ControllerBattery.Wireless(20), core.Status.Battery);
        }

        [Fact]
        public void A_pad_connecting_or_going_shows_in_the_battery_at_once()
        {
            controller.Battery = ControllerBattery.None;

            core.OnControllersChanged();

            Assert.Equal(ControllerBattery.None, core.Status.Battery);
        }

        [Fact]
        public void Opening_the_Guide_reads_the_battery_and_clock_afresh()
        {
            controller.Battery = ControllerBattery.Wireless(3);
            clock.Advance(TimeSpan.FromSeconds(10));

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(ControllerBattery.Wireless(3), core.Status.Battery);
        }

        [Fact]
        public void The_status_area_counts_Playnite_notifications()
        {
            Assert.Equal(0, core.Status.NotificationCount);

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Steam import failed") });

            Assert.Equal(2, core.Status.NotificationCount);
        }

        private static string Time(int hour, int minute) =>
            new DateTime(2026, 10, 14, hour, minute, 0).ToString("t", CultureInfo.CurrentCulture);
    }
}
