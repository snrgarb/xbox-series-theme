#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using Playnite.SDK.Data;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Debug builds only: the Debug menu's trials of each way of asking Steam to install, for finding one that shows no
    /// Steam window (#59). Each trial watches what Steam puts on screen until a while after it asks, then reports which
    /// windows came up and what Steam wrote to the app's manifest, as a Toast and in extensions.log.
    /// </summary>
    internal sealed partial class SteamDownloads
    {
        /// <summary>The ways of asking Steam to install that the Debug menu tries.</summary>
        public enum Trial
        {
            /// <summary><c>steam.exe +app_install</c>, as handed to the running client, nothing else.</summary>
            Console,

            /// <summary><c>steam.exe -silent +app_install</c>: the hub's command, without its tray handling.</summary>
            ConsoleSilent,

            /// <summary><c>steam.exe +app_install &lt;id&gt; 0</c>: naming the first library, which may skip the wizard's choice.</summary>
            ConsoleLibrary0,

            /// <summary>The Game hub's own install: Steam's windows to the tray first, and Playnite kept in front.</summary>
            Hub,

            /// <summary><c>steam://install/&lt;id&gt;</c>: Playnite's Steam library's route, known to show the install dialog.</summary>
            InstallUrl,

            /// <summary>Over CEF debugging: the client's own install wizard, opened and continued from script.</summary>
            CefWizard,

            /// <summary>Over CEF debugging: <c>SteamClient.Downloads.QueueAppUpdate</c>, with no wizard at all.</summary>
            CefQueue,

            /// <summary>
            /// Over CEF debugging: <c>app_install</c> typed into the client's own console, as its Console tab would,
            /// so no steam.exe is started to hand the command over.
            /// </summary>
            CefConsole,

            /// <summary>A manifest stub saying the app needs an update, then <c>QueueAppUpdate</c> over CEF debugging.</summary>
            StubCefQueue,

            /// <summary>A manifest stub, then <c>steam.exe +app_set_update_flag</c> to have the running client notice it.</summary>
            StubUpdateFlag,

            /// <summary>A manifest stub, then Steam restarted to the tray, which reads it as an app wanting an update.</summary>
            StubRestart,
        }

        // Spacewar: Valve's free test app, a few megabytes, which any account can take a licence for.
        private const uint Spacewar = 480;

        // How long a trial watches Steam once it has asked: the wizard takes a second or two to come up.
        private static readonly TimeSpan TrialWatchFor = TimeSpan.FromSeconds(20);

        // How long a trial that restarts Steam watches once Steam is started again: it signs in and reads its
        // manifests first.
        private static readonly TimeSpan RestartWatchFor = TimeSpan.FromSeconds(60);

        // StateFlags UpdateStarted | UpdateRequired, without FullyInstalled: what the community's stub says, an app
        // whose update is wanted and none of whose files are on disk, so Steam fetches the whole app.
        private const long StubStateFlags = 1026;

        // The empty file in Steam's folder that has the client open its CEF debugger on 127.0.0.1 at each start.
        private const string CefDebuggingFile = ".cef-enable-remote-debugging";

        private List<KeyValuePair<uint, string>> trialTargets;
        private int trialTarget;

        /// <summary>The app the trials install: "Spacewar (480)" to begin with.</summary>
        public string TrialTargetName => $"{TrialTargets()[trialTarget].Value} ({TrialTargets()[trialTarget].Key})";

        /// <summary>The next app to try with: Spacewar, then the five Steam games most recently added to Playnite that aren't installed.</summary>
        public void NextTrialTarget()
        {
            trialTargets = null;
            trialTarget = (trialTarget + 1) % TrialTargets().Count;
        }

        private List<KeyValuePair<uint, string>> TrialTargets()
        {
            if (trialTargets == null)
            {
                trialTargets = new List<KeyValuePair<uint, string>> { new KeyValuePair<uint, string>(Spacewar, "Spacewar") };
                trialTargets.AddRange(api.Database.Games
                    .Where(g => !g.IsInstalled && SteamAppId(g) != null)
                    .OrderByDescending(g => g.Added ?? DateTime.MinValue)
                    .Take(5)
                    .Select(g => new KeyValuePair<uint, string>(SteamAppId(g).Value, g.Name)));
                trialTarget = Math.Min(trialTarget, trialTargets.Count - 1);
            }

            return trialTargets;
        }

        /// <summary>Tries one way of installing the target, reporting what Steam showed and wrote through <paramref name="report"/>.</summary>
        public async void RunTrial(Trial trial, Action<string> report)
        {
            var target = TrialTargets()[trialTarget];
            var appId = target.Key;
            var name = $"{trial} {target.Value}";
            TrialWatch watch = null;
            try
            {
                var exe = SteamExe();
                if (exe == null)
                {
                    report($"{name}: Steam isn't installed");
                    return;
                }

                logger.Info($"Steam trial {name}: starting");
                watch = WatchSteam(name, appId, report);
                var warned = false;
                var watchFor = TrialWatchFor;
                switch (trial)
                {
                    case Trial.Console:
                        AskSteam(exe, $"+app_install {appId}", name, ref warned);
                        break;
                    case Trial.ConsoleSilent:
                        AskSteam(exe, $"-silent +app_install {appId}", name, ref warned);
                        break;
                    case Trial.ConsoleLibrary0:
                        AskSteam(exe, $"+app_install {appId} 0", name, ref warned);
                        break;
                    case Trial.Hub:
                        InstallApp(exe, appId, name);
                        break;
                    case Trial.InstallUrl:
                        Process.Start($"steam://install/{appId}")?.Dispose();
                        break;
                    case Trial.CefWizard:
                        watch.Note(await EvaluateInSteam(CefWizardScript(appId)));
                        break;
                    case Trial.CefQueue:
                        watch.Note(await EvaluateInSteam(CefQueueScript(appId)));
                        break;
                    case Trial.CefConsole:
                        watch.Note(await EvaluateInSteam(CefConsoleScript(appId)));
                        break;
                    case Trial.StubCefQueue:
                        if (TryWriteStub(appId, target.Value, watch))
                        {
                            watch.Note(await EvaluateInSteam(CefQueueScript(appId)));
                        }

                        break;
                    case Trial.StubUpdateFlag:
                        if (TryWriteStub(appId, target.Value, watch))
                        {
                            AskSteam(exe, $"+app_set_update_flag {appId}", name, ref warned);
                        }

                        break;
                    case Trial.StubRestart:
                        if (TryWriteStub(appId, target.Value, watch))
                        {
                            watch.Note(await RestartSteam(exe, "-silent"));
                            watchFor = RestartWatchFor;
                        }

                        break;
                }

                watch.EndIn(watchFor);
            }
            catch (Exception e)
            {
                // The watch still reports, with the failure among its findings.
                logger.Error(e, $"Steam trial {name} failed");
                if (watch == null)
                {
                    report($"{name}: failed, {e.Message}");
                }
                else
                {
                    watch.Note($"failed, {e.Message}");
                    watch.EndIn(TimeSpan.Zero);
                }
            }
        }

        /// <summary>Asks Steam to uninstall the target, so a trial can be run again; watched as a trial is.</summary>
        public void UninstallTrialTarget(Action<string> report) => AskSteamWatched("Uninstall", "+app_uninstall", report);

        /// <summary>Asks Steam for a free licence to the target, which Spacewar needs before Steam will install it.</summary>
        public void RequestTrialLicence(Action<string> report) => AskSteamWatched("Licence", "+app_license_request", report);

        private void AskSteamWatched(string trial, string command, Action<string> report)
        {
            var target = TrialTargets()[trialTarget];
            var exe = SteamExe();
            if (exe == null)
            {
                report($"{trial}: Steam isn't installed");
                return;
            }

            var warned = false;
            WatchSteam($"{trial} {target.Value}", target.Key, report).EndIn(TrialWatchFor);
            AskSteam(exe, $"{command} {target.Key}", trial, ref warned);
        }

        /// <summary>Whether the client opens its CEF debugger as it starts, as <see cref="ToggleCefDebugging"/> last left it.</summary>
        public bool IsCefDebuggingOn
        {
            get
            {
                var steam = SteamPath();
                return steam != null && File.Exists(Path.Combine(steam, CefDebuggingFile));
            }
        }

        /// <summary>
        /// Turns the client's CEF debugger on or off, by the file in Steam's folder it looks for as it starts, and
        /// restarts Steam to the tray so it takes effect. While on, any program on this PC can drive Steam through
        /// 127.0.0.1:8080, so it's for trying with and best turned off after. Left as it was while a Steam game runs,
        /// which the restart would end.
        /// </summary>
        public async void ToggleCefDebugging(Action<string> report)
        {
            try
            {
                var steam = SteamPath();
                var exe = SteamExe();
                if (steam == null || exe == null)
                {
                    report("CEF debugging: Steam isn't installed");
                    return;
                }

                var running = RunningSteamGame();
                if (running != 0)
                {
                    report($"CEF debugging: left as it was, as Steam is running app {running}, which a restart would end");
                    return;
                }

                var file = Path.Combine(steam, CefDebuggingFile);
                var on = !File.Exists(file);
                if (on)
                {
                    File.WriteAllText(file, string.Empty);
                }
                else
                {
                    File.Delete(file);
                }

                report($"CEF debugging {(on ? "on" : "off")}: restarting Steam");
                report($"CEF debugging {(on ? "on" : "off")}: {await RestartSteam(exe, on ? "-silent -cef-enable-debugging" : "-silent")}");
            }
            catch (Exception e)
            {
                logger.Error(e, "Couldn't turn Steam's CEF debugging over");
                report($"CEF debugging: failed, {e.Message}");
            }
        }

        /// <summary>
        /// Opens the client's install wizard for the app from its own script, as its Install button does, waits for the
        /// wizard to reach the page that asks where to install (or the licence agreements), and continues it there
        /// into the first library, as pressing Install would. The wizard's states (Steam's EInstallMgrState) come from
        /// its ShowInstallWizard event as well as from asking, as GetInstallManagerInfo stayed at 0 when only asked.
        /// </summary>
        private static string CefWizardScript(uint appId) => @"(async () => {
  const installs = SteamClient.Installs;
  const states = [];
  let shown = null;
  const reg = installs.RegisterForShowInstallWizard(info => { shown = info; });
  try {
    installs.OpenInstallWizard([" + appId + @"]);
    for (let i = 0; i < 100; i++) {
      const info = shown || await installs.GetInstallManagerInfo();
      shown = null;
      if (states[states.length - 1] !== info.eInstallState) states.push(info.eInstallState);
      if (info.eInstallState === 7 || info.eInstallState === 8) {
        installs.SetInstallFolder(0);
        installs.ContinueInstall();
        await new Promise(r => setTimeout(r, 1000));
        const after = await installs.GetInstallManagerInfo();
        states.push(after.eInstallState);
        return 'wizard ' + states.join('>') + ', apps ' + JSON.stringify(info.rgApps.map(a => a.nAppID)) + ', error ' + after.eAppError;
      }
      if (info.eInstallState >= 14) break;
      await new Promise(r => setTimeout(r, 100));
    }
    return 'wizard ' + states.join('>') + ', never asked where';
  } finally {
    reg.unregister();
  }
})()";

        /// <summary>Has the client queue the app's update, as the Downloads page's play button does, then lists its download queue.</summary>
        private static string CefQueueScript(uint appId) => @"(async () => {
  SteamClient.Downloads.QueueAppUpdate(" + appId + @");
  await new Promise(r => setTimeout(r, 1000));
  return 'queue ' + await " + DownloadQueueScript + @";
})()";

        /// <summary>
        /// Types <c>app_install</c> into the client's console, as its Console tab would, then reports what the console
        /// wrote back over the next three seconds and the download queue.
        /// </summary>
        private static string CefConsoleScript(uint appId) => @"(async () => {
  const said = [];
  const reg = SteamClient.Console.RegisterForSpewOutput(s => { if (s.spew_type !== 'input') said.push(s.spew.trim()); });
  try {
    SteamClient.Console.ExecCommand('app_install " + appId + @"');
    await new Promise(r => setTimeout(r, 3000));
  } finally {
    reg.unregister();
  }
  return 'console said ' + JSON.stringify(said.filter(s => s).join(' / ').slice(0, 200)) + '; queue ' + await " + DownloadQueueScript + @";
})()";

        // The client's download queue, as its Downloads page has it: each app id, starred if downloading. The event
        // hands (downloading, [{ remote_client_id, item_data: [...] }]), one entry per PC, this one's id "0".
        private const string DownloadQueueScript = @"new Promise(resolve => {
    let done = false;
    const reg = SteamClient.Downloads.RegisterForDownloadItems((downloading, clients) => {
      if (done) return;
      done = true;
      reg.unregister();
      const items = [].concat(...clients.map(c => c.item_data || []));
      resolve(items.length ? items.map(i => i.appid + (i.active ? '*' : '')).join(',') : 'empty');
    });
    setTimeout(() => { if (!done) { done = true; reg.unregister(); resolve('not listed within 3 s'); } }, 3000);
  })";

        /// <summary>
        /// Runs <paramref name="expression"/> in the client's SharedJSContext, its script that runs without a window,
        /// through its CEF debugger, and returns what it came to, or why it couldn't.
        /// </summary>
        private static async Task<string> EvaluateInSteam(string expression)
        {
            string targets = null;
            foreach (var port in new[] { 8080, 8081 })
            {
                try
                {
                    using (var web = new WebClient())
                    {
                        targets = await web.DownloadStringTaskAsync($"http://127.0.0.1:{port}/json");
                        break;
                    }
                }
                catch (WebException)
                {
                }
            }

            if (targets == null)
            {
                return "CEF debugging is off: turn it on in this menu first";
            }

            var shared = Serialization.FromJson<List<CefTarget>>(targets).FirstOrDefault(t => t.Title == "SharedJSContext");
            if (shared?.WebSocketDebuggerUrl == null)
            {
                return "CEF debugging is on, but Steam has no SharedJSContext yet";
            }

            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var socket = new ClientWebSocket())
            {
                await socket.ConnectAsync(new Uri(shared.WebSocketDebuggerUrl), cancel.Token);
                var request = Serialization.ToJson(new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new Dictionary<string, object> { ["expression"] = expression, ["awaitPromise"] = true, ["returnByValue"] = true },
                });
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, cancel.Token);

                var buffer = new byte[16384];
                while (true)
                {
                    var message = new StringBuilder();
                    WebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancel.Token);
                        message.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
                    }
                    while (!received.EndOfMessage);

                    var reply = Serialization.FromJson<CefReply>(message.ToString());
                    if (reply?.Id != 1)
                    {
                        continue;
                    }

                    var thrown = reply.Result?.ExceptionDetails;
                    return thrown != null
                        ? $"script threw {thrown.Exception?.Description ?? thrown.Text}"
                        : reply.Result?.Result?.Value?.ToString() ?? "script returned nothing";
                }
            }
        }

        /// <summary>
        /// Writes a manifest into Steam's own library saying the app is wanted but none of it is on disk, as the
        /// community's silent install does; Steam fetches it when it next reads its manifests. Its install folder is
        /// named after the app, as Steam's own usually is (Spacewar's is "Spacewar"), but may not match the app's
        /// real one. Never over a manifest Steam has, nor into a folder already there; notes why on the watch.
        /// </summary>
        private bool TryWriteStub(uint appId, string name, TrialWatch watch)
        {
            var existing = ManifestOf(appId);
            if (existing != null)
            {
                watch.Note($"no stub: Steam already has {existing}; uninstall first");
                return false;
            }

            var steam = SteamPath();
            var owner = MostRecentSteamId(steam);
            if (owner == null)
            {
                watch.Note("no stub: Steam isn't signed in, nor marks an account most recent in loginusers.vdf");
                return false;
            }

            var installDir = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
            var folder = Path.Combine(steam, "steamapps", "common", installDir);
            if (installDir.Length == 0 || Directory.Exists(folder))
            {
                watch.Note($"no stub: install folder '{folder}' is already there");
                return false;
            }

            var manifest = Path.Combine(steam, "steamapps", $"appmanifest_{appId}.acf");
            File.WriteAllText(manifest, string.Join("\n",
                "\"AppState\"",
                "{",
                $"\t\"appid\"\t\t\"{appId}\"",
                "\t\"Universe\"\t\t\"1\"",
                $"\t\"name\"\t\t\"{name.Replace("\"", string.Empty)}\"",
                $"\t\"StateFlags\"\t\t\"{StubStateFlags}\"",
                $"\t\"installdir\"\t\t\"{installDir}\"",
                "\t\"LastUpdated\"\t\t\"0\"",
                "\t\"SizeOnDisk\"\t\t\"0\"",
                "\t\"buildid\"\t\t\"0\"",
                $"\t\"LastOwner\"\t\t\"{owner}\"",
                "\t\"BytesToDownload\"\t\t\"0\"",
                "\t\"BytesDownloaded\"\t\t\"0\"",
                "\t\"AutoUpdateBehavior\"\t\t\"0\"",
                "\t\"AllowOtherDownloadsWhileRunning\"\t\t\"0\"",
                "\t\"ScheduledAutoUpdate\"\t\t\"0\"",
                "\t\"InstalledDepots\"",
                "\t{",
                "\t}",
                "\t\"UserConfig\"",
                "\t{",
                "\t}",
                "\t\"MountedConfig\"",
                "\t{",
                "\t}",
                "}",
                string.Empty));
            watch.Note($"stub written to {manifest}, folder {installDir}");
            return true;
        }

        /// <summary>
        /// The SteamID64 of the account Steam is signed in to, from the account id it keeps in the registry while it
        /// runs, or else the one config\loginusers.vdf marks most recent, which today's clients no longer mark; null
        /// when neither says.
        /// </summary>
        private static string MostRecentSteamId(string steam)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
            {
                if (key?.GetValue("ActiveUser") is int account && account != 0)
                {
                    // An individual account's SteamID64 is its account id over this base.
                    return (76561197960265728UL + (uint)account).ToString();
                }
            }

            var file = Path.Combine(steam, "config", "loginusers.vdf");
            if (!File.Exists(file))
            {
                return null;
            }

            string user = null;
            foreach (var line in File.ReadLines(file))
            {
                var id = Regex.Match(line, @"^\s*""(\d{17})""\s*$");
                if (id.Success)
                {
                    user = id.Groups[1].Value;
                }
                else if (user != null && TryKeyValue(line, out var key, out var value)
                    && key.Equals("MostRecent", StringComparison.OrdinalIgnoreCase) && value == "1")
                {
                    return user;
                }
            }

            return null;
        }

        /// <summary>
        /// Closes Steam, waits for it to go, and starts it again with <paramref name="arguments"/>. Refused while a
        /// Steam game runs, which closing Steam would end.
        /// </summary>
        private async Task<string> RestartSteam(string exe, string arguments)
        {
            var running = RunningSteamGame();
            if (running != 0)
            {
                return $"not restarted: Steam is running app {running}";
            }

            var warned = false;
            if (IsSteamRunning())
            {
                AskSteam(exe, "-shutdown", "a restart", ref warned);
                var until = DateTime.UtcNow + TimeSpan.FromSeconds(60);
                while (IsSteamRunning() && DateTime.UtcNow < until)
                {
                    await Task.Delay(500);
                }

                if (IsSteamRunning())
                {
                    return "not restarted: Steam didn't close within a minute";
                }
            }

            AskSteam(exe, arguments, "a restart", ref warned);
            return $"restarted with {arguments}";
        }

        /// <summary>The app id of the Steam game running now, as Steam records it; 0 for none.</summary>
        private static int RunningSteamGame()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                return key?.GetValue("RunningAppID") is int running ? running : 0;
            }
        }

        /// <summary>
        /// Watches Steam from now until a while after the trial has done its asking (<see cref="TrialWatch.EndIn"/>):
        /// each window it shows that wasn't up before, and whether any of its windows takes the foreground; then
        /// reports those, the notes the trial added, and the app's manifest.
        /// </summary>
        private TrialWatch WatchSteam(string name, uint appId, Action<string> report)
        {
            var watch = new TrialWatch(name);
            var before = new HashSet<IntPtr>(NativeMethods.VisibleWindowsWithTitles(IsSteamProgram).Select(w => w.Key));
            var timer = new DispatcherTimer { Interval = KeepInFrontEvery };
            timer.Tick += (_, __) =>
            {
                foreach (var window in NativeMethods.VisibleWindowsWithTitles(IsSteamProgram))
                {
                    if (!before.Contains(window.Key) && watch.Shown.Add(window.Key))
                    {
                        watch.Note($"showed '{window.Value}'");
                    }
                }

                var front = NativeMethods.GetForegroundWindow();
                if (!watch.TookFront && front != IntPtr.Zero && IsSteamWindow(front))
                {
                    watch.TookFront = true;
                    watch.Note("Steam took the foreground");
                }

                if (watch.Until == null || DateTime.UtcNow < watch.Until)
                {
                    return;
                }

                timer.Stop();
                if (watch.Shown.Count == 0)
                {
                    watch.Note("no new Steam window");
                }

                watch.Note(ManifestState(appId));
                logger.Info($"Steam trial {name}: done");
                report(watch.Summary);
            };
            timer.Start();
            return watch;
        }

        /// <summary>What Steam has written for the app: its manifest's state and byte counts, and whether the registry says it's updating.</summary>
        private string ManifestState(uint appId)
        {
            var manifest = Find($"appmanifest_{appId}.acf", LibraryFolders(refresh: true));
            if (manifest == null)
            {
                return "no manifest";
            }

            var values = Values(manifest, "StateFlags", "BytesDownloaded", "BytesToDownload");
            return $"manifest flags {Number(values, "StateFlags")}, {Number(values, "BytesDownloaded")} of {Number(values, "BytesToDownload")} bytes"
                + (IsUpdating(appId) ? ", registry updating" : string.Empty);
        }

        /// <summary>One trial's findings, each logged as it comes, and summed up once its watch ends.</summary>
        private sealed class TrialWatch
        {
            private readonly string name;
            private readonly List<string> notes = new List<string>();

            public TrialWatch(string name)
            {
                this.name = name;
            }

            public HashSet<IntPtr> Shown { get; } = new HashSet<IntPtr>();

            public bool TookFront { get; set; }

            /// <summary>When the watch ends and reports; null until the trial has done its asking.</summary>
            public DateTime? Until { get; private set; }

            public string Summary => $"{name}: {string.Join("; ", notes)}";

            /// <summary>Has the watch end <paramref name="after"/> from now, the trial having done its asking.</summary>
            public void EndIn(TimeSpan after)
            {
                Until = DateTime.UtcNow + after;
            }

            /// <summary>Adds a finding, logged now and reported when the watch ends.</summary>
            public void Note(string note)
            {
                notes.Add(note);
                logger.Info($"Steam trial {name}: {note}");
            }
        }

        private sealed class CefTarget
        {
            public string Title { get; set; }

            public string WebSocketDebuggerUrl { get; set; }
        }

        private sealed class CefReply
        {
            public int? Id { get; set; }

            public CefEvaluation Result { get; set; }
        }

        private sealed class CefEvaluation
        {
            public CefObject Result { get; set; }

            public CefException ExceptionDetails { get; set; }
        }

        private sealed class CefObject
        {
            public object Value { get; set; }

            public string Description { get; set; }
        }

        private sealed class CefException
        {
            public string Text { get; set; }

            public CefObject Exception { get; set; }
        }
    }
}
#endif
