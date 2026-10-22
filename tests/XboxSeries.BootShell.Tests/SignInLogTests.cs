using System;
using System.IO;
using System.Linq;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // The Boot shell's log, one file per sign-in, in a folder of the test's own: the newest 30 sign-ins are kept.
    public class SignInLogTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "XboxSeries.BootShell.Tests." + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(folder, true);

        [Fact]
        public void A_sign_in_writes_its_lines_to_a_file_named_for_when_it_started()
        {
            using (var log = new SignInLog(new DateTime(2026, 10, 22, 20, 5, 9)))
            {
                log.Write("before the file opens");
                log.Open(folder);
                log.Write("after");
            }

            var lines = File.ReadAllLines(Path.Combine(folder, "20261022-200509.log"));
            Assert.Equal(2, lines.Length);
            Assert.EndsWith("  before the file opens", lines[0]);
            Assert.EndsWith("  after", lines[1]);
        }

        [Fact]
        public void A_31st_sign_in_deletes_the_oldest_file()
        {
            Directory.CreateDirectory(folder);
            var first = new DateTime(2026, 9, 1, 8, 0, 0);
            for (var day = 0; day < 30; day++)
            {
                File.WriteAllText(Path.Combine(folder, first.AddDays(day).ToString("yyyyMMdd-HHmmss") + ".log"), "");
            }

            using (var log = new SignInLog(new DateTime(2026, 10, 22, 20, 5, 9)))
            {
                log.Open(folder);
            }

            var kept = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n).ToList();
            Assert.Equal(30, kept.Count);
            Assert.Equal("20260902-080000.log", kept[0]);
            Assert.Equal("20261022-200509.log", kept[29]);
        }
    }
}
