using System;
using System.Collections.Generic;
using System.IO;
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
    /// Adapter: Windows' current media session, the one its media flyout shows, through the Global System Media
    /// Transport Controls (Windows.Media.Control). It follows the session Windows names current, and what it plays,
    /// from <see cref="Start"/> until <see cref="Dispose"/>, raising <see cref="Changed"/> on a thread of its own as any
    /// of that changes. On Windows older than 10 1809, which hasn't the API, it does nothing and there is never a session.
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

        /// <summary>Raised, on a thread of its own, when <see cref="Current"/> has changed.</summary>
        public event Action Changed;

        public MediaSession Current => current;

        // Stub (#75): every app's session, read and followed.
        public IReadOnlyList<MediaSession> Sessions => current == null ? new MediaSession[0] : new[] { current };

        // Stub (#73): asks the app to carry out the command.
        public void Send(MediaCommand command, string appId)
        {
        }

        // Stub (#74): reads and decodes the app's art, answering through ArtFetched.
        public void FetchArt(string appId, int request)
        {
        }

        /// <summary>Starts following Windows' current media session, where Windows has the API.</summary>
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

        /// <summary>The session as just read: <see cref="Current"/>, and <see cref="Changed"/> raised if it differs.</summary>
        private void Publish(MediaSession session)
        {
            var last = current;
            current = session;
            if (!Same(last, session))
            {
                Changed?.Invoke();
            }
        }

        private static bool Same(MediaSession a, MediaSession b) =>
            a == null || b == null
                ? a == b
                : a.AppId == b.AppId && a.Title == b.Title && a.Artist == b.Artist && a.IsPlaying == b.IsPlaying && a.Art == b.Art
                    && a.CanTogglePlayPause == b.CanTogglePlayPause && a.CanGoPrevious == b.CanGoPrevious && a.CanGoNext == b.CanGoNext
                    && a.Position == b.Position && a.Duration == b.Duration && a.LastUpdated == b.LastUpdated;

        /// <summary>
        /// Follows Windows' current media session. Every refresh, whatever asked for it, runs alone and in turn, off the
        /// UI thread: it picks the session Windows names current, moves the handlers onto it from the last, the last's
        /// unsubscribed first, and reads it. A session whose app closes as it is read throws; that is logged, the session
        /// dropped and the current one picked again.
        /// </summary>
        private sealed class Follower : IDisposable
        {
            /// <summary>
            /// The width the art is decoded at: the card's 90 px square at twice its size, as the Guide decodes covers,
            /// so it is sharp on a 4K screen.
            /// </summary>
            private const int ArtWidth = 180;

            private readonly Action<MediaSession> publish;

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

            /// <summary>The session whose handlers are subscribed; null for none.</summary>
            private GlobalSystemMediaTransportControlsSession session;
            private string sessionApp;
            private bool disposed;

            /// <summary>The art last decoded, for the track it was decoded for, so play and pause don't decode it again.</summary>
            private object art;
            private string artTrack;

            public Follower(Action<MediaSession> publish)
            {
                this.publish = publish;
                onCurrentChanged = (_, __) => Refresh("the current media session changed");
                onSessionsChanged = (_, __) => Refresh("media sessions came or went");
                onMediaChanged = (_, __) => Refresh($"{sessionApp} changed what it plays");
                onPlaybackChanged = (_, __) => Refresh($"{sessionApp} played, paused or stopped");
                onTimelineChanged = (_, __) => Refresh($"{sessionApp} moved in its track");
            }

            /// <summary>Reads the current session again, in turn, off the calling thread. Never throws.</summary>
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
                    Unfollow();
                    if (manager != null)
                    {
                        Unsubscribe(() =>
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
                    logger.Error(e, "Now Playing couldn't read Windows' media session");
                }
            }

            private async Task RefreshNow()
            {
                // Twice at most: once more, from a fresh pick, if the session went as it was read.
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

                        var next = manager?.GetCurrentSession();
                        if (!Follow(next))
                        {
                            return;
                        }

                        var read = next == null ? null : await Read(next).ConfigureAwait(false);
                        if (!IsDisposed)
                        {
                            publish(read);
                        }

                        return;
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException))
                    {
                        // As an app closes mid-call: its session is gone, so it is dropped and the current one picked again.
                        logger.Warn($"Now Playing: the media session of {sessionApp} went as it was read ({e.Message}); picking again");
                        lock (sync)
                        {
                            Unfollow();
                        }
                    }
                }

                if (!IsDisposed)
                {
                    publish(null);
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
            /// Moves the session handlers onto <paramref name="next"/>, off the last session first, unless it is the
            /// same one; false once disposed, when nothing is subscribed.
            /// </summary>
            private bool Follow(GlobalSystemMediaTransportControlsSession next)
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        return false;
                    }

                    if (ReferenceEquals(next, session))
                    {
                        return true;
                    }

                    Unfollow();
                    if (next == null)
                    {
                        logger.Info("Now Playing: no media session");
                        return true;
                    }

                    // Followed before it is subscribed to, so that if its app closes midway, what was subscribed is let go.
                    session = next;
                    sessionApp = next.SourceAppUserModelId;
                    var app = sessionApp;
                    next.MediaPropertiesChanged += onMediaChanged;
                    next.PlaybackInfoChanged += onPlaybackChanged;
                    next.TimelinePropertiesChanged += onTimelineChanged;
                    logger.Info($"Now Playing: following the media session of {app}");
                    return true;
                }
            }

            /// <summary>Unsubscribes from the session followed, if any. Called holding <see cref="sync"/>.</summary>
            private void Unfollow()
            {
                var last = session;
                session = null;
                if (last == null)
                {
                    return;
                }

                Unsubscribe(() =>
                {
                    last.MediaPropertiesChanged -= onMediaChanged;
                    last.PlaybackInfoChanged -= onPlaybackChanged;
                    last.TimelinePropertiesChanged -= onTimelineChanged;
                });
            }

            /// <summary>
            /// Unsubscribes, a session or the manager whose app has already gone included: Windows has dropped the
            /// handlers with it, so a failure here is no error.
            /// </summary>
            private void Unsubscribe(Action unsubscribe)
            {
                try
                {
                    unsubscribe();
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Info($"Now Playing: {sessionApp} had gone as its media session was let go ({e.Message})");
                }
            }

            /// <summary>What <paramref name="playing"/> plays now, its art decoded when the track is new.</summary>
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

                // An app often tells of a track before its art: the art is read again until it has some.
                var track = app + "\n" + title + "\n" + artist + "\n" + properties?.AlbumTitle;
                if (track != artTrack || art == null)
                {
                    art = await Decode(properties?.Thumbnail).ConfigureAwait(false);
                    artTrack = track;
                }

                return new MediaSession(
                    app, title, artist, isPlaying, art,
                    canTogglePlayPause: controls?.IsPlayPauseToggleEnabled ?? false,
                    canGoPrevious: controls?.IsPreviousEnabled ?? false,
                    canGoNext: controls?.IsNextEnabled ?? false,
                    position: position < TimeSpan.Zero ? TimeSpan.Zero : position,
                    duration: duration < TimeSpan.Zero ? TimeSpan.Zero : duration,
                    lastUpdated: timeline?.LastUpdatedTime.LocalDateTime ?? default);
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
        }
    }
}
