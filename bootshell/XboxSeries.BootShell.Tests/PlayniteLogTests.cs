using System;
using System.IO;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // Hearing Playnite's crash handler from what it adds to its log, in a file of the test's own.
    public class PlayniteLogTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "XboxSeries.BootShell.Tests." + Guid.NewGuid().ToString("N") + ".log");

        public void Dispose() => File.Delete(path);

        [Fact]
        public void A_crash_logged_before_the_Boot_shell_looked_isnt_heard_and_one_after_is_heard_once()
        {
            File.WriteAllText(path, "21:40:01.112|ERROR|PlayniteApplication:Unhandled exception occured.\n");
            var log = new PlayniteLog(path);
            Assert.False(log.CrashLogged());

            File.AppendAllText(path, "21:52:13.020|INFO|Something else\n21:52:13.481|ERROR|PlayniteApplication:Unhandled excep");
            Assert.False(log.CrashLogged());

            File.AppendAllText(path, "tion occured.\nSystem.Exception: boom\n");
            Assert.True(log.CrashLogged());
            Assert.False(log.CrashLogged());
        }
    }
}
