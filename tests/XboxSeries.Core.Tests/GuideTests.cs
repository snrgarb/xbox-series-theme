using System;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class GuideTests
    {
        [Fact]
        public void Guide_is_closed_at_start()
        {
            var clock = new FakeClock(new DateTime(2026, 10, 13, 20, 0, 0));
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12), series: new[] { "Halo" }, genres: new[] { "Shooter" }, source: "Steam")
                .WithGame("Forza Horizon 5", added: new DateTime(2026, 9, 1), installed: false, source: "Xbox")
                .WithFilterPreset("Favourites", showInFullscreenQuickSelection: true)
                .WithApp("Discord", @"C:\Discord\Update.exe", "--processStart Discord.exe");

            var core = new ExtensionCore(clock);
            core.OnLibraryChanged(library.Snapshot());

            Assert.False(core.Guide.IsOpen);
        }
    }
}
