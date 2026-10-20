using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// Port for the games' achievements, as PlayniteAchievements records them on this PC. When it records new unlocks, or
    /// refreshes a game (after it exits, or on its schedule), the extension calls <see cref="ExtensionCore.OnAchievementsChanged"/>
    /// on the UI thread, only as a sign to read <see cref="Games"/> again; an unlock it hears of live, while the game runs,
    /// also reaches <see cref="ExtensionCore.OnAchievementUnlocked"/> on the UI thread.
    /// </summary>
    public interface IAchievements
    {
        /// <summary>Whether PlayniteAchievements is installed and could be read; false leaves out all that comes of achievements.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Every game PlayniteAchievements has achievements for, as read now; empty when it has none, or isn't
        /// <see cref="IsAvailable"/>. Never throws.
        /// </summary>
        IReadOnlyList<GameAchievements> Games();
    }

    /// <summary>One game's achievements.</summary>
    public sealed class GameAchievements
    {
        public GameAchievements(Guid gameId, IReadOnlyList<Achievement> achievements)
        {
            GameId = gameId;
            Achievements = achievements;
        }

        /// <summary>The Playnite game they are for.</summary>
        public Guid GameId { get; }

        /// <summary>All of the game's achievements, unlocked or not.</summary>
        public IReadOnlyList<Achievement> Achievements { get; }
    }

    /// <summary>One achievement of a game.</summary>
    public sealed class Achievement
    {
        public Achievement(string key, string name, string icon, double? rarity, DateTime? unlockedAt, string description = null, int? points = null)
        {
            Description = description;
            Points = points;
            Key = key;
            Name = name;
            Icon = icon;
            Rarity = rarity;
            UnlockedAt = unlockedAt;
        }

        /// <summary>What tells it from the game's other achievements, its API name; the same from one read to the next.</summary>
        public string Key { get; }

        /// <summary>Its name, as the game shows it.</summary>
        public string Name { get; }

        /// <summary>What it asks of the player, as the game words it; null for none.</summary>
        public string Description { get; }

        /// <summary>A full path or URI to its unlocked icon; null for none.</summary>
        public string Icon { get; }

        /// <summary>The percentage of players who have unlocked it, 0 to 100; null when that isn't known.</summary>
        public double? Rarity { get; }

        /// <summary>
        /// Its points on its own platform, as PlayniteAchievements records them (Xbox's Gamerscore, RetroAchievements'
        /// points, Epic's XP); null where the platform has none, as Steam, GOG and PSN.
        /// </summary>
        public int? Points { get; }

        /// <summary>When it was unlocked, in local time as <see cref="IClock.Now"/>; null while it is locked.</summary>
        public DateTime? UnlockedAt { get; }
    }

    /// <summary>No achievements, ever: for a core given no port.</summary>
    internal sealed class NoAchievements : IAchievements
    {
        public static readonly NoAchievements Instance = new NoAchievements();

        public bool IsAvailable => false;

        public IReadOnlyList<GameAchievements> Games() => new GameAchievements[0];
    }
}
