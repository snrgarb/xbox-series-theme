using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // --install and --uninstall against a fake registry, and the install folder, Startup folder and Playnite's data in a
    // folder of the test's own.
    public class SetupTests : IDisposable
    {
        private const string Winlogon = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
        private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string Teams = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\MSTeams_8wekyb3d8bbwe\TeamsTfwStartupTask";
        private const string Engagement = @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement";
        private const string Bundle = "XboxSeries_eb759ff1-325b-4c34-9a64-d9704f72612b";

        private static readonly byte[] SteamOn = { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly byte[] RobloxOff = { 0x01, 0, 0, 0, 0xAF, 0x3D, 0x76, 0xD2, 0xF6, 0x61, 0xDD, 0x01 };

        private readonly string root = Path.Combine(Path.GetTempPath(), "XboxSeries.BootShell.Tests." + Guid.NewGuid().ToString("N"));
        private readonly FakeRegistry registry = new();
        private readonly FakeTasks tasks = new();
        private readonly List<string> said = new();
        private readonly SetupPaths paths;
        private int closes;
        private bool playniteWontClose;

        public SetupTests()
        {
            paths = new SetupPaths(
                Path.Combine(root, "publish", "XboxSeries.BootShell.exe"),
                Path.Combine(root, "LocalAppData", "XboxSeries.BootShell"),
                Path.Combine(root, "Startup"),
                Path.Combine(root, "AppData", "Playnite"),
                @"PC\gabe");
            Directory.CreateDirectory(Path.GetDirectoryName(paths.Exe)!);
            File.WriteAllText(paths.Exe, "build 1");
            Directory.CreateDirectory(paths.StartupFolder);
            File.WriteAllText(Path.Combine(paths.StartupFolder, "Playnite.lnk"), "Playnite");
            File.WriteAllText(Path.Combine(paths.StartupFolder, "DiscordMini.lnk"), "DiscordMini");
            File.WriteAllText(Path.Combine(paths.StartupFolder, "Orca.lnk"), "Orca");
            Directory.CreateDirectory(paths.PlayniteData);
            File.WriteAllText(Config, """
                {
                  "StartOnBoot": true,
                  "DisabledPlugins": [ "PS5Core_84d85c00-14b1-4859-805f-6bd9d95fdae9", "BattlenetLibrary_Builtin" ],
                  "AutoBackupEnabled": false,
                  "AutoBackupFrequency": 1,
                  "DevelExtenions": [ { "Selected": true, "Item": "C:\\dev\\Other" } ],
                  "Theme": "Café"
                }
                """);
            File.WriteAllText(FullscreenConfig, """{ "MainMenuShowMinimize": true, "MainMenuShowRestart": true }""");

            // This PC as it was: Steam on, Roblox off already, AMD's and Discord's with no switch, Spotify not installed.
            registry.Set(Run, "Steam", "steam.exe -silent");
            registry.Set(Run, "Discord", "Discord.exe");
            registry.Set(Run, "AMDNoiseSuppression", "AMDNoiseSuppression.exe");
            registry.Set(Run, "RobloxPlayerBeta", "RobloxPlayerBeta.exe --launch-to-tray");
            registry.Set(Approved, "Steam", SteamOn);
            registry.Set(Approved, "RobloxPlayerBeta", RobloxOff);
            registry.Set(Teams, "State", 2);
        }

        private string Config => Path.Combine(paths.PlayniteData, "config.json");

        private string FullscreenConfig => Path.Combine(paths.PlayniteData, "fullscreenConfig.json");

        private string InstalledExe => Path.Combine(paths.InstallFolder, "XboxSeries.BootShell.exe");

        private string Record => Path.Combine(paths.InstallFolder, "setup-record.json");

        public void Dispose() => Directory.Delete(root, true);

        [Fact]
        public void Install_sets_the_PC_up()
        {
            Assert.True(NewSetup().Install());

            Assert.Equal("build 1", File.ReadAllText(InstalledExe));
            Assert.Equal(InstalledExe, registry.Get(Winlogon, "Shell"));
            Assert.Equal($"\"{InstalledExe}\" --sentinel", registry.Get(Run, "XboxSeriesBootShellSentinel"));

            // The safety net runs no Boot shell exe: PowerShell, windowless, 90 s after this user signs in.
            var task = XDocument.Parse(tasks.Get("XboxSeries.BootShell.SafetyNet")!);
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            Assert.Equal(@"PC\gabe", task.Descendants(ns + "LogonTrigger").Single().Element(ns + "UserId")!.Value);
            Assert.Equal("PT90S", task.Descendants(ns + "LogonTrigger").Single().Element(ns + "Delay")!.Value);
            Assert.Equal("LeastPrivilege", task.Descendants(ns + "RunLevel").Single().Value);
            Assert.Equal(@"C:\Windows\System32\conhost.exe", task.Descendants(ns + "Command").Single().Value);
            var arguments = task.Descendants(ns + "Arguments").Single().Value;
            Assert.StartsWith(@"--headless C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe ", arguments);
            Assert.Contains(Path.Combine(paths.InstallFolder, "logs", "safety-net.log"), arguments);
            Assert.DoesNotContain("XboxSeries.BootShell.exe", arguments);
            foreach (var name in new[] { "Steam", "Discord", "AMDNoiseSuppression" })
            {
                Assert.Equal(1, ((byte[])registry.Get(Approved, name)!)[0] & 1);
            }

            Assert.Equal(RobloxOff, registry.Get(Approved, "RobloxPlayerBeta"));
            Assert.Null(registry.Get(Approved, "Spotify"));
            Assert.Equal(1, registry.Get(Teams, "State"));
            Assert.Equal(0, registry.Get(Engagement, "ScoobeSystemSettingEnabled"));

            Assert.Equal(new[] { "Orca.lnk" }, Directory.GetFiles(paths.StartupFolder).Select(Path.GetFileName));
            Assert.Equal("Playnite", File.ReadAllText(Path.Combine(paths.InstallFolder, "backup", "Playnite.lnk")));
            Assert.Equal("DiscordMini", File.ReadAllText(Path.Combine(paths.InstallFolder, "backup", "DiscordMini.lnk")));

            var config = Json(Config);
            Assert.False((bool)config["StartOnBoot"]!);
            Assert.Equal(
                new[] { "PS5Core_84d85c00-14b1-4859-805f-6bd9d95fdae9", "BattlenetLibrary_Builtin", "Extra_Metadata_tools_2e0349ed-6da2-4095-9457-4c9fb544551e", "UniPlaySong.a1b2c3d4-e5f6-7890-abcd-ef1234567890" },
                config["DisabledPlugins"]!.AsArray().Select(p => (string)p!));
            Assert.True((bool)config["AutoBackupEnabled"]!);
            Assert.Equal(2, (int)config["AutoBackupFrequency"]!);
            Assert.True(JsonNode.DeepEquals(
                JsonNode.Parse($$"""[ { "Selected": true, "Item": "C:\\dev\\Other" }, { "Selected": true, "Item": {{new JsonArray(Path.Combine(paths.PlayniteData, "Extensions", Bundle))[0]!.ToJsonString()}} } ]"""),
                config["DevelExtenions"]));
            Assert.Equal("Café", (string)config["Theme"]!);
            Assert.False((bool)Json(FullscreenConfig)["MainMenuShowMinimize"]!);
            Assert.True((bool)Json(FullscreenConfig)["MainMenuShowRestart"]!);
        }

        [Fact]
        public void Install_closes_Playnite_before_it_writes_Playnites_settings_and_writes_Shell_last()
        {
            var config = File.ReadAllText(Config);
            var fullscreenConfig = File.ReadAllText(FullscreenConfig);
            closePlaynite = () =>
            {
                Assert.Equal(config, File.ReadAllText(Config));
                Assert.Equal(fullscreenConfig, File.ReadAllText(FullscreenConfig));
                return true;
            };

            Assert.True(NewSetup().Install());

            Assert.Equal(1, closes);
            Assert.NotEqual(config, File.ReadAllText(Config));
            Assert.Equal((Winlogon, "Shell"), registry.Writes[^1]);
        }

        [Fact]
        public void Uninstall_puts_back_every_recorded_value()
        {
            var registryBefore = registry.Snapshot();
            var config = Json(Config);
            var fullscreenConfig = Json(FullscreenConfig);
            Assert.True(NewSetup().Install());

            closes = 0;
            Assert.True(NewSetup().Uninstall());

            Assert.Equal(1, closes);
            Assert.Equal(registryBefore, registry.Snapshot());
            Assert.Null(tasks.Get("XboxSeries.BootShell.SafetyNet"));
            Assert.True(JsonNode.DeepEquals(config, Json(Config)));
            Assert.True(JsonNode.DeepEquals(fullscreenConfig, Json(FullscreenConfig)));
            Assert.Equal(new[] { "DiscordMini.lnk", "Orca.lnk", "Playnite.lnk" }, Directory.GetFiles(paths.StartupFolder).Select(Path.GetFileName).Order());

            // The exe stays, and the record is put aside, so a later --install records afresh.
            Assert.True(File.Exists(InstalledExe));
            Assert.False(File.Exists(Record));
            Assert.Single(Directory.GetFiles(paths.InstallFolder, "setup-record.*.json"));
        }

        [Fact]
        public void A_second_install_keeps_each_values_first_before_value()
        {
            var registryBefore = registry.Snapshot();
            var config = Json(Config);
            Assert.True(NewSetup().Install());

            // A new build, and Playnite put its shortcut back and switched itself on at boot again.
            File.WriteAllText(paths.Exe, "build 2");
            File.WriteAllText(Path.Combine(paths.StartupFolder, "Playnite.lnk"), "Playnite again");
            var reset = Json(Config);
            reset["StartOnBoot"] = true;
            File.WriteAllText(Config, reset.ToJsonString());
            Assert.True(NewSetup().Install());

            Assert.Equal("build 2", File.ReadAllText(InstalledExe));
            Assert.Single(Directory.GetFiles(paths.InstallFolder, "XboxSeries.BootShell.*.old"));
            Assert.False((bool)Json(Config)["StartOnBoot"]!);
            Assert.False(File.Exists(Path.Combine(paths.StartupFolder, "Playnite.lnk")));

            Assert.True(NewSetup().Uninstall());
            Assert.Equal(registryBefore, registry.Snapshot());
            Assert.True(JsonNode.DeepEquals(config, Json(Config)));
            Assert.Equal("Playnite", File.ReadAllText(Path.Combine(paths.StartupFolder, "Playnite.lnk")));
        }

        [Fact]
        public void Uninstall_leaves_a_value_changed_after_setup_alone_and_reports_it()
        {
            Assert.True(NewSetup().Install());
            registry.Set(Winlogon, "Shell", "other.exe");
            registry.Set(Approved, "Steam", SteamOn);

            Assert.True(NewSetup().Uninstall());

            Assert.Equal("other.exe", registry.Get(Winlogon, "Shell"));
            Assert.Equal(SteamOn, registry.Get(Approved, "Steam"));
            Assert.Contains(said, line => line.Contains("Shell") && line.Contains("left alone"));
            Assert.Null(registry.Get(Approved, "Discord"));
        }

        [Fact]
        public void A_setup_stopped_halfway_never_sets_Shell_and_is_still_undone()
        {
            var registryBefore = registry.Snapshot();
            var config = File.ReadAllText(Config);
            playniteWontClose = true;

            Assert.False(NewSetup().Install());

            // Nor the sentinel, which would write Shell itself at the next sign-in.
            Assert.Null(registry.Get(Winlogon, "Shell"));
            Assert.Null(registry.Get(Run, "XboxSeriesBootShellSentinel"));
            Assert.Equal(config, File.ReadAllText(Config));
            Assert.NotEqual(registryBefore, registry.Snapshot());

            playniteWontClose = false;
            Assert.True(NewSetup().Uninstall());
            Assert.Equal(registryBefore, registry.Snapshot());
            Assert.Equal(new[] { "DiscordMini.lnk", "Orca.lnk", "Playnite.lnk" }, Directory.GetFiles(paths.StartupFolder).Select(Path.GetFileName).Order());
        }

        [Fact]
        public void The_record_keeps_the_sentinel_and_the_safety_net_as_the_other_values()
        {
            Assert.True(NewSetup().Install());

            var record = Json(Record);
            Assert.True(record.AsObject().ContainsKey(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\XboxSeriesBootShellSentinel"));
            Assert.True(record.AsObject().ContainsKey(@"Task\XboxSeries.BootShell.SafetyNet"));
            Assert.Null(record[@"Task\XboxSeries.BootShell.SafetyNet"]);
        }

        [Fact]
        public void A_second_install_leaves_the_safety_net_as_Windows_rewrote_it()
        {
            Assert.True(NewSetup().Install());

            // Windows hands the task back reordered, with the delay as PT1M30S.
            var rewritten = tasks.Get("XboxSeries.BootShell.SafetyNet")!.Replace("PT90S", "PT1M30S");
            tasks.Set("XboxSeries.BootShell.SafetyNet", rewritten);
            tasks.Writes = 0;
            Assert.True(NewSetup().Install());

            Assert.Equal(0, tasks.Writes);
            Assert.Contains(said, line => line.StartsWith(@"Task\XboxSeries.BootShell.SafetyNet: holds"));
        }

        [Fact]
        public void Install_refuses_to_run_from_the_installed_exe()
        {
            Assert.True(NewSetup().Install());

            Assert.False(new Setup(registry, tasks, paths with { Exe = InstalledExe }, ClosePlaynite, said.Add, () => new DateTime(2026, 10, 23, 13, 0, 0)).Install());
        }

        private Func<bool> closePlaynite = () => true;

        private Setup NewSetup() => new(registry, tasks, paths, ClosePlaynite, said.Add, () => new DateTime(2026, 10, 23, 12, 0, 0));

        private bool ClosePlaynite()
        {
            closes++;
            return !playniteWontClose && closePlaynite();
        }

        private static JsonNode Json(string path) => JsonNode.Parse(File.ReadAllText(path))!;

        private sealed class FakeTasks : ITaskScheduler
        {
            private readonly Dictionary<string, string> xml = new();

            public int Writes { get; set; }

            public string Get(string name) => xml.GetValueOrDefault(name);

            public void Set(string name, string task)
            {
                xml[name] = task;
                Writes++;
            }

            public void Delete(string name)
            {
                xml.Remove(name);
                Writes++;
            }
        }

        private sealed class FakeRegistry : IRegistry
        {
            private readonly Dictionary<(string Key, string Name), object> values = new();

            public List<(string Key, string Name)> Writes { get; } = new();

            public object Get(string key, string name) => values.GetValueOrDefault((key, name));

            public void Set(string key, string name, object value)
            {
                values[(key, name)] = value;
                Writes.Add((key, name));
            }

            public void Delete(string key, string name)
            {
                values.Remove((key, name));
                Writes.Add((key, name));
            }

            public string Snapshot() => string.Join("\n", values
                .OrderBy(v => v.Key.Key + "\\" + v.Key.Name, StringComparer.Ordinal)
                .Select(v => $"{v.Key.Key}\\{v.Key.Name}={(v.Value is byte[] b ? Convert.ToHexString(b) : v.Value)}"));
        }
    }
}
