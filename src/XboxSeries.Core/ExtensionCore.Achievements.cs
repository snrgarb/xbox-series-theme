using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Achievements, from PlayniteAchievements: an Achievement unlocked Toast as the game running now, or the one that just
    /// exited, unlocks one, and the totals on the Guide's Profile &amp; system.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The Achievement unlocked Toast's line for one unlock.</summary>
        private const string AchievementUnlockedLine = "Achievement unlocked";

        /// <summary>The Achievement unlocked Toast's line for several, after their count.</summary>
        private const string AchievementsUnlockedLine = " achievements unlocked";

        /// <summary>
        /// How long an unlock heard live waits for others with it before its Toast. PlayniteAchievements' in-game check
        /// finds every unlock since the last one at once and tells of each in turn, so those come together as one Toast.
        /// </summary>
        private static readonly TimeSpan UnlocksGatherFor = TimeSpan.FromSeconds(1);

        /// <summary>How many of the last unlocks Profile &amp; system lists.</summary>
        private const int RecentAchievementCount = 3;

        /// <summary>
        /// An achievement's Score by its rarity, the share of players who have unlocked it: at most each percentage of
        /// <see cref="RarityAtMost"/>, the Score in the same place of <see cref="ScoreAtRarity"/>; any commoner,
        /// <see cref="CommonScore"/>.
        /// </summary>
        private static readonly double[] RarityAtMost = { 1, 5, 10, 25, 50 };
        private static readonly int[] ScoreAtRarity = { 100, 50, 30, 20, 15 };

        /// <summary>The Score of an achievement more than half of players have, or whose rarity isn't known.</summary>
        private const int CommonScore = 10;

        /// <summary>Every unlock heard of, from a read or live, by <see cref="UnlockKey"/>, so each is told of once at most.</summary>
        private readonly HashSet<string> heardUnlocks = new HashSet<string>();

        /// <summary>
        /// Whether <see cref="IAchievements.Games"/> has been read since start-up. The first read is what was there
        /// already, so tells of nothing.
        /// </summary>
        private bool achievementsRead;

        /// <summary>Every game's achievements, as last read.</summary>
        private IReadOnlyList<GameAchievements> recordedAchievements = new GameAchievements[0];

        /// <summary>Unlocks heard live that no read has shown unlocked yet, which the totals count all the same.</summary>
        private readonly List<GameAchievement> liveUnlocks = new List<GameAchievement>();

        /// <summary>
        /// The game whose unlocks are told of: the one running, or the last to stop until another starts; null before
        /// any has run.
        /// </summary>
        private Guid? achievementsGame;

        /// <summary>When <see cref="achievementsGame"/> started: an unlock from before then is an older session's.</summary>
        private DateTime achievementsGameStartedAt;

        /// <summary>Unlocks to tell of in the next Achievement unlocked Toast.</summary>
        private readonly List<Achievement> unlocksToTell = new List<Achievement>();

        /// <summary>The end of the wait for unlocks heard live with the first; null while none waits.</summary>
        private IDisposable unlocksTimer;

        /// <summary>The last unlocks, newest first, Profile &amp; system's <see cref="GuideItemKind.RecentAchievement"/> entries.</summary>
        private IReadOnlyList<GameAchievement> recentAchievements = new GameAchievement[0];

        /// <summary>
        /// PlayniteAchievements recorded unlocks or refreshed a game: <see cref="IAchievements.Games"/> is read again.
        /// Unlocks it shows that weren't heard of before, from the game whose unlocks are told of since it started, are
        /// one Toast at once, with any heard live still waiting; the first read after start-up tells of none. The totals
        /// follow.
        /// </summary>
        public void OnAchievementsChanged()
        {
            recordedAchievements = achievements.IsAvailable ? achievements.Games() : new GameAchievements[0];
            var unlocked = RecordedUnlocks().ToList();
            var shown = new HashSet<string>(unlocked.Select(u => u.Key));
            liveUnlocks.RemoveAll(u => shown.Contains(u.Key));

            var fresh = unlocked.Where(u => heardUnlocks.Add(u.Key)).ToList();
            var first = !achievementsRead;
            achievementsRead = true;
            var toTell = first ? new List<GameAchievement>() : fresh.Where(IsThisSessions).ToList();
            if (toTell.Count > 0)
            {
                unlocksToTell.AddRange(toTell.Select(u => u.Achievement));
                TellUnlocks();
            }

            RefreshAchievements();
        }

        /// <summary>
        /// PlayniteAchievements heard of an unlock live, while the game runs: its Toast waits a second for any others
        /// heard with it, then shows in its turn. One already heard of, or another game's, tells of nothing. It counts in
        /// the totals at once.
        /// </summary>
        public void OnAchievementUnlocked(Guid gameId, Achievement achievement)
        {
            if (!achievements.IsAvailable || achievement == null)
            {
                return;
            }

            var unlock = new GameAchievement(
                gameId,
                achievement.UnlockedAt != null
                    ? achievement
                    : new Achievement(achievement.Key, achievement.Name, achievement.Icon, achievement.Rarity, clock.Now));
            if (!heardUnlocks.Add(unlock.Key))
            {
                return;
            }

            liveUnlocks.Add(unlock);
            if (IsThisSessions(unlock))
            {
                unlocksToTell.Add(unlock.Achievement);
                if (unlocksTimer == null)
                {
                    unlocksTimer = clock.After(UnlocksGatherFor, TellUnlocks);
                }
            }

            RefreshAchievements();
        }

        /// <summary>
        /// The Debug menu: tells of <paramref name="count"/> made-up unlocks, rare ones (unlocked by 1% of players) or not,
        /// as unlocks heard live are, without counting them in the totals.
        /// </summary>
        public void DebugAchievementsUnlocked(int count, bool rare)
        {
        }

        /// <summary>A game started: its unlocks are the ones told of now, from this moment on.</summary>
        private void FollowAchievementsOf(Guid gameId)
        {
            achievementsGame = gameId;
            achievementsGameStartedAt = clock.Now;
        }

        /// <summary>Whether an unlock is the game's whose unlocks are told of, since it started: not an older session's.</summary>
        private bool IsThisSessions(GameAchievement unlock) =>
            unlock.GameId == achievementsGame && unlock.Achievement.UnlockedAt >= achievementsGameStartedAt;

        /// <summary>
        /// The unlocks waiting are told of: one with its icon, "Achievement unlocked", its name and its Score; several
        /// as one Toast, "&lt;n&gt; achievements unlocked". It waits its turn behind any Toast showing.
        /// </summary>
        private void TellUnlocks()
        {
            unlocksTimer?.Dispose();
            unlocksTimer = null;
            if (unlocksToTell.Count == 0)
            {
                return;
            }

            var one = unlocksToTell[0];
            EnqueueToast(unlocksToTell.Count == 1
                ? new WaitingToast(AchievementUnlockedLine, ToastKind.AchievementUnlocked, one.Icon, detail: one.Name, score: ScoreOf(one))
                : new WaitingToast(unlocksToTell.Count + AchievementsUnlockedLine, ToastKind.AchievementUnlocked, null));
            unlocksToTell.Clear();
        }

        /// <summary>An achievement's Score: rarer is worth more, from 100 for the rarest down to 10.</summary>
        private static int ScoreOf(Achievement achievement)
        {
            var rarity = achievement.Rarity;
            if (rarity == null)
            {
                return CommonScore;
            }

            for (var i = 0; i < RarityAtMost.Length; i++)
            {
                if (rarity <= RarityAtMost[i])
                {
                    return ScoreAtRarity[i];
                }
            }

            return CommonScore;
        }

        /// <summary>Every unlock in the games' achievements as last read.</summary>
        private IEnumerable<GameAchievement> RecordedUnlocks() =>
            recordedAchievements.SelectMany(g => g.Achievements.Where(a => a.UnlockedAt != null).Select(a => new GameAchievement(g.GameId, a)));

        /// <summary>
        /// The Profile's totals and last unlocks, over every game this PC has achievements for; none at all without
        /// PlayniteAchievements, with no game having any, or before the first read, so unlocks heard live never make
        /// totals on their own. Profile &amp; system showing shows them in place.
        /// </summary>
        private void RefreshAchievements()
        {
            var any = achievements.IsAvailable && achievementsRead
                && (liveUnlocks.Count > 0 || recordedAchievements.Any(g => g.Achievements.Count > 0));
            if (!any)
            {
                Guide.Achievements = null;
                recentAchievements = new GameAchievement[0];
            }
            else
            {
                var unlocked = RecordedUnlocks()
                    .Concat(liveUnlocks)
                    .ToList();
                Guide.Achievements = new ProfileAchievements(unlocked.Sum(u => ScoreOf(u.Achievement)), unlocked.Count);
                recentAchievements = unlocked.OrderByDescending(u => u.Achievement.UnlockedAt).Take(RecentAchievementCount).ToList();
            }

            ShowAchievementsInGuide();
        }

        /// <summary>
        /// Profile &amp; system's entries for the last unlocks, newest first, the first under a divider: the achievement's
        /// name, its game's and its icon.
        /// </summary>
        private IEnumerable<GuideItem> RecentAchievementItems() =>
            recentAchievements.Select((u, i) => new GuideItem(
                GuideItemKind.RecentAchievement,
                u.Achievement.Name,
                hasDividerAbove: i == 0,
                image: u.Achievement.Icon,
                detail: library.Games.FirstOrDefault(g => g.Id == u.GameId)?.Name));

        /// <summary>
        /// The totals changed with Profile &amp; system showing: its entries are rebuilt, focus on the same entry, or on
        /// a recent unlock in the same place; with fewer, the last of them, and with none, the profile card.
        /// </summary>
        private void ShowAchievementsInGuide()
        {
            if (!Guide.IsOpen || Guide.Tab != GuideTabKind.ProfileAndSystem || Guide.Flyout != null)
            {
                return;
            }

            var focused = Guide.FocusedItem;
            var recentIndex = Guide.Items.Where(i => i.Kind == GuideItemKind.RecentAchievement).ToList().IndexOf(focused);
            ShowProfileTab();
            if (focused == null)
            {
                return;
            }

            var recents = Guide.Items.Where(i => i.Kind == GuideItemKind.RecentAchievement).ToList();
            Guide.FocusedItem = focused.Kind != GuideItemKind.RecentAchievement
                ? Guide.Items.FirstOrDefault(i => i.Kind == focused.Kind) ?? Guide.Items[0]
                : recents.Count > 0 ? recents[Clamp(recentIndex, recents.Count)] : Guide.Items[0];
        }

        /// <summary>One unlock of one game.</summary>
        private sealed class GameAchievement
        {
            public GameAchievement(Guid gameId, Achievement achievement)
            {
                GameId = gameId;
                Achievement = achievement;
            }

            public Guid GameId { get; }
            public Achievement Achievement { get; }

            /// <summary>What tells it from every other game's achievements.</summary>
            public string Key => UnlockKey(GameId, Achievement.Key);
        }

        private static string UnlockKey(Guid gameId, string key) => gameId.ToString("N") + "/" + key;
    }
}
