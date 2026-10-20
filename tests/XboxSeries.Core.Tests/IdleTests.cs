using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Idle screen's timers: with no input the screen dims after Dim after and shows things after Show things after,
    /// both counted from the last input; the first input after only wakes it.
    /// </summary>
    public class IdleTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public IdleTests()
        {
            // Playnite's Fullscreen window opens in front.
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
        }

        private void Wait(double minutes) => clock.Advance(TimeSpan.FromMinutes(minutes));

        [Fact]
        public void With_no_input_the_screen_dims_at_two_minutes()
        {
            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);

            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            Assert.True(core.Idle.IsDimmed);
        }

        [Fact]
        public void The_Idle_screen_shows_things_at_five_minutes_with_the_dim_still_on()
        {
            Wait(4.99);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            Wait(0.01);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
            Assert.True(core.Idle.IsDimmed);
        }

        [Fact]
        public void Input_wakes_the_screen_and_the_timers_count_again_from_it()
        {
            Wait(1);
            core.OnUserInput();

            Wait(1.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);

            core.OnUserInput();
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(4.99);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void Dim_after_Never_goes_straight_to_showing_things()
        {
            core.SetDimAfter(null);

            Wait(4.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void Show_things_after_Never_stays_dimmed()
        {
            core.SetShowThingsAfter(null);

            Wait(60);

            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }

        [Fact]
        public void Never_on_both_never_idles()
        {
            core.SetDimAfter(null);
            core.SetShowThingsAfter(null);

            Wait(60);

            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
        }

        [Fact]
        public void Show_things_after_no_later_than_Dim_after_shows_things_at_its_own_time_and_the_dim_with_them()
        {
            core.SetDimAfter(TimeSpan.FromMinutes(5));
            core.SetShowThingsAfter(TimeSpan.FromMinutes(3));

            Wait(3);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
            Wait(5);
            Assert.Equal(IdleStage.ShowingThings, core.Idle.Stage);
        }

        [Fact]
        public void New_times_count_from_when_they_were_set()
        {
            Wait(1);
            core.SetDimAfter(TimeSpan.FromMinutes(1));

            Wait(0.99);
            Assert.Equal(IdleStage.Awake, core.Idle.Stage);
            Wait(0.01);
            Assert.Equal(IdleStage.Dimmed, core.Idle.Stage);
        }
    }
}
