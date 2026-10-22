using System;
using System.Collections.Generic;
using XboxSeries.Core.Tests.Fakes;
using XboxSeries.Extension;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // When one Start-up sets the Boot shell's two events, as its sequence runs: FirstFrame at its first frame that shows
    // something, and BootVideoEnded as the boot video ends, however it ends, never before FirstFrame. The Start-up
    // window says when that frame is drawn; the rest is plain C# (BootShellSignals.cs, compiled in here).
    public class BootShellSignalsTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 22, 20, 0, 0));
        private readonly Profile gabe = new Profile("g", "gabe", "Gabe Smith", null);
        private readonly List<string> set = new List<string>();
        private readonly StartupSequence sequence;
        private readonly BootShellSignals signals;

        public BootShellSignalsTests()
        {
            sequence = new StartupSequence(clock, new FakeSoundPlayer());
            signals = new BootShellSignals(sequence.State, set.Add);
        }

        private void Begin(BootVideo video = BootVideo.XboxSeriesX, bool ask = true) =>
            sequence.Begin(new StartupPlan(video, ask, new[] { gabe }, gabe));

        [Fact]
        public void FirstFrame_is_set_as_the_boot_videos_first_frame_shows_and_BootVideoEnded_as_it_finishes()
        {
            Begin();
            Assert.Empty(set);

            signals.FirstFrameShown();
            Assert.Equal(new[] { BootShellEvents.FirstFrame }, set);

            sequence.OnBootVideoEnded();
            Assert.Equal(new[] { BootShellEvents.FirstFrame, BootShellEvents.BootVideoEnded }, set);
        }

        [Fact]
        public void With_the_boot_video_Off_both_are_set_at_Whos_playing_todays_first_frame()
        {
            Begin(BootVideo.Off);
            Assert.Equal(StartupPhase.WhosPlaying, sequence.State.Phase);
            Assert.Empty(set);

            signals.FirstFrameShown();

            Assert.Equal(new[] { BootShellEvents.FirstFrame, BootShellEvents.BootVideoEnded }, set);
        }

        [Fact]
        public void A_boot_video_skipped_sets_BootVideoEnded_as_it_is_skipped()
        {
            Begin();
            signals.FirstFrameShown();

            sequence.OnButton(ControllerButton.A, ButtonState.Pressed);

            Assert.Equal(new[] { BootShellEvents.FirstFrame, BootShellEvents.BootVideoEnded }, set);
        }

        [Fact]
        public void A_boot_video_past_its_20_s_deadline_sets_BootVideoEnded()
        {
            Begin();
            signals.FirstFrameShown();

            clock.Advance(TimeSpan.FromSeconds(19.9));
            Assert.Equal(new[] { BootShellEvents.FirstFrame }, set);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.Equal(new[] { BootShellEvents.FirstFrame, BootShellEvents.BootVideoEnded }, set);
        }

        [Fact]
        public void A_boot_video_ended_before_the_first_frame_sets_BootVideoEnded_only_with_FirstFrame()
        {
            // Skipped before it opened, or missing: Who's playing today?'s frame is the first to show anything.
            Begin();
            sequence.OnButton(ControllerButton.B, ButtonState.Pressed);
            Assert.Empty(set);

            signals.FirstFrameShown();

            Assert.Equal(new[] { BootShellEvents.FirstFrame, BootShellEvents.BootVideoEnded }, set);
        }

        [Fact]
        public void With_Whos_playing_today_not_asking_both_are_set_at_Signing_you_ins_first_frame()
        {
            Begin(BootVideo.Off, ask: false);
            Assert.Equal(StartupPhase.SigningIn, sequence.State.Phase);

            signals.FirstFrameShown();

            Assert.Equal(new[] { BootShellEvents.FirstFrame, BootShellEvents.BootVideoEnded }, set);
        }

        [Fact]
        public void Each_is_set_once_whatever_follows()
        {
            Begin();
            signals.FirstFrameShown();
            sequence.OnBootVideoEnded();

            signals.FirstFrameShown();
            sequence.OnButton(ControllerButton.A, ButtonState.Pressed);
            sequence.End();

            Assert.Equal(new[] { BootShellEvents.FirstFrame, BootShellEvents.BootVideoEnded }, set);
        }
    }
}
