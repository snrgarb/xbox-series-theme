#if DEBUG
using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Debug builds' Debug menu in Settings, for trying Toasts on the TV.</summary>
    public class DebugMenuTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public DebugMenuTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(),
                new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            core.OnNotificationsChanged(new Notification[0]);
        }

        [Fact]
        public void The_Debug_category_can_be_focused_on_the_rail()
        {
            core.OpenSettings();

            core.FocusSettingsCategory(SettingsCategory.Debug);

            Assert.Equal(SettingsCategory.Debug, core.Settings.Category);
        }

        [Fact]
        public void A_test_notification_is_posted_to_Playnite_with_a_fresh_id_each_time()
        {
            core.DebugNotify("Library updated");
            core.DebugNotify("Screenshot saved");

            Assert.Equal(new[] { "Library updated", "Screenshot saved" }, playnite.PostedNotifications.Select(n => n.Text));
            Assert.All(playnite.PostedNotifications, n => Assert.StartsWith("xbox-series-debug-", n.Id));
            Assert.Equal(2, playnite.PostedNotifications.Select(n => n.Id).Distinct().Count());
        }

        [Fact]
        public void A_test_ring_shows_the_call_Toast_with_the_callers_name_and_image()
        {
            core.DebugRing("ana", "https://cdn.example/ana.png");

            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);
            Assert.Equal("ana is calling", core.Toast.Text);
            Assert.Equal("https://cdn.example/ana.png", core.Toast.Image);
        }

        [Fact]
        public void A_test_rings_Toast_shows_for_30_seconds()
        {
            core.DebugRing("ana", null);

            clock.Advance(TimeSpan.FromSeconds(29.9));
            Assert.True(core.Toast.IsShown);

            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.False(core.Toast.IsShown);
        }

        [Fact]
        public void A_test_ring_cuts_in_before_a_notification_which_shows_after_it()
        {
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            clock.Advance(TimeSpan.FromSeconds(2));

            core.DebugRing("ana", null);
            Assert.Equal(ToastKind.Call, core.Toast.Kind);

            clock.Advance(TimeSpan.FromSeconds(30));
            Assert.False(core.Toast.IsShown);
            clock.Advance(TimeSpan.FromSeconds(0.5));
            Assert.True(core.Toast.IsShown);
            Assert.Equal(ToastKind.Notification, core.Toast.Kind);
            Assert.Equal("Library updated", core.Toast.Text);
        }

        [Fact]
        public void A_Guide_tap_during_a_test_rings_Toast_opens_the_Guide_on_the_landing_tab_without_Answer()
        {
            core.DebugRing("ana", null);

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);
            clock.Advance(TimeSpan.FromMilliseconds(300));

            Assert.True(core.Guide.IsOpen);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.DoesNotContain(core.Guide.Items, i => i.Kind == GuideItemKind.DiscordIncomingCall);
            Assert.False(core.Toast.IsShown);
        }
    }
}
#endif
