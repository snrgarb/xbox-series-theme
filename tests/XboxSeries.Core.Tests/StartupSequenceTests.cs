using System;
using System.Collections.Generic;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Start-up sequence on its own thread: the boot video, Who's playing today? and Signing you in, driven by the
    /// buttons and pad readings it is handed, and telling whoever listens who signed in.
    /// </summary>
    public class StartupSequenceTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly Profile gabe = new Profile("g", "gabe", "Gabe Smith", @"C:\pics\gabe.png");
        private readonly Profile ana = new Profile("a", "Ana", "", null);
        private readonly Profile bo = new Profile("b", "Bo", "", null);
        private readonly List<Profile> signedIn = new List<Profile>();
        private readonly StartupSequence sequence;

        public StartupSequenceTests()
        {
            sequence = new StartupSequence(clock, sounds);
            sequence.SignedIn += signedIn.Add;
        }

        private StartupState State => sequence.State;

        /// <summary>A plan over gabe, Ana and Bo, asking Who's playing today?, gabe signed in last.</summary>
        private StartupPlan Plan(BootVideo video = BootVideo.XboxSeriesX, bool ask = true, Profile last = null) =>
            new StartupPlan(video, ask, new[] { gabe, ana, bo }, last ?? gabe);

        [Fact]
        public void Nothing_shows_until_the_sequence_begins()
        {
            Assert.Equal(StartupPhase.None, State.Phase);
            Assert.False(State.IsShown);
        }

        [Fact]
        public void Beginning_plays_the_plans_boot_video()
        {
            sequence.Begin(Plan(BootVideo.XboxOne));

            Assert.Equal(StartupPhase.BootVideo, State.Phase);
            Assert.True(State.IsShown);
            Assert.Equal(BootVideo.XboxOne, State.BootVideo);
        }

        [Fact]
        public void After_the_boot_video_Whos_playing_today_asks_focus_on_the_profile_signed_in_last()
        {
            sequence.Begin(Plan(last: ana));

            sequence.OnBootVideoEnded();

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
            Assert.Same(ana, State.FocusedProfile);
            Assert.Null(State.SigningIn);
            Assert.Equal(new[] { gabe, ana, bo }, State.Profiles);
        }

        [Fact]
        public void With_the_boot_video_Off_Whos_playing_today_asks_at_once()
        {
            sequence.Begin(Plan(BootVideo.Off));

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
            Assert.Equal(BootVideo.Off, State.BootVideo);
        }

        [Theory]
        [InlineData(ControllerButton.A)]
        [InlineData(ControllerButton.B)]
        [InlineData(ControllerButton.Start)]
        [InlineData(ControllerButton.DPadLeft)]
        [InlineData(ControllerButton.Guide)]
        public void Any_button_skips_the_boot_video(ControllerButton button)
        {
            sequence.Begin(Plan());

            Tap(button);

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
        }

        [Fact]
        public void A_button_let_go_doesnt_skip_the_boot_video()
        {
            sequence.Begin(Plan());

            sequence.OnButton(ControllerButton.A, ButtonState.Released);

            Assert.Equal(StartupPhase.BootVideo, State.Phase);
        }

        [Fact]
        public void A_boot_video_running_past_20_s_ends_as_one_that_finished()
        {
            sequence.Begin(Plan());

            clock.Advance(TimeSpan.FromSeconds(19.9));
            Assert.Equal(StartupPhase.BootVideo, State.Phase);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
        }

        [Fact]
        public void A_boot_video_ended_isnt_ended_again_by_the_deadline()
        {
            sequence.Begin(Plan(ask: false));
            sequence.OnBootVideoEnded();
            Assert.Single(signedIn);

            clock.Advance(TimeSpan.FromSeconds(20));

            Assert.Single(signedIn);
            Assert.Equal(StartupPhase.SigningIn, State.Phase);
        }

        [Fact]
        public void A_boot_video_skipped_isnt_ended_again_by_the_deadline()
        {
            sequence.Begin(Plan(ask: false));
            Tap(ControllerButton.B);

            clock.Advance(TimeSpan.FromSeconds(20));

            Assert.Single(signedIn);
        }

        [Fact]
        public void The_A_that_skips_the_boot_video_doesnt_also_sign_in()
        {
            sequence.Begin(Plan());

            Tap(ControllerButton.A);

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
            Assert.Empty(signedIn);
        }

        [Fact]
        public void Left_and_right_move_along_the_profiles_with_the_Navigate_sound_held_at_either_end()
        {
            sequence.Begin(Plan(BootVideo.Off));

            Tap(ControllerButton.DPadLeft);
            Assert.Same(gabe, State.FocusedProfile);

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadRight);
            Assert.Same(bo, State.FocusedProfile);

            Tap(ControllerButton.DPadRight);
            Assert.Same(bo, State.FocusedProfile);

            Tap(ControllerButton.DPadLeft);
            Assert.Same(ana, State.FocusedProfile);
            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate, UiSound.Navigate }, sounds.Played);
        }

        [Fact]
        public void A_signs_the_focused_profile_in_with_the_Enter_and_Login_sounds()
        {
            sequence.Begin(Plan(BootVideo.Off));
            Tap(ControllerButton.DPadRight);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal(StartupPhase.SigningIn, State.Phase);
            Assert.Same(ana, State.SigningIn);
            Assert.Equal(new[] { ana }, signedIn);
            Assert.Equal(new[] { UiSound.Enter, UiSound.Login }, sounds.Played);
        }

        [Theory]
        [InlineData(ControllerButton.B)]
        [InlineData(ControllerButton.Start)]
        [InlineData(ControllerButton.DPadUp)]
        [InlineData(ControllerButton.Guide)]
        public void Other_buttons_do_nothing_on_Whos_playing_today(ControllerButton button)
        {
            sequence.Begin(Plan(BootVideo.Off));

            Tap(button);

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
            Assert.Same(gabe, State.FocusedProfile);
            Assert.Empty(sounds.Played);
            Assert.Empty(signedIn);
        }

        [Fact]
        public void Not_asking_signs_the_last_profile_in_after_the_boot_video()
        {
            sequence.Begin(Plan(BootVideo.Xbox, ask: false, last: ana));
            Assert.Equal(StartupPhase.BootVideo, State.Phase);

            sequence.OnBootVideoEnded();

            Assert.Equal(StartupPhase.SigningIn, State.Phase);
            Assert.Same(ana, State.SigningIn);
            Assert.Equal(new[] { ana }, signedIn);
            Assert.Equal(new[] { UiSound.Login }, sounds.Played);
        }

        [Fact]
        public void Not_asking_with_the_boot_video_Off_signs_the_last_profile_in_at_once()
        {
            sequence.Begin(Plan(BootVideo.Off, ask: false));

            Assert.Equal(StartupPhase.SigningIn, State.Phase);
            Assert.Same(gabe, State.SigningIn);
            Assert.Equal(new[] { gabe }, signedIn);
        }

        [Fact]
        public void Signing_in_shows_until_the_sequence_is_ended()
        {
            sequence.Begin(Plan(BootVideo.Off));
            Tap(ControllerButton.A);

            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal(StartupPhase.SigningIn, State.Phase);

            sequence.End();
            Assert.Equal(StartupPhase.None, State.Phase);
            Assert.False(State.IsShown);
        }

        [Fact]
        public void Buttons_while_signing_in_do_nothing()
        {
            sequence.Begin(Plan(BootVideo.Off));
            Tap(ControllerButton.A);
            sounds.Played.Clear();

            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadRight);

            Assert.Same(gabe, State.SigningIn);
            Assert.Single(signedIn);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void The_boot_video_ending_twice_or_out_of_turn_changes_nothing()
        {
            sequence.OnBootVideoEnded();
            Assert.False(State.IsShown);

            sequence.Begin(Plan());
            sequence.OnBootVideoEnded();
            Tap(ControllerButton.A);
            sequence.OnBootVideoEnded();

            Assert.Equal(StartupPhase.SigningIn, State.Phase);
            Assert.Single(signedIn);
        }

        [Fact]
        public void Beginning_again_while_it_shows_changes_nothing()
        {
            sequence.Begin(Plan(BootVideo.Off));

            sequence.Begin(Plan(BootVideo.XboxOne));

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
            Assert.Equal(BootVideo.Off, State.BootVideo);
        }

        [Fact]
        public void Ending_during_the_boot_video_stops_its_deadline()
        {
            sequence.Begin(Plan(ask: false));

            sequence.End();
            clock.Advance(TimeSpan.FromSeconds(20));

            Assert.Equal(StartupPhase.None, State.Phase);
            Assert.Empty(signedIn);
        }

        [Fact]
        public void Nothing_happens_once_the_sequence_has_ended()
        {
            sequence.Begin(Plan(BootVideo.Off));
            sequence.End();

            Tap(ControllerButton.A);
            sequence.OnBootVideoEnded();
            sequence.OnPadReading(new[] { ControllerButton.A });
            sequence.Begin(Plan(BootVideo.Off));
            sequence.End();

            Assert.Equal(StartupPhase.None, State.Phase);
            Assert.Empty(signedIn);
            Assert.Empty(sounds.Played);
        }

        // Pad readings

        [Fact]
        public void A_button_newly_down_in_a_pad_reading_is_a_press()
        {
            sequence.Begin(Plan(BootVideo.Off));
            Read();

            Read(ControllerButton.DPadRight);

            Assert.Same(ana, State.FocusedProfile);
        }

        [Fact]
        public void A_button_still_down_in_the_next_reading_presses_only_once()
        {
            sequence.Begin(Plan(BootVideo.Off));
            Read();

            Read(ControllerButton.DPadRight);
            Read(ControllerButton.DPadRight);
            Read(ControllerButton.DPadRight);

            Assert.Same(ana, State.FocusedProfile);
        }

        [Fact]
        public void A_button_let_go_and_pressed_again_presses_again()
        {
            sequence.Begin(Plan(BootVideo.Off));
            Read();

            Read(ControllerButton.DPadRight);
            Read();
            Read(ControllerButton.DPadRight);

            Assert.Same(bo, State.FocusedProfile);
        }

        [Fact]
        public void Each_button_newly_down_in_one_reading_is_pressed()
        {
            sequence.Begin(Plan());
            Read();

            Read(ControllerButton.B, ControllerButton.DPadRight);

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
            Assert.Same(ana, State.FocusedProfile);
        }

        [Fact]
        public void A_button_held_from_before_Start_up_isnt_a_press()
        {
            sequence.Begin(Plan(BootVideo.Off));

            Read(ControllerButton.A);
            Read(ControllerButton.A);

            Assert.Equal(StartupPhase.WhosPlaying, State.Phase);
            Assert.Empty(signedIn);
        }

        [Fact]
        public void A_button_held_from_before_Start_up_doesnt_skip_the_boot_video()
        {
            sequence.Begin(Plan());

            Read(ControllerButton.A);

            Assert.Equal(StartupPhase.BootVideo, State.Phase);
        }

        [Fact]
        public void A_button_held_from_before_Start_up_let_go_then_pressed_is_a_press()
        {
            sequence.Begin(Plan(BootVideo.Off));
            Read(ControllerButton.A);

            Read();
            Read(ControllerButton.A);

            Assert.Equal(new[] { gabe }, signedIn);
        }

        private void Tap(ControllerButton button)
        {
            sequence.OnButton(button, ButtonState.Pressed);
            sequence.OnButton(button, ButtonState.Released);
        }

        private void Read(params ControllerButton[] down) => sequence.OnPadReading(down);
    }
}
