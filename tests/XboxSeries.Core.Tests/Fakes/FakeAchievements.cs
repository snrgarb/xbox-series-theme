using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// PlayniteAchievements' records, which a test sets as a sync or an unlock would. Setting them here doesn't tell the
    /// core; the test does, as the extension would.
    /// </summary>
    public sealed class FakeAchievements : IAchievements
    {
        public bool IsAvailable { get; set; } = true;

        /// <summary>Every game's achievements, as <see cref="Games"/> reads them.</summary>
        public List<GameAchievements> Recorded { get; } = new List<GameAchievements>();

        public IReadOnlyList<GameAchievements> Games() => IsAvailable ? Recorded.ToArray() : new GameAchievements[0];
    }
}
