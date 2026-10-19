using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// PlayniteAchievements' members, reached by reflection on its plugin instance, as checked against its source
    /// (v4.1): PlayniteAchievementsPlugin's AchievementDataService, CacheManager and RefreshRuntime, and its static
    /// AchievementUnlocked event.
    /// </summary>
    internal sealed partial class PlayniteAchievementsSource
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// PlayniteAchievements bound, or null, logged once, when it isn't installed or a member the source reads is
        /// missing or of another shape.
        /// </summary>
        private Binding Bind()
        {
            object plugin;
            try
            {
                plugin = api.Addons.Plugins.FirstOrDefault(p => p.Id == PluginId);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Error(e, "Couldn't look for PlayniteAchievements among the add-ons; no achievements");
                return null;
            }

            if (plugin == null)
            {
                logger.Info("PlayniteAchievements isn't installed; no achievements");
                return null;
            }

            try
            {
                return new Binding(plugin, DataDirectory());
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                var version = plugin.GetType().Assembly.GetName().Version;
                logger.Warn($"PlayniteAchievements {version} isn't one the extension can read ({e.Message}); no achievements");
                return null;
            }
        }

        /// <summary>PlayniteAchievements' data directory, which its icon paths are kept relative to; null if Playnite won't say.</summary>
        private string DataDirectory()
        {
            try
            {
                return Path.Combine(api.Paths.ExtensionsDataPath, PluginId.ToString());
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return null;
            }
        }

        private void Subscribe(Binding bound)
        {
            bound.Add(bound.CacheInvalidated, bound.CacheManager, this, nameof(OnCacheInvalidated));
            bound.Add(bound.CacheDeltaUpdated, bound.CacheManager, this, nameof(OnCacheDelta));
            bound.Add(bound.GameRefreshed, bound.RefreshRuntime, this, nameof(OnGameRefreshed));
            bound.Add(bound.AchievementUnlocked, null, this, nameof(OnAchievementUnlocked));
        }

        private void Unsubscribe(Binding bound)
        {
            try
            {
                bound.RemoveAll();
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Warn($"Couldn't stop following PlayniteAchievements: {e.Message}");
            }
        }

        // The handlers, made into delegates of each event's own type: (object, EventArgs) takes any EventHandler<T>.

        /// <summary>ICacheManager.CacheInvalidated: every game, or those it names.</summary>
        private void OnCacheInvalidated(object sender, EventArgs e)
        {
            try
            {
                var bound = binding;
                if (bound == null)
                {
                    return;
                }

                var changed = bound.ChangedGames(e);
                if (changed == null)
                {
                    MarkAll();
                }
                else
                {
                    Mark(changed);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                MarkAll();
            }
        }

        /// <summary>ICacheManager.CacheDeltaUpdated: one game saved or removed, or the whole cache reset.</summary>
        private void OnCacheDelta(object sender, EventArgs e)
        {
            try
            {
                var bound = binding;
                if (bound == null)
                {
                    return;
                }

                var game = bound.DeltaGame(e);
                if (game == null)
                {
                    MarkAll();
                }
                else
                {
                    Mark(new[] { game.Value });
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                MarkAll();
            }
        }

        /// <summary>RefreshRuntime.GameRefreshed: a game's refresh saved.</summary>
        private void OnGameRefreshed(Guid gameId) => Mark(new[] { gameId });

        /// <summary>PlayniteAchievementsPlugin.AchievementUnlocked: an unlock its in-game monitor heard of.</summary>
        private void OnAchievementUnlocked(object sender, EventArgs e)
        {
            try
            {
                var bound = binding;
                if (bound != null && bound.Unlock(e, out var gameId, out var achievement))
                {
                    DeliverUnlock(gameId, achievement);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                logger.Warn($"Couldn't read an unlock from PlayniteAchievements: {ex.Message}");
            }
        }

        /// <summary>
        /// The percentage of players with it, 0 to 100, from PlayniteAchievements' GlobalPercentUnlocked; null, as it
        /// keeps it, when it isn't known.
        /// </summary>
        internal static double? Rarity(double? percent)
        {
            if (!percent.HasValue || double.IsNaN(percent.Value) || percent.Value < 0)
            {
                return null;
            }

            return Math.Min(percent.Value, 100);
        }

        /// <summary>
        /// When it was unlocked, in local time, from PlayniteAchievements' UnlockTimeUtc; as its own DateUnlocked, an
        /// unspecified kind is UTC and DateTime.MinValue is no time. An unlock with no time is
        /// <see cref="DateTime.MinValue"/>, long ago; null is still locked.
        /// </summary>
        internal static DateTime? UnlockedAt(bool unlocked, DateTime? utc)
        {
            if (utc.HasValue && utc.Value != DateTime.MinValue)
            {
                var time = utc.Value;
                return time.Kind == DateTimeKind.Local
                    ? time
                    : DateTime.SpecifyKind(time, DateTimeKind.Utc).ToLocalTime();
            }

            return unlocked ? DateTime.MinValue : (DateTime?)null;
        }

        /// <summary>
        /// A full path or URI to the icon, from one of PlayniteAchievements' icon paths: any display prefix it added
        /// ("cachebust|token|", "gray:") taken off, and a path relative to its data directory made full; null for none.
        /// </summary>
        internal static string Icon(string path, string dataDirectory)
        {
            var icon = path?.Trim();
            while (!string.IsNullOrEmpty(icon))
            {
                if (icon.StartsWith("cachebust|", StringComparison.OrdinalIgnoreCase))
                {
                    var second = icon.IndexOf('|', "cachebust|".Length);
                    icon = second < 0 ? null : icon.Substring(second + 1);
                }
                else if (icon.StartsWith("gray:", StringComparison.OrdinalIgnoreCase))
                {
                    icon = icon.Substring("gray:".Length);
                }
                else
                {
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(icon))
            {
                return null;
            }

            if (Uri.TryCreate(icon, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                return icon;
            }

            try
            {
                return Path.IsPathRooted(icon) || dataDirectory == null ? icon : Path.Combine(dataDirectory, icon);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>PlayniteAchievements' members, found once; the constructor throws on any that is missing.</summary>
        private sealed class Binding
        {
            private readonly string dataDirectory;
            private readonly object dataService;
            private readonly MethodInfo getAll;
            private readonly MethodInfo getOne;

            // GameAchievementData's
            private readonly PropertyInfo gameId;
            private readonly PropertyInfo hasAchievements;
            private readonly PropertyInfo excludedFromSummaries;
            private readonly PropertyInfo achievements;

            // AchievementDetail's
            private readonly PropertyInfo apiName;
            private readonly PropertyInfo displayName;
            private readonly PropertyInfo unlockedIconPath;
            private readonly PropertyInfo globalPercent;
            private readonly PropertyInfo unlockTimeUtc;
            private readonly PropertyInfo unlocked;
            private readonly PropertyInfo isFiltered;
            private readonly PropertyInfo isFilteredFromSummaries;

            private readonly Dictionary<Type, ArgsMembers> args = new Dictionary<Type, ArgsMembers>();

            private readonly List<KeyValuePair<EventInfo, KeyValuePair<object, Delegate>>> subscribed =
                new List<KeyValuePair<EventInfo, KeyValuePair<object, Delegate>>>();

            public Binding(object plugin, string dataDirectory)
            {
                this.dataDirectory = dataDirectory;
                var pluginType = plugin.GetType();
                dataService = Value(plugin, pluginType, "AchievementDataService");
                var serviceType = dataService.GetType();
                getAll = Method(serviceType, "GetAllGameAchievementData", Type.EmptyTypes);
                getOne = Method(serviceType, "GetGameAchievementData", new[] { typeof(Guid) });

                var gameType = getOne.ReturnType;
                if (!typeof(IEnumerable).IsAssignableFrom(getAll.ReturnType))
                {
                    throw new MissingMemberException(serviceType.Name, getAll.Name);
                }

                gameId = Property(gameType, "PlayniteGameId", typeof(Guid?));
                hasAchievements = Property(gameType, "HasAchievements", typeof(bool));
                excludedFromSummaries = Optional(gameType, "ExcludedFromSummaries", typeof(bool));
                achievements = Property(gameType, "Achievements", null);
                var detailType = achievements.PropertyType.GetInterfaces().Concat(new[] { achievements.PropertyType })
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    ?.GetGenericArguments()[0] ?? throw new MissingMemberException(gameType.Name, "Achievements");

                apiName = Property(detailType, "ApiName", typeof(string));
                displayName = Property(detailType, "DisplayName", typeof(string));
                unlockedIconPath = Property(detailType, "UnlockedIconPath", typeof(string));
                globalPercent = Property(detailType, "GlobalPercentUnlocked", typeof(double?));
                unlockTimeUtc = Property(detailType, "UnlockTimeUtc", typeof(DateTime?));
                unlocked = Property(detailType, "Unlocked", typeof(bool));
                isFiltered = Optional(detailType, "IsFiltered", typeof(bool));
                isFilteredFromSummaries = Optional(detailType, "IsFilteredFromSummaries", typeof(bool));

                // What tells of changes: each is followed if it's there, as the source reads without it.
                CacheManager = OptionalValue(plugin, pluginType, "CacheManager");
                RefreshRuntime = OptionalValue(plugin, pluginType, "RefreshRuntime");
                CacheInvalidated = Event(CacheManager, "CacheInvalidated");
                CacheDeltaUpdated = Event(CacheManager, "CacheDeltaUpdated");
                GameRefreshed = Event(RefreshRuntime, "GameRefreshed");
                AchievementUnlocked = pluginType.GetEvent("AchievementUnlocked", BindingFlags.Public | BindingFlags.Static);
                if (AchievementUnlocked == null)
                {
                    logger.Warn("PlayniteAchievements has no AchievementUnlocked event; unlocks are heard of only as it refreshes");
                }
            }

            public object CacheManager { get; }

            public object RefreshRuntime { get; }

            public EventInfo CacheInvalidated { get; }

            public EventInfo CacheDeltaUpdated { get; }

            public EventInfo GameRefreshed { get; }

            public EventInfo AchievementUnlocked { get; }

            /// <summary>Every game PlayniteAchievements has achievements for.</summary>
            public IEnumerable<GameAchievements> ReadAll()
            {
                var all = (IEnumerable)Invoke(getAll);
                var read = new List<GameAchievements>();
                foreach (var data in all ?? new object[0])
                {
                    var game = Map(data);
                    if (game != null)
                    {
                        read.Add(game);
                    }
                }

                return read;
            }

            /// <summary>One game's achievements; null when it has none.</summary>
            public GameAchievements ReadOne(Guid id) => Map(Invoke(getOne, id), id);

            /// <summary>The games a CacheInvalidatedEventArgs names; null for every game.</summary>
            public IReadOnlyList<Guid> ChangedGames(EventArgs e)
            {
                var members = Args(e);
                if (members == null || Get<bool>(members.IsFull, e) || !(members.ChangedGameIds?.GetValue(e) is IEnumerable<Guid> ids))
                {
                    return null;
                }

                return ids.ToArray();
            }

            /// <summary>The game a CacheDeltaEventArgs is for; null for every game, as on a reset.</summary>
            public Guid? DeltaGame(EventArgs e)
            {
                var members = Args(e);
                if (members?.Key == null || members.OperationType == null)
                {
                    return null;
                }

                var operation = members.OperationType.GetValue(e)?.ToString();
                return operation != "FullReset" && Guid.TryParse(members.Key.GetValue(e) as string, out var id) ? id : (Guid?)null;
            }

            /// <summary>
            /// The user's own unlock an AchievementUnlockedEventArgs tells of; false for a progress update, a friend's, a
            /// settings preview or test, or the game-complete banner, none of which is one.
            /// </summary>
            public bool Unlock(EventArgs e, out Guid game, out Achievement achievement)
            {
                game = Guid.Empty;
                achievement = null;
                var members = Args(e);
                if (members?.PlayniteGameId == null || members.ApiName == null
                    || Get<bool>(members.IsProgressUpdate, e) || Get<bool>(members.IsFriendUnlock, e)
                    || Get<bool>(members.IsPreview, e) || Get<bool>(members.IsTestFire, e) || Get<bool>(members.IsGameCompleted, e))
                {
                    return false;
                }

                game = Get<Guid>(members.PlayniteGameId, e);
                var key = members.ApiName.GetValue(e) as string;
                if (game == Guid.Empty || string.IsNullOrWhiteSpace(key))
                {
                    return false;
                }

                var name = members.DisplayName?.GetValue(e) as string;
                achievement = new Achievement(
                    key,
                    string.IsNullOrWhiteSpace(name) ? key : name,
                    Icon(members.IconPath?.GetValue(e) as string, dataDirectory),
                    Rarity(members.GlobalPercent?.GetValue(e) as double?),
                    UnlockedAt(true, members.UnlockTimeUtc?.GetValue(e) as DateTime?) is DateTime at && at != DateTime.MinValue ? at : DateTime.Now);
                return true;
            }

            public void Add(EventInfo evt, object target, object handlerTarget, string handlerName)
            {
                if (evt == null)
                {
                    return;
                }

                var method = handlerTarget.GetType().GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
                try
                {
                    var handler = Delegate.CreateDelegate(evt.EventHandlerType, handlerTarget, method);
                    evt.AddEventHandler(target, handler);
                    subscribed.Add(new KeyValuePair<EventInfo, KeyValuePair<object, Delegate>>(evt, new KeyValuePair<object, Delegate>(target, handler)));
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Warn($"Couldn't follow PlayniteAchievements' {evt.Name}: {e.Message}");
                }
            }

            public void RemoveAll()
            {
                // Each on its own, so one that fails still leaves the static AchievementUnlocked's handler removed.
                foreach (var entry in subscribed)
                {
                    try
                    {
                        entry.Key.RemoveEventHandler(entry.Value.Key, entry.Value.Value);
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException))
                    {
                        logger.Warn($"Couldn't stop following PlayniteAchievements' {entry.Key.Name}: {e.Message}");
                    }
                }

                subscribed.Clear();
            }

            private GameAchievements Map(object data, Guid? asked = null)
            {
                if (data == null || !Get<bool>(hasAchievements, data) || Get<bool>(excludedFromSummaries, data))
                {
                    return null;
                }

                var id = gameId.GetValue(data) as Guid? ?? asked;
                if (!id.HasValue || id.Value == Guid.Empty || !(achievements.GetValue(data) is IEnumerable details))
                {
                    return null;
                }

                var mapped = new List<Achievement>();
                foreach (var detail in details)
                {
                    if (detail == null || Get<bool>(isFiltered, detail) || Get<bool>(isFilteredFromSummaries, detail))
                    {
                        continue;
                    }

                    var key = apiName.GetValue(detail) as string;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    var name = displayName.GetValue(detail) as string;
                    mapped.Add(new Achievement(
                        key,
                        string.IsNullOrWhiteSpace(name) ? key : name,
                        Icon(unlockedIconPath.GetValue(detail) as string, dataDirectory),
                        Rarity(globalPercent.GetValue(detail) as double?),
                        UnlockedAt(Get<bool>(unlocked, detail), unlockTimeUtc.GetValue(detail) as DateTime?)));
                }

                return mapped.Count == 0 ? null : new GameAchievements(id.Value, mapped);
            }

            private object Invoke(MethodInfo method, params object[] arguments)
            {
                try
                {
                    return method.Invoke(dataService, arguments);
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    throw;
                }
            }

            private ArgsMembers Args(EventArgs e)
            {
                if (e == null)
                {
                    return null;
                }

                var type = e.GetType();
                lock (args)
                {
                    if (!args.TryGetValue(type, out var members))
                    {
                        members = new ArgsMembers(type);
                        args[type] = members;
                    }

                    return members;
                }
            }

            private static T Get<T>(PropertyInfo property, object target) where T : struct =>
                property?.GetValue(target) is T value ? value : default(T);

            private static object Value(object target, Type type, string name) =>
                type.GetProperty(name, Instance)?.GetValue(target) ?? throw new MissingMemberException(type.Name, name);

            private static object OptionalValue(object target, Type type, string name)
            {
                try
                {
                    var value = type.GetProperty(name, Instance)?.GetValue(target);
                    if (value == null)
                    {
                        logger.Warn($"PlayniteAchievements has no {name}; its changes aren't followed through it");
                    }

                    return value;
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    logger.Warn($"Couldn't reach PlayniteAchievements' {name}: {e.Message}");
                    return null;
                }
            }

            private static EventInfo Event(object target, string name)
            {
                if (target == null)
                {
                    return null;
                }

                // CacheManager is declared as its ICacheManager, whose events a class may implement explicitly.
                var type = target.GetType();
                var evt = type.GetEvent(name, Instance)
                    ?? type.GetInterfaces().Select(i => i.GetEvent(name, Instance)).FirstOrDefault(e => e != null);
                if (evt == null)
                {
                    logger.Warn($"PlayniteAchievements has no {name} event; its changes aren't followed through it");
                }

                return evt;
            }

            private static MethodInfo Method(Type type, string name, Type[] parameters) =>
                type.GetMethod(name, Instance, null, parameters, null) ?? throw new MissingMemberException(type.Name, name);

            private static PropertyInfo Property(Type type, string name, Type expected)
            {
                var property = type.GetProperty(name, Instance);
                if (property == null || (expected != null && property.PropertyType != expected))
                {
                    throw new MissingMemberException(type.Name, name);
                }

                return property;
            }

            private static PropertyInfo Optional(Type type, string name, Type expected)
            {
                var property = type.GetProperty(name, Instance);
                return property != null && property.PropertyType == expected ? property : null;
            }
        }

        /// <summary>The members of one of PlayniteAchievements' event args types the source reads; null where missing.</summary>
        private sealed class ArgsMembers
        {
            public ArgsMembers(Type type)
            {
                PropertyInfo Find(string name) => type.GetProperty(name, Instance);

                IsFull = Find("IsFull");
                ChangedGameIds = Find("ChangedGameIds");
                Key = Find("Key");
                OperationType = Find("OperationType");
                PlayniteGameId = Find("PlayniteGameId");
                ApiName = Find("ApiName");
                DisplayName = Find("DisplayName");
                IconPath = Find("IconPath");
                GlobalPercent = Find("GlobalPercent");
                UnlockTimeUtc = Find("UnlockTimeUtc");
                IsProgressUpdate = Find("IsProgressUpdate");
                IsFriendUnlock = Find("IsFriendUnlock");
                IsPreview = Find("IsPreview");
                IsTestFire = Find("IsTestFire");
                IsGameCompleted = Find("IsGameCompleted");
            }

            public PropertyInfo IsFull { get; }
            public PropertyInfo ChangedGameIds { get; }
            public PropertyInfo Key { get; }
            public PropertyInfo OperationType { get; }
            public PropertyInfo PlayniteGameId { get; }
            public PropertyInfo ApiName { get; }
            public PropertyInfo DisplayName { get; }
            public PropertyInfo IconPath { get; }
            public PropertyInfo GlobalPercent { get; }
            public PropertyInfo UnlockTimeUtc { get; }
            public PropertyInfo IsProgressUpdate { get; }
            public PropertyInfo IsFriendUnlock { get; }
            public PropertyInfo IsPreview { get; }
            public PropertyInfo IsTestFire { get; }
            public PropertyInfo IsGameCompleted { get; }
        }
    }
}
