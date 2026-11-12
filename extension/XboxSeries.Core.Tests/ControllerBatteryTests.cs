using Xunit;

namespace XboxSeries.Core.Tests
{
    // The pad's battery a Debug build fakes from XBOXSERIES_FAKE_PAD_BATTERY, so the e2e VM, whose virtual pad is on a
    // cable, can show the pad's battery beside the PC's.
    public class ControllerBatteryTests
    {
        [Theory]
        [InlineData("88", 88)]
        [InlineData(" 12 ", 12)]
        [InlineData("150", 100)]
        [InlineData("-5", 0)]
        public void A_Debug_build_fakes_a_wireless_pad_from_its_variable(string variable, int percent)
        {
            Assert.Equal(ControllerBattery.Wireless(percent), ControllerBattery.FromFake(variable));
        }

        [Theory]
        [InlineData("wired")]
        [InlineData(" Wired ")]
        public void A_Debug_build_fakes_a_pad_on_a_cable(string variable)
        {
            Assert.Equal(ControllerBattery.Wired, ControllerBattery.FromFake(variable));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("lots")]
        public void Anything_else_fakes_nothing_so_the_real_pad_shows(string variable)
        {
            Assert.Null(ControllerBattery.FromFake(variable));
        }
    }
}
