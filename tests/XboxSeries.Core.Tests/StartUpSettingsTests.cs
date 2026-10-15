using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class StartUpSettingsTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private readonly FakeUserAccount account = new FakeUserAccount
        {
            Name = "gabe",
            RealName = "Gabe Smith",
            Picture = @"C:\pics\gabe.png",
            Gamerpics = new[] { @"C:\Gamerpics\a.png", @"C:\Gamerpics\b.png" },
        };

        private ExtensionCore core;

        public StartUpSettingsTests()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same store, as when Playnite starts again.</summary>
        private void NewCore()
        {
            var windows = new FakeWindowSystem();
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), account,
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets);
        }

        private StartUpSettingsState StartUp => core.Settings.StartUp;

        [Fact]
        public void With_nothing_saved_the_Xbox_Series_X_boot_video_plays_and_Whos_playing_today_asks()
        {
            Assert.Equal(BootVideo.XboxSeriesX, StartUp.BootVideo);
            Assert.True(StartUp.AskWhosPlaying);
        }

        [Fact]
        public void With_nothing_saved_there_is_one_profile_of_the_Windows_user()
        {
            var profile = Assert.Single(StartUp.Profiles);
            Assert.Equal("gabe", profile.Gamertag);
            Assert.Equal("Gabe Smith", profile.RealName);
            Assert.Equal(@"C:\pics\gabe.png", profile.Picture);
            Assert.False(string.IsNullOrEmpty(profile.Id));
            Assert.Equal(StartUp.Profiles, core.Startup.Profiles);
        }

        [Fact]
        public void The_gamerpics_are_the_account_picture_then_the_Gamerpics_folders()
        {
            Assert.Equal(new[] { @"C:\pics\gabe.png", @"C:\Gamerpics\a.png", @"C:\Gamerpics\b.png" }, StartUp.Gamerpics);
        }

        [Fact]
        public void Without_an_account_picture_the_gamerpics_are_only_the_folders()
        {
            account.Picture = null;
            NewCore();

            Assert.Equal(new[] { @"C:\Gamerpics\a.png", @"C:\Gamerpics\b.png" }, StartUp.Gamerpics);
        }

        [Fact]
        public void The_boot_video_and_whether_it_asks_are_saved_at_once_and_kept()
        {
            core.SetBootVideo(BootVideo.Xbox360);
            core.SetAskWhosPlaying(false);

            Assert.Equal(BootVideo.Xbox360, StartUp.BootVideo);
            Assert.False(StartUp.AskWhosPlaying);

            NewCore();

            Assert.Equal(BootVideo.Xbox360, StartUp.BootVideo);
            Assert.False(StartUp.AskWhosPlaying);
        }

        [Fact]
        public void Nothing_of_Start_up_goes_to_the_store_until_something_changes()
        {
            Assert.Equal(0, secrets.Writes);
        }

        [Fact]
        public void A_profile_added_goes_after_the_others_trimmed_wearing_the_account_picture_and_is_kept()
        {
            var added = core.AddProfile("  Ana  ");

            Assert.Equal("Ana", added.Gamertag);
            Assert.Equal("", added.RealName);
            Assert.Equal(@"C:\pics\gabe.png", added.Picture);
            Assert.Equal(new[] { "gabe", "Ana" }, StartUp.Profiles.Select(p => p.Gamertag));
            Assert.Equal(StartUp.Profiles, core.Startup.Profiles);

            NewCore();

            Assert.Equal(new[] { "gabe", "Ana" }, StartUp.Profiles.Select(p => p.Gamertag));
            Assert.Equal(added.Id, StartUp.Profiles[1].Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void A_profile_without_a_gamertag_is_refused(string gamertag)
        {
            Assert.Null(core.AddProfile(gamertag));
            Assert.Single(StartUp.Profiles);
        }

        [Fact]
        public void Two_profiles_added_get_ids_of_their_own()
        {
            var ana = core.AddProfile("Ana");
            var bo = core.AddProfile("Bo");

            Assert.Equal(3, StartUp.Profiles.Select(p => p.Id).Distinct().Count());
            Assert.NotEqual(ana.Id, bo.Id);
        }

        [Fact]
        public void A_gamertag_is_changed_trimmed_and_an_empty_one_refused()
        {
            var id = StartUp.Profiles[0].Id;

            core.SetProfileGamertag(id, "  Master Chief ");
            Assert.Equal("Master Chief", StartUp.Profiles[0].Gamertag);

            core.SetProfileGamertag(id, "  ");
            Assert.Equal("Master Chief", StartUp.Profiles[0].Gamertag);
            Assert.Equal(id, StartUp.Profiles[0].Id);

            NewCore();
            Assert.Equal("Master Chief", StartUp.Profiles[0].Gamertag);
        }

        [Fact]
        public void A_real_name_is_changed_trimmed_and_may_be_emptied()
        {
            var id = StartUp.Profiles[0].Id;

            core.SetProfileRealName(id, " John Spartan ");
            Assert.Equal("John Spartan", StartUp.Profiles[0].RealName);

            core.SetProfileRealName(id, " ");
            Assert.Equal("", StartUp.Profiles[0].RealName);
            Assert.Equal("gabe", StartUp.Profiles[0].FirstName);

            NewCore();
            Assert.Equal("", StartUp.Profiles[0].RealName);
        }

        [Fact]
        public void A_picture_is_one_of_the_gamerpics_and_is_kept()
        {
            var id = StartUp.Profiles[0].Id;

            core.SetProfilePicture(id, @"C:\Gamerpics\b.png");
            Assert.Equal(@"C:\Gamerpics\b.png", StartUp.Profiles[0].Picture);
            Assert.Equal(StartUp.Profiles, core.Startup.Profiles);

            NewCore();
            Assert.Equal(@"C:\Gamerpics\b.png", StartUp.Profiles[0].Picture);
        }

        [Fact]
        public void A_picture_that_isnt_a_gamerpic_is_refused()
        {
            var id = StartUp.Profiles[0].Id;

            core.SetProfilePicture(id, @"C:\Elsewhere\x.png");

            Assert.Equal(@"C:\pics\gabe.png", StartUp.Profiles[0].Picture);
        }

        [Fact]
        public void A_gamerpic_added_to_the_folder_since_the_start_can_be_worn()
        {
            account.Gamerpics = new[] { @"C:\Gamerpics\a.png", @"C:\Gamerpics\b.png", @"C:\Gamerpics\c.png" };
            core.OpenSettings();

            Assert.Contains(@"C:\Gamerpics\c.png", StartUp.Gamerpics);
            core.SetProfilePicture(StartUp.Profiles[0].Id, @"C:\Gamerpics\c.png");
            Assert.Equal(@"C:\Gamerpics\c.png", StartUp.Profiles[0].Picture);
        }

        [Fact]
        public void Changes_to_an_unknown_profile_are_ignored()
        {
            core.SetProfileGamertag("nobody", "Ana");
            core.SetProfileRealName("nobody", "Ana");
            core.SetProfilePicture("nobody", @"C:\Gamerpics\a.png");
            core.RemoveProfile("nobody");

            Assert.Equal("gabe", Assert.Single(StartUp.Profiles).Gamertag);
            Assert.Equal(0, secrets.Writes);
        }

        [Fact]
        public void A_profile_removed_is_forgotten()
        {
            var ana = core.AddProfile("Ana");
            core.AddProfile("Bo");

            core.RemoveProfile(ana.Id);

            Assert.Equal(new[] { "gabe", "Bo" }, StartUp.Profiles.Select(p => p.Gamertag));
            Assert.Equal(StartUp.Profiles, core.Startup.Profiles);
            NewCore();
            Assert.Equal(new[] { "gabe", "Bo" }, StartUp.Profiles.Select(p => p.Gamertag));
        }

        [Fact]
        public void The_last_profile_is_never_removed()
        {
            core.RemoveProfile(StartUp.Profiles[0].Id);

            Assert.Single(StartUp.Profiles);
        }

        [Fact]
        public void A_store_that_cant_be_read_falls_back_to_the_defaults()
        {
            secrets.Values["StartUp"] = "{not json";
            NewCore();

            Assert.Equal(BootVideo.XboxSeriesX, StartUp.BootVideo);
            Assert.Equal("gabe", Assert.Single(StartUp.Profiles).Gamertag);
        }
    }
}
