using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The core's side of the Start-up sequence: what it hands the sequence's thread to begin with, what it covers and
    /// holds back while the sequence shows, and Signing you in's 3 s, then "Hi X!", once the sequence says who signed in.
    /// What the sequence itself shows and does with the pad is in <see cref="StartupSequenceTests"/>.
    /// </summary>
    public class StartupTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private readonly FakeUserAccount account = new FakeUserAccount { Name = "gabe", RealName = "Gabe Smith", Picture = @"C:\pics\gabe.png" };
        private readonly FakeLibrary library = new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12));
        private ExtensionCore core;
        private int startupsEnded;

        public StartupTests()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same store, as when Playnite starts again.</summary>
        private void NewCore()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), sounds, new FakeDrives(), account,
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, new FakeDownloads());
            core.OnLibraryChanged(library.Snapshot());
            core.OnNotificationsChanged(new Notification[0]);
            core.StartupEnded += () => startupsEnded++;
        }

        [Fact]
        public void Nothing_shows_until_Playnite_starts_in_Fullscreen()
        {
            Assert.False(core.IsStartupShown);
            Assert.Null(core.Status.Profile);
        }

        [Fact]
        public void Starting_hands_the_sequence_the_chosen_boot_video_and_the_profiles_focus_on_the_first()
        {
            core.AddProfile("Ana");
            core.SetBootVideo(BootVideo.XboxOne);

            var plan = core.OnStartup();

            Assert.True(core.IsStartupShown);
            Assert.Equal(BootVideo.XboxOne, plan.BootVideo);
            Assert.True(plan.AskWhosPlaying);
            Assert.Equal(core.Settings.StartUp.Profiles, plan.Profiles);
            Assert.Equal("gabe", plan.LastProfile.Gamertag);
        }

        [Fact]
        public void Starting_tells_the_sequence_when_Whos_playing_today_doesnt_ask()
        {
            core.SetAskWhosPlaying(false);

            Assert.False(core.OnStartup().AskWhosPlaying);
        }

        [Fact]
        public void Starting_again_while_Start_up_shows_begins_nothing()
        {
            core.OnStartup();

            Assert.Null(core.OnStartup());
            Assert.True(core.IsStartupShown);
        }

        [Fact]
        public void The_Guide_button_neither_opens_the_Guide_nor_Power_during_Start_up()
        {
            core.OnStartup();

            TapGuide();
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.False(core.Guide.IsOpen);
            Assert.False(core.Guide.IsOverlayShown);
            Assert.Null(core.Guide.Dialog);
        }

        [Theory]
        [InlineData(ControllerButton.A)]
        [InlineData(ControllerButton.B)]
        [InlineData(ControllerButton.Start)]
        [InlineData(ControllerButton.DPadUp)]
        public void Other_buttons_do_nothing_during_Start_up(ControllerButton button)
        {
            core.OnStartup();
            sounds.Played.Clear();

            Tap(button);

            Assert.True(core.IsStartupShown);
            Assert.Empty(sounds.Played);
            Assert.False(core.Guide.IsOpen);
            Assert.Null(core.Status.Profile);
        }

        [Fact]
        public void Home_tiles_take_no_buttons_during_Start_up()
        {
            core.FocusHomeTile(core.Home.RecentRow[0]);
            core.OnStartup();

            Tap(ControllerButton.Start);

            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void Signing_in_signs_the_profile_in_at_once_with_no_sound_of_its_own()
        {
            core.AddProfile("Ana");
            var plan = core.OnStartup();
            sounds.Played.Clear();

            core.OnStartupSignedIn(plan.Profiles[1]);

            Assert.Equal("Ana", core.Status.Profile.Gamertag);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Signing_in_shows_for_three_seconds_then_Home_with_Hi_and_the_first_name()
        {
            SignIn(0);
            sounds.Played.Clear();

            clock.Advance(TimeSpan.FromSeconds(2.9));
            Assert.True(core.IsStartupShown);
            Assert.Equal(0, startupsEnded);
            Assert.False(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.False(core.IsStartupShown);
            Assert.Equal(1, startupsEnded);
            Assert.True(core.Toast.IsShown);
            Assert.Equal("Hi Gabe!", core.Toast.Text);
            Assert.Equal(ToastKind.Greeting, core.Toast.Kind);
            Assert.Equal(@"C:\pics\gabe.png", core.Toast.Image);
            Assert.Equal(new[] { UiSound.Notification }, sounds.Played);

            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Home_is_revealed_as_Start_up_ends_on_start_and_on_waking_from_sleep()
        {
            var revealed = new List<bool>();
            core.Home.Revealed += () => revealed.Add(core.IsStartupShown);

            SignIn(0);
            clock.Advance(TimeSpan.FromSeconds(2.9));
            Assert.Empty(revealed);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.Equal(new[] { false }, revealed);

            clock.Advance(TimeSpan.FromSeconds(10));
            core.OnStartupSignedIn(core.OnResumedFromSleep().Profiles[0]);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal(new[] { false, false }, revealed);
        }

        [Fact]
        public void The_greeting_uses_the_gamertag_without_a_real_name()
        {
            core.SetProfileRealName(core.Settings.StartUp.Profiles[0].Id, "");
            SignIn(0);

            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal("Hi gabe!", core.Toast.Text);
        }

        [Fact]
        public void Signing_in_wears_the_cores_own_profile_of_that_id()
        {
            var ana = core.AddProfile("Ana");
            core.OnStartup();

            core.OnStartupSignedIn(new Profile(ana.Id, "Ana", "", null));

            Assert.Same(core.Settings.StartUp.Profiles[1], core.Status.Profile);
        }

        [Fact]
        public void Signing_in_twice_or_out_of_turn_changes_nothing()
        {
            var ana = core.AddProfile("Ana");
            core.OnStartupSignedIn(ana);
            Assert.Null(core.Status.Profile);
            Assert.False(core.IsStartupShown);

            SignIn(0);
            clock.Advance(TimeSpan.FromSeconds(2));
            core.OnStartupSignedIn(ana);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal("gabe", core.Status.Profile.Gamertag);
            Assert.Equal(1, startupsEnded);
            Assert.Equal("Hi Gabe!", core.Toast.Text);

            core.OnStartupSignedIn(ana);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal("gabe", core.Status.Profile.Gamertag);
            Assert.Equal(1, startupsEnded);
        }

        [Fact]
        public void The_profile_signed_in_is_the_last_next_time_Playnite_starts()
        {
            core.AddProfile("Ana");
            SignIn(1);
            clock.Advance(TimeSpan.FromSeconds(10));

            NewCore();

            Assert.Equal("Ana", core.OnStartup().LastProfile.Gamertag);
        }

        [Fact]
        public void Removing_the_profile_signed_in_last_makes_the_first_the_last_next_time()
        {
            var ana = core.AddProfile("Ana");
            SignIn(1);
            clock.Advance(TimeSpan.FromSeconds(10));

            core.RemoveProfile(ana.Id);
            NewCore();

            Assert.Equal("gabe", core.OnStartup().LastProfile.Gamertag);
        }

        [Fact]
        public void Playnite_notifications_arriving_during_Start_up_show_after_the_greeting()
        {
            var plan = core.OnStartup();
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            Assert.False(core.Toast.IsShown);

            core.OnStartupSignedIn(plan.Profiles[0]);
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
            SignIn(0);
            clock.Advance(TimeSpan.FromSeconds(3));

            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Assert.Equal("Hi Gabe!", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal("Library updated", core.Toast.Text);
        }

        [Fact]
        public void Tapping_the_Guide_button_during_the_greeting_opens_the_Guide_on_Profile_and_system()
        {
            SignIn(0);
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
            SignIn(1);
            clock.Advance(TimeSpan.FromSeconds(10));

            TapGuide();
            Tap(ControllerButton.RightShoulder);
            Tap(ControllerButton.RightShoulder);

            Assert.Equal("Ana", core.Guide.Items[0].Title);
            Assert.Equal(@"C:\Gamerpics\a.png", core.Guide.Items[0].Image);
            Assert.Equal(@"C:\Gamerpics\a.png", core.Guide.Tabs.Last().Image);
        }

        [Fact]
        public void Editing_the_profile_signed_in_shows_on_the_status_area_and_the_Guide()
        {
            SignIn(0);
            clock.Advance(TimeSpan.FromSeconds(10));
            TapGuide();
            Tap(ControllerButton.RightShoulder);
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
            SignIn(0);
            var signedIn = core.Status.Profile;

            core.SetProfileGamertag(ana.Id, "Anabel");

            Assert.Same(signedIn, core.Status.Profile);
        }

        [Fact]
        public void Waking_from_sleep_asks_Whos_playing_today_again_without_the_boot_video_focus_on_the_last_profile()
        {
            core.AddProfile("Ana");
            SignIn(1);
            clock.Advance(TimeSpan.FromSeconds(10));

            var plan = core.OnResumedFromSleep();

            Assert.True(core.IsStartupShown);
            Assert.Equal(BootVideo.Off, plan.BootVideo);
            Assert.True(plan.AskWhosPlaying);
            Assert.Equal("Ana", plan.LastProfile.Gamertag);

            core.OnStartupSignedIn(plan.Profiles[0]);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.False(core.IsStartupShown);
            Assert.Equal(2, startupsEnded);
            Assert.Equal("gabe", core.Status.Profile.Gamertag);
            Assert.Equal("Hi Gabe!", core.Toast.Text);
        }

        [Fact]
        public void Waking_from_sleep_closes_the_Guide()
        {
            TapGuide();

            Assert.NotNull(core.OnResumedFromSleep());

            Assert.False(core.Guide.IsOpen);
            Assert.True(core.IsStartupShown);
        }

        [Fact]
        public void Waking_from_sleep_doesnt_ask_when_it_doesnt_ask()
        {
            core.SetAskWhosPlaying(false);

            Assert.Null(core.OnResumedFromSleep());

            Assert.False(core.IsStartupShown);
        }

        [Fact]
        public void Waking_from_sleep_during_Start_up_begins_nothing()
        {
            core.OnStartup();

            Assert.Null(core.OnResumedFromSleep());

            Assert.True(core.IsStartupShown);
        }

        [Fact]
        public void A_notifications_Toast_showing_as_the_PC_wakes_goes_and_shows_again_after_the_greeting()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            Assert.True(core.Toast.IsShown);

            var plan = core.OnResumedFromSleep();
            Assert.False(core.Toast.IsShown);

            core.OnStartupSignedIn(plan.LastProfile);
            clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal("Hi Gabe!", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal("Library updated", core.Toast.Text);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void The_pad_works_as_before_once_Start_up_has_finished()
        {
            SignIn(0);
            clock.Advance(TimeSpan.FromSeconds(10));

            TapGuide();

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
        }
#if DEBUG

        [Theory]
        [InlineData("Gabe Smith", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void A_profile_has_a_real_name_for_Homes_top_bar_to_swap_with_its_gamertag_only_with_some_text(string realName, bool has)
        {
            Assert.Equal(has, new Profile("p", "gabe", realName, null).HasRealName);
        }

        [Fact]
        public void A_call_ringing_during_Start_up_shows_its_Toast_after_the_greeting()
        {
            var plan = core.OnStartup();
            core.DebugRing("Ana", @"C:\avatars\ana.png");
            Assert.False(core.Toast.IsShown);

            core.OnStartupSignedIn(plan.LastProfile);
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

        /// <summary>Playnite starts, and the sequence signs in the profile at <paramref name="index"/> in the plan.</summary>
        private void SignIn(int index) => core.OnStartupSignedIn(core.OnStartup().Profiles[index]);

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
