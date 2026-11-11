using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>The PC's own battery in the Status area, before the pad's: on a laptop or handheld, nothing on a desktop.</summary>
    public class PcBatteryTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 11, 11, 20, 0, 0));
        private readonly FakePcBattery pc = new FakePcBattery();

        private ExtensionCore Make()
        {
            var windows = new FakeWindowSystem();
            return new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads(),
                pcBattery: pc);
        }

        // The core as the plugin has it on the Dashboard, the PC's battery started.
        private ExtensionCore Start()
        {
            var core = Make();
            core.StartPcBattery();
            return core;
        }

        [Fact]
        public void A_PC_with_no_battery_shows_nothing()
        {
            pc.Battery = PcBattery.None;

            var shown = Start().Status.PcBattery;

            Assert.False(shown.IsPresent);
            Assert.False(shown.IsCharging);
            Assert.Equal("", shown.Text);
        }

        [Fact]
        public void A_core_given_no_port_shows_no_PC_battery()
        {
            var windows = new FakeWindowSystem();
            var core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            core.StartPcBattery();

            Assert.False(core.Status.PcBattery.IsPresent);
        }

        [Theory]
        [InlineData(0, "0%")]
        [InlineData(50, "50%")]
        [InlineData(100, "100%")]
        public void A_battery_shows_its_cell_filled_to_the_charge_and_the_percent(int percent, string text)
        {
            pc.Battery = PcBattery.Of(percent, charging: false);

            var shown = Start().Status.PcBattery;

            Assert.True(shown.IsPresent);
            Assert.Equal(percent, shown.Percent);
            Assert.Equal(text, shown.Text);
            Assert.False(shown.IsCharging);
        }

        [Fact]
        public void A_battery_charging_shows_the_bolt_beside_its_cell()
        {
            pc.Battery = PcBattery.Of(57, charging: true);

            var shown = Start().Status.PcBattery;

            Assert.True(shown.IsPresent);
            Assert.True(shown.IsCharging);
            Assert.Equal(57, shown.Percent);
            Assert.Equal("57%", shown.Text);
        }

        [Theory]
        [InlineData(-5, 0)]
        [InlineData(150, 100)]
        public void A_charge_Windows_reports_out_of_range_is_held_to_0_to_100(int reported, int shown)
        {
            Assert.Equal(shown, PcBattery.Of(reported, charging: false).Percent);
        }

        [Fact]
        public void The_battery_is_read_again_every_30_seconds()
        {
            pc.Battery = PcBattery.Of(80, charging: false);
            var core = Start();

            pc.Battery = PcBattery.Of(79, charging: false);
            clock.Advance(TimeSpan.FromSeconds(29));
            Assert.Equal(80, core.Status.PcBattery.Percent);

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(79, core.Status.PcBattery.Percent);

            pc.Battery = PcBattery.Of(78, charging: false);
            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.Equal(78, core.Status.PcBattery.Percent);
        }

        [Fact]
        public void A_charge_Windows_does_not_know_shows_the_cell_without_a_percent()
        {
            pc.Battery = PcBattery.WithoutPercent(charging: false);

            var shown = Start().Status.PcBattery;

            Assert.True(shown.IsPresent);
            Assert.False(shown.HasPercent);
            Assert.Equal("", shown.Text);
        }

        [Fact]
        public void A_charge_Windows_stops_knowing_keeps_the_last_percent_known()
        {
            pc.Battery = PcBattery.Of(60, charging: false);
            var core = Start();

            pc.Battery = PcBattery.WithoutPercent(charging: true);
            clock.Advance(TimeSpan.FromSeconds(30));

            Assert.Equal(PcBattery.Of(60, charging: true), core.Status.PcBattery);
            Assert.Equal("60%", core.Status.PcBattery.Text);
        }

        [Fact]
        public void A_status_Windows_cannot_read_keeps_the_last_reading()
        {
            pc.Battery = PcBattery.Of(45, charging: true);
            var core = Start();

            pc.Battery = null;
            clock.Advance(TimeSpan.FromSeconds(30));
            core.OnPowerChanged();

            Assert.Equal(PcBattery.Of(45, charging: true), core.Status.PcBattery);
        }

        [Fact]
        public void Nothing_is_read_until_the_Dashboard_starts_it()
        {
            pc.Battery = PcBattery.Of(70, charging: false);
            var core = Make();

            clock.Advance(TimeSpan.FromMinutes(2));
            core.OnPowerChanged();

            Assert.Equal(PcBattery.None, core.Status.PcBattery);
            Assert.Equal(0, pc.Reads);
        }

        [Fact]
        public void Reading_stops_as_Playnite_stops()
        {
            pc.Battery = PcBattery.Of(70, charging: false);
            var core = Start();

            core.OnApplicationStopped();
            var reads = pc.Reads;
            clock.Advance(TimeSpan.FromMinutes(2));
            core.OnPowerChanged();

            Assert.Equal(reads, pc.Reads);
        }

        [Fact]
        public void A_power_change_puts_the_next_read_30_seconds_after_it()
        {
            pc.Battery = PcBattery.Of(80, charging: false);
            var core = Start();

            clock.Advance(TimeSpan.FromSeconds(20));
            core.OnPowerChanged();
            var reads = pc.Reads;

            clock.Advance(TimeSpan.FromSeconds(29));
            Assert.Equal(reads, pc.Reads);

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(reads + 1, pc.Reads);
        }

        [Fact]
        public void Plugging_in_or_unplugging_shows_at_once()
        {
            pc.Battery = PcBattery.Of(40, charging: false);
            var core = Start();

            pc.Battery = PcBattery.Of(40, charging: true);
            core.OnPowerChanged();

            Assert.True(core.Status.PcBattery.IsCharging);
        }

        [Theory]
        [InlineData("57,charging", 57, true)]
        [InlineData(" 57 , Charging ", 57, true)]
        [InlineData("12", 12, false)]
        [InlineData("100,discharging", 100, false)]
        public void A_Debug_build_fakes_a_battery_from_its_variable(string variable, int percent, bool charging)
        {
            Assert.Equal(PcBattery.Of(percent, charging), PcBattery.FromFake(variable));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("none")]
        [InlineData("lots")]
        public void A_fake_that_names_no_charge_is_no_battery(string variable)
        {
            Assert.Equal(PcBattery.None, PcBattery.FromFake(variable));
        }
    }
}
