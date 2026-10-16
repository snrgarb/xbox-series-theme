using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
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

        // The bits Steam sets while it has an update of the app under way: UpdateRunning while it works on it,
        // UpdatePaused while it's paused, and UpdateStarted from the moment it's queued. UpdateRequired alone isn't
        // among them: an installed game waiting on an update it hasn't been asked for is still installed.
        private const long UpdateUnderWay = UpdateRunning | 512 | 1024;

        // StateFlags' Uninstalling bit: a manifest on its way out is no install, whatever else it says.
        private const long Uninstalling = 2048;

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
        /// </summary>
        public void Install(Guid gameId)
        {
            try
            {
                var appId = AppIdOf(gameId);
                var exe = appId == null ? null : SteamExe();
                if (exe == null)
                {
                    return;
                }

                using (Process.Start(new ProcessStartInfo(exe, $"-silent +app_install {appId}")
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
                WarnOnce(ref installWarned, e, $"Couldn't ask Steam to install game {gameId}");
            }
        }

        /// <summary>
        /// Asked once a second on the UI thread, so it lists each library's steamapps once and reads only the
        /// manifests of Steam games Playnite has as neither installed nor installing, which are few; the rest are
        /// known by name alone.
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
                        if (game == null || game.IsInstalled || game.IsInstalling || installing.Contains(gameId))
                        {
                            continue;
                        }

                        var flags = Number(Values(manifest, "StateFlags"), "StateFlags");
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

        /// <summary>Whether a manifest's StateFlags say Steam has the app's files all on disk, with no update going.</summary>
        private static bool IsInstalled(long flags) => (flags & FullyInstalled) != 0 && (flags & UpdateUnderWay) == 0;

        /// <summary>The game's Steam app id; null when it isn't from Playnite's Steam library.</summary>
        private uint? AppIdOf(Guid gameId)
        {
            var game = api.Database.Games.Get(gameId);
            return game != null && game.PluginId == SteamLibraryId && uint.TryParse(game.GameId, out var appId) ? appId : (uint?)null;
        }

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
                if (game.PluginId == SteamLibraryId && uint.TryParse(game.GameId, out var appId))
                {
                    games[appId] = game.Id;
                }
            }

            steamGames = games;
            steamGamesRead = DateTime.UtcNow;
            return games;
        }

        private static bool IsReadFailure(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException || e is SecurityException || e is ArgumentException || e is NotSupportedException;
        }

        private static void WarnOnce(ref bool warned, Exception e, string message)
        {
            if (!warned)
            {
                warned = true;
                logger.Warn(e, message);
            }
        }
    }
}
