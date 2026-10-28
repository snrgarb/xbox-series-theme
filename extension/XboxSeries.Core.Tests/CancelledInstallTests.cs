using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// An install Playnite started for a Steam game that Steam drops: Playnite's own Steam install watches for the game
    /// installed and nothing else, so it would read installing for ever. The queue follows Steam instead.
    /// </summary>
    public class CancelledInstallTests
    {
        private const long GB = 1L << 30;

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 17, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly ExtensionCore core;

        public CancelledInstallTests()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
        }

        [Fact]
        public void An_install_Steam_never_starts_on_is_marked_cancelled_after_a_minute()
        {
            // Steam's install dialog was cancelled, or left: Steam writes nothing for the game.
            var hades = Guid.NewGuid();
            PlayniteInstalling(hades);

            clock.Advance(TimeSpan.FromSeconds(59));
            Assert.Empty(downloads.MarkedCancelled);
            Assert.Single(core.MyGames.Queue);

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(new[] { hades }, downloads.MarkedCancelled);

            // Playnite's game no longer installing leaves the queue.
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", source: "Steam", installed: false).Snapshot());
            Assert.Empty(core.MyGames.Queue);
        }

        [Fact]
        public void An_install_cancelled_in_Steam_mid_download_is_marked_cancelled_at_once()
        {
            var hades = Guid.NewGuid();
            PlayniteInstalling(hades);
            downloads.ClientInstalls.Add(hades);
            downloads.With(hades, isActive: true, 1 * GB, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Empty(downloads.MarkedCancelled);

            downloads.ClientInstalls.Remove(hades);
            downloads.With(hades, isActive: false, 0, 0);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { hades }, downloads.MarkedCancelled);
        }

        [Fact]
        public void An_install_Steam_is_downloading_is_never_marked_cancelled()
        {
            var hades = Guid.NewGuid();
            PlayniteInstalling(hades);
            downloads.ClientInstalls.Add(hades);
            downloads.With(hades, isActive: true, 1 * GB, 4 * GB);

            clock.Advance(TimeSpan.FromMinutes(5));

            Assert.Empty(downloads.MarkedCancelled);
            Assert.Equal("Installing...", core.MyGames.CurrentDownload.State);
        }

        [Fact]
        public void An_install_from_a_library_that_cant_say_is_left_to_Playnite()
        {
            var forza = Guid.NewGuid();
            core.OnLibraryChanged(new FakeLibrary().WithGame(forza, "Forza Horizon 5", source: "Xbox", installed: false, installing: true).Snapshot());

            clock.Advance(TimeSpan.FromMinutes(5));

            Assert.Empty(downloads.MarkedCancelled);
            Assert.Single(core.MyGames.Queue);
        }

        [Fact]
        public void An_install_is_marked_cancelled_once_only()
        {
            var hades = Guid.NewGuid();
            PlayniteInstalling(hades);

            // Playnite keeps it installing a while longer, as its next snapshot hasn't come.
            clock.Advance(TimeSpan.FromMinutes(2));

            Assert.Equal(new[] { hades }, downloads.MarkedCancelled);
        }

        /// <summary>A Steam game Playnite has started installing, which Steam has written nothing for yet.</summary>
        private void PlayniteInstalling(Guid id)
        {
            downloads.With(id, isActive: false, 0, 0);
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", source: "Steam", installed: false, installing: true).Snapshot());
        }
    }
}
