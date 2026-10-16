using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>The Game hub's Install for a Steam game: Steam installs it without its windows, and the queue follows it.</summary>
    public class SilentInstallTests
    {
        private const long GB = 1L << 30;

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 17, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public SilentInstallTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
        }

        [Fact]
        public void Only_a_game_Steam_can_install_that_isnt_installed_or_installing_installs_silently()
        {
            var library = new FakeLibrary()
                .WithGame("Hades", installed: false)
                .WithGame("Halo Infinite", installed: true)
                .WithGame("Forza Horizon 5", installed: false, installing: true)
                .WithGame("Not Steam", installed: false);
            foreach (var name in new[] { "Hades", "Halo Infinite", "Forza Horizon 5" })
            {
                downloads.Installable.Add(library.Id(name));
            }

            core.OnLibraryChanged(library.Snapshot());

            Assert.True(core.CanInstallSilently(library.Id("Hades")));
            Assert.False(core.CanInstallSilently(library.Id("Halo Infinite")));
            Assert.False(core.CanInstallSilently(library.Id("Forza Horizon 5")));
            Assert.False(core.CanInstallSilently(library.Id("Not Steam")));
            Assert.False(core.CanInstallSilently(Guid.NewGuid()));
        }

        [Fact]
        public void Install_asks_Steam_queues_the_game_at_once_and_goes_back_from_its_Game_hub()
        {
            var library = SteamGame("Hades");

            core.InstallGame(library.Id("Hades"));

            Assert.Equal(new[] { library.Id("Hades") }, downloads.InstallsAsked);
            var entry = core.MyGames.Queue.Single();
            Assert.Equal("Hades", entry.Tile.Title);
            Assert.Equal("Queued", entry.State);
            Assert.Equal("Queued", entry.Tile.Status);
            Assert.Equal("Queued", core.Home.RecentRow.Single(t => t.Title == "Hades").Status);
            Assert.Equal(new[] { library.Id("Hades") }, playnite.ClosedGameHubs);
            Assert.False(core.CanInstallSilently(library.Id("Hades")));
        }

        [Fact]
        public void Install_on_a_game_that_cant_install_silently_does_nothing()
        {
            var library = new FakeLibrary().WithGame("Forza Horizon 5", source: "Xbox", installed: false);
            core.OnLibraryChanged(library.Snapshot());

            core.InstallGame(library.Id("Forza Horizon 5"));

            Assert.Empty(downloads.InstallsAsked);
            Assert.Empty(core.MyGames.Queue);
            Assert.Empty(playnite.ClosedGameHubs);
        }

        [Fact]
        public void An_install_started_in_Steam_itself_joins_the_queue_with_the_next_library_snapshot()
        {
            var library = new FakeLibrary()
                .WithGame("Hades", source: "Steam", installed: false)
                .WithGame("Halo Infinite", source: "Steam", installed: true);
            downloads.ClientInstalls.AddRange(new[] { library.Id("Hades"), library.Id("Halo Infinite"), Guid.NewGuid() });
            downloads.With(library.Id("Hades"), isActive: true, 1 * GB, 4 * GB);

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(new[] { "Hades" }, QueueTitles());
            Assert.Equal("Installing...", core.MyGames.CurrentDownload.State);
        }

        [Fact]
        public void Games_Steam_reports_installing_join_the_queue_on_its_next_read_while_anything_is_queued()
        {
            var library = SteamGame("Hades", "Celeste");
            core.InstallGame(library.Id("Hades"));

            downloads.ClientInstalls.Add(library.Id("Celeste"));
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { "Hades", "Celeste" }, QueueTitles());
        }

        [Fact]
        public void An_install_Steam_never_reports_leaves_the_queue_after_a_minute()
        {
            var library = SteamGame("Hades");
            core.InstallGame(library.Id("Hades"));

            clock.Advance(TimeSpan.FromSeconds(59));
            Assert.Equal(new[] { "Hades" }, QueueTitles());

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Empty(core.MyGames.Queue);
            Assert.Null(core.Home.RecentRow.Single(t => t.Title == "Hades").Status);
            Assert.True(core.CanInstallSilently(library.Id("Hades")));
        }

        [Fact]
        public void An_install_Steam_has_written_nothing_for_leaves_the_queue_after_a_minute()
        {
            // Steam's adapter reports a game it has no manifest for as waiting, with nothing down of nothing.
            var library = SteamGame("Hades");
            downloads.With(library.Id("Hades"), isActive: false, 0, 0);
            core.InstallGame(library.Id("Hades"));

            clock.Advance(TimeSpan.FromSeconds(60));

            Assert.Empty(core.MyGames.Queue);
        }

        [Fact]
        public void An_install_Steam_reports_progress_for_stays_in_the_queue_past_a_minute()
        {
            var library = SteamGame("Hades");
            core.InstallGame(library.Id("Hades"));

            downloads.With(library.Id("Hades"), isActive: true, 1 * GB, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(90));

            Assert.Equal(new[] { "Hades" }, QueueTitles());
        }

        [Fact]
        public void A_game_Steam_stops_reporting_is_marked_installed_once_leaves_the_queue_and_heads_Recently_installed()
        {
            var hades = Guid.NewGuid();
            var library = new FakeLibrary()
                .WithGame(hades, "Hades", source: "Steam", installed: false)
                .WithGame("Halo Infinite", added: new DateTime(2026, 10, 1));
            downloads.Installable.Add(hades);
            core.OnLibraryChanged(library.Snapshot());
            core.InstallGame(hades);
            downloads.ClientInstalls.Add(hades);
            downloads.With(hades, isActive: true, 1 * GB, 4 * GB);
            clock.Advance(TimeSpan.FromSeconds(1));

            downloads.ClientInstalls.Clear();
            downloads.Without(hades);
            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(new[] { hades }, downloads.MarkedInstalled);
            Assert.Empty(core.MyGames.Queue);

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(hades, "Hades", source: "Steam", added: new DateTime(2020, 1, 1), installed: true)
                .WithGame("Halo Infinite", added: new DateTime(2026, 10, 1))
                .Snapshot());

            Assert.Equal(new[] { hades }, downloads.MarkedInstalled);
            Assert.Equal(new[] { "Hades", "Halo Infinite" }, core.MyGames.RecentlyInstalled.Select(t => t.Title));
        }

        [Fact]
        public void An_install_started_in_Steam_itself_is_marked_installed_when_it_finishes()
        {
            var library = new FakeLibrary().WithGame("Hades", source: "Steam", installed: false);
            downloads.ClientInstalls.Add(library.Id("Hades"));
            core.OnLibraryChanged(library.Snapshot());

            downloads.ClientInstalls.Clear();
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades") }, downloads.MarkedInstalled);
            Assert.Empty(core.MyGames.Queue);
        }

        [Fact]
        public void A_game_Playnite_is_installing_is_left_to_Playnite_to_mark_installed()
        {
            var library = new FakeLibrary().WithGame("Hades", source: "Steam", installed: false, installing: true);
            downloads.ClientInstalls.Add(library.Id("Hades"));
            core.OnLibraryChanged(library.Snapshot());

            downloads.ClientInstalls.Clear();
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Empty(downloads.MarkedInstalled);
            Assert.Equal(new[] { "Hades" }, QueueTitles());
        }

        [Fact]
        public void A_small_game_that_installs_between_two_reads_is_still_marked_installed_and_heads_Recently_installed()
        {
            var hades = Guid.NewGuid();
            var library = new FakeLibrary()
                .WithGame(hades, "Hades", source: "Steam", installed: false)
                .WithGame("Halo Infinite", added: new DateTime(2026, 10, 1));
            downloads.Installable.Add(hades);
            core.OnLibraryChanged(library.Snapshot());
            core.InstallGame(hades);

            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(new[] { hades, hades, hades }, downloads.MarkedInstalled);

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(hades, "Hades", source: "Steam", added: new DateTime(2020, 1, 1), installed: true)
                .WithGame("Halo Infinite", added: new DateTime(2026, 10, 1))
                .Snapshot());
            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Empty(core.MyGames.Queue);
            Assert.Equal(3, downloads.MarkedInstalled.Count);
            Assert.Equal(new[] { "Hades", "Halo Infinite" }, core.MyGames.RecentlyInstalled.Select(t => t.Title));
        }

        [Fact]
        public void The_snapshots_that_follow_an_Install_leave_the_Game_hub_closed_once()
        {
            var library = SteamGame("Hades");
            core.InstallGame(library.Id("Hades"));
            downloads.ClientInstalls.Add(library.Id("Hades"));

            core.OnLibraryChanged(library.Snapshot());
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { library.Id("Hades") }, playnite.ClosedGameHubs);
        }

        /// <summary>A library of not-installed Steam games, as the core has it.</summary>
        private FakeLibrary SteamGame(params string[] names)
        {
            var library = new FakeLibrary();
            foreach (var name in names)
            {
                library.WithGame(name, source: "Steam", installed: false);
                downloads.Installable.Add(library.Id(name));
            }

            core.OnLibraryChanged(library.Snapshot());
            return library;
        }

        private string[] QueueTitles() => core.MyGames.Queue.Select(e => e.Tile.Title).ToArray();
    }
}
