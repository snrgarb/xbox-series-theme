using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// Achievements, from PlayniteAchievements: an Achievement unlocked Toast as the game running now, or the one that just
    /// exited, unlocks one, and the totals on the Guide's Profile &amp; system.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// PlayniteAchievements recorded unlocks or refreshed a game: <see cref="IAchievements.Games"/> is read again.
        /// </summary>
        public void OnAchievementsChanged()
        {
        }

        /// <summary>PlayniteAchievements heard of an unlock live, while the game runs.</summary>
        public void OnAchievementUnlocked(Guid gameId, Achievement achievement)
        {
        }
    }
}
