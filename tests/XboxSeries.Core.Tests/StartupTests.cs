using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class StartupTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private readonly FakeUserAccount account = new FakeUserAccount { Name = "gabe", RealName = "Gabe Smith", Picture = @"C:\pics\gabe.png" };
        private readonly FakeLibrary library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12));
        private ExtensionCore core;

        public StartupTests()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same store, as when Playnite starts again.</summary>
        private void NewCore()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), sounds, new FakeDrives(), account,
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets);
            core.OnLibraryChanged(library.Snapshot());
            core.OnNotificationsChanged(new Notification[0]);
        }

        private StartupState Startup => core.Startup;

        [Fact]
        public void Nothing_shows_until_Playnite_starts_in_Fullscreen()
        {
            Assert.Equal(StartupPhase.None, Startup.Phase);
            Assert.False(Startup.IsShown);
            Assert.Null(core.Status.Profile);
        }

        [Fact]
        public void Starting_plays_the_chosen_boot_video()
        {
            core.SetBootVideo(BootVideo.XboxOne);

            core.OnStartup();

            Assert.Equal(StartupPhase.BootVideo, Startup.Phase);
            Assert.True(Startup.IsShown);
            Assert.Equal(BootVideo.XboxOne, Startup.BootVideo);
        }

        [Fact]
        public void After_the_boot_video_Whos_playing_today_asks_focus_on_the_first_profile()
        {
            core.OnStartup();

            core.OnBootVideoEnded();

            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
            Assert.Equal("gabe", Startup.FocusedProfile.Gamertag);
            Assert.Equal(core.Settings.StartUp.Profiles, Startup.Profiles);
        }

        [Fact]
        public void With_the_boot_video_Off_Whos_playing_today_asks_at_once()
        {
            core.SetBootVideo(BootVideo.Off);

            core.OnStartup();

            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
        }

        [Theory]
        [InlineData(ControllerButton.A)]
        [InlineData(ControllerButton.B)]
        [InlineData(ControllerButton.Start)]
        [InlineData(ControllerButton.DPadLeft)]
        public void Any_button_skips_the_boot_video(ControllerButton button)
        {
            core.OnStartup();

            Tap(button);

            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
        }

        [Fact]
        public void The_Guide_button_skips_the_boot_video_without_opening_the_Guide_or_Power()
        {
            core.OnStartup();

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
            Assert.False(core.Guide.IsOpen);
            Assert.False(core.Guide.IsOverlayShown);
        }

        [Fact]
        public void The_A_that_skips_the_boot_video_doesnt_also_sign_in()
        {
            core.OnStartup();

            Tap(ControllerButton.A);

            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
        }

        [Fact]
        public void Left_and_right_move_along_the_profiles_with_the_Navigate_sound_held_at_either_end()
        {
            core.AddProfile("Ana");
            core.AddProfile("Bo");
            core.OnStartup();
            core.OnBootVideoEnded();
            sounds.Played.Clear();

            Tap(ControllerButton.DPadLeft);
            Assert.Equal("gabe", Startup.FocusedProfile.Gamertag);

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.DPadRight);
            Assert.Equal("Bo", Startup.FocusedProfile.Gamertag);

            Tap(ControllerButton.DPadRight);
            Assert.Equal("Bo", Startup.FocusedProfile.Gamertag);

            Tap(ControllerButton.DPadLeft);
            Assert.Equal("Ana", Startup.FocusedProfile.Gamertag);
            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate, UiSound.Navigate }, sounds.Played);
        }

        [Fact]
        public void A_signs_the_focused_profile_in_with_the_Enter_and_Login_sounds()
        {
            core.AddProfile("Ana");
            core.OnStartup();
            core.OnBootVideoEnded();
            Tap(ControllerButton.DPadRight);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal(StartupPhase.SigningIn, Startup.Phase);
            Assert.Equal("Ana", Startup.SigningIn.Gamertag);
            Assert.Equal("Ana", core.Status.Profile.Gamertag);
            Assert.Equal(new[] { UiSound.Enter, UiSound.Login }, sounds.Played);
        }

        [Theory]
        [InlineData(ControllerButton.B)]
        [InlineData(ControllerButton.Start)]
        [InlineData(ControllerButton.DPadUp)]
        public void Other_buttons_do_nothing_on_Whos_playing_today(ControllerButton button)
        {
            core.OnStartup();
            core.OnBootVideoEnded();
            sounds.Played.Clear();

            Tap(button);

            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
            Assert.Empty(sounds.Played);
            Assert.False(core.Guide.IsOpen);
        }

        [Fact]
        public void The_Guide_button_neither_opens_the_Guide_nor_Power_on_Whos_playing_today()
        {
            core.OnStartup();
            core.OnBootVideoEnded();

            TapGuide();
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);

            Assert.False(core.Guide.IsOpen);
            Assert.Null(core.Guide.Dialog);
            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
        }

        [Fact]
        public void Home_tiles_take_no_buttons_during_Start_up()
        {
            core.FocusHomeTile(core.Home.RecentRow[0]);
            core.OnStartup();
            core.OnBootVideoEnded();

            Tap(ControllerButton.Start);

            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Signing_in_shows_for_three_seconds_then_Home_with_Hi_and_the_first_name()
        {
            core.OnStartup();
            core.OnBootVideoEnded();
            Tap(ControllerButton.A);
            sounds.Played.Clear();

            clock.Advance(TimeSpan.FromSeconds(2.9));
            Assert.Equal(StartupPhase.SigningIn, Startup.Phase);
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.Equal(StartupPhase.None, Startup.Phase);
            Assert.False(Startup.IsShown);
            Assert.True(core.Toast.IsShown);
            Assert.Equal("Hi Gabe!", core.Toast.Text);
            Assert.Equal(ToastKind.Greeting, core.Toast.Kind);
            Assert.Equal(@"C:\pics\gabe.png", core.Toast.Image);
            Assert.Equal(new[] { UiSound.Notification }, sounds.Played);

            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_greeting_uses_the_gamertag_without_a_real_name()
        {
            core.SetProfileRealName(core.Settings.StartUp.Profiles[0].Id, "");
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.A);

            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal("Hi gabe!", core.Toast.Text);
        }

        [Fact]
        public void Not_asking_signs_the_last_profile_in_after_the_boot_video()
        {
            var ana = core.AddProfile("Ana");
            core.AddProfile("Bo");
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));
            core.SetAskWhosPlaying(false);
            core.SetBootVideo(BootVideo.Xbox);

            NewCore();
            core.OnStartup();
            Assert.Equal(StartupPhase.BootVideo, Startup.Phase);
            core.OnBootVideoEnded();

            Assert.Equal(StartupPhase.SigningIn, Startup.Phase);
            Assert.Equal(ana.Id, Startup.SigningIn.Id);
            Assert.Equal(ana.Id, core.Status.Profile.Id);
        }

        [Fact]
        public void Not_asking_with_none_signed_in_before_signs_the_first_in()
        {
            core.AddProfile("Ana");
            core.SetAskWhosPlaying(false);
            core.SetBootVideo(BootVideo.Off);

            core.OnStartup();

            Assert.Equal(StartupPhase.SigningIn, Startup.Phase);
            Assert.Equal("gabe", Startup.SigningIn.Gamertag);
        }

        [Fact]
        public void Whos_playing_today_focuses_the_profile_signed_in_last()
        {
            core.AddProfile("Ana");
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));

            NewCore();
            core.OnStartup();

            Assert.Equal("Ana", Startup.FocusedProfile.Gamertag);
        }

        [Fact]
        public void Removing_the_profile_signed_in_last_focuses_the_first_next_time()
        {
            var ana = core.AddProfile("Ana");
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));

            core.RemoveProfile(ana.Id);
            NewCore();
            core.OnStartup();

            Assert.Equal("gabe", Startup.FocusedProfile.Gamertag);
        }

        [Fact]
        public void Playnite_notifications_arriving_during_Start_up_show_after_the_greeting()
        {
            core.OnStartup();
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            Assert.False(core.Toast.IsShown);

            core.OnBootVideoEnded();
            Tap(ControllerButton.A);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Update available") });
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal("Hi Gabe!", core.Toast.Text);

            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(core.Toast.IsShown);
            clock.Advance(TimeSpan.FromSeconds(0.5));
            Assert.Equal("Library updated", core.Toast.Text);
            Assert.True(core.Toast.IsShown);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal("Update available", core.Toast.Text);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void A_notification_arriving_during_the_greeting_waits_its_turn()
        {
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(3));

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Assert.Equal("Hi Gabe!", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal("Library updated", core.Toast.Text);
        }

        [Fact]
        public void Tapping_the_Guide_button_during_the_greeting_opens_the_Guide_on_Profile_and_system()
        {
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(3));

            TapGuide();

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.ProfileAndSystem, core.Guide.Tab);
            Assert.Null(core.Guide.Flyout);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_Guides_profile_card_and_tab_show_the_profile_signed_in()
        {
            var ana = core.AddProfile("Ana");
            core.SetProfilePicture(ana.Id, @"C:\Gamerpics\a.png");
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));

            TapGuide();
            Tap(ControllerButton.RightShoulder);

            Assert.Equal("Ana", core.Guide.Items[0].Title);
            Assert.Equal(@"C:\Gamerpics\a.png", core.Guide.Items[0].Image);
            Assert.Equal(@"C:\Gamerpics\a.png", core.Guide.Tabs.Last().Image);
        }

        [Fact]
        public void Editing_the_profile_signed_in_shows_on_the_status_area_and_the_Guide()
        {
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));
            TapGuide();
            Tap(ControllerButton.RightShoulder);

            var id = core.Status.Profile.Id;
            core.SetProfileGamertag(id, "Master Chief");
            core.SetProfilePicture(id, @"C:\Gamerpics\b.png");

            Assert.Equal("Master Chief", core.Status.Profile.Gamertag);
            Assert.Equal(@"C:\Gamerpics\b.png", core.Status.Profile.Picture);
            Assert.Equal("Master Chief", core.Guide.Items[0].Title);
            Assert.Equal(@"C:\Gamerpics\b.png", core.Guide.Items[0].Image);
            Assert.Equal(@"C:\Gamerpics\b.png", core.Guide.Tabs.Last().Image);
            Assert.Equal(GuideItemKind.Profile, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void Editing_another_profile_leaves_the_one_signed_in()
        {
            var ana = core.AddProfile("Ana");
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.A);
            var signedIn = core.Status.Profile;

            core.SetProfileGamertag(ana.Id, "Anabel");

            Assert.Same(signedIn, core.Status.Profile);
        }

        [Fact]
        public void Waking_from_sleep_asks_Whos_playing_today_again_without_the_boot_video_focus_on_the_last_profile()
        {
            core.AddProfile("Ana");
            core.OnStartup();
            core.OnBootVideoEnded();
            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));

            core.OnResumedFromSleep();

            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
            Assert.Equal("Ana", Startup.FocusedProfile.Gamertag);

            Tap(ControllerButton.DPadLeft);
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal("gabe", core.Status.Profile.Gamertag);
            Assert.Equal("Hi Gabe!", core.Toast.Text);
        }

        [Fact]
        public void Waking_from_sleep_closes_the_Guide()
        {
            TapGuide();

            core.OnResumedFromSleep();

            Assert.False(core.Guide.IsOpen);
            Assert.Equal(StartupPhase.WhosPlaying, Startup.Phase);
        }

        [Fact]
        public void Waking_from_sleep_doesnt_ask_when_it_doesnt_ask()
        {
            core.SetAskWhosPlaying(false);

            core.OnResumedFromSleep();

            Assert.False(Startup.IsShown);
        }

        [Fact]
        public void Waking_from_sleep_during_Start_up_changes_nothing()
        {
            core.OnStartup();

            core.OnResumedFromSleep();

            Assert.Equal(StartupPhase.BootVideo, Startup.Phase);
        }

        [Fact]
        public void A_notifications_Toast_showing_as_the_PC_wakes_goes_and_shows_again_after_the_greeting()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            Assert.True(core.Toast.IsShown);

            core.OnResumedFromSleep();
            Assert.False(core.Toast.IsShown);

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal("Hi Gabe!", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal("Library updated", core.Toast.Text);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void The_boot_video_ending_twice_or_out_of_turn_changes_nothing()
        {
            core.OnBootVideoEnded();
            Assert.False(Startup.IsShown);

            core.OnStartup();
            core.OnBootVideoEnded();
            Tap(ControllerButton.A);
            core.OnBootVideoEnded();

            Assert.Equal(StartupPhase.SigningIn, Startup.Phase);
        }

        [Fact]
        public void The_pad_works_as_before_once_Start_up_has_finished()
        {
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(10));

            TapGuide();

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
        }
#if DEBUG

        [Fact]
        public void A_call_ringing_during_Start_up_shows_its_Toast_after_the_greeting()
        {
            core.SetBootVideo(BootVideo.Off);
            core.OnStartup();
            core.DebugRing("Ana", @"C:\avatars\ana.png");
            Assert.False(core.Toast.IsShown);

            Tap(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal(ToastKind.Greeting, core.Toast.Kind);

            core.DebugRing("Bo", null);
            Assert.Equal(ToastKind.Greeting, core.Toast.Kind);

            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);
            Assert.Equal("Bo is calling", core.Toast.Text);
        }
#endif

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }

        private void TapGuide()
        {
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));
        }
    }
}
