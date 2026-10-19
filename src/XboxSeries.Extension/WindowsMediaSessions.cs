using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Playnite.SDK;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media.Control;
using Windows.Storage.Streams;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: Windows' media sessions, every app's and the current one its media flyout shows, through the Global
    /// System Media Transport Controls (Windows.Media.Control). It follows every session, which Windows names current,
    /// and what each plays, from <see cref="Start"/> until <see cref="Dispose"/>, raising <see cref="Changed"/> on a
    /// thread of its own as any of that changes. On Windows older than 10 1809, which hasn't the API, it does nothing and
    /// there is never a session.
    /// </summary>
    internal sealed class WindowsMediaSessions : IMediaSessions, IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// The one that talks to Windows, made only where Windows has the API, so that its types are never loaded
        /// where they would fail to; null before <see cref="Start"/>, where Windows hasn't it, and once disposed.
        /// </summary>
        private Follower follower;

        private volatile MediaSession current;

        private volatile IReadOnlyList<MediaSession> sessions = new MediaSession[0];

        /// <summary>Raised, on a thread of its own, when <see cref="Current"/> or <see cref="Sessions"/> has changed.</summary>
        public event Action Changed;

        /// <summary>
        /// Raised, on a thread of its own, with the art a <see cref="FetchArt"/> asked for, by its request number: a
        /// frozen bitmap, or null for none.
        /// </summary>
        public event Action<int, object> ArtFetched;

        public MediaSession Current => current;

        public IReadOnlyList<MediaSession> Sessions => sessions;

        public void Send(MediaCommand command, string appId)
        {
            var following = follower;
            if (following == null)
            {
                logger.Warn($"Now Playing: no media sessions to ask {appId} to {command}");
                return;
            }

            following.Send(command, appId);
        }

        /// <summary>
        /// Reads and decodes the art of the app with <paramref name="appId"/>, off the calling thread, answering through
        /// <see cref="ArtFetched"/>; never where Windows hasn't the API, as there is never a session to ask for it.
        /// </summary>
        public void FetchArt(string appId, int request) => follower?.FetchArt(appId, request, (r, art) => ArtFetched?.Invoke(r, art));

        /// <summary>Starts following Windows' media sessions, where Windows has the API.</summary>
        public void Start()
        {
            if (!IsSupported())
            {
                logger.Info("Windows has no media sessions to follow (it needs Windows 10 1809); Now Playing says no music app is running");
                return;
            }

            StartFollowing();
        }

        /// <summary>Stops following, every one of Windows' handlers unsubscribed. Safe to call more than once.</summary>
        public void Dispose()
        {
            var stopping = follower;
            follower = null;
            stopping?.Dispose();
        }

        private static bool IsSupported()
        {
            try
            {
                return ApiInformation.IsTypePresent("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager");
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return false;
            }
        }

        // Not inlined into Start, so that Follower and the API's types load only once Windows is known to have them.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StartFollowing()
        {
            follower = new Follower(Publish);
            follower.Refresh("start");
        }

        /// <summary>
        /// The sessions as just read: <see cref="Current"/> and <see cref="Sessions"/>, and <see cref="Changed"/> raised
        /// if either differs. Called by one refresh at a time.
        /// </summary>
        private void Publish(MediaSession session, IReadOnlyList<MediaSession> all)
        {
            var last = current;
            var lastAll = sessions;
            current = session;
            sessions = all;
            if (!Same(last, session) || lastAll.Count != all.Count || lastAll.Where((s, i) => !Same(s, all[i])).Any())
            {
                Changed?.Invoke();
            }
        }

        private static bool Same(MediaSession a, MediaSession b) =>
            a == null || b == null
                ? a == b
                : a.AppId == b.AppId && a.Title == b.Title && a.Artist == b.Artist && a.IsPlaying == b.IsPlaying
                    && a.CanTogglePlayPause == b.CanTogglePlayPause && a.CanGoPrevious == b.CanGoPrevious && a.CanGoNext == b.CanGoNext
                    && a.Position == b.Position && a.Duration == b.Duration && a.LastUpdated == b.LastUpdated
                    && a.HasArt == b.HasArt;

        /// <summary>
        /// Follows every one of Windows' media sessions. Every refresh, whatever asked for it, runs alone and in turn, off
        /// the UI thread: it lists the sessions, lets go of those gone, subscribes to those new, and reads each, noting
        /// the one Windows names current. A session whose app closes as it is read throws; that is logged and the session
        /// left out, the sessions changing bringing another refresh. Should the list itself fail, every session is let go
        /// and listed again.
        /// </summary>
        private sealed class Follower : IDisposable
        {
            /// <summary>
            /// The width the art is decoded at: the card's 90 px square at twice its size, as the Guide decodes covers,
            /// so it is sharp on a 4K screen.
            /// </summary>
            private const int ArtWidth = 180;

            private readonly Action<MediaSession, IReadOnlyList<MediaSession>> publish;

            /// <summary>Lets one refresh run at a time, so the handlers are only ever moved by one.</summary>
            private readonly SemaphoreSlim turn = new SemaphoreSlim(1, 1);

            /// <summary>Guards subscribing against <see cref="Dispose"/>, which may come on another thread mid-refresh.</summary>
            private readonly object sync = new object();

            private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, CurrentSessionChangedEventArgs> onCurrentChanged;
            private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, SessionsChangedEventArgs> onSessionsChanged;
            private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs> onMediaChanged;
            private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs> onPlaybackChanged;
            private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSession, TimelinePropertiesChangedEventArgs> onTimelineChanged;

            /// <summary>Windows' session manager, asked for once and kept; null until it has answered.</summary>
            private GlobalSystemMediaTransportControlsSessionManager manager;

            /// <summary>Every session whose handlers are subscribed, with its app's id, for the log.</summary>
            private readonly Dictionary<GlobalSystemMediaTransportControlsSession, string> followed =
                new Dictionary<GlobalSystemMediaTransportControlsSession, string>();

            private bool disposed;

            public Follower(Action<MediaSession, IReadOnlyList<MediaSession>> publish)
            {
                this.publish = publish;
                onCurrentChanged = (_, __) => Refresh("the current media session changed");
                onSessionsChanged = (_, __) => Refresh("media sessions came or went");
                onMediaChanged = (changed, __) => Refresh($"{AppOf(changed)} changed what it plays");
                onPlaybackChanged = (changed, __) => Refresh($"{AppOf(changed)} played, paused or stopped");
                onTimelineChanged = (changed, __) => Refresh($"{AppOf(changed)} moved in its track");
            }

            /// <summary>Reads the sessions again, in turn, off the calling thread. Never throws.</summary>
            public void Refresh(string why)
            {
                logger.Info($"Now Playing: {why}");
                Task.Run(RefreshInTurn);
            }

            public void Dispose()
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    UnfollowAll();
                    if (manager != null)
                    {
                        Unsubscribe("Windows' session manager", () =>
                        {
                            manager.CurrentSessionChanged -= onCurrentChanged;
                            manager.SessionsChanged -= onSessionsChanged;
                        });
                        manager = null;
                    }
                }
            }

            private async Task RefreshInTurn()
            {
                try
                {
                    await turn.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        await RefreshNow().ConfigureAwait(false);
                    }
                    finally
                    {
                        turn.Release();
                    }
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Error(e, "Now Playing couldn't read Windows' media sessions");
                }
            }

            private async Task RefreshNow()
            {
                // Twice at most: once more, every session listed afresh, if listing them failed.
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    try
                    {
                        if (!await HaveManager().ConfigureAwait(false))
                        {
                            return;
                        }

                        var all = manager?.GetSessions()?.ToList() ?? new List<GlobalSystemMediaTransportControlsSession>();
                        var currentSession = manager?.GetCurrentSession();
                        if (!Follow(all))
                        {
                            return;
                        }

                        var reads = new List<MediaSession>();
                        MediaSession currentRead = null;
                        foreach (var each in all)
                        {
                            var read = await ReadOrSkip(each).ConfigureAwait(false);
                            if (read == null)
                            {
                                continue;
                            }

                            reads.Add(read);
                            if (ReferenceEquals(each, currentSession))
                            {
                                currentRead = read;
                            }
                        }

                        // The current session as a separate wrapper of the same one: matched by its app.
                        if (currentRead == null && currentSession != null)
                        {
                            var currentApp = AppOf(currentSession);
                            currentRead = reads.FirstOrDefault(r => r.AppId == currentApp);
                        }

                        if (!IsDisposed)
                        {
                            publish(currentRead, reads);
                        }

                        return;
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException))
                    {
                        logger.Warn($"Now Playing: Windows' media sessions couldn't be listed ({e.Message}); listing them again");
                        lock (sync)
                        {
                            UnfollowAll();
                        }
                    }
                }

                if (!IsDisposed)
                {
                    publish(null, new MediaSession[0]);
                }
            }

            /// <summary>
            /// What <paramref name="session"/> plays now; null when its app closed as it was read, which is logged, the
            /// sessions changing bringing another refresh.
            /// </summary>
            private async Task<MediaSession> ReadOrSkip(GlobalSystemMediaTransportControlsSession session)
            {
                try
                {
                    return await Read(session).ConfigureAwait(false);
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Warn($"Now Playing: the media session of {NameOf(session)} went as it was read ({e.Message}); leaving it out");
                    return null;
                }
            }

            private bool IsDisposed
            {
                get
                {
                    lock (sync)
                    {
                        return disposed;
                    }
                }
            }

            /// <summary>Asks Windows for its session manager the first time, and subscribes to it; false once disposed.</summary>
            private async Task<bool> HaveManager()
            {
                if (manager != null)
                {
                    return true;
                }

                var requested = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().ConfigureAwait(false);
                lock (sync)
                {
                    if (disposed)
                    {
                        return false;
                    }

                    manager = requested;
                    manager.CurrentSessionChanged += onCurrentChanged;
                    manager.SessionsChanged += onSessionsChanged;
                    return true;
                }
            }

            /// <summary>
            /// Moves the session handlers onto <paramref name="all"/>: those followed but no longer listed are let go,
            /// and those listed but not yet followed subscribed to; false once disposed, when nothing is subscribed.
            /// </summary>
            private bool Follow(IReadOnlyList<GlobalSystemMediaTransportControlsSession> all)
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        return false;
                    }

                    foreach (var gone in followed.Keys.Where(f => !all.Any(s => ReferenceEquals(s, f))).ToList())
                    {
                        Unfollow(gone);
                    }

                    foreach (var next in all.Where(s => s != null && !followed.ContainsKey(s)))
                    {
                        // Followed before it is subscribed to, so that if its app closes midway, what was subscribed is let go.
                        var app = NameOf(next);
                        followed[next] = app;
                        next.MediaPropertiesChanged += onMediaChanged;
                        next.PlaybackInfoChanged += onPlaybackChanged;
                        next.TimelinePropertiesChanged += onTimelineChanged;
                        logger.Info($"Now Playing: following the media session of {app}");
                    }

                    if (followed.Count == 0)
                    {
                        logger.Info("Now Playing: no media session");
                    }

                    return true;
                }
            }

            /// <summary>Unsubscribes from <paramref name="last"/>, a session followed. Called holding <see cref="sync"/>.</summary>
            private void Unfollow(GlobalSystemMediaTransportControlsSession last)
            {
                var app = followed[last];
                followed.Remove(last);
                Unsubscribe(app, () =>
                {
                    last.MediaPropertiesChanged -= onMediaChanged;
                    last.PlaybackInfoChanged -= onPlaybackChanged;
                    last.TimelinePropertiesChanged -= onTimelineChanged;
                });
                logger.Info($"Now Playing: no longer following the media session of {app}");
            }

            /// <summary>Unsubscribes from every session followed. Called holding <see cref="sync"/>.</summary>
            private void UnfollowAll()
            {
                foreach (var last in followed.Keys.ToList())
                {
                    Unfollow(last);
                }
            }

            /// <summary>
            /// Unsubscribes, a session or the manager whose app has already gone included: Windows has dropped the
            /// handlers with it, so a failure here is no error.
            /// </summary>
            private void Unsubscribe(string app, Action unsubscribe)
            {
                try
                {
                    unsubscribe();
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Info($"Now Playing: {app} had gone as its media session was let go ({e.Message})");
                }
            }

            /// <summary>The app id of a session followed, for the log, as noted when it was followed; else read from it.</summary>
            private string AppOf(GlobalSystemMediaTransportControlsSession changed)
            {
                lock (sync)
                {
                    if (changed != null && followed.TryGetValue(changed, out var app))
                    {
                        return app;
                    }
                }

                return NameOf(changed);
            }

            /// <summary>The session's app id, or "an app" when its app has gone and it can't be asked.</summary>
            private static string NameOf(GlobalSystemMediaTransportControlsSession session)
            {
                try
                {
                    return session?.SourceAppUserModelId ?? "an app";
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    return "an app";
                }
            }

            /// <summary>What <paramref name="playing"/> plays now; its art is fetched apart (<see cref="FetchArt"/>).</summary>
            private async Task<MediaSession> Read(GlobalSystemMediaTransportControlsSession playing)
            {
                var app = playing.SourceAppUserModelId;
                var properties = await playing.TryGetMediaPropertiesAsync().AsTask().ConfigureAwait(false);
                var playback = playing.GetPlaybackInfo();
                var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var controls = playback?.Controls;
                var timeline = playing.GetTimelineProperties();
                var duration = timeline == null ? TimeSpan.Zero : timeline.EndTime - timeline.StartTime;
                var position = timeline == null ? TimeSpan.Zero : timeline.Position - timeline.StartTime;
                var title = properties?.Title;
                var artist = properties?.Artist;

                return new MediaSession(
                    app, title, artist, isPlaying,
                    canTogglePlayPause: controls?.IsPlayPauseToggleEnabled ?? false,
                    canGoPrevious: controls?.IsPreviousEnabled ?? false,
                    canGoNext: controls?.IsNextEnabled ?? false,
                    position: position < TimeSpan.Zero ? TimeSpan.Zero : position,
                    duration: duration < TimeSpan.Zero ? TimeSpan.Zero : duration,
                    lastUpdated: timeline?.LastUpdatedTime.LocalDateTime ?? default,
                    hasArt: properties?.Thumbnail != null);
            }

            /// <summary>The art, a frozen bitmap decoded at <see cref="ArtWidth"/>; null for none, or art that won't decode.</summary>
            private static async Task<BitmapSource> Decode(IRandomAccessStreamReference thumbnail)
            {
                if (thumbnail == null)
                {
                    return null;
                }

                try
                {
                    var bytes = new MemoryStream();
                    using (var stream = await thumbnail.OpenReadAsync().AsTask().ConfigureAwait(false))
                    using (var read = stream.AsStreamForRead())
                    {
                        await read.CopyToAsync(bytes).ConfigureAwait(false);
                    }

                    bytes.Position = 0;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = ArtWidth;
                    image.StreamSource = bytes;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Warn($"Now Playing: couldn't decode the album art ({e.Message})");
                    return null;
                }
            }

            /// <summary>
            /// Reads and decodes the art the session of <paramref name="appId"/> shows now, off the calling thread, and
            /// gives it to <paramref name="answer"/> with <paramref name="request"/>: null for none, the app gone, or
            /// art that won't decode. Never throws; nothing once disposed.
            /// </summary>
            public void FetchArt(string appId, int request, Action<int, object> answer)
            {
                Task.Run(async () =>
                {
                    object art = null;
                    try
                    {
                        var playing = SessionOf(appId);
                        if (playing != null)
                        {
                            var properties = await playing.TryGetMediaPropertiesAsync().AsTask().ConfigureAwait(false);
                            art = await Decode(properties?.Thumbnail).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException))
                    {
                        logger.Warn($"Now Playing: couldn't read the album art of {appId} ({e.Message})");
                    }

                    if (!IsDisposed)
                    {
                        answer(request, art);
                    }
                });
            }

            /// <summary>
            /// Asks the app with <paramref name="appId"/>, by its session among Windows' sessions now, to carry out
            /// <paramref name="command"/>, off the calling thread. Never throws: an app that refuses, has no session or
            /// closes as it is asked is logged, and what it does shows only as its session changes.
            /// </summary>
            public void Send(MediaCommand command, string appId)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var target = SessionOf(appId);
                        if (target == null)
                        {
                            logger.Warn($"Now Playing: {appId} has no media session to {command}");
                            return;
                        }

                        var asked = command == MediaCommand.Previous ? target.TrySkipPreviousAsync()
                            : command == MediaCommand.Next ? target.TrySkipNextAsync()
                            : target.TryTogglePlayPauseAsync();
                        if (await asked.AsTask().ConfigureAwait(false))
                        {
                            logger.Info($"Now Playing: asked {appId} to {command}");
                        }
                        else
                        {
                            logger.Warn($"Now Playing: {appId} refused to {command}");
                        }
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException))
                    {
                        // As the app closes mid-command.
                        logger.Warn($"Now Playing: {appId} failed to {command} ({e.Message})");
                    }
                });
            }

            /// <summary>The session of <paramref name="appId"/> among Windows' sessions now; null for none, or no manager yet.</summary>
            private GlobalSystemMediaTransportControlsSession SessionOf(string appId)
            {
                GlobalSystemMediaTransportControlsSessionManager asked;
                lock (sync)
                {
                    asked = manager;
                }

                return asked?.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == appId);
            }
        }
    }
}
