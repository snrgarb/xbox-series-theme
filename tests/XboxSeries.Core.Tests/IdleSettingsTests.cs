using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Theme settings' Idle page (#90): Dim after and Show things after, each one of the same steps, Show things after
    /// never shorter than Dim after; both save at once, shared by every Profile, and survive a restart.
    /// </summary>
    public class IdleSettingsTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private ExtensionCore core;

        public IdleSettingsTests()
        {
            NewCore();
        }

        private IdleState Idle => core.Idle;

        private static TimeSpan? Minutes(double minutes) => TimeSpan.FromMinutes(minutes);

        /// <summary>A core made afresh over the same secret store, as when Playnite starts again.</summary>
        private void NewCore()
        {
            var windows = new FakeWindowSystem();
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, new FakeDownloads());
        }

        [Fact]
        public void Each_step_reads_as_its_dropdown_shows_it()
        {
            Assert.Equal(
                new[] { "1 min", "2 min", "5 min", "10 min", "15 min", "30 min", "1 h", "2 h", "3 h", "4 h", "Never" },
                IdleState.Steps.Select(IdleState.Describe));
        }
    }
}
