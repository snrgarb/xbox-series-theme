using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;
using Playnite.SDK;
using Playnite.SDK.Models;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: Steam's downloads, read from its app manifests, for the games Playnite is installing from Steam. Asked
    /// once a second on the UI thread for each game being installed, so a call reads one small manifest and at most
    /// one registry value; Steam's library folders are remembered between calls. A game from another library, or one
    /// Steam can't be read for, has no progress.
    /// </summary>
    internal sealed partial class SteamDownloads : IDownloads
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // Playnite's own Steam library plugin, whose games' GameId is the Steam app id.
        private static readonly Guid SteamLibraryId = new Guid("CB91DFC9-B977-43BF-8E70-55F46E410FAB");

        // StateFlags' UpdateRunning bit (Steam's EAppState): set while Steam works on the app's download, through
        // preallocating, downloading, staging and committing; clear while it waits in Steam's queue (UpdateRequired |
        // UpdateStarted) or is paused (UpdatePaused).
        private const long UpdateRunning = 256;

        // How long a miss waits before Steam's library folders are read again: a manifest Steam hasn't written yet
        // would otherwise have libraryfolders.vdf reread every second.
        private static readonly TimeSpan FoldersLifetime = TimeSpan.FromSeconds(5);

        private readonly IPlayniteAPI api;
        private List<string> libraryFolders;
        private DateTime foldersRead;

        /// <summary>
        /// How long a library folder that was slow to look at, or wasn't there, is passed over: a library on a drive
        /// unplugged, or on a share offline, can block each look for the drive's or the network's timeout, on the UI
        /// thread, every second while a download is followed.
        /// </summary>
        private static readonly TimeSpan PassOverFor = TimeSpan.FromMinutes(1);

        /// <summary>A look at a library folder that takes longer than this has its folder passed over for <see cref="PassOverFor"/>.</summary>
        private static readonly TimeSpan SlowLook = TimeSpan.FromMilliseconds(250);

        /// <summary>Library folders passed over, each until when.</summary>
        private readonly Dictionary<string, DateTime> passedOver = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private bool warned;

        // Steam's writes as each running download was first seen, by app id, and the Steam process they are read from.
        private readonly Dictionary<uint, WriteBaseline> written = new Dictionary<uint, WriteBaseline>();
        private int? steamProcessId;

        public SteamDownloads(IPlayniteAPI api)
        {
            this.api = api;
        }

        public DownloadProgress ProgressOf(Guid gameId)
        {
            var appId = AppIdOf(gameId);
            if (appId == null)
            {
                return null;
            }

            try
            {
                var manifest = ManifestOf(appId.Value);
                if (manifest == null)
                {
                    // Steam hasn't started on it: it's waiting behind another download, or on Steam's install dialog.
                    return new DownloadProgress(false, 0, 0);
                }

                var values = Values(manifest, "StateFlags", "BytesDownloaded", "BytesToDownload", "BytesStaged", "BytesToStage");
                var active = (Number(values, "StateFlags") & UpdateRunning) != 0 || IsUpdating(appId.Value);
                var downloaded = Number(values, "BytesDownloaded");
                var total = Number(values, "BytesToDownload");
                if (active)
                {
                    downloaded = Math.Max(downloaded, Math.Min(total, DownloadedSoFar(appId.Value, values)));
                }
                else
                {
                    written.Remove(appId.Value);
                }

                return new DownloadProgress(active, downloaded, total);
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                // Steam rewrites the manifest as it goes, so a read can meet it mid-write; the next second's read
                // usually succeeds, and one warning is enough.
                WarnOnce(ref warned, e, $"Couldn't read Steam's progress on app {appId}");
                return null;
            }
        }

        /// <summary>
        /// How much of the app Steam has downloaded, from what Steam itself has written since its download began. Steam
        /// leaves the manifest's byte counts as they were until the download pauses or ends, but stages what it
        /// downloads to disk as it goes, one app at a time, so its own writes since then are what it has staged since.
        /// The manifest's own counts are the start; what is staged is turned into what is downloaded by the two
        /// totals' share, as Steam downloads compressed what it stages whole. Null while Windows won't say.
        /// </summary>
        private long DownloadedSoFar(uint appId, Dictionary<string, string> values)
        {
            var steam = SteamProcessId();
            var writes = steam == null ? null : NativeMethods.BytesWrittenBy(steam.Value);
            if (writes == null)
            {
                return 0;
            }

            if (!written.TryGetValue(appId, out var since) || since.SteamProcessId != steam.Value || writes.Value < since.Writes)
            {
                since = written[appId] = new WriteBaseline(steam.Value, writes.Value, Number(values, "BytesStaged"));
            }

            var staged = since.Staged + (long)(writes.Value - since.Writes);
            var toStage = Number(values, "BytesToStage");
            var toDownload = Number(values, "BytesToDownload");
            return toStage <= 0 ? staged : (long)(Math.Min(staged, toStage) * ((double)toDownload / toStage));
        }

        /// <summary>The running Steam client's process; null when it isn't running. Remembered until it exits.</summary>
        private int? SteamProcessId()
        {
            if (steamProcessId != null && NativeMethods.BytesWrittenBy(steamProcessId.Value) != null && IsSteam(steamProcessId.Value))
            {
                return steamProcessId;
            }

            steamProcessId = null;
            var running = Process.GetProcessesByName("steam");
            foreach (var process in running)
            {
                steamProcessId = steamProcessId ?? process.Id;
                process.Dispose();
            }

            return steamProcessId;
        }

        private static bool IsSteam(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    return string.Equals(process.ProcessName, "steam", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (ArgumentException)
            {
                // Exited.
                return false;
            }
        }

        /// <summary>Steam's writes when the app's download was first seen running, and what the manifest had staged then.</summary>
        private sealed class WriteBaseline
        {
            public WriteBaseline(int steamProcessId, ulong writes, long staged)
            {
                SteamProcessId = steamProcessId;
                Writes = writes;
                Staged = staged;
            }

            public int SteamProcessId { get; }
            public ulong Writes { get; }
            public long Staged { get; }
        }

        /// <summary>The game's Steam app id; null when it isn't from Playnite's Steam library.</summary>
        private uint? AppIdOf(Guid gameId) => SteamAppId(api.Database.Games.Get(gameId));

        /// <summary>
        /// A game's Steam app id, which Playnite's Steam library keeps as its GameId; null for a game from any other
        /// library, or none.
        /// </summary>
        private static uint? SteamAppId(Game game)
        {
            return game != null && game.PluginId == SteamLibraryId && uint.TryParse(game.GameId, out var appId) ? appId : (uint?)null;
        }

        /// <summary>The failures reading Steam's files or registry can meet, all of which leave that read without an answer.</summary>
        private static bool IsReadFailure(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException || e is SecurityException || e is ArgumentException || e is NotSupportedException;
        }

        /// <summary>Logs a failure the first time only: each is asked about every second, and one warning says it.</summary>
        private static void WarnOnce(ref bool warned, Exception e, string message)
        {
            if (!warned)
            {
                warned = true;
                logger.Warn(e, message);
            }
        }

        /// <summary>
        /// Steam's manifest for the app, in whichever library folder holds it; null when there's none yet. Steam
        /// writes it when it starts on the app, possibly into a library added since the folders were read, so a miss
        /// rereads them, at most every few seconds.
        /// </summary>
        private string ManifestOf(uint appId)
        {
            var name = $"appmanifest_{appId}.acf";
            var found = Find(name, LibraryFolders(refresh: false));
            if (found == null && DateTime.UtcNow - foldersRead > FoldersLifetime)
            {
                found = Find(name, LibraryFolders(refresh: true));
            }

            return found;
        }

        private string Find(string manifestName, List<string> folders)
        {
            foreach (var folder in folders)
            {
                var path = Path.Combine(folder, "steamapps", manifestName);
                if (Look(folder, () => File.Exists(path), passOverMissing: false))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Runs <paramref name="look"/>, at <paramref name="folder"/>, unless the folder is being passed over; false then.
        /// A look slower than <see cref="SlowLook"/>, or, with <paramref name="passOverMissing"/>, one that finds nothing,
        /// has the folder passed over for <see cref="PassOverFor"/>.
        /// </summary>
        private bool Look(string folder, Func<bool> look, bool passOverMissing)
        {
            if (passedOver.TryGetValue(folder, out var until))
            {
                if (DateTime.UtcNow < until)
                {
                    return false;
                }

                passedOver.Remove(folder);
            }

            var watch = Stopwatch.StartNew();
            var found = look();
            if (watch.Elapsed > SlowLook || (passOverMissing && !found))
            {
                logger.Info($"Steam library {folder} {(found ? $"took {watch.ElapsedMilliseconds} ms to look at" : "isn't there")}; passing over it for {PassOverFor.TotalSeconds} s");
                passedOver[folder] = DateTime.UtcNow + PassOverFor;
            }

            return found;
        }

        /// <summary>Steam's own folder and the libraries libraryfolders.vdf names; none when Steam isn't installed.</summary>
        private List<string> LibraryFolders(bool refresh)
        {
            if (libraryFolders != null && !refresh)
            {
                return libraryFolders;
            }

            var folders = new List<string>();
            var steam = SteamPath();
            if (steam != null)
            {
                folders.Add(steam);
                var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    foreach (var line in File.ReadLines(vdf))
                    {
                        // Steam lists its own folder among the libraries too, in its own capitalisation.
                        if (TryKeyValue(line, out var key, out var value)
                            && key.Equals("path", StringComparison.OrdinalIgnoreCase)
                            && !folders.Exists(f => string.Equals(f.TrimEnd('\\'), value.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                        {
                            folders.Add(value);
                        }
                    }
                }
            }

            libraryFolders = folders;
            foldersRead = DateTime.UtcNow;
            return folders;
        }

        /// <summary>Steam's folder, which it records, with forward slashes, for the user it runs as; null when it has none.</summary>
        private static string SteamPath()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                var path = key?.GetValue("SteamPath") as string;
                return string.IsNullOrEmpty(path) ? null : path.Replace('/', '\\');
            }
        }

        /// <summary>
        /// Whether Steam marks the app as updating in the registry, which it keeps more current than the manifest it
        /// rewrites only every so often: a second witness for a download that's just started.
        /// </summary>
        private static bool IsUpdating(uint appId)
        {
            using (var key = Registry.CurrentUser.OpenSubKey($@"Software\Valve\Steam\Apps\{appId}"))
            {
                return key?.GetValue("Updating") is int updating && updating == 1;
            }
        }

        /// <summary>
        /// The first value of each of the keys in a Valve KeyValues file (an .acf or .vdf), by key. Enough for the
        /// manifest's top-level numbers, whose keys appear nowhere else in it; nesting isn't followed.
        /// </summary>
        private static Dictionary<string, string> Values(string path, params string[] keys)
        {
            var wanted = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(path))
            {
                if (TryKeyValue(line, out var key, out var value) && wanted.Contains(key) && !values.ContainsKey(key))
                {
                    values[key] = value;
                    if (values.Count == wanted.Count)
                    {
                        break;
                    }
                }
            }

            return values;
        }

        /// <summary>A key's value as a number; 0 when it's missing or not a number, as for a total Steam doesn't know yet.</summary>
        private static long Number(Dictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var value) && long.TryParse(value, out var number) ? number : 0;
        }

        /// <summary>
        /// A <c>"key" "value"</c> line of a KeyValues file, its escaped backslashes undone; false for any other line,
        /// such as a section's name or brace.
        /// </summary>
        private static bool TryKeyValue(string line, out string key, out string value)
        {
            key = value = null;
            var keyStart = line.IndexOf('"');
            var keyEnd = keyStart < 0 ? -1 : line.IndexOf('"', keyStart + 1);
            var valueStart = keyEnd < 0 ? -1 : line.IndexOf('"', keyEnd + 1);
            var valueEnd = valueStart < 0 ? -1 : line.LastIndexOf('"');
            if (valueEnd <= valueStart)
            {
                return false;
            }

            key = line.Substring(keyStart + 1, keyEnd - keyStart - 1);
            value = line.Substring(valueStart + 1, valueEnd - valueStart - 1).Replace(@"\\", @"\");
            return true;
        }
    }
}
