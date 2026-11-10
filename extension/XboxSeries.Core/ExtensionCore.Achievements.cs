using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Achievements, from PlayniteAchievements: an Achievement unlocked Toast, the console's pill, as the game running now,
    /// or the one that just exited, unlocks one (its rare form for one few players have), and the totals on the Guide's
    /// Profile &amp; system and Home's top bar. Each unlock's Score is its platform's own points where PlayniteAchievements
    /// records them, else by rarity, so the total Score sums every platform's.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The Achievement unlocked Toast's line for one unlock.</summary>
        private const string AchievementUnlockedLine = "Achievement unlocked";

        /// <summary>The Achievement unlocked Toast's line for several, after their count.</summary>
        private const string AchievementsUnlockedLine = " achievements unlocked";

        /// <summary>The rare Toast's line, before the view adds " - &lt;n&gt;%".</summary>
        private const string RareAchievementUnlockedLine = "Rare Achievement unlocked";

        /// <summary>Home's Achievements tile's line without PlayniteAchievements: not installed, disabled, or a version that can't be read.</summary>
        private const string NeedsPlayniteAchievementsNote = "Needs PlayniteAchievements";

        /// <summary>Home's Achievements tile's line while PlayniteAchievements is read and no game has any achievements: nothing signed in to, or never refreshed.</summary>
        private const string NothingRecordedNote = "Nothing recorded yet";

        /// <summary>An achievement fewer than this percentage of players have is rare, as the console's rule; one of unknown rarity never is.</summary>
        private const double RareBelow = 10;

        /// <summary>
        /// How long an Achievement unlocked Toast shows before its outro, rare or not, and the "&lt;n&gt; achievements
        /// unlocked" too: each is the console's pill. Its sound starts, its circle pops and its pill unfurls (the rare
        /// one's diamond flying out), then its pages (the header, Score and name, from +2.27 s; the description, from
        /// +4.54 s; the prompt, from +7.22 s) hold until the pill starts to collapse at +8.46 s, and the outro the view
        /// plays as it hides takes the rest of the ~9.6 s.
        /// </summary>
        private static readonly TimeSpan AchievementToastDuration = TimeSpan.FromSeconds(8.5);

        /// <summary>
        /// How long the rare Toast shows before its outro, whatever its description: its sound's closing flourish starts
        /// 8.45 s in, and the pill collapses with it, as on the console. A long description's pages are held for less.
        /// </summary>
        private static readonly TimeSpan RareAchievementToastDuration = TimeSpan.FromSeconds(8.45);

        /// <summary>
        /// How long an Achievement unlocked Toast's outro takes once it hides, rare or not, the pill collapsing into its
        /// circle and the circle to a dot: the next Toast waits for it, then the usual gap.
        /// </summary>
        private static readonly TimeSpan AchievementToastOutro = TimeSpan.FromSeconds(1.12);

        /// <summary>
        /// About how much of a description fits on one page of the achievement Toast, two lines across its text column, a
        /// little under what the view lays out, so a long one is given its time rather than rushed.
        /// </summary>
        private const int DescriptionPageChars = 80;

        /// <summary>
        /// How much longer an achievement's Toast shows for each page of its description after the first: the console's
        /// hold of a page and its push to the next, so every page reads as long as the console's one does.
        /// </summary>
        private static readonly TimeSpan DescriptionPageTime = TimeSpan.FromSeconds(3);

#if DEBUG

        /// <summary>The Debug menu's made-up unlocks' name, and their description for the pill's second page.</summary>
        private const string DebugAchievementName = "Two's Good, but I Want Three";
        private const string DebugAchievementDescription = "Earned at least 2 stars on 15 different maps in Frontier Defense";

        /// <summary>The Debug menu's made-up unlocks' rarity: a rare one's, and any other's.</summary>
        private const double DebugRareRarity = 1;
        private const double DebugCommonRarity = 40;
#endif

        /// <summary>
        /// How long an unlock heard live waits for others with it before its Toast. PlayniteAchievements' in-game check
        /// finds every unlock since the last one at once and tells of each in turn, so those come together as one Toast.
        /// </summary>
        private static readonly TimeSpan UnlocksGatherFor = TimeSpan.FromSeconds(1);

        /// <summary>How many of the last unlocks Profile &amp; system lists.</summary>
        private const int RecentAchievementCount = 3;

        /// <summary>
        /// An achievement's Score by its rarity, the share of players who have unlocked it, for one whose platform has no
        /// points of its own where PlayniteAchievements records them: at most each percentage of <see cref="RarityAtMost"/>,
        /// the Score in the same place of <see cref="ScoreAtRarity"/>; any commoner, <see cref="CommonScore"/>.
        /// </summary>
        private static readonly double[] RarityAtMost = { 1, 5, 10, 25, 50 };
        private static readonly int[] ScoreAtRarity = { 100, 50, 30, 20, 15 };

        /// <summary>
        /// The Score of an achievement with no points of its own that more than half of players have, or whose rarity isn't
        /// known.
        /// </summary>
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
        /// Whether PlayniteAchievements is installed, enabled and read: without it, nothing of achievements shows anywhere,
        /// not even the Debug menu's made-up unlocks.
        /// </summary>
        public bool HasAchievements => achievements.IsAvailable;

        /// <summary>
        /// The extension has looked for PlayniteAchievements, every plugin loaded by then, found or not: from now on Home's
        /// Achievements tile says when it isn't there (<see cref="StatusState.AchievementsNote"/>). Found, the tile says
        /// nothing until the first read.
        /// </summary>
        public void OnAchievementsLookedFor() => RefreshAchievements();

        /// <summary>
        /// PlayniteAchievements recorded unlocks or refreshed a game: <see cref="IAchievements.Games"/> is read again.
        /// Unlocks it shows that weren't heard of before, from the game whose unlocks are told of since it started, are
        /// one Toast at once, with any heard live still waiting; the first read after start-up tells of none. The totals
        /// follow.
        /// </summary>
        public void OnAchievementsChanged()
        {
            recordedAchievements = HasAchievements ? achievements.Games() : new GameAchievements[0];
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
        /// the totals at once. One heard with no time is taken as unlocked now, its points and all else kept.
        /// </summary>
        public void OnAchievementUnlocked(Guid gameId, Achievement achievement)
        {
            if (!HasAchievements || achievement == null)
            {
                return;
            }

            var unlock = new GameAchievement(
                gameId,
                achievement.UnlockedAt != null
                    ? achievement
                    : new Achievement(
                        achievement.Key, achievement.Name, achievement.Icon, achievement.Rarity, clock.Now, achievement.Description, achievement.Points));
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

#if DEBUG

        /// <summary>
        /// The Debug menu: tells of <paramref name="count"/> made-up unlocks, rare ones (unlocked by 1% of players) or not,
        /// as unlocks heard live are, without counting them in the totals. Whatever game runs, or none, their Toast waits
        /// its turn: one is its own Toast, the rare one's for a rare unlock, and several the "&lt;n&gt; achievements unlocked".
        /// Without <see cref="HasAchievements"/>, none.
        /// </summary>
        public void DebugAchievementsUnlocked(int count, bool rare)
        {
            if (!HasAchievements)
            {
                return;
            }

            var made = Enumerable.Range(0, count)
                .Select(i => new Achievement(
                    "xbox-series-debug-" + i,
                    DebugAchievementName,
                    null,
                    rare ? DebugRareRarity : DebugCommonRarity,
                    clock.Now,
                    DebugAchievementDescription))
                .ToList();
            if (made.Count > 0)
            {
                EnqueueToast(UnlocksToast(made));
            }
        }
#endif

        /// <summary>
        /// How long an achievement's Toast shows before its outro: <see cref="AchievementToastDuration"/>, and
        /// <see cref="DescriptionPageTime"/> more for each page its description runs to after the first.
        /// </summary>
        private static TimeSpan AchievementShownFor(string description)
        {
            var pages = string.IsNullOrWhiteSpace(description) ? 0 : (description.Trim().Length + DescriptionPageChars - 1) / DescriptionPageChars;
            return AchievementToastDuration + TimeSpan.FromTicks(DescriptionPageTime.Ticks * Math.Max(0, pages - 1));
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

        /// <summary>The unlocks waiting are told of in one <see cref="UnlocksToast"/>, which waits its turn behind any Toast showing.</summary>
        private void TellUnlocks()
        {
            unlocksTimer?.Dispose();
            unlocksTimer = null;
            if (unlocksToTell.Count == 0)
            {
                return;
            }

            EnqueueToast(UnlocksToast(unlocksToTell));
            unlocksToTell.Clear();
        }

        /// <summary>
        /// The Toast telling of these unlocks: one with its icon, "Achievement unlocked", its name, its Score and its
        /// description, or when <see cref="IsRare"/> the rare Toast, "Rare Achievement unlocked", with its rarity as well;
        /// several as one ordinary Toast, "&lt;n&gt; achievements unlocked", rare ones among them or not. Only the rare
        /// Toast has a rarity, so the view writes " - &lt;n&gt;%" on that one alone.
        /// </summary>
        private static WaitingToast UnlocksToast(IReadOnlyList<Achievement> unlocks)
        {
            if (unlocks.Count > 1)
            {
                return new WaitingToast(unlocks.Count + AchievementsUnlockedLine, ToastKind.AchievementUnlocked, null);
            }

            var one = unlocks[0];
            return IsRare(one)
                ? new WaitingToast(
                    RareAchievementUnlockedLine, ToastKind.RareAchievementUnlocked, one.Icon, detail: one.Name, score: ScoreOf(one),
                    rarity: one.Rarity, description: one.Description)
                : new WaitingToast(
                    AchievementUnlockedLine, ToastKind.AchievementUnlocked, one.Icon, detail: one.Name, score: ScoreOf(one),
                    description: one.Description);
        }

        /// <summary>Whether fewer than <see cref="RareBelow"/> percent of players have an achievement; unknown rarity is never rare.</summary>
        private static bool IsRare(Achievement achievement) => achievement.Rarity < RareBelow;

        /// <summary>
        /// An achievement's Score: its platform's own points where PlayniteAchievements records them (Xbox's Gamerscore,
        /// RetroAchievements' points, Epic's XP), else by rarity, rarer worth more, from 100 for the rarest down to 10.
        /// Points of 0 are worth 0, as PlayniteAchievements' own totals count them.
        /// </summary>
        private static int ScoreOf(Achievement achievement)
        {
            if (achievement.Points != null)
            {
                return achievement.Points.Value;
            }

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

        /// <summary>Every unlock: those in the games' achievements as last read, and those heard live that no read has shown yet.</summary>
        private IEnumerable<GameAchievement> AllUnlocks() => RecordedUnlocks().Concat(liveUnlocks);

        /// <summary>
        /// The Profile's totals and last unlocks, over every game this PC has achievements for; none at all without
        /// PlayniteAchievements, with no game having any, or before the first read, so unlocks heard live never make
        /// totals on their own. Profile &amp; system showing shows them in place, and Home's top bar shows the Score.
        /// Without totals, Home's Achievements tile says why: PlayniteAchievements isn't there, or it is, has been read
        /// and has nothing; not yet read, it says nothing.
        /// </summary>
        private void RefreshAchievements()
        {
            var any = HasAchievements && achievementsRead
                && (liveUnlocks.Count > 0 || recordedAchievements.Any(g => g.Achievements.Count > 0));
            if (!any)
            {
                Guide.Achievements = null;
                recentAchievements = new GameAchievement[0];
            }
            else
            {
                var unlocked = AllUnlocks().ToList();
                Guide.Achievements = new ProfileAchievements(unlocked.Sum(u => ScoreOf(u.Achievement)), unlocked.Count);
                recentAchievements = unlocked.OrderByDescending(u => u.Achievement.UnlockedAt).Take(RecentAchievementCount).ToList();
            }

            // Whichever of the two the tile is to show is set first, so it never shows neither, or the note of a moment ago.
            if (any)
            {
                Status.Achievements = Guide.Achievements;
                Status.AchievementsNote = null;
            }
            else
            {
                Status.AchievementsNote = !HasAchievements ? NeedsPlayniteAchievementsNote : achievementsRead ? NothingRecordedNote : null;
                Status.Achievements = null;
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
