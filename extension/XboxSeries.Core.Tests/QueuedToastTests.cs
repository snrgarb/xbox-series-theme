using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>The Queued Toast: each game joining the queue tells of it, and the Guide button goes to Manage Queue.</summary>
    public class QueuedToastTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 17, 21, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeDownloads downloads = new FakeDownloads();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public QueuedToastTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), downloads);
        }

        [Fact]
        public void A_game_Playnite_starts_installing_shows_a_Queued_Toast_with_its_cover()
        {
            var id = Guid.NewGuid();
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false, coverImage: "hades.jpg").Snapshot());

            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false, installing: true, coverImage: "hades.jpg").Snapshot());

            Assert.True(core.Toast.IsShown);
            Assert.Equal("Queued", core.Toast.Text);
            Assert.Equal(ToastKind.Queued, core.Toast.Kind);
            Assert.Equal("hades.jpg", core.Toast.Image);
        }
        [Fact]
        public void Games_already_installing_as_Playnite_starts_show_no_Queued_Toast()
        {
            var library = new FakeLibrary()
                .WithGame("Hades", installed: false, installing: true)
                .WithGame("Celeste", source: "Steam", installed: false);
            downloads.ClientInstalls.Add(library.Id("Celeste"));

            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(2, core.MyGames.QueueCount);
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_game_the_extension_installs_shows_a_Queued_Toast_without_a_cover_when_it_has_none()
        {
            var library = new FakeLibrary().WithGame("Hades", source: "Steam", installed: false);
            downloads.Installable.Add(library.Id("Hades"));
            core.OnLibraryChanged(library.Snapshot());

            core.InstallGame(library.Id("Hades"));

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Queued, core.Toast.Kind);
            Assert.Null(core.Toast.Image);
        }

        [Fact]
        public void An_install_Steam_reports_shows_a_Queued_Toast()
        {
            var library = new FakeLibrary().WithGame("Hades", source: "Steam", installed: false, coverImage: "hades.jpg");
            core.OnLibraryChanged(library.Snapshot());

            downloads.ClientInstalls.Add(library.Id("Hades"));
            core.OnLibraryChanged(library.Snapshot());

            Assert.Equal(ToastKind.Queued, core.Toast.Kind);
            Assert.Equal("hades.jpg", core.Toast.Image);
        }

        [Fact]
        public void Games_joining_together_show_one_Queued_Toast_each_in_turn()
        {
            var hades = Guid.NewGuid();
            var celeste = Guid.NewGuid();
            core.OnLibraryChanged(new FakeLibrary().WithGame(hades, "Hades", installed: false).WithGame(celeste, "Celeste", installed: false).Snapshot());

            core.OnLibraryChanged(new FakeLibrary()
                .WithGame(hades, "Hades", installed: false, installing: true, coverImage: "hades.jpg")
                .WithGame(celeste, "Celeste", installed: false, installing: true, coverImage: "celeste.jpg")
                .Snapshot());

            Assert.Equal("hades.jpg", core.Toast.Image);
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(core.Toast.IsShown);
            clock.Advance(TimeSpan.FromSeconds(0.5));
            Assert.True(core.Toast.IsShown);
            Assert.Equal("celeste.jpg", core.Toast.Image);
        }

        [Fact]
        public void A_Queued_Toast_waits_behind_a_notifications_Toast()
        {
            var id = Guid.NewGuid();
            core.OnNotificationsChanged(new Notification[0]);
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false).Snapshot());
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false, installing: true).Snapshot());

            Assert.Equal("Library updated", core.Toast.Text);
            clock.Advance(TimeSpan.FromSeconds(5.5));
            Assert.Equal(ToastKind.Queued, core.Toast.Kind);
            Assert.Equal("Queued", core.Toast.Text);
        }

#if DEBUG
        [Fact]
        public void A_Queued_Toast_a_call_cuts_short_shows_again_after_the_call()
        {
            StartInstalling("hades.jpg");
            clock.Advance(TimeSpan.FromSeconds(2));

            core.DebugRing("Alex", null);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);
            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.False(core.Toast.IsShown);
            clock.Advance(TimeSpan.FromSeconds(0.5));

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Queued, core.Toast.Kind);
            Assert.Equal("hades.jpg", core.Toast.Image);
        }
#endif

        [Fact]
        public void The_Guide_button_tapped_during_a_Queued_Toast_goes_to_Manage_Queue_not_the_Guide()
        {
            StartInstalling("hades.jpg");

            TapGuide();

            Assert.False(core.Guide.IsOpen);
            Assert.Equal(DashboardPage.MyGamesAndApps, playnite.Page);
            Assert.Equal(MyGamesItem.ManageQueue, core.MyGames.Item);
            Assert.False(core.Toast.IsShown);
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void The_Guide_button_held_during_a_Queued_Toast_still_opens_Power()
        {
            StartInstalling("hades.jpg");

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.True(core.Guide.IsOpen);
            Assert.NotEqual(MyGamesItem.ManageQueue, core.MyGames.Item);
        }

        [Fact]
        public void The_Guide_button_tapped_during_a_Queued_Toast_over_the_Guide_closes_the_Guide()
        {
            var id = Guid.NewGuid();
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false).Snapshot());
            TapGuide();
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false, installing: true).Snapshot());
            Assert.Equal(ToastKind.Queued, core.Toast.Kind);

            TapGuide();

            Assert.False(core.Guide.IsOpen);
            Assert.NotEqual(MyGamesItem.ManageQueue, core.MyGames.Item);
        }

        /// <summary>A game Playnite wasn't installing starts installing, as its Install was pressed: it joins the queue.</summary>
        private void StartInstalling(string cover)
        {
            var id = Guid.NewGuid();
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false, coverImage: cover).Snapshot());
            core.OnLibraryChanged(new FakeLibrary().WithGame(id, "Hades", installed: false, installing: true, coverImage: cover).Snapshot());
        }

        private void TapGuide()
        {
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));
        }
    }
}
