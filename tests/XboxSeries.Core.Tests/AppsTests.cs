using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Apps on My games &amp; apps: Playnite's Software tools as tiles, which A starts.</summary>
    public class AppsTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public AppsTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore());
        }

        [Fact]
        public void Apps_lists_Playnites_Software_tools_in_Playnites_order()
        {
            core.OnLibraryChanged(new FakeLibrary()
                .WithApp("Steam", @"C:\Steam\steam.exe", "-bigpicture", @"C:\Icons\steam.png")
                .WithApp("Discord", @"C:\Discord\Update.exe")
                .Snapshot());

            Assert.True(core.MyGames.HasApps);
            Assert.Equal(new[] { "Steam", "Discord" }, core.MyGames.Apps.Select(a => a.Name));
            Assert.Equal(@"C:\Icons\steam.png", core.MyGames.Apps[0].Icon);
        }

        [Fact]
        public void Apps_shows_its_own_tiles_without_the_grid()
        {
            core.OnLibraryChanged(new FakeLibrary().WithApp("Steam", @"C:\Steam\steam.exe").Snapshot());

            core.OpenMyGames(MyGamesItem.Apps);

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.Apps, core.MyGames.Item);
            Assert.False(core.MyGames.ShowsGameGrid);
            Assert.False(core.MyGames.ShowsFullLibrary);
        }

        [Fact]
        public void Apps_leaves_the_main_list_as_it_is()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite")
                .WithGame("Not Installed", installed: false)
                .WithApp("Steam", @"C:\Steam\steam.exe");
            core.OnLibraryChanged(library.Snapshot());
            core.OpenMyGames(MyGamesItem.Games);

            core.OpenMyGames(MyGamesItem.Apps);
            core.OpenMyGames(MyGamesItem.Games);

            Assert.Equal(new[] { "Halo Infinite" }, MainList(library));
            core.LeaveMyGames();
            Assert.True(playnite.ShowsUsersOwnFilter);
        }

        [Fact]
        public void Apps_opened_first_leaves_the_users_filter_alone()
        {
            core.OnLibraryChanged(new FakeLibrary().WithApp("Steam", @"C:\Steam\steam.exe").Snapshot());

            core.OpenMyGames(MyGamesItem.Apps);
            core.LeaveMyGames();

            Assert.False(core.MyGames.IsShown);
            Assert.Equal(0, playnite.UserFilterRestores);
        }

        [Fact]
        public void A_on_an_App_starts_the_tool_with_its_arguments_without_the_Launch_screen()
        {
            var snapshot = new FakeLibrary()
                .WithApp("Steam", @"C:\Steam\steam.exe", "-bigpicture")
                .WithApp("Discord", @"C:\Discord\Update.exe")
                .Snapshot();
            core.OnLibraryChanged(snapshot);
            core.OpenMyGames(MyGamesItem.Apps);

            core.StartApp(snapshot.Apps[0].Id);

            var started = Assert.Single(playnite.StartedApps);
            Assert.Equal(@"C:\Steam\steam.exe", started.Path);
            Assert.Equal("-bigpicture", started.Arguments);
            Assert.Empty(playnite.StartedGames);
            Assert.False(core.LaunchScreen.IsShown);
        }

        [Fact]
        public void An_App_starts_while_a_game_runs()
        {
            var library = new FakeLibrary().WithGame("Halo Infinite").WithApp("Steam", @"C:\Steam\steam.exe");
            var snapshot = library.Snapshot();
            core.OnLibraryChanged(snapshot);
            core.OnGameStarted(library.Id("Halo Infinite"));
            core.OpenMyGames(MyGamesItem.Apps);

            core.StartApp(snapshot.Apps[0].Id);

            Assert.Single(playnite.StartedApps);
            Assert.Null(core.Home.TileMenu);
        }

        [Fact]
        public void An_unknown_App_starts_nothing()
        {
            core.OnLibraryChanged(new FakeLibrary().WithApp("Steam", @"C:\Steam\steam.exe").Snapshot());
            core.OpenMyGames(MyGamesItem.Apps);

            core.StartApp(Guid.NewGuid());

            Assert.Empty(playnite.StartedApps);
        }

        [Fact]
        public void A_reaching_the_page_under_the_Guide_starts_nothing()
        {
            var snapshot = new FakeLibrary().WithApp("Steam", @"C:\Steam\steam.exe").Snapshot();
            core.OnLibraryChanged(snapshot);
            core.OpenMyGames(MyGamesItem.Apps);
            Tap(ControllerButton.Guide);

            core.StartApp(snapshot.Apps[0].Id);

            Assert.Empty(playnite.StartedApps);
        }

        [Fact]
        public void An_App_starts_only_from_Apps()
        {
            var snapshot = new FakeLibrary().WithApp("Steam", @"C:\Steam\steam.exe").Snapshot();
            core.OnLibraryChanged(snapshot);
            core.OpenMyGames(MyGamesItem.Games);

            core.StartApp(snapshot.Apps[0].Id);
            core.LeaveMyGames();
            core.StartApp(snapshot.Apps[0].Id);

            Assert.Empty(playnite.StartedApps);
        }

        [Fact]
        public void When_Software_tools_cant_be_read_Apps_is_hidden_and_the_page_still_works()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 13))
                .WithGame("Not Installed", installed: false)
                .WithoutApps();
            core.OnLibraryChanged(library.Snapshot());

            Assert.False(core.MyGames.HasApps);
            Assert.Empty(core.MyGames.Apps);

            core.OpenMyGames(MyGamesItem.Games);
            Assert.Equal(new[] { "Halo Infinite" }, MainList(library));
            Assert.True(core.MyGames.ShowsGameGrid);

            core.OpenMyGames(MyGamesItem.Apps);
            Assert.Equal(MyGamesItem.Games, core.MyGames.Item);

            core.OpenMyGames(MyGamesItem.PlayHistory);
            Assert.Equal(MyGamesItem.PlayHistory, core.MyGames.Item);
            Assert.Single(core.MyGames.PlayHistory.SelectMany(s => s.Entries));

            core.OpenMyGames(MyGamesItem.FullLibrary);
            Assert.True(core.MyGames.ShowsFullLibrary);
            Assert.True(playnite.ShowsUsersOwnFilter);

            core.LeaveMyGames();
            Assert.False(core.MyGames.IsShown);
            Assert.Empty(playnite.StartedApps);
        }

        [Fact]
        public void Apps_opens_nothing_when_Software_tools_cant_be_read()
        {
            core.OnLibraryChanged(new FakeLibrary().WithoutApps().Snapshot());

            core.OpenMyGames(MyGamesItem.Apps);

            Assert.False(core.MyGames.IsShown);
        }

        /// <summary>The games Playnite's main list shows under the filter the core last applied.</summary>
        private string[] MainList(FakeLibrary library) =>
            playnite.MainListFilter.Apply(library.Snapshot().Games).Select(g => g.Name).ToArray();

        /// <summary>Pressed and let go. A Guide tap opens the Guide 0.3 s after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
