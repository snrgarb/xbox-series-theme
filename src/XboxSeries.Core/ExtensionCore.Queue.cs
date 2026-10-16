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

        /// <summary>The Toast's line as a queued game finishes installing.</summary>
        private const string ReadyToPlay = "Ready to play";

        /// <summary>How many covers Manage Queue's Recently installed row holds.</summary>
        private const int RecentlyInstalledCount = 8;

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

        /// <summary>Whether the queue has been built from a library snapshot yet: what it holds then gets no Queued Toast.</summary>
        private bool queueSeen;

        /// <summary>The games the queue has held, whose install finishing shows a Ready to play Toast.</summary>
        private readonly HashSet<Guid> everQueued = new HashSet<Guid>();

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

        /// <summary>
        /// The installs Playnite started that the client can follow but hasn't reported yet, with when each was first
        /// seen: Steam writes nothing for a game while its install dialog waits, and nothing ever once that is cancelled.
        /// </summary>
        private readonly Dictionary<Guid, DateTime> playniteInstallsWaiting = new Dictionary<Guid, DateTime>();

        /// <summary>The installs Playnite started that the client has reported, so one it stops reporting has ended.</summary>
        private readonly HashSet<Guid> playniteInstallsReported = new HashSet<Guid>();

        /// <summary>The installs Playnite started that have been marked cancelled, until Playnite's snapshot agrees.</summary>
        private readonly HashSet<Guid> playniteInstallsCancelled = new HashSet<Guid>();

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
        /// What the extension's own main action on the game's Game hub reads: "Install" while <see cref="CanInstallSilently"/>;
        /// "Queued" or "Installing..." while the queue holds an install Playnite didn't start, which Playnite's own action
        /// would offer to start again; null where Playnite's own action stands (any other game).
        /// </summary>
        public string InstallActionLabel(Guid gameId)
        {
            if (CanInstallSilently(gameId))
            {
                return "Install";
            }

            var game = library.Games.FirstOrDefault(g => g.Id == gameId);
            return game != null && !game.Installing && installOrder.Contains(gameId) ? StatusOf(gameId) : null;
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
            FollowPlayniteInstalls(clientInstalling);
            foreach (var reported in installsAsked.Keys.Where(clientInstalling.Contains).ToList())
            {
                installsAsked.Remove(reported);
            }

            // One a snapshot shows installed got there between two reads, as a small game can in a few seconds.
            foreach (var installed in library.Games.Where(g => g.Installed && installsAsked.ContainsKey(g.Id)))
            {
                installsAsked.Remove(installed.Id);
                HeadRecentlyInstalled(installed.Id);
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
            var joined = installing.Keys.Where(id => !installOrder.Contains(id)).ToList();
            installOrder.AddRange(joined);
            everQueued.UnionWith(joined);

            // Each game joining tells of it, as the console does; those already installing as Playnite started had
            // joined before the extension was there to see it.
            if (queueSeen)
            {
                foreach (var id in joined)
                {
                    EnqueueToast(new WaitingToast(Queued, ToastKind.Queued, installing[id].CoverImage));
                }
            }

            queueSeen = true;

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
                HeadRecentlyInstalled(finished);
            }

            clientReported.UnionWith(clientInstalling.Where(id => queued.TryGetValue(id, out var game) && !game.Installing));
        }

        /// <summary>
        /// The installs Playnite started that the client can follow. Playnite's own Steam install watches only for the
        /// game installed, so one cancelled in Steam, at its install dialog or mid-download, would read installing for
        /// ever: one the client stops reporting, or says nothing of a minute on, is marked cancelled, once, and leaves
        /// the queue with the snapshot that follows. Marking does nothing while the client has any of it, as when it
        /// has just finished, which Playnite's watch then finds.
        /// </summary>
        private void FollowPlayniteInstalls(HashSet<Guid> clientInstalling)
        {
            var followed = new HashSet<Guid>(library.Games.Where(g => g.Installing && downloads.ProgressOf(g.Id) != null).Select(g => g.Id));
            foreach (var gone in playniteInstallsWaiting.Keys.Where(id => !followed.Contains(id)).ToList())
            {
                playniteInstallsWaiting.Remove(gone);
            }

            playniteInstallsReported.IntersectWith(followed);
            playniteInstallsCancelled.IntersectWith(followed);

            foreach (var id in followed.Where(id => !playniteInstallsCancelled.Contains(id)))
            {
                if (clientInstalling.Contains(id))
                {
                    playniteInstallsReported.Add(id);
                    playniteInstallsWaiting.Remove(id);
                    continue;
                }

                if (!playniteInstallsReported.Remove(id))
                {
                    if (!playniteInstallsWaiting.TryGetValue(id, out var since))
                    {
                        playniteInstallsWaiting[id] = clock.Now;
                        continue;
                    }

                    if (clock.Now - since < AskedInstallGivesUpAfter)
                    {
                        continue;
                    }
                }

                playniteInstallsWaiting.Remove(id);
                playniteInstallsCancelled.Add(id);
                downloads.MarkCancelled(id);
            }
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
                HeadRecentlyInstalled(finished.Id);
            }
        }

        /// <summary>
        /// Each game the queue held that a snapshot shows installed, as it wasn't in the one before, tells of it with a
        /// Ready to play Toast, once, however it was installed: by Playnite, or by its client alone, which the next
        /// snapshot shows once the extension has marked it installed. One the queue let go of uninstalled, as cancelled,
        /// still tells of it should its client install it later.
        /// </summary>
        private void ToastFinishedInstalls(LibrarySnapshot previous)
        {
            var wasInstalled = new HashSet<Guid>(previous.Games.Where(g => g.Installed).Select(g => g.Id));
            foreach (var finished in library.Games.Where(g => g.Installed && !g.Installing && everQueued.Contains(g.Id) && !wasInstalled.Contains(g.Id)).ToList())
            {
                everQueued.Remove(finished.Id);
                EnqueueToast(new WaitingToast(ReadyToPlay, ToastKind.Installed, finished.CoverImage, finished.Id));
            }
        }

        /// <summary>A game seen to finish installing heads Recently installed, once only, however it was seen to finish.</summary>
        private void HeadRecentlyInstalled(Guid gameId)
        {
            installedThisSession.Remove(gameId);
            installedThisSession.Insert(0, gameId);
        }

        /// <summary>
        /// Manage Queue's Recently installed: games seen to finish installing since Playnite started, the latest first,
        /// then the rest by when they were installed, as near as their install folders tell, and by when they were added
        /// for one whose folder can't say.
        /// </summary>
        private void RefreshRecentlyInstalled()
        {
            var installed = GameListFilter.Installed.Apply(library.Games).ToList();
            var finished = installedThisSession.Select(id => installed.FirstOrDefault(g => g.Id == id)).Where(g => g != null);
            MyGames.RecentlyInstalled = finished
                .Concat(installed.OrderByDescending(g => g.InstalledAt ?? g.Added ?? DateTime.MinValue))
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
