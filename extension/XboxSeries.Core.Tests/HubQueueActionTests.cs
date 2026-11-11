using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// The Game hub's Queue action (#208): shown while the game is in the Queue, Installing or Queued; A opens Manage
    /// Queue, and B there goes back to the hub, focus on Queue, rather than to Manage.
    /// </summary>
    public class HubQueueActionTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 11, 11, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public HubQueueActionTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
        }

        [Fact]
        public void Queue_shows_on_the_hub_of_a_game_Installing_or_Queued_and_on_no_other()
        {
            var library = new FakeLibrary()
                .WithGame("Hades", source: "Steam", installed: false)
                .WithGame("33 Immortals", installed: false, installing: true)
                .WithGame("Halo Infinite", installed: true)
                .WithGame("Celeste", source: "Steam", installed: false);
            downloads.Installable.Add(library.Id("Hades"));
            downloads.Installable.Add(library.Id("Celeste"));
            core.OnLibraryChanged(library.Snapshot());
            core.InstallGame(library.Id("Hades"));

            Assert.Equal("Queued", core.MyGames.Queue.Single(e => e.Tile.Title == "Hades").State);
            Assert.Equal("Installing...", core.MyGames.Queue.Single(e => e.Tile.Title == "33 Immortals").State);
            Assert.True(core.HubShowsQueue(library.Id("Hades")));
            Assert.True(core.HubShowsQueue(library.Id("33 Immortals")));
            Assert.False(core.HubShowsQueue(library.Id("Halo Infinite")));
            Assert.False(core.HubShowsQueue(library.Id("Celeste")));
            Assert.False(core.HubShowsQueue(Guid.NewGuid()));
        }

        [Fact]
        public void Queue_leaves_the_hub_as_the_game_leaves_the_Queue_with_the_change_told()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
            var id = library.Id("33 Immortals");
            Assert.True(core.HubShowsQueue(id));
            var told = new List<string>();
            core.MyGames.PropertyChanged += (s, e) => told.Add(e.PropertyName);

            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "33 Immortals", installed: true).Snapshot());

            Assert.False(core.HubShowsQueue(id));
            Assert.Contains(nameof(MyGamesState.Queue), told);
        }

        [Fact]
        public void A_queued_install_Steam_gives_up_on_leaves_the_hub_too()
        {
            var library = new FakeLibrary().WithGame("Hades", source: "Steam", installed: false);
            downloads.Installable.Add(library.Id("Hades"));
            core.OnLibraryChanged(library.Snapshot());
            core.InstallGame(library.Id("Hades"));
            Assert.True(core.HubShowsQueue(library.Id("Hades")));

            clock.Advance(TimeSpan.FromSeconds(60));

            Assert.False(core.HubShowsQueue(library.Id("Hades")));
        }

        [Fact]
        public void A_on_Queue_closes_the_hub_and_opens_Manage_Queue()
        {
            var id = InstallingGame();

            core.OpenQueueFromHub(id);

            Assert.Equal(new[] { id }, playnite.ClosedGameHubs);
            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.ManageQueue, core.MyGames.Item);
        }

        [Fact]
        public void A_on_Queue_for_a_game_no_longer_in_the_Queue_does_nothing()
        {
            var library = new FakeLibrary().WithGame("Celeste", installed: false);
            core.OnLibraryChanged(library.Snapshot());

            core.OpenQueueFromHub(library.Id("Celeste"));

            Assert.Empty(playnite.ClosedGameHubs);
            Assert.False(core.MyGames.IsShown);
        }

        [Fact]
        public void B_on_Manage_Queue_opened_from_the_hub_goes_back_to_the_hub_focus_on_Queue()
        {
            var id = InstallingGame();
            core.OpenQueueFromHub(id);

            core.LeaveMyGames();

            Assert.Equal(new[] { id }, playnite.OpenedHubs);
            Assert.False(core.MyGames.IsShown);
            Assert.True(core.HubReturnsToQueue(id));
            Assert.False(core.HubReturnsToQueue(Guid.NewGuid()));
            Assert.True(core.TakeHubQueueFocus(id));
            Assert.False(core.TakeHubQueueFocus(id));
            Assert.False(core.HubReturnsToQueue(id));
        }

        [Fact]
        public void From_a_hub_over_My_games_and_apps_B_puts_the_page_back_as_it_was_under_the_hub()
        {
            var id = InstallingGame();
            core.OpenMyGames(MyGamesItem.Games);
            var filter = playnite.MainListFilter;
            core.OpenQueueFromHub(id);

            core.LeaveMyGames();

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);
            Assert.Same(filter, playnite.MainListFilter);
            Assert.Equal(new[] { id }, playnite.OpenedHubs);
        }

        [Fact]
        public void From_a_hub_over_Search_B_goes_back_to_the_hub_with_the_game_in_the_main_list()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true, added: new DateTime(2026, 11, 1));
            core.OnLibraryChanged(library.Snapshot());
            var id = library.Id("33 Immortals");
            core.OpenSearch();
            core.ChooseSearchGame(id);
            core.OpenQueueFromHub(id);

            core.LeaveMyGames();

            // The user's own filter, given back as Search went, could leave the game out; the hub needs it in the list.
            Assert.False(playnite.ShowsUsersOwnFilter);
            Assert.Contains("33 Immortals", playnite.MainListFilter.Apply(library.Snapshot().Games).Select(g => g.Name));
            Assert.Equal(new[] { id, id }, playnite.OpenedHubs);
            Assert.True(core.TakeHubQueueFocus(id));
        }

        [Fact]
        public void Manage_Queue_opened_another_way_after_goes_back_to_Manage_as_before()
        {
            var id = InstallingGame();
            core.OpenQueueFromHub(id);
            core.OpenMyGames(MyGamesItem.Manage);
            core.OpenMyGames(MyGamesItem.ManageQueue);

            core.LeaveMyGames();

            Assert.Equal(MyGamesItem.Manage, core.MyGames.Item);
            Assert.Empty(playnite.OpenedHubs);
            Assert.False(core.HubReturnsToQueue(id));
        }

        /// <summary>A game Playnite is installing, in the Queue; its id.</summary>
        private Guid InstallingGame()
        {
            var library = new FakeLibrary().WithGame("33 Immortals", installed: false, installing: true);
            core.OnLibraryChanged(library.Snapshot());
            return library.Id("33 Immortals");
        }
    }
}
