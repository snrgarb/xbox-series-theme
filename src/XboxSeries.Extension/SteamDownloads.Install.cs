using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Installing Steam games through Steam's own command line, which the running client takes without a window, and
    /// following the installs Steam runs that Playnite didn't start, from the manifests Steam writes for them.
    /// </summary>
    internal sealed partial class SteamDownloads
    {
        // StateFlags' FullyInstalled bit (Steam's EAppState): set once the app's files are all on disk, and only then;
        // a fresh install's manifest lacks it until Steam commits the last of the download.
        private const long FullyInstalled = 4;

        // StateFlags' UpdatePaused bit: set while the user has the app's download paused.
        private const long UpdatePaused = 512;

        // StateFlags' UpdateStarted bit: set from the moment Steam queues the app's download until it's done.
        private const long UpdateStarted = 1024;

        // The bits Steam sets while it has an update of the app under way: UpdateRunning while it works on it,
        // UpdatePaused while it's paused, and UpdateStarted from the moment it's queued. UpdateRequired alone isn't
        // among them: an installed game waiting on an update it hasn't been asked for is still installed.
        private const long UpdateUnderWay = UpdateRunning | UpdateStarted | UpdatePaused;

        // StateFlags' Uninstalling bit: a manifest on its way out is no install, whatever else it says.
        private const long Uninstalling = 2048;

        // How long after asking Steam to install its windows are kept from coming in front of Playnite, and how often
        // that is checked. Steam takes the command within a second or two.
        private static readonly TimeSpan KeepInFrontFor = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan KeepInFrontEvery = TimeSpan.FromMilliseconds(200);

        // How long the install waits for Steam's open windows to go to the tray before asking anyway, and how often
        // it looks: Steam hides its window within a moment of being asked to close it.
        private static readonly TimeSpan TrayWaitFor = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan TrayWaitEvery = TimeSpan.FromMilliseconds(50);

        // How long the app id to game map stands without a library change: a backstop for a change Playnite didn't
        // announce, cheap at this rate however large the library.
        private static readonly TimeSpan MapLifetime = TimeSpan.FromSeconds(5);

        private Dictionary<uint, Guid> steamGames;
        private DateTime steamGamesRead;
        private volatile bool steamGamesStale = true;
        private bool watchingLibrary;
        private bool installWarned;
        private bool installingWarned;
        private bool markWarned;

        public bool CanInstall(Guid gameId)
        {
            try
            {
                return AppIdOf(gameId) != null && SteamExe() != null;
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref installWarned, e, "Couldn't find Steam to install with");
                return false;
            }
        }

        /// <summary>
        /// Hands <c>+app_install</c> to steam.exe, which passes it to the running client and exits; the client installs
        /// to its default library with no window. Not waited for: Steam is followed from its manifests instead. When
        /// Steam isn't running this starts it, and <c>-silent</c> keeps its main window down as it does, though its
        /// sign-in or update windows can still show.
        /// <para>
        /// The running client brings its window to the front as it takes a command when that window is open, but
        /// stays in the tray when it's there, so Steam's open windows are sent to the tray first, and the command
        /// goes once they've gone, or after <see cref="TrayWaitFor"/> regardless.
        /// </para>
        /// </summary>
        public void Install(Guid gameId)
        {
            string exe;
            uint? appId;
            try
            {
                appId = AppIdOf(gameId);
                exe = appId == null ? null : SteamExe();
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref installWarned, e, $"Couldn't find Steam to install game {gameId} with");
                return;
            }

            if (exe != null)
            {
                InstallApp(exe, appId.Value, $"game {gameId}");
            }
        }

        /// <summary><see cref="Install"/> for an app id, which <paramref name="what"/> names in the log.</summary>
        private void InstallApp(string exe, uint appId, string what)
        {
            if (!IsSteamRunning())
            {
                AskSteam(exe, $"-silent +app_install {appId}", what, ref installWarned);
                return;
            }

            var open = SteamWindows();
            if (open.Count == 0)
            {
                AskSteam(exe, $"-silent +app_install {appId}", what, ref installWarned);
                KeepPlayniteInFront();
                return;
            }

            logger.Info($"Sending Steam's {open.Count} open window(s) to the tray before asking it to install app {appId}");
            foreach (var window in open)
            {
                NativeMethods.PostMessage(window, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            var until = DateTime.UtcNow + TrayWaitFor;
            var timer = new DispatcherTimer { Interval = TrayWaitEvery };
            timer.Tick += (_, __) =>
            {
                if (SteamWindows().Count > 0 && DateTime.UtcNow < until)
                {
                    return;
                }

                timer.Stop();
                AskSteam(exe, $"-silent +app_install {appId}", what, ref installWarned);
                KeepPlayniteInFront();
            };
            timer.Start();
        }

        /// <summary>
        /// Runs steam.exe with <paramref name="arguments"/>, which a running client takes over; for <paramref name="what"/>,
        /// in the log, where a failure is warned of once for each <paramref name="warned"/>.
        /// </summary>
        private static void AskSteam(string exe, string arguments, string what, ref bool warned)
        {
            try
            {
                using (Process.Start(new ProcessStartInfo(exe, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                }))
                {
                }
            }
            catch (Exception e) when (IsReadFailure(e) || e is Win32Exception || e is InvalidOperationException)
            {
                WarnOnce(ref warned, e, $"Couldn't ask Steam '{arguments}' for {what}");
            }
        }

        /// <summary>Steam's visible windows: the client's own, and its web helper's, which draws the main window.</summary>
        private static List<IntPtr> SteamWindows() => NativeMethods.TopLevelWindows(IsSteamProgram);

        private static bool IsSteamProgram(string path)
        {
            var name = Path.GetFileName(path);
            return string.Equals(name, "steam.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "steamwebhelper.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSteamRunning()
        {
            var steam = Process.GetProcessesByName("steam");
            foreach (var process in steam)
            {
                process.Dispose();
            }

            return steam.Length > 0;
        }

        /// <summary>
        /// The running client brings its own window to the front as it takes a command, whatever <c>-silent</c> says,
        /// when that window is open rather than in the tray. Should it come forward even so, from the tray or from a
        /// window that wouldn't close in time, then for a while after asking, any Steam window in front is
        /// closed, which sends Steam's to the tray as its own close button does, and Playnite takes the foreground back.
        /// Only when Steam was running already, so its sign-in window, which a Steam just started may need, is left.
        /// </summary>
        private static void KeepPlayniteInFront()
        {
            var playnite = Application.Current?.MainWindow;
            if (playnite == null)
            {
                return;
            }

            var playniteWindow = new WindowInteropHelper(playnite).Handle;
            var until = DateTime.UtcNow + KeepInFrontFor;
            var timer = new DispatcherTimer { Interval = KeepInFrontEvery };
            timer.Tick += (_, __) =>
            {
                if (DateTime.UtcNow >= until)
                {
                    timer.Stop();
                    return;
                }

                var front = NativeMethods.GetForegroundWindow();
                if (front == IntPtr.Zero || front == playniteWindow || !IsSteamWindow(front))
                {
                    return;
                }

                logger.Info("Steam came to the front as it took the install; sending it back to the tray");
                NativeMethods.PostMessage(front, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                NativeMethods.BringToForeground(playniteWindow);
            };
            timer.Start();
        }

        /// <summary>Whether the window is Steam's: the client's own, or its web helper's, which draws the main window.</summary>
        private static bool IsSteamWindow(IntPtr window)
        {
            var path = NativeMethods.ProgramPathOf((uint)NativeMethods.ProcessIdOf(window));
            return path != null && IsSteamProgram(path);
        }

        /// <summary>
        /// Asked once a second on the UI thread, so it lists each library's steamapps once and reads only the
        /// manifests of Steam games Playnite has as not installed, which are few; the rest are known by name alone.
        /// </summary>
        public IReadOnlyList<Guid> Installing()
        {
            var installing = new List<Guid>();
            try
            {
                var games = SteamGames();
                if (games.Count == 0)
                {
                    return installing;
                }

                foreach (var folder in LibraryFolders(refresh: DateTime.UtcNow - foldersRead > FoldersLifetime))
                {
                    var steamapps = Path.Combine(folder, "steamapps");
                    if (!Directory.Exists(steamapps))
                    {
                        continue;
                    }

                    foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
                    {
                        if (!uint.TryParse(Path.GetFileNameWithoutExtension(manifest).Substring("appmanifest_".Length), out var appId)
                            || !games.TryGetValue(appId, out var gameId))
                        {
                            continue;
                        }

                        var game = api.Database.Games.Get(gameId);
                        if (game == null || game.IsInstalled || installing.Contains(gameId))
                        {
                            continue;
                        }

                        // Each manifest is read on its own: Steam rewriting one mustn't drop the games after it
                        // from this read, which the core would take for installs that have finished.
                        long flags;
                        try
                        {
                            flags = Number(Values(manifest, "StateFlags"), "StateFlags");
                        }
                        catch (Exception e) when (IsReadFailure(e))
                        {
                            WarnOnce(ref installingWarned, e, $"Couldn't read Steam's manifest {manifest}");
                            continue;
                        }

                        if (!IsInstalled(flags) && (flags & Uninstalling) == 0)
                        {
                            installing.Add(gameId);
                        }
                    }
                }
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref installingWarned, e, "Couldn't list Steam's installs");
            }

            return installing;
        }

        /// <summary>
        /// Ends as Playnite's own Steam install does, with the game installed in the folder Steam put it in; Playnite's
        /// next library import reads the same manifest and agrees.
        /// </summary>
        public void MarkInstalled(Guid gameId)
        {
            try
            {
                var appId = AppIdOf(gameId);
                var manifest = appId == null ? null : ManifestOf(appId.Value);
                if (manifest == null)
                {
                    return;
                }

                var values = Values(manifest, "StateFlags", "installdir");
                if (!IsInstalled(Number(values, "StateFlags")) || !values.TryGetValue("installdir", out var installDir) || installDir.Length == 0)
                {
                    return;
                }

                var game = api.Database.Games.Get(gameId);
                if (game == null || game.IsInstalled)
                {
                    return;
                }

                // The manifest sits in <library>\steamapps, and the game in <library>\steamapps\common\<installdir>.
                game.IsInstalled = true;
                game.InstallDirectory = Path.Combine(Path.GetDirectoryName(manifest), "common", installDir);
                api.Database.Games.Update(game);
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref markWarned, e, $"Couldn't mark game {gameId} installed from Steam's manifest");
            }
        }

        /// <summary>
        /// Ends Playnite's install as Playnite's own "stop watching" would, leaving its watch on Steam running: should
        /// Steam install the game after all, that watch still marks it installed. Only while Steam has no manifest for
        /// the game, as it has none for one never begun or cancelled.
        /// </summary>
        public void MarkCancelled(Guid gameId)
        {
            try
            {
                var appId = AppIdOf(gameId);
                var game = api.Database.Games.Get(gameId);
                if (appId == null || game == null || !game.IsInstalling || ManifestOf(appId.Value) != null)
                {
                    return;
                }

                game.IsInstalling = false;
                api.Database.Games.Update(game);
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref markWarned, e, $"Couldn't mark game {gameId} no longer installing");
            }
        }

        /// <summary>
        /// Whether a manifest's StateFlags say Steam has the app's files all on disk, with no update going and no
        /// uninstall under way: one rule for both <see cref="Installing"/> and <see cref="MarkInstalled"/>, so a game
        /// Steam is uninstalling is never marked installed.
        /// </summary>
        private static bool IsInstalled(long flags) =>
            (flags & FullyInstalled) != 0 && (flags & UpdateUnderWay) == 0 && (flags & Uninstalling) == 0;

        /// <summary>Steam's exe, as it records it for the user it runs as, or else in its folder; null when neither is on disk.</summary>
        private static string SteamExe()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                var exe = (key?.GetValue("SteamExe") as string)?.Replace('/', '\\');
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    return exe;
                }
            }

            var steam = SteamPath();
            var beside = steam == null ? null : Path.Combine(steam, "steam.exe");
            return beside != null && File.Exists(beside) ? beside : null;
        }

        /// <summary>
        /// Playnite's Steam games by app id, built from the whole library only when it has changed, or every few
        /// seconds at most, rather than on each of <see cref="Installing"/>'s calls. Games are held by id, so each is
        /// read as it is now.
        /// </summary>
        private Dictionary<uint, Guid> SteamGames()
        {
            if (!watchingLibrary)
            {
                // Database events can arrive on background threads, so they only mark the map stale.
                watchingLibrary = true;
                api.Database.Games.ItemCollectionChanged += (_, __) => steamGamesStale = true;
                api.Database.Games.ItemUpdated += (_, __) => steamGamesStale = true;
            }

            if (steamGames != null && !steamGamesStale && DateTime.UtcNow - steamGamesRead < MapLifetime)
            {
                return steamGames;
            }

            steamGamesStale = false;
            var games = new Dictionary<uint, Guid>();
            foreach (var game in api.Database.Games)
            {
                var appId = SteamAppId(game);
                if (appId != null)
                {
                    games[appId.Value] = game.Id;
                }
            }

            steamGames = games;
            steamGamesRead = DateTime.UtcNow;
            return games;
        }
    }
}
