using System.Collections.Generic;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // The sentinel's check, as explorer's startup run starts it: with the Boot shell running, or with explorer the
    // shell from the start.
    public class SentinelTests
    {
        private const string Winlogon = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
        private const string Exe = @"C:\Users\gabe\AppData\Local\XboxSeries.BootShell\XboxSeries.BootShell.exe";

        private readonly Dictionary<(string, string), object> values = new();
        private readonly List<string> log = new();
        private int fullscreenStarts;

        [Fact]
        public void With_the_Boot_shell_running_it_changes_nothing_and_says_so()
        {
            values[(Winlogon, "Shell")] = Exe;

            Check(bootShellRuns: true);

            Assert.Equal(Exe, values[(Winlogon, "Shell")]);
            Assert.Equal(0, fullscreenStarts);
            Assert.Contains(log, line => line.Contains("Boot shell runs"));
        }

        [Fact]
        public void Without_the_Boot_shell_it_writes_Shell_back_and_starts_Fullscreen()
        {
            Check(bootShellRuns: false);

            Assert.Equal(Exe, values[(Winlogon, "Shell")]);
            Assert.Equal(1, fullscreenStarts);
            Assert.Contains(log, line => line.Contains("Shell") && line.Contains("none -> "));
        }

        [Fact]
        public void Without_the_Boot_shell_but_Shell_still_set_it_only_starts_Fullscreen()
        {
            var registry = new Registry(values);
            values[(Winlogon, "Shell")] = Exe;

            Sentinel.Check(registry, Exe, false, () => fullscreenStarts++, log.Add);

            Assert.Equal(0, registry.Writes);
            Assert.Equal(1, fullscreenStarts);
        }

        private void Check(bool bootShellRuns) => Sentinel.Check(new Registry(values), Exe, bootShellRuns, () => fullscreenStarts++, log.Add);

        private sealed class Registry(Dictionary<(string, string), object> values) : IRegistry
        {
            public int Writes { get; private set; }

            public object Get(string key, string name) => values.GetValueOrDefault((key, name));

            public void Set(string key, string name, object value)
            {
                values[(key, name)] = value;
                Writes++;
            }

            public void Delete(string key, string name)
            {
                values.Remove((key, name));
                Writes++;
            }
        }
    }
}
