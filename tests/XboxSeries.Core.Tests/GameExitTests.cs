using System;
using System.Collections.Generic;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// A game launched from the Dashboard exiting, however it was quit: the user lands back on Home, its Recent row
    /// taking the pad again on the game just played, as the console does.
    /// </summary>
    public class GameExitTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;
        private readonly FakeLibrary library;
        private readonly List<Guid> returns = new List<Guid>();

        public GameExitTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore());
            library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());
            core.Home.ReturnedFromGame += returns.Add;
        }

        [Fact]
        public void A_game_exiting_hands_Home_the_pad_again_on_the_game_just_played()
        {
            Play("Hades");

            core.OnGameStopped(library.Id("Hades"));

            Assert.Equal(new[] { library.Id("Hades") }, returns);
            Assert.Equal(DashboardPage.Home, playnite.Page);
            Assert.Equal(playnite.Window, windows.Foreground);
        }

        [Fact]
        public void A_game_launched_from_My_games_and_apps_exits_to_Home()
        {
            core.OpenMyGames(MyGamesItem.Games);
            Play("Hades");

            core.OnGameStopped(library.Id("Hades"));

            Assert.False(core.MyGames.IsShown);
            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.Equal(new[] { library.Id("Hades") }, returns);
        }

        [Fact]
        public void A_game_launched_from_Settings_exits_to_Home()
        {
            core.OpenSettings();
            Play("Hades");

            core.OnGameStopped(library.Id("Hades"));

            Assert.False(core.Settings.IsShown);
        }

        [Fact]
        public void A_game_exiting_under_the_open_Guide_leaves_the_foreground_with_the_Guide()
        {
            Play("Hades");
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(core.Guide.IsOpen);

            core.OnGameStopped(library.Id("Hades"));

            Assert.Equal(FakeWindowSystem.Overlay, windows.Foreground);
            Assert.Equal(new[] { library.Id("Hades") }, returns);
        }

        [Fact]
        public void A_game_that_was_never_seen_starting_changes_nothing_on_stopping()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.OnGameStopped(library.Id("Hades"));

            Assert.True(core.MyGames.IsShown);
            Assert.Empty(returns);
        }

        private void Play(string name)
        {
            var id = library.Id(name);
            Assert.True(core.OnGameStarting(id));
            core.OnGameStarted(id);
        }
    }
}
