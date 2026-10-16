using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The queue: the games Playnite is installing, as My games &amp; apps' nav, Manage and Manage Queue show them, with
    /// each download's progress as its library's client reports it.
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

        /// <summary>The next read of the queue's progress, while anything is installing; null otherwise.</summary>
        private IDisposable queueTimer;

        /// <summary>
        /// Rebuilds the queue from the library and each game's progress now: whatever is downloading first, then the
        /// rest in the order they started installing. While anything is installing, it is read again every second.
        /// Returns whether any game's tile now reads differently ("Queued", "Installing..." or neither).
        /// </summary>
        private bool RefreshQueue()
        {
            var installing = library.Games.Where(g => g.Installing).ToDictionary(g => g.Id);
            installOrder.RemoveAll(id => !installing.ContainsKey(id));
            installOrder.AddRange(library.Games.Where(g => g.Installing && !installOrder.Contains(g.Id)).Select(g => g.Id));

            var progress = installOrder.ToDictionary(id => id, id => downloads.ProgressOf(id));
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
            if (librarySeen && library.Games.Any(g => g.Installing && !wasInstalling.Contains(g.Id)))
            {
                playnite.CloseGameHub();
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
        /// A game's row in the queue. Downloading, it shows its amount, share and speed; queued behind another, only its
        /// share (0.0% until it starts). A game whose library can't say how far it has got (any but Steam) still reads
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

            var fraction = progress.BytesTotal <= 0 ? 0 : Math.Max(0, Math.Min(1, (double)progress.BytesDownloaded / progress.BytesTotal));
            var percent = (fraction * 100).ToString("0.0", CultureInfo.CurrentCulture) + "%";
            if (!progress.IsActive)
            {
                return new QueueEntry(tile, false, Queued, null, null, percent, fraction);
            }

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
