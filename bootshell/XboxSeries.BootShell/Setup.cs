using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace XboxSeries.BootShell
{
    /// <summary>Where setup reads and writes: the exe it runs from, and the folders it sets up; and for whom.</summary>
    /// <param name="Exe">The exe setup runs from, in the publish folder.</param>
    /// <param name="InstallFolder">%LOCALAPPDATA%\XboxSeries.BootShell, which holds the installed exe, the record and backup\.</param>
    /// <param name="StartupFolder">The Startup folder, asked of Windows, as it's redirected on this PC.</param>
    /// <param name="PlayniteData">%APPDATA%\Playnite, which holds config.json and fullscreenConfig.json.</param>
    /// <param name="User">The user, as DOMAIN\name, whose sign-in the safety net follows.</param>
    internal sealed record SetupPaths(string Exe, string InstallFolder, string StartupFolder, string PlayniteData, string User)
    {
        public static SetupPaths ThisPc() => new(
            Environment.ProcessPath!,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxSeries.BootShell"),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite"),
            $@"{Environment.UserDomainName}\{Environment.UserName}");
    }

    /// <summary>
    /// --install and --uninstall. Install copies the exe into the install folder, then walks <see cref="Settings"/> in
    /// order, writing each that doesn't hold, the Winlogon Shell value last. Before it touches a value it records the
    /// value as it was in setup-record.json, unless the record has it already, so a second install keeps the first
    /// before-value. Uninstall walks them back, putting each recorded value back while it still holds what setup wrote,
    /// and leaving one changed since alone, said. Playnite is closed before its settings are read or written, as it
    /// writes them back as it exits.
    /// </summary>
    internal sealed class Setup
    {
        public const string ExeName = "XboxSeries.BootShell.exe";

        public const string Winlogon = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
        private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupApproved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string TeamsStartupTask = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\MSTeams_8wekyb3d8bbwe\TeamsTfwStartupTask";
        private const string Engagement = @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement";

        /// <summary>The HKCU Run entry that starts the installed exe with --sentinel (<see cref="Sentinel"/>).</summary>
        private const string SentinelEntry = "XboxSeriesBootShellSentinel";

        private const string SafetyNetTask = "XboxSeries.BootShell.SafetyNet";

        private static readonly XNamespace TaskXml = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        /// <summary>The Bundle's extension's folder name under Playnite's Extensions.</summary>
        private const string BundleExtension = "XboxSeries_eb759ff1-325b-4c34-9a64-d9704f72612b";

        /// <summary>The HKCU Run entries whose Startup-apps switch setup turns off.</summary>
        private static readonly string[] SwitchedOff = { "Steam", "Discord", "Spotify", "AMDNoiseSuppression", "RobloxPlayerBeta" };

        private static readonly string[] DisabledPlugins =
        {
            "Extra_Metadata_tools_2e0349ed-6da2-4095-9457-4c9fb544551e",
            "UniPlaySong.a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "BattlenetLibrary_Builtin",
        };

        private static readonly JsonWriterOptions Indented = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>For setup's lines: quotes and apostrophes as they are, not escaped.</summary>
        private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private readonly IRegistry registry;
        private readonly ITaskScheduler tasks;
        private readonly SetupPaths paths;
        private readonly Func<bool> closePlaynite;
        private readonly Action<string> say;
        private readonly Func<DateTime> now;

        /// <param name="closePlaynite">Closes Playnite if it runs; false if it's still up.</param>
        /// <param name="say">Tells whoever ran setup what it did, a line at a time.</param>
        public Setup(IRegistry registry, ITaskScheduler tasks, SetupPaths paths, Func<bool> closePlaynite, Action<string> say, Func<DateTime> now)
        {
            this.registry = registry;
            this.tasks = tasks;
            this.paths = paths;
            this.closePlaynite = closePlaynite;
            this.say = say;
            this.now = now;
        }

        private string InstalledExe => Path.Combine(paths.InstallFolder, ExeName);

        private string RecordPath => Path.Combine(paths.InstallFolder, "setup-record.json");

        private string Backup => Path.Combine(paths.InstallFolder, "backup");

        /// <summary>Sets the PC up, or brings a set-up PC up to date; false, said, if it stopped short.</summary>
        public bool Install()
        {
            try
            {
                CopyExe();
                var record = LoadRecord() ?? new JsonObject();
                var playniteClosed = false;
                foreach (var setting in Settings())
                {
                    if (!setting.Applies())
                    {
                        say($"{setting.Name}: not on this PC");
                        continue;
                    }

                    if (setting.Playnite && !playniteClosed)
                    {
                        if (!closePlaynite())
                        {
                            throw new SetupException("Playnite didn't close");
                        }

                        playniteClosed = true;
                    }

                    var current = setting.Read();
                    if (!record.ContainsKey(setting.Name))
                    {
                        record[setting.Name] = current?.DeepClone();
                        Save(RecordPath, record);
                    }

                    if (setting.Holds(current))
                    {
                        say($"{setting.Name}: holds, {Show(current)}");
                        continue;
                    }

                    setting.Write(setting.Wanted);
                    say($"{setting.Name}: {Show(current)} -> {Show(setting.Wanted)}");
                }

                say("Set up. The Boot shell runs from the next sign-in. --uninstall undoes it.");
                return true;
            }
            catch (Exception e) when (IsSetupError(e))
            {
                say($"ERROR: setup stopped: {e.Message}. Shell is set last, so the next sign-in is as before; --uninstall undoes what was done.");
                return false;
            }
        }

        /// <summary>
        /// Closes Playnite before its settings are put back; false, said, if it didn't or couldn't be asked to. Never
        /// throws: the rest of --uninstall, Shell above all, is the way back to explorer and mustn't wait on Playnite.
        /// </summary>
        private bool ClosePlayniteForUninstall()
        {
            try
            {
                return closePlaynite();
            }
            catch (Exception e) when (IsSetupError(e))
            {
                say($"ERROR: couldn't close Playnite: {e.Message}");
                return false;
            }
        }

        /// <summary>Puts back every value the record holds; false, said, if one couldn't be.</summary>
        public bool Uninstall()
        {
            JsonObject? record;
            try
            {
                record = LoadRecord();
            }
            catch (Exception e) when (IsSetupError(e))
            {
                say($"ERROR: couldn't read {RecordPath}: {e.Message}");
                return false;
            }

            if (record == null)
            {
                say($"Not set up: there's no {RecordPath}.");
                return true;
            }

            var settings = Settings().Where(s => record.ContainsKey(s.Name)).Reverse().ToList();
            var playniteUp = settings.Any(s => s.Playnite) && !ClosePlayniteForUninstall();
            var undone = true;
            foreach (var setting in settings)
            {
                var before = record[setting.Name];
                try
                {
                    if (setting.Playnite && playniteUp)
                    {
                        throw new SetupException("Playnite didn't close");
                    }

                    var current = setting.Read();
                    if (JsonNode.DeepEquals(current, before))
                    {
                        say($"{setting.Name}: as before setup, {Show(current)}");
                    }
                    else if (!setting.Holds(current))
                    {
                        say($"{setting.Name}: changed since setup, left alone: {Show(current)} (before setup {Show(before)})");
                    }
                    else
                    {
                        setting.Write(before);
                        say($"{setting.Name}: {Show(current)} -> {Show(before)}");
                    }
                }
                catch (Exception e) when (IsSetupError(e))
                {
                    say($"ERROR: {setting.Name}: {e.Message}");
                    undone = false;
                }
            }

            if (!undone)
            {
                say($"Not all undone: {RecordPath} is kept, so --uninstall can try again.");
                return false;
            }

            // Put aside, so a later --install records the PC afresh.
            var aside = Path.Combine(paths.InstallFolder, $"setup-record.{now():yyyyMMdd-HHmmss}.json");
            File.Move(RecordPath, aside, overwrite: true);
            say($"Undone from the next sign-in. The record is now {aside}; the exe and logs stay.");
            return true;
        }

        /// <summary>
        /// Copies the exe in. One already there may be running as the shell: it's renamed aside, which Windows allows,
        /// and the next sign-in deletes it.
        /// </summary>
        private void CopyExe()
        {
            if (string.Equals(Path.GetFullPath(paths.Exe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
            {
                throw new SetupException("this is the installed exe: run --install from the publish folder");
            }

            Directory.CreateDirectory(paths.InstallFolder);
            var aside = Path.Combine(paths.InstallFolder, $"XboxSeries.BootShell.{now():yyyyMMdd-HHmmss-fff}.old");
            var replacing = File.Exists(InstalledExe);
            if (replacing)
            {
                File.Move(InstalledExe, aside);
            }

            try
            {
                File.Copy(paths.Exe, InstalledExe);
            }
            catch when (replacing)
            {
                // Shell may name it already: never leave it naming no exe.
                File.Move(aside, InstalledExe);
                throw;
            }
            say($"copied {paths.Exe} to {InstalledExe}");
        }

        /// <summary>
        /// Everything setup changes, in the order it changes them: the safety net first, Playnite's after the rest, then
        /// the sentinel and Shell last.
        /// </summary>
        private IEnumerable<Setting> Settings()
        {
            yield return SafetyNet();

            // An odd first byte switches an entry off; Windows' own toggle writes 01 00 00 00 and when.
            var off = new byte[] { 0x01, 0, 0, 0 }.Concat(BitConverter.GetBytes(now().ToFileTime())).ToArray();
            foreach (var name in SwitchedOff)
            {
                yield return Value(StartupApproved, name, off) with
                {
                    Applies = () => registry.Get(Run, name) != null,
                    HoldsWhen = current => current is JsonValue hex && Convert.FromHexString((string)hex!) is [var first, ..] && (first & 1) == 1,
                };
            }

            // 1 is off as the user switches it off; 2 is on.
            yield return Value(TeamsStartupTask, "State", 1) with { Applies = () => registry.Get(TeamsStartupTask, "State") != null };
            foreach (var name in new[] { "Playnite.lnk", "DiscordMini.lnk" })
            {
                yield return Shortcut(name);
            }

            yield return Value(Engagement, "ScoobeSystemSettingEnabled", 0);

            var config = Path.Combine(paths.PlayniteData, "config.json");
            yield return PlayniteValue(config, "StartOnBoot", false);
            foreach (var id in DisabledPlugins)
            {
                yield return DisabledPlugin(config, id);
            }

            yield return PlayniteValue(config, "AutoBackupEnabled", true);

            // Playnite's AutoBackupFrequency.OnceAWeek.
            yield return PlayniteValue(config, "AutoBackupFrequency", 2);

            // The Bundle's extension, selected, so it loads first.
            yield return DevelExtension(config, Path.Combine(paths.PlayniteData, "Extensions", BundleExtension));
            yield return PlayniteValue(Path.Combine(paths.PlayniteData, "fullscreenConfig.json"), "MainMenuShowMinimize", false);

            // Last, so a setup that stops short never boots into the Boot shell. The sentinel writes Shell itself, so it
            // comes just before, and undo takes it off straight after Shell.
            yield return Value(Run, SentinelEntry, $"\"{InstalledExe}\" --sentinel");
            yield return Value(Winlogon, "Shell", InstalledExe);
        }

        /// <summary>
        /// An HKCU value of <paramref name="wanted"/>'s type: a string, a DWORD as an int, or binary as a byte[], in the
        /// record as a JSON string, a number, or hex.
        /// </summary>
        private Setting Value(string key, string name, object wanted) => new(
            $@"HKCU\{key}\{name}",
            FromRegistry(wanted),
            () => FromRegistry(registry.Get(key, name)),
            value =>
            {
                if (value == null)
                {
                    registry.Delete(key, name);
                    return;
                }

                registry.Set(key, name, wanted switch
                {
                    int => (int)value,
                    byte[] => Convert.FromHexString((string)value!),
                    _ => (string)value!,
                });
            });

        /// <summary>
        /// The safety net: a logon task, 90 s after the user signs in, at normal rights, that starts explorer if none runs
        /// in the session and logs it. It runs PowerShell, windowless under conhost --headless, never the Boot shell's exe,
        /// so it covers a missing or broken one too. In the record it's its command and delay, as Windows rewrites the rest.
        /// </summary>
        private Setting SafetyNet()
        {
            const string system32 = @"C:\Windows\System32";
            var log = Path.Combine(paths.InstallFolder, "logs", "safety-net.log").Replace("'", "''");
            var script = "$s = (Get-Process -Id $PID).SessionId; "
                + "if (-not (Get-Process explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $s)) { "
                + $"Start-Process '{Launcher.Explorer}'; "
                + $"Add-Content -LiteralPath '{log}' -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') + '  no explorer 90 s after sign-in: started it') }}";
            var wanted = new JsonObject
            {
                ["Command"] = Path.Combine(system32, "conhost.exe"),
                ["Arguments"] = $@"--headless {system32}\WindowsPowerShell\v1.0\powershell.exe -NoProfile -NonInteractive -Command ""{script}""",
                ["DelaySeconds"] = 90,
            };

            return new Setting(
                $@"Task\{SafetyNetTask}",
                wanted,
                () => tasks.Get(SafetyNetTask) is { } xml ? FromTask(XDocument.Parse(xml)) : null,
                value =>
                {
                    if (value == null)
                    {
                        tasks.Delete(SafetyNetTask);
                        return;
                    }

                    tasks.Set(SafetyNetTask, TaskFor((string)value["Command"]!, (string)value["Arguments"]!, (int)value["DelaySeconds"]!));
                });
        }

        private static JsonNode FromTask(XDocument task)
        {
            var exec = task.Descendants(TaskXml + "Exec").FirstOrDefault();
            var delay = task.Descendants(TaskXml + "LogonTrigger").FirstOrDefault()?.Element(TaskXml + "Delay")?.Value;
            return new JsonObject
            {
                ["Command"] = exec?.Element(TaskXml + "Command")?.Value,
                ["Arguments"] = exec?.Element(TaskXml + "Arguments")?.Value,
                ["DelaySeconds"] = delay == null ? null : (int)XmlConvert.ToTimeSpan(delay).TotalSeconds,
            };
        }

        /// <summary>The task's XML: at the user's sign-in after <paramref name="delaySeconds"/>, as the user, not elevated.</summary>
        private string TaskFor(string command, string arguments, int delaySeconds)
        {
            static XElement Element(string name, params object[] content) => new(TaskXml + name, content);
            var task = new XDocument(
                new XDeclaration("1.0", "UTF-16", null),
                Element("Task", new XAttribute("version", "1.2"),
                    Element("RegistrationInfo", Element("Description", "The Boot shell's safety net: starts explorer if none runs 90 s after sign-in.")),
                    Element("Triggers", Element("LogonTrigger",
                        Element("Enabled", "true"),
                        Element("UserId", paths.User),
                        Element("Delay", $"PT{delaySeconds}S"))),
                    Element("Principals", Element("Principal", new XAttribute("id", "Author"),
                        Element("UserId", paths.User),
                        Element("LogonType", "InteractiveToken"),
                        Element("RunLevel", "LeastPrivilege"))),
                    Element("Settings",
                        Element("MultipleInstancesPolicy", "IgnoreNew"),
                        Element("DisallowStartIfOnBatteries", "false"),
                        Element("StopIfGoingOnBatteries", "false"),
                        Element("ExecutionTimeLimit", "PT5M"),
                        Element("Enabled", "true")),
                    Element("Actions", new XAttribute("Context", "Author"), Element("Exec",
                        Element("Command", command),
                        Element("Arguments", arguments)))));
            return task.Declaration + Environment.NewLine + task;
        }

        private static JsonNode? FromRegistry(object? value) => value switch
        {
            null => null,
            int dword => dword,
            byte[] binary => Convert.ToHexString(binary),
            var text => text.ToString(),
        };

        /// <summary>A shortcut in the Startup folder, true while it's there; setup moves it into backup\ and undo back.</summary>
        private Setting Shortcut(string name)
        {
            var inStartup = Path.Combine(paths.StartupFolder, name);
            var backedUp = Path.Combine(Backup, name);
            return new Setting(
                $@"Startup\{name}",
                false,
                () => File.Exists(inStartup),
                value =>
                {
                    if ((bool)value!)
                    {
                        File.Move(backedUp, inStartup);
                    }
                    else
                    {
                        // One Playnite put back after an earlier setup is dropped, so backup\ keeps the PC's own.
                        Directory.CreateDirectory(Backup);
                        if (File.Exists(backedUp))
                        {
                            File.Delete(inStartup);
                        }
                        else
                        {
                            File.Move(inStartup, backedUp);
                        }
                    }
                });
        }

        /// <summary>One of Playnite's settings in <paramref name="file"/>, absent from it as null.</summary>
        private Setting PlayniteValue(string file, string key, JsonNode wanted) => PlayniteSetting(
            $"{Path.GetFileName(file)} {key}",
            file,
            wanted,
            settings => settings[key]?.DeepClone(),
            (settings, value) =>
            {
                if (value == null)
                {
                    settings.Remove(key);
                }
                else
                {
                    settings[key] = value.DeepClone();
                }
            });

        /// <summary>A plugin's id in DisabledPlugins, true while it's there.</summary>
        private Setting DisabledPlugin(string file, string id) => PlayniteSetting(
            $"{Path.GetFileName(file)} DisabledPlugins {id}",
            file,
            true,
            settings => settings["DisabledPlugins"] is JsonArray list && list.Any(p => (string?)p == id),
            (settings, value) =>
            {
                if (settings["DisabledPlugins"] is not JsonArray list)
                {
                    settings["DisabledPlugins"] = list = new JsonArray();
                }

                foreach (var listed in list.Where(p => (string?)p == id).ToList())
                {
                    list.Remove(listed);
                }

                if ((bool)value!)
                {
                    list.Add((JsonNode)id);
                }
            });

        /// <summary>A folder in DevelExtenions (Playnite's spelling): whether it's selected, or null while it isn't listed.</summary>
        private Setting DevelExtension(string file, string folder)
        {
            static JsonObject? Listed(JsonObject settings, string folder) => (settings["DevelExtenions"] as JsonArray)?
                .OfType<JsonObject>()
                .FirstOrDefault(e => string.Equals((string?)e["Item"], folder, StringComparison.OrdinalIgnoreCase));

            return PlayniteSetting(
                $"{Path.GetFileName(file)} DevelExtenions {folder}",
                file,
                true,
                settings => Listed(settings, folder) is { } entry ? (bool?)entry["Selected"] ?? false : null,
                (settings, value) =>
                {
                    if (settings["DevelExtenions"] is not JsonArray list)
                    {
                        settings["DevelExtenions"] = list = new JsonArray();
                    }

                    var entry = Listed(settings, folder);
                    if (value == null)
                    {
                        list.Remove(entry);
                    }
                    else if (entry == null)
                    {
                        list.Add((JsonNode)new JsonObject { ["Selected"] = (bool)value, ["Item"] = folder });
                    }
                    else
                    {
                        entry["Selected"] = (bool)value;
                    }
                });
        }

        /// <summary>A setting read from and written into one of Playnite's JSON files, only while the file is there.</summary>
        private static Setting PlayniteSetting(string name, string file, JsonNode wanted, Func<JsonObject, JsonNode?> read, Action<JsonObject, JsonNode?> write) =>
            new Setting(
                name,
                wanted,
                () => Load(file) is { } settings ? read(settings) : null,
                value =>
                {
                    var settings = Load(file) ?? throw new SetupException($"{file} isn't there");
                    write(settings, value);
                    Save(file, settings);
                })
            {
                Applies = () => File.Exists(file),
                Playnite = true,
            };

        private JsonObject? LoadRecord() => Load(RecordPath);

        private static JsonObject? Load(string file) =>
            File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? throw new SetupException($"{file} isn't a JSON object") : null;

        /// <summary>Writes <paramref name="json"/> beside <paramref name="file"/>, then over it, so a stop never leaves half a file.</summary>
        private static void Save(string file, JsonObject json)
        {
            var temporary = file + ".bootshell.tmp";
            using (var stream = File.Create(temporary))
            using (var writer = new Utf8JsonWriter(stream, Indented))
            {
                json.WriteTo(writer);
            }

            File.Move(temporary, file, overwrite: true);
        }

        private static string Show(JsonNode? value) => value?.ToJsonString(Readable) ?? "none";

        private static bool IsSetupError(Exception e) =>
            e is IOException or UnauthorizedAccessException or SecurityException or JsonException or FormatException or InvalidOperationException or Win32Exception or SetupException;

        /// <summary>One thing setup changes.</summary>
        /// <param name="Name">How the record and setup's lines name it.</param>
        /// <param name="Wanted">What setup writes.</param>
        /// <param name="Read">Its value now, as the record keeps it; null for none.</param>
        /// <param name="Write">Writes a value; null deletes it.</param>
        private sealed record Setting(string Name, JsonNode? Wanted, Func<JsonNode?> Read, Action<JsonNode?> Write)
        {
            /// <summary>Whether it's on this PC; one that isn't is passed over.</summary>
            public Func<bool> Applies { get; init; } = () => true;

            /// <summary>Whether a value is as setup wants it, where that's more than being <see cref="Wanted"/> itself.</summary>
            public Func<JsonNode?, bool>? HoldsWhen { get; init; }

            /// <summary>One of Playnite's own settings, written only while Playnite is closed.</summary>
            public bool Playnite { get; init; }

            public bool Holds(JsonNode? current) => HoldsWhen?.Invoke(current) ?? JsonNode.DeepEquals(current, Wanted);
        }

        private sealed class SetupException(string message) : Exception(message);
    }
}
