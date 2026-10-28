using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: the games' achievements as PlayniteAchievements (justin-delano/PlayniteAchievements) records them, read
    /// through its plugin instance at run time, as it has no API of its own to build against. Nothing is read before
    /// <see cref="Start"/>, as it may load after the extension. Not installed, a version whose members don't match, or a
    /// read that fails leaves it unavailable or with no games, and never throws into Playnite.
    /// </summary>
    /// <remarks>
    /// Its cache's changes, from a game's refresh after it exits, its periodic refresh or the user's edits, arrive on
    /// threads of its own, often many at once: they mark the games named for reading again, and once
    /// <see cref="ChangeCoalescing"/> has passed, those marked meanwhile are read off the UI thread, then
    /// <see cref="Changed"/> is raised once on it. <see cref="Games"/> only hands out the last read, so the UI thread never
    /// waits on PlayniteAchievements. An unlock its in-game monitor hears of while the game runs raises
    /// <see cref="Unlocked"/> on the UI thread.
    /// </remarks>
    internal sealed partial class PlayniteAchievementsSource : IAchievements, IDisposable
    {
        /// <summary>PlayniteAchievements' plugin Id (PlayniteAchievementsPlugin.cs, its GenericPlugin Id).</summary>
        internal static readonly Guid PluginId = Guid.Parse("e6aad2c9-6e06-4d8d-ac55-ac3b252b5f7b");

        /// <summary>How long changes are gathered into one <see cref="Changed"/>, as a periodic refresh saves game after game.</summary>
        private static readonly TimeSpan ChangeCoalescing = TimeSpan.FromMilliseconds(500);

        private static readonly ILogger logger = LogManager.GetLogger();

        private static readonly GameAchievements[] NoGames = new GameAchievements[0];

        private readonly IPlayniteAPI api;

        /// <summary>Guards <see cref="readAll"/> and <see cref="stale"/>, which any thread marks.</summary>
        private readonly object marks = new object();

        /// <summary>One read of PlayniteAchievements at a time, and the games it keeps.</summary>
        private readonly object reading = new object();

        private readonly Dictionary<Guid, GameAchievements> games = new Dictionary<Guid, GameAchievements>();

        private readonly HashSet<Guid> stale = new HashSet<Guid>();

        private bool readAll = true;

        /// <summary>The games as last read, which <see cref="Games"/> hands out.</summary>
        private volatile GameAchievements[] lastRead = NoGames;

        /// <summary>The thread reading now, whose own cache events (as its scope settling) aren't changes; 0 when none is.</summary>
        private int readingThread;

        private bool loggedReadFailure;

        /// <summary>Bound to PlayniteAchievements once it's found and its members match; null before, and without it.</summary>
        private volatile Binding binding;

        private Dispatcher dispatcher;

        private DispatcherTimer changeTimer;

        private int changePending;

        public PlayniteAchievementsSource(IPlayniteAPI api)
        {
            this.api = api;
        }

        /// <summary>Raised on the UI thread when PlayniteAchievements' achievements have changed: <see cref="Games"/> again.</summary>
        public event Action Changed;

        /// <summary>Raised on the UI thread with an unlock PlayniteAchievements heard of live, while the game runs, by its game.</summary>
        public event Action<Guid, Achievement> Unlocked;

        public bool IsAvailable => binding != null;

        /// <summary>
        /// Finds PlayniteAchievements and starts following it, on the UI thread once every plugin is loaded; reads every
        /// game off the UI thread, and raises <see cref="Changed"/> once they're read. Does nothing without it.
        /// </summary>
        public void Start(Dispatcher uiDispatcher)
        {
            dispatcher = uiDispatcher;
            changeTimer = new DispatcherTimer(DispatcherPriority.Background, uiDispatcher) { Interval = ChangeCoalescing };
            changeTimer.Tick += OnChangeTimer;

            var bound = Bind();
            if (bound == null)
            {
                return;
            }

            binding = bound;
            Subscribe(bound);
            logger.Info("Achievements are read from PlayniteAchievements");

            // Every game is marked from the start: this is the first read.
            SignalChanged();
        }

        public IReadOnlyList<GameAchievements> Games() => binding == null ? NoGames : lastRead;

        /// <summary>Reads the games marked since the last read, off the UI thread, keeping what was read before on a failure.</summary>
        private void Read()
        {
            var bound = binding;
            if (bound == null)
            {
                return;
            }

            lock (reading)
            {
                bool all;
                Guid[] some;
                lock (marks)
                {
                    all = readAll;
                    some = stale.ToArray();
                    readAll = false;
                    stale.Clear();
                }

                if (!all && some.Length == 0)
                {
                    return;
                }

                readingThread = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    if (all)
                    {
                        games.Clear();
                        foreach (var game in bound.ReadAll())
                        {
                            games[game.GameId] = game;
                        }
                    }
                    else
                    {
                        foreach (var id in some)
                        {
                            var game = bound.ReadOne(id);
                            if (game == null)
                            {
                                games.Remove(id);
                            }
                            else
                            {
                                games[id] = game;
                            }
                        }
                    }
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    // Read again next time; until then, what was read before.
                    lock (marks)
                    {
                        readAll |= all;
                        stale.UnionWith(some);
                    }

                    if (!loggedReadFailure)
                    {
                        loggedReadFailure = true;
                        logger.Error(e, "Couldn't read PlayniteAchievements' achievements; showing what was last read");
                    }

                    return;
                }
                finally
                {
                    readingThread = 0;
                }

                lastRead = games.Values.ToArray();
            }
        }

        /// <summary>Stops following PlayniteAchievements, every handler unsubscribed. Safe to call more than once.</summary>
        public void Dispose()
        {
            var bound = binding;
            binding = null;
            if (changeTimer != null)
            {
                // Playnite may stop it off the UI thread; a tick still due finds it unbound, and raises nothing.
                changeTimer.Tick -= OnChangeTimer;
                try
                {
                    changeTimer.Stop();
                }
                catch (InvalidOperationException)
                {
                }
            }

            if (bound != null)
            {
                Unsubscribe(bound);
            }
        }

        /// <summary>Every game is to be read again.</summary>
        private void MarkAll()
        {
            if (IsOwnRead())
            {
                return;
            }

            lock (marks)
            {
                readAll = true;
            }

            SignalChanged();
        }

        /// <summary>These games are to be read again.</summary>
        private void Mark(IEnumerable<Guid> ids)
        {
            if (IsOwnRead())
            {
                return;
            }

            lock (marks)
            {
                stale.UnionWith(ids.Where(id => id != Guid.Empty));
            }

            SignalChanged();
        }

        private bool IsOwnRead() => readingThread == Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// A read of what is marked once <see cref="ChangeCoalescing"/> has passed, then <see cref="Changed"/> on the UI
        /// thread; any signalled meanwhile are part of it.
        /// </summary>
        private void SignalChanged()
        {
            var ui = dispatcher;
            if (ui == null || binding == null || Interlocked.Exchange(ref changePending, 1) == 1)
            {
                return;
            }

            ui.BeginInvoke(new Action(() => changeTimer?.Start()));
        }

        private void OnChangeTimer(object sender, EventArgs e)
        {
            changeTimer.Stop();
            var ui = dispatcher;
            Task.Run(() =>
            {
                // Marks from now on wait for the next read: this one may already have taken them.
                Interlocked.Exchange(ref changePending, 0);
                Read();
                ui.BeginInvoke(new Action(() =>
                {
                    if (binding != null)
                    {
                        Changed?.Invoke();
                    }
                }));
            });
        }

        /// <summary>An unlock heard of live, on the UI thread, as PlayniteAchievements raises it there.</summary>
        private void DeliverUnlock(Guid gameId, Achievement achievement)
        {
            var ui = dispatcher;
            if (ui == null || ui.CheckAccess())
            {
                RaiseUnlocked(gameId, achievement);
            }
            else
            {
                ui.BeginInvoke(new Action(() => RaiseUnlocked(gameId, achievement)));
            }
        }

        private void RaiseUnlocked(Guid gameId, Achievement achievement)
        {
            if (binding == null)
            {
                return;
            }

            // Its cache has it too by now, or will soon: the game is read again.
            Mark(new[] { gameId });

            Unlocked?.Invoke(gameId, achievement);
        }
    }
}
