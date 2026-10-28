using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>The Ready to play Toast: a queued game finishing its install tells of it, and the Guide button starts it.</summary>
    public class ReadyToPlayToastTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 17, 21, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly Guid hades = Guid.NewGuid();

        public ReadyToPlayToastTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
        }

        [Fact]
        public void A_game_Playnite_installs_shows_a_Ready_to_play_Toast_with_its_cover_once_installed()
        {
            InstallThroughPlaynite();

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Installed, core.Toast.Kind);
            Assert.Equal("Ready to play", core.Toast.Text);
            Assert.Equal("hades.jpg", core.Toast.Image);
        }

        [Fact]
        public void A_game_Steam_installs_for_the_extension_shows_a_Ready_to_play_Toast_once_a_snapshot_shows_it_installed()
        {
            var library = new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: false, coverImage: "hades.jpg");
            downloads.Installable.Add(hades);
            core.OnLibraryChanged(library.Snapshot());
            core.InstallGame(hades);
            downloads.ClientInstalls.Add(hades);
            clock.Advance(TimeSpan.FromSeconds(10));

            downloads.ClientInstalls.Remove(hades);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Contains(hades, downloads.MarkedInstalled);
            Assert.NotEqual(ToastKind.Installed, core.Toast.Kind);

            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: true, coverImage: "hades.jpg").Snapshot());

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Installed, core.Toast.Kind);
        }

        [Fact]
        public void A_game_installed_without_being_queued_shows_no_Ready_to_play_Toast()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: false).Snapshot());

            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: true).Snapshot());

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void Games_already_installed_as_Playnite_starts_show_no_Ready_to_play_Toast()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: true).Snapshot());

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_game_shows_its_Ready_to_play_Toast_once()
        {
            InstallThroughPlaynite();
            clock.Advance(TimeSpan.FromSeconds(10));

            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: false, coverImage: "hades.jpg").Snapshot());
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: true, coverImage: "hades.jpg").Snapshot());

            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_Guide_button_tapped_during_a_Ready_to_play_Toast_starts_the_game_not_the_Guide()
        {
            InstallThroughPlaynite();

            TapGuide();

            Assert.False(core.Guide.IsOpen);
            Assert.Equal(new[] { hades }, playnite.StartedGames);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_Guide_button_tapped_after_a_Ready_to_play_Toast_has_gone_opens_the_Guide()
        {
            InstallThroughPlaynite();
            clock.Advance(TimeSpan.FromSeconds(10));

            TapGuide();

            Assert.True(core.Guide.IsOpen);
            Assert.Empty(playnite.StartedGames);
        }

        /// <summary>Hades joins the queue as Playnite installs it, its Queued Toast goes, and it finishes installing.</summary>
        private void InstallThroughPlaynite()
        {
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: false, coverImage: "hades.jpg").Snapshot());
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: false, installing: true, coverImage: "hades.jpg").Snapshot());
            clock.Advance(TimeSpan.FromSeconds(10));
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: true, coverImage: "hades.jpg").Snapshot());
        }

        private void TapGuide()
        {
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));
        }
    }
}
