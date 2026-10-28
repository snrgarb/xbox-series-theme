using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Uninstalling a game through its client (Steam) in place of Playnite's own Uninstall: only a game the client can
    /// uninstall, followed until a snapshot shows it uninstalled, then told of with an Uninstalled Toast.
    /// </summary>
    public class UninstallTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 19, 21, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly ExtensionCore core;
        private readonly Guid hades = Guid.NewGuid();

        public UninstallTests()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
        }

        [Fact]
        public void A_game_uninstalled_through_its_client_shows_an_Uninstalled_Toast_with_its_cover_once_a_snapshot_shows_it_uninstalled()
        {
            Installed();
            downloads.Uninstallable.Add(hades);

            core.UninstallGame(hades);
            clock.Advance(TimeSpan.FromSeconds(3));
            Uninstalled();

            Assert.Equal(new[] { hades }, downloads.UninstallsAsked);
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Uninstalled, core.Toast.Kind);
            Assert.Equal("Hades was uninstalled", core.Toast.Text);
            Assert.Equal("hades.jpg", core.Toast.Image);
        }

        [Fact]
        public void A_game_being_uninstalled_is_marked_uninstalled_each_second()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.UninstallGame(hades);

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(new[] { hades }, downloads.MarkedUninstalled);

            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.Equal(new[] { hades, hades, hades }, downloads.MarkedUninstalled);
        }

        [Fact]
        public void No_Uninstalled_Toast_shows_before_a_snapshot_shows_the_game_uninstalled()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.UninstallGame(hades);

            clock.Advance(TimeSpan.FromSeconds(5));
            Installed();

            Assert.NotEmpty(downloads.MarkedUninstalled);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Marking_stops_once_a_snapshot_shows_the_game_uninstalled()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.UninstallGame(hades);
            clock.Advance(TimeSpan.FromSeconds(1));

            Uninstalled();
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(new[] { hades }, downloads.MarkedUninstalled);
        }

        [Fact]
        public void An_uninstall_still_unfinished_after_ten_minutes_is_warned_of_once_and_given_up_on_without_a_Toast()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.UninstallGame(hades);

            clock.Advance(TimeSpan.FromMinutes(9.9));
            Assert.Empty(downloads.UninstallsUnfinished);

            clock.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(new[] { hades }, downloads.UninstallsUnfinished);
            var marks = downloads.MarkedUninstalled.Count;
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(marks, downloads.MarkedUninstalled.Count);

            Uninstalled();
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_game_gone_from_the_library_mid_uninstall_is_no_longer_followed_and_shows_no_Toast()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.UninstallGame(hades);

            core.OnLibraryChanged(new FakeLibrary().Snapshot());
            clock.Advance(TimeSpan.FromSeconds(5));
            Uninstalled();
            clock.Advance(TimeSpan.FromMinutes(11));

            Assert.Empty(downloads.MarkedUninstalled);
            Assert.Empty(downloads.UninstallsUnfinished);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void An_installed_game_its_client_can_uninstall_can_be_uninstalled_silently()
        {
            Installed();
            downloads.Uninstallable.Add(hades);

            Assert.True(core.CanUninstallSilently(hades));
        }

        [Fact]
        public void A_game_its_client_cant_uninstall_keeps_Playnites_own_Uninstall()
        {
            Installed();

            Assert.False(core.CanUninstallSilently(hades));
        }

        [Fact]
        public void A_game_not_installed_keeps_Playnites_own_Uninstall()
        {
            Uninstalled();
            downloads.Uninstallable.Add(hades);

            Assert.False(core.CanUninstallSilently(hades));
        }

        [Fact]
        public void A_game_installing_keeps_Playnites_own_Uninstall()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: true, installing: true).Snapshot());
            downloads.Uninstallable.Add(hades);

            Assert.False(core.CanUninstallSilently(hades));
        }

        [Fact]
        public void A_game_not_in_the_library_keeps_Playnites_own_Uninstall()
        {
            core.OnLibraryChanged(new FakeLibrary().Snapshot());
            downloads.Uninstallable.Add(hades);

            Assert.False(core.CanUninstallSilently(hades));
        }

        [Fact]
        public void The_running_game_keeps_Playnites_own_Uninstall()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.OnGameStarted(hades);

            Assert.False(core.CanUninstallSilently(hades));
        }

        [Fact]
        public void A_game_already_being_uninstalled_stays_silent_and_is_asked_once()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.UninstallGame(hades);

            // Pressed again before Steam is done, the menu's Uninstall mustn't fall back on Steam's own dialog.
            Assert.True(core.CanUninstallSilently(hades));
            core.UninstallGame(hades);
            Assert.Equal(new[] { hades }, downloads.UninstallsAsked);
        }

        [Fact]
        public void Uninstalling_a_game_its_client_cant_uninstall_asks_nothing_and_follows_nothing()
        {
            Installed();

            core.UninstallGame(hades);
            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Empty(downloads.UninstallsAsked);
            Assert.Empty(downloads.MarkedUninstalled);
        }

        [Fact]
        public void An_Uninstalled_Toast_waits_behind_a_Toast_already_showing()
        {
            Installed();
            downloads.Uninstallable.Add(hades);
            core.OnNotificationsChanged(new Notification[0]);
            core.UninstallGame(hades);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            Uninstalled();

            Assert.Equal("Library updated", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal(ToastKind.Uninstalled, core.Toast.Kind);
            Assert.Equal("Hades was uninstalled", core.Toast.Text);
        }

        [Fact]
        public void A_game_Playnite_uninstalls_itself_shows_no_Uninstalled_Toast()
        {
            Installed();
            downloads.Uninstallable.Add(hades);

            Uninstalled();

            Assert.False(core.Toast.IsShown);
        }

        private void Installed() =>
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: true, coverImage: "hades.jpg").Snapshot());

        private void Uninstalled() =>
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: false, coverImage: "hades.jpg").Snapshot());
    }
}
