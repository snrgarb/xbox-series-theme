using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Update after downloads: a game finishing installing is followed by a library update and metadata for that game and
    /// those the update added, ending with the Library updated Toast; and Settings' Download metadata box.
    /// </summary>
    public class LibraryUpdateTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 20, 21, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private readonly FakePlayniteHost playnite;
        private ExtensionCore core;
        private readonly Guid hades = Guid.NewGuid();
        private readonly Guid celeste = Guid.NewGuid();
        private readonly Guid added = Guid.NewGuid();

        public LibraryUpdateTests()
        {
            playnite = new FakePlayniteHost(windows);
            NewCore();
        }

        [Fact]
        public void A_game_Playnite_installs_is_followed_by_a_library_update_once_things_are_quiet()
        {
            InstallThroughPlaynite(hades);

            Assert.Empty(playnite.LibraryUpdates);
            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Single(playnite.LibraryUpdates);
        }

        [Fact]
        public void Once_the_library_is_updated_metadata_is_downloaded_for_the_game_that_finished_and_those_the_update_added()
        {
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(10));

            playnite.LibraryUpdates.Single()(new[] { added });

            Assert.Equal(new[] { hades, added }, playnite.MetadataDownloads.Single().Games);
        }

        [Fact]
        public void Once_the_metadata_is_downloaded_a_Library_updated_Toast_says_how_many_games_were_added()
        {
            RunUpdateAfter(hades, new[] { added, Guid.NewGuid() });

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.LibraryUpdated, core.Toast.Kind);
            Assert.Equal("Library updated", core.Toast.Text);
            Assert.Null(core.Toast.Image);
            Assert.Equal("2 games added", core.Toast.Detail);
        }

        [Fact]
        public void The_Library_updated_Toast_says_1_game_added_for_one()
        {
            RunUpdateAfter(hades, new[] { added });

            Assert.Equal("1 game added", core.Toast.Detail);
        }

        [Fact]
        public void The_Library_updated_Toast_has_no_second_line_when_the_update_added_none()
        {
            RunUpdateAfter(hades, new Guid[0]);

            Assert.Equal(ToastKind.LibraryUpdated, core.Toast.Kind);
            Assert.Null(core.Toast.Detail);
        }

        [Fact]
        public void A_library_update_cancelled_or_failed_downloads_no_metadata_brings_no_Toast_and_isn_t_tried_again()
        {
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(10));

            playnite.LibraryUpdates.Single()(null);

            AssertNoLibraryUpdatedToastFor(TimeSpan.FromMinutes(5));
            Assert.Empty(playnite.MetadataDownloads);
            Assert.Single(playnite.LibraryUpdates);
        }

        [Fact]
        public void A_metadata_download_cancelled_or_failed_after_the_update_brings_no_Toast()
        {
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(10));
            playnite.LibraryUpdates.Single()(new[] { added });

            playnite.MetadataDownloads.Single().Done(false);

            AssertNoLibraryUpdatedToastFor(TimeSpan.FromMinutes(5));
            Assert.Single(playnite.LibraryUpdates);
        }

        [Fact]
        public void A_game_Steam_installs_alone_is_followed_by_a_library_update_and_its_metadata()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: false).Snapshot());
            downloads.ClientInstalls.Add(hades);
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: false).Snapshot());
            downloads.ClientInstalls.Remove(hades);
            clock.Advance(TimeSpan.FromSeconds(1));

            clock.Advance(TimeSpan.FromSeconds(10));
            playnite.LibraryUpdates.Single()(new Guid[0]);

            Assert.Equal(new[] { hades }, playnite.MetadataDownloads.Single().Games);
        }

        [Fact]
        public void Several_games_finishing_close_together_give_one_update_once_the_last_has_been_quiet_a_while()
        {
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(6));
            InstallThroughPlaynite(celeste);
            clock.Advance(TimeSpan.FromSeconds(6));

            Assert.Empty(playnite.LibraryUpdates);
            clock.Advance(TimeSpan.FromSeconds(4));
            playnite.LibraryUpdates.Single()(new Guid[0]);

            Assert.Equal(new[] { hades, celeste }, playnite.MetadataDownloads.Single().Games);
        }

        [Fact]
        public void A_game_finishing_while_the_update_runs_waits_for_the_next_update()
        {
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(10));
            InstallThroughPlaynite(celeste);
            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.Single(playnite.LibraryUpdates);

            playnite.LibraryUpdates[0](new Guid[0]);
            playnite.MetadataDownloads[0].Done(true);
            Assert.Single(playnite.LibraryUpdates);
            clock.Advance(TimeSpan.FromSeconds(10));
            playnite.LibraryUpdates[1](new Guid[0]);

            Assert.Equal(new[] { hades }, playnite.MetadataDownloads[0].Games);
            Assert.Equal(new[] { celeste }, playnite.MetadataDownloads[1].Games);
        }

        [Fact]
        public void The_Library_updated_Toast_waits_behind_a_Toast_already_showing()
        {
            core.OnNotificationsChanged(new Notification[0]);
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(20));
            playnite.LibraryUpdates.Single()(new Guid[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Steam is updating") });

            playnite.MetadataDownloads.Single().Done(true);
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            clock.Advance(TimeSpan.FromSeconds(5.5));

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.LibraryUpdated, core.Toast.Kind);
        }

        [Fact]
        public void The_update_holds_off_while_a_game_is_played_and_runs_once_it_stops()
        {
            var played = Guid.NewGuid();
            core.OnGameStarting(played);
            core.OnGameStarted(played);
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromMinutes(30));
            Assert.Empty(playnite.LibraryUpdates);

            core.OnGameStopped(played);
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Single(playnite.LibraryUpdates);
        }

        [Fact]
        public void A_game_started_during_the_library_update_holds_the_metadata_and_Toast_until_its_Play_session_ends()
        {
            var played = Guid.NewGuid();
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(10));
            core.OnGameStarting(played);

            playnite.LibraryUpdates.Single()(new[] { added });
            Assert.Empty(playnite.MetadataDownloads);
            core.OnGameStarted(played);
            clock.Advance(TimeSpan.FromMinutes(30));
            Assert.Empty(playnite.MetadataDownloads);

            core.OnGameStopped(played);
            clock.Advance(TimeSpan.FromSeconds(5));
            playnite.MetadataDownloads.Single().Done(true);

            Assert.Equal(new[] { hades, added }, playnite.MetadataDownloads.Single().Games);
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.LibraryUpdated, core.Toast.Kind);
            Assert.Equal("1 game added", core.Toast.Detail);
        }

        [Fact]
        public void The_update_holds_off_while_a_game_is_launching()
        {
            var played = Guid.NewGuid();
            core.OnGameStarting(played);
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.Empty(playnite.LibraryUpdates);

            core.OnGameStartupCancelled(played);
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Single(playnite.LibraryUpdates);
        }

        [Fact]
        public void The_update_waits_for_a_library_task_of_Playnite_s_own_to_finish_first()
        {
            playnite.IsLibraryBusy = true;
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Empty(playnite.LibraryUpdates);

            playnite.IsLibraryBusy = false;
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Single(playnite.LibraryUpdates);
        }

        [Fact]
        public void Update_after_downloads_is_on_after_a_fresh_install()
        {
            Assert.True(core.Settings.Playnite.UpdateAfterDownloads);
        }

        [Fact]
        public void With_Update_after_downloads_off_a_game_finishing_brings_no_update()
        {
            core.SetUpdateAfterDownloads(false);

            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.False(core.Settings.Playnite.UpdateAfterDownloads);
            Assert.Empty(playnite.LibraryUpdates);
        }

        [Fact]
        public void Turning_Update_after_downloads_off_while_an_update_waits_drops_it()
        {
            InstallThroughPlaynite(hades);
            core.SetUpdateAfterDownloads(false);
            core.SetUpdateAfterDownloads(true);
            clock.Advance(TimeSpan.FromMinutes(1));

            Assert.Empty(playnite.LibraryUpdates);
        }

        [Fact]
        public void Update_after_downloads_is_saved_at_once_and_read_back_by_a_new_core()
        {
            core.SetUpdateAfterDownloads(false);

            NewCore();

            Assert.False(core.Settings.Playnite.UpdateAfterDownloads);
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Empty(playnite.LibraryUpdates);
        }

        [Fact]
        public void Update_after_downloads_turned_back_on_is_saved_too()
        {
            core.SetUpdateAfterDownloads(false);
            core.SetUpdateAfterDownloads(true);

            NewCore();

            Assert.True(core.Settings.Playnite.UpdateAfterDownloads);
        }

        [Fact]
        public void Download_metadata_asks_Playnite_for_the_games_missing_it_and_reads_downloading_until_done_with_no_Toast()
        {
            core.DownloadMissingMetadata();

            Assert.Single(playnite.MissingMetadataDownloads);
            Assert.True(core.Settings.Playnite.IsDownloadingMetadata);

            playnite.MissingMetadataDownloads[0](true);

            Assert.False(core.Settings.Playnite.IsDownloadingMetadata);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Download_metadata_pressed_again_while_it_runs_starts_no_second_run()
        {
            core.DownloadMissingMetadata();
            core.DownloadMissingMetadata();

            Assert.Single(playnite.MissingMetadataDownloads);
        }

        [Fact]
        public void Download_metadata_does_nothing_while_Playnite_runs_a_library_task()
        {
            playnite.IsLibraryBusy = true;

            core.DownloadMissingMetadata();

            Assert.Empty(playnite.MissingMetadataDownloads);
            Assert.False(core.Settings.Playnite.IsDownloadingMetadata);
        }

        [Fact]
        public void Download_metadata_does_nothing_while_the_update_after_downloads_runs()
        {
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(10));

            core.DownloadMissingMetadata();

            Assert.Empty(playnite.MissingMetadataDownloads);
        }

        [Fact]
        public void The_update_after_downloads_waits_for_the_Download_metadata_run_to_finish()
        {
            core.DownloadMissingMetadata();
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Empty(playnite.LibraryUpdates);

            playnite.MissingMetadataDownloads[0](true);
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Single(playnite.LibraryUpdates);
        }

        [Fact]
        public void Cancel_stops_the_Download_metadata_run_which_reads_downloading_until_Playnite_says_it_has_stopped()
        {
            core.DownloadMissingMetadata();

            core.CancelMetadataDownload();

            Assert.Equal(1, playnite.LibraryTaskCancels);
            Assert.True(core.Settings.Playnite.IsDownloadingMetadata);
            playnite.MissingMetadataDownloads[0](true);
            Assert.False(core.Settings.Playnite.IsDownloadingMetadata);
        }

        [Fact]
        public void Cancel_with_no_Download_metadata_run_stops_nothing()
        {
            InstallThroughPlaynite(hades);
            clock.Advance(TimeSpan.FromSeconds(10));

            core.CancelMetadataDownload();

            Assert.Equal(0, playnite.LibraryTaskCancels);
        }

        [Fact]
        public void The_Guide_button_tapped_during_the_Library_updated_Toast_opens_the_Guide()
        {
            RunUpdateAfter(hades, new[] { added });

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));

            Assert.True(core.Guide.IsOpen);
            Assert.Empty(playnite.StartedGames);
        }

        private void NewCore()
        {
            core = new ExtensionCore(
                clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(),
                new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, downloads);
        }

        /// <summary>The game finishes installing through Playnite, and the update that follows adds <paramref name="gamesAdded"/>.</summary>
        private void RunUpdateAfter(Guid game, IReadOnlyList<Guid> gamesAdded)
        {
            InstallThroughPlaynite(game);
            clock.Advance(TimeSpan.FromSeconds(10));
            clock.Advance(TimeSpan.FromSeconds(10));
            playnite.LibraryUpdates.Single()(gamesAdded);
            playnite.MetadataDownloads.Single().Done(true);
        }

        /// <summary>Time passes a second at a time, the Library updated Toast showing at none of them.</summary>
        private void AssertNoLibraryUpdatedToastFor(TimeSpan time)
        {
            for (var passed = TimeSpan.Zero; passed <= time; passed += TimeSpan.FromSeconds(1))
            {
                Assert.False(core.Toast.IsShown && core.Toast.Kind == ToastKind.LibraryUpdated);
                clock.Advance(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>The game joins the queue as Playnite installs it, and finishes installing; no time passes after.</summary>
        private void InstallThroughPlaynite(Guid game)
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame(game, "Game", installed: false).Snapshot());
            core.OnLibraryChanged(new FakeLibrary().WithGame(game, "Game", installed: false, installing: true).Snapshot());
            core.OnLibraryChanged(new FakeLibrary().WithGame(game, "Game", installed: true).Snapshot());
        }
    }
}
