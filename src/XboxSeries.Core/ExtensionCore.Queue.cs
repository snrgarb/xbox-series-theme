using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The queue: the games Playnite is installing, and those its client (Steam) is installing without Playnite knowing,
    /// as My games &amp; apps' nav, Manage and Manage Queue show them, with each download's progress as the client reports it.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The states a queued game's row and tile read, as the console words them.</summary>
        private const string Installing = "Installing...";
        private const string Queued = "Queued";

        /// <summary>How many covers Manage Queue's Recently installed row holds.</summary>
        private const int RecentlyInstalledCount = 10;

        /// <summary>How often the queue reads its downloads' progress again while anything is installing.</summary>
        private static readonly TimeSpan QueuePoll = TimeSpan.FromSeconds(1);

        /// <summary>How long a game the client was asked to install can go unreported before the queue gives up on it.</summary>
        private static readonly TimeSpan AskedInstallGivesUpAfter = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How long a download's bytes can stand still before its speed reads 0 Kbps. Steam writes its progress only
        /// every few seconds, so a second without a change is no sign it has stalled.
        /// </summary>
        private static readonly TimeSpan StalledAfter = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The games installing, in the order the core first saw each start. Steam's own queue order can't be read from
        /// outside (its manifests don't carry it), so this stands in for "next up" behind the current download.
        /// </summary>
        private readonly List<Guid> installOrder = new List<Guid>();

        /// <summary>Each downloading game's bytes when they last moved, and the speed they moved at; none while queued.</summary>
        private readonly Dictionary<Guid, SpeedSample> speedSamples = new Dictionary<Guid, SpeedSample>();

        /// <summary>Whether a library snapshot has come in yet: the first one starts no install.</summary>
        private bool librarySeen;

        /// <summary>The games seen to finish installing since Playnite started, the latest first.</summary>
        private readonly List<Guid> installedThisSession = new List<Guid>();

        /// <summary>What each installing game's tile reads, by game, as the queue last found it.</summary>
        private Dictionary<Guid, string> tileStatuses = new Dictionary<Guid, string>();

        /// <summary>
        /// The games <see cref="InstallGame"/> asked the client to install that it hasn't yet reported installing, with
        /// when each was asked. The client writes nothing for a moment after it is asked, so they are queued meanwhile.
        /// </summary>
        private readonly Dictionary<Guid, DateTime> installsAsked = new Dictionary<Guid, DateTime>();

        /// <summary>The queued games the client has reported installing that Playnite isn't, to mark installed once it stops.</summary>
        private readonly HashSet<Guid> clientReported = new HashSet<Guid>();

        /// <summary>The next read of the queue's progress, while anything is installing; null otherwise.</summary>
        private IDisposable queueTimer;

        /// <summary>
        /// Whether the Game hub's Install installs the game through its client without the client's own windows (a Steam
        /// game not installed, installing or queued), in place of Playnite's own Install.
        /// </summary>
        public bool CanInstallSilently(Guid gameId)
        {
            var game = library.Games.FirstOrDefault(g => g.Id == gameId);
            return game != null && !game.Installed && !game.Installing && !installOrder.Contains(gameId) && downloads.CanInstall(gameId);
        }

        /// <summary>
        /// The Game hub's Install, for a game <see cref="CanInstallSilently"/>: asks its client to install it, puts it in
        /// the queue as Queued, and goes back from the hub to where it was opened from, as the console does.
        /// </summary>
        public void InstallGame(Guid gameId)
        {
            if (!CanInstallSilently(gameId))
            {
                return;
            }

            downloads.Install(gameId);
            installsAsked[gameId] = clock.Now;
            RefreshQueue();
            RefreshHome();

            // Playnite never sees this install start, so FollowInstalls won't close the hub: it is closed here.
            playnite.CloseGameHub(gameId);
        }

        /// <summary>
        /// Rebuilds the queue from the library and each game's progress now: whatever is downloading first, then the
        /// rest in the order they started installing. While anything is installing, it is read again every second.
        /// Returns whether any game's tile now reads differently ("Queued", "Installing..." or neither).
        /// </summary>
        private bool RefreshQueue()
        {
            // The client's own installs are read on each snapshot and each poll. One started in the client while nothing
            // is queued has no poll to find it, so it joins with the next library snapshot: Playnite sends one often
            // enough that polling the client all the time isn't worth it.
            var clientInstalling = new HashSet<Guid>(downloads.Installing());
            foreach (var reported in installsAsked.Keys.Where(clientInstalling.Contains).ToList())
            {
                installsAsked.Remove(reported);
            }

            // One a snapshot shows installed got there between two reads, as a small game can in a few seconds.
            foreach (var installed in library.Games.Where(g => g.Installed && installsAsked.ContainsKey(g.Id)))
            {
                installsAsked.Remove(installed.Id);
                installedThisSession.Remove(installed.Id);
                installedThisSession.Insert(0, installed.Id);
            }

            // One the client says nothing of a minute on, not even its progress, it won't install (it isn't owned, or
            // the client refused): it leaves the queue rather than read Queued for ever. Steam's adapter reports a game
            // it has written nothing for as waiting with 0 of 0 bytes, which is no sign of an install either.
            foreach (var refused in installsAsked.Where(a => clock.Now - a.Value >= AskedInstallGivesUpAfter && !HasStarted(downloads.ProgressOf(a.Key))).ToList())
            {
                installsAsked.Remove(refused.Key);
            }

            var installing = library.Games
                .Where(g => g.Installing || (!g.Installed && (clientInstalling.Contains(g.Id) || installsAsked.ContainsKey(g.Id))))
                .ToDictionary(g => g.Id);
            FollowClientInstalls(installing, clientInstalling);
            installOrder.RemoveAll(id => !installing.ContainsKey(id));
            installOrder.AddRange(installing.Keys.Where(id => !installOrder.Contains(id)).ToList());

            // A game the client was asked to install that it says nothing of yet is waiting its turn: queued.
            var progress = installOrder.ToDictionary(
                id => id,
                id => downloads.ProgressOf(id) ?? (installing[id].Installing ? null : new DownloadProgress(false, 0, 0)));
            foreach (var gone in speedSamples.Keys.Where(id => !progress.TryGetValue(id, out var p) || p?.IsActive != true).ToList())
            {
                speedSamples.Remove(gone);
            }

            // A game whose library can't say how far it has got is taken at Playnite's word: installing.
            var statuses = progress.ToDictionary(p => p.Key, p => p.Value == null || p.Value.IsActive ? Installing : Queued);
            var changed = statuses.Count != tileStatuses.Count
                || statuses.Any(s => !tileStatuses.TryGetValue(s.Key, out var was) || was != s.Value);
            tileStatuses = statuses;

            var tiles = MyGames.Queue.ToDictionary(e => e.Tile.GameId, e => e.Tile);
            MyGames.Queue = installOrder
                .OrderByDescending(id => progress[id]?.IsActive == true)
                .Select((id, i) => EntryOf(installing[id], progress[id], i == 0, tiles.TryGetValue(id, out var was) ? was : null))
                .ToList();

            if (installOrder.Count == 0)
            {
                queueTimer?.Dispose();
                queueTimer = null;
            }
            else if (queueTimer == null)
            {
                queueTimer = clock.After(QueuePoll, PollQueue);
            }

            return changed;
        }

        /// <summary>
        /// The installs the client reported that Playnite doesn't know of: one the client stops reporting has finished
        /// (or was cancelled), and Playnite, which never saw it start, won't notice. It is marked installed, which does
        /// nothing if the client didn't install it, and heads Recently installed once a snapshot shows it installed.
        /// A game Playnite itself is installing is left to Playnite.
        /// </summary>
        private void FollowClientInstalls(Dictionary<Guid, LibraryGame> queued, HashSet<Guid> clientInstalling)
        {
            clientReported.RemoveWhere(id => queued.TryGetValue(id, out var game) && game.Installing);
            foreach (var finished in clientReported.Where(id => !clientInstalling.Contains(id)).ToList())
            {
                clientReported.Remove(finished);
                downloads.MarkInstalled(finished);
                installedThisSession.Remove(finished);
                installedThisSession.Insert(0, finished);
            }

            clientReported.UnionWith(clientInstalling.Where(id => queued.TryGetValue(id, out var game) && !game.Installing));
        }

        /// <summary>
        /// The queue's second is up: its progress is read again, and the storage ring with it while the page shows it,
        /// as the download takes the drive's space. Home's rows are only rebuilt when a tile's state changed, since
        /// rebuilding them loads every tile's art again.
        /// </summary>
        private void PollQueue()
        {
            queueTimer = null;
            if (RefreshQueue())
            {
                RefreshHome();
            }

            // A small game can install between two reads, so the client never reports it installing: each one asked for
            // and not yet reported is marked installed, which does nothing until the client has installed it. The
            // snapshot that follows shows it installed, and takes it from the queue.
            foreach (var asked in installsAsked.Keys.ToList())
            {
                downloads.MarkInstalled(asked);
            }

            if (MyGames.IsShown)
            {
                RefreshStorage();
            }
        }

        /// <summary>
        /// What changed in the installs since the last snapshot. A game installing that wasn't means the user has just
        /// pressed Install on its Game hub, which, as on the console, goes back to where it was opened from; the first
        /// snapshot is only what was already installing as Playnite started. A game installed that was installing has
        /// finished, and heads Recently installed.
        /// </summary>
        private void FollowInstalls(LibrarySnapshot previous)
        {
            var wasInstalling = new HashSet<Guid>(previous.Games.Where(g => g.Installing).Select(g => g.Id));
            if (librarySeen)
            {
                foreach (var started in library.Games.Where(g => g.Installing && !wasInstalling.Contains(g.Id)))
                {
                    playnite.CloseGameHub(started.Id);
                }
            }

            librarySeen = true;
            foreach (var finished in library.Games.Where(g => g.Installed && !g.Installing && wasInstalling.Contains(g.Id)))
            {
                installedThisSession.Remove(finished.Id);
                installedThisSession.Insert(0, finished.Id);
            }
        }

        /// <summary>
        /// Manage Queue's Recently installed: games seen to finish installing since Playnite started, the latest first,
        /// then the rest by when they were added, as the library can't say when a game was installed.
        /// </summary>
        private void RefreshRecentlyInstalled()
        {
            var installed = GameListFilter.Installed.Apply(library.Games).ToList();
            var finished = installedThisSession.Select(id => installed.FirstOrDefault(g => g.Id == id)).Where(g => g != null);
            MyGames.RecentlyInstalled = finished
                .Concat(installed.OrderByDescending(g => g.Added ?? DateTime.MinValue))
                .Distinct()
                .Take(RecentlyInstalledCount)
                .Select(TileOf)
                .ToList();
        }

        /// <summary>What a game's tile reads of its install: "Queued", "Installing..." or, when it isn't installing, null.</summary>
        private string StatusOf(Guid gameId) => tileStatuses.TryGetValue(gameId, out var status) ? status : null;

        /// <summary>
        /// A game's row in the queue. Downloading, it shows its amount, share and speed; queued behind another, only 0.0%.
        /// A game whose library can't say how far it has got (any but Steam) still reads
        /// "Installing...", without numbers, as Playnite says it is installing. Its tile is <paramref name="lastTile"/>,
        /// its row's tile until now, while that still reads the same.
        /// </summary>
        private QueueEntry EntryOf(LibraryGame game, DownloadProgress progress, bool first, HomeTile lastTile)
        {
            // The row keeps its tile while the tile reads the same: a new one would take the row's focus and load its art again.
            var tile = TileOf(game);
            if (lastTile != null && SameTile(lastTile, tile))
            {
                tile = lastTile;
            }

            if (progress == null)
            {
                return new QueueEntry(tile, first, Installing, null, null, null, 0);
            }

            // As on the console, a game waiting its turn reads 0.0% with an empty bar, whatever of it is already down.
            if (!progress.IsActive)
            {
                return new QueueEntry(tile, false, Queued, null, null, PercentOf(0), 0);
            }

            var fraction = progress.BytesTotal <= 0 ? 0 : Math.Max(0, Math.Min(1, (double)progress.BytesDownloaded / progress.BytesTotal));
            var percent = PercentOf(fraction);

            var amount = progress.BytesTotal <= 0 ? null
                : StorageRing.Size(progress.BytesDownloaded, 2) + " of " + StorageRing.Size(progress.BytesTotal, 2);
            return new QueueEntry(tile, first, Installing, SpeedOf(game.Id, progress.BytesDownloaded), amount, percent, fraction);
        }

        /// <summary>
        /// A download's speed: the bytes gained since they last moved, over the time since. Steam writes its progress
        /// every few seconds, not every second, so a read that finds them where they were keeps the last speed rather
        /// than flicker to nothing; only once they have stood still a while does it read 0 Kbps. Null until they first move.
        /// </summary>
        private string SpeedOf(Guid gameId, long bytes)
        {
            var now = clock.Now;
            if (!speedSamples.TryGetValue(gameId, out var last))
            {
                speedSamples[gameId] = new SpeedSample(bytes, now, null);
                return null;
            }

            var elapsed = (now - last.At).TotalSeconds;
            if (bytes == last.Bytes)
            {
                return now - last.At > StalledAfter ? Speed(0) : last.Speed;
            }

            if (elapsed <= 0)
            {
                return last.Speed;
            }

            var speed = Speed(Math.Max(0, bytes - last.Bytes) * 8 / elapsed);
            speedSamples[gameId] = new SpeedSample(bytes, now, speed);
            return speed;
        }

        /// <summary>Whether the client has begun on a download: it is downloading it, or knows its size.</summary>
        private static bool HasStarted(DownloadProgress progress) =>
            progress != null && (progress.IsActive || progress.BytesTotal > 0 || progress.BytesDownloaded > 0);

        /// <summary>A share as the console shows it, to a tenth: "1.9%".</summary>
        private static string PercentOf(double fraction) => (fraction * 100).ToString("0.0", CultureInfo.CurrentCulture) + "%";

        /// <summary>Whether two tiles show the same: same game, name, art, source, running mark and install state.</summary>
        private static bool SameTile(HomeTile a, HomeTile b) =>
            a.GameId == b.GameId
            && a.Title == b.Title
            && a.CoverImage == b.CoverImage
            && a.Source == b.Source
            && a.IsRunning == b.IsRunning
            && a.Status == b.Status;

        /// <summary>A speed as the console shows it, in decimal units, whole: "323 Kbps" below 1 Mbps, else "106 Mbps".</summary>
        private static string Speed(double bitsPerSecond)
        {
            var culture = CultureInfo.CurrentCulture;
            return bitsPerSecond < 1_000_000 ? (bitsPerSecond / 1_000).ToString("0", culture) + " Kbps"
                : (bitsPerSecond / 1_000_000).ToString("0", culture) + " Mbps";
        }

        /// <summary>A downloading game's bytes when they last moved, when that was, and the speed they moved at.</summary>
        private sealed class SpeedSample
        {
            public SpeedSample(long bytes, DateTime at, string speed)
            {
                Bytes = bytes;
                At = at;
                Speed = speed;
            }

            public long Bytes { get; }
            public DateTime At { get; }
            public string Speed { get; }
        }
    }
}
