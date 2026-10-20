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

        [Fact]
        public void Show_things_after_offers_no_step_shorter_than_Dim_after()
        {
            core.SetDimAfter(Minutes(15));

            Assert.Equal(
                new[] { "15 min", "30 min", "1 h", "2 h", "3 h", "4 h", "Never" },
                Idle.ShowThingsAfterSteps.Select(IdleState.Describe));
        }

        [Fact]
        public void With_Dim_after_on_Never_Show_things_after_offers_only_Never()
        {
            core.SetDimAfter(null);

            Assert.Equal(new TimeSpan?[] { null }, Idle.ShowThingsAfterSteps);
        }

        [Fact]
        public void Show_things_after_can_t_be_set_below_Dim_after()
        {
            core.SetShowThingsAfter(Minutes(1));

            Assert.Equal(Minutes(5), Idle.ShowThingsAfter);
        }

        [Fact]
        public void Show_things_after_can_be_set_to_Dim_after()
        {
            core.SetShowThingsAfter(Minutes(2));

            Assert.Equal(Minutes(2), Idle.ShowThingsAfter);
        }

        [Fact]
        public void Raising_Dim_after_past_Show_things_after_raises_Show_things_after_with_it()
        {
            core.SetDimAfter(Minutes(30));

            Assert.Equal(Minutes(30), Idle.DimAfter);
            Assert.Equal(Minutes(30), Idle.ShowThingsAfter);
        }

        [Fact]
        public void Dim_after_on_Never_puts_Show_things_after_on_Never_too()
        {
            core.SetDimAfter(null);

            Assert.Null(Idle.ShowThingsAfter);
        }

        [Fact]
        public void Lowering_Dim_after_leaves_Show_things_after_as_it_was()
        {
            core.SetDimAfter(Minutes(1));

            Assert.Equal(Minutes(5), Idle.ShowThingsAfter);
        }

        [Fact]
        public void A_time_that_isn_t_a_step_is_refused()
        {
            core.SetDimAfter(Minutes(3));
            core.SetShowThingsAfter(Minutes(7));

            Assert.Equal(Minutes(2), Idle.DimAfter);
            Assert.Equal(Minutes(5), Idle.ShowThingsAfter);
        }
    }
}
