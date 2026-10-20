using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Idle screen's cards: at Showing things, a pool of the nearly-finished games, the last unlocks, the last games
    /// played and the tips shows one card at a time in shuffled turn, each for <see cref="IdleCardShownFor"/>,
    /// reshuffled when it runs out; waking hides the card.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>How long each Idle screen card shows before the next.</summary>
        private static readonly TimeSpan IdleCardShownFor = TimeSpan.FromSeconds(20);

        /// <summary>How many cards the pool holds of each kind but tips, which are all in it.</summary>
        private const int IdleCardsOfAKind = 5;

        /// <summary>
        /// The Idle screen's tips, every one in the pool, each with the buttons it is about, highlighted on its controller.
        /// A button its text names is in braces, "{Y}", which the view draws as the button's icon.
        /// </summary>
        private static readonly IdleTip[] IdleTips =
        {
            new IdleTip("Press {Guide} anywhere, even in a game, to open the Guide.", new[] { ControllerButton.Guide }),
            new IdleTip("Press {Start} on a game tile to play it, favourite it or manage it.", new[] { ControllerButton.Start }),
            new IdleTip("Press {Y} on Home to search your games and apps.", new[] { ControllerButton.Y }),
            new IdleTip("Customise Home from a tile's menu, or Settings › Theme settings › Personalisation.", new[] { ControllerButton.Start }),
            new IdleTip("Flag a filter preset in Playnite to show it as a Group on Home.", new ControllerButton[0]),
            new IdleTip("On the keyboard, {X} is backspace, {Y} is space, {Start} is Enter.", new[] { ControllerButton.X, ControllerButton.Y, ControllerButton.Start }),
            new IdleTip("See what's downloading in My games & apps › Manage › Queue.", new ControllerButton[0]),
            new IdleTip("Your Score adds up achievements across every platform.", new ControllerButton[0]),
            new IdleTip("Connect Discord in Settings to see your calls in the Guide.", new[] { ControllerButton.Guide }),
            new IdleTip("Rejoin a recent Discord call in one press from the Discord tab.", new[] { ControllerButton.Guide }),
            new IdleTip("See what's playing in the Guide's Audio, under Now Playing.", new[] { ControllerButton.Guide }),
            new IdleTip("Pick a boot video, original Xbox to Series X, in Personalisation › Start up.", new ControllerButton[0]),
        };

        /// <summary>The Y prompt of a game's card, whose Game hub Y opens.</summary>
        private const string SeeGame = "See game";

        /// <summary>The cards of this Showing things, as built when it started, in their turn's order.</summary>
        private List<IdleCard> idleCards = new List<IdleCard>();

        /// <summary>Where the card showing is in <see cref="idleCards"/>.</summary>
        private int idleCardIndex;

        /// <summary>The next card's timer; null while no card shows, or one card stays.</summary>
        private IDisposable idleCardTimer;

        /// <summary>Showing things started: the pool is built and shuffled, and its first card shows at once. An empty pool shows none.</summary>
        private void StartIdleCards()
        {
            idleCards = IdleCardPool();
            Shuffle(idleCards, null);
            idleCardIndex = 0;
            ShowIdleCard();
        }

        /// <summary>The card at <see cref="idleCardIndex"/> shows, and with more than one card, the next after its time.</summary>
        private void ShowIdleCard()
        {
            Idle.Card = idleCards.Count > 0 ? idleCards[idleCardIndex] : null;
            if (idleCards.Count > 1)
            {
                idleCardTimer = clock.After(IdleCardShownFor, NextIdleCard);
            }
        }

        /// <summary>The next card in turn; past the last, the pool is shuffled again for another round.</summary>
        private void NextIdleCard()
        {
            idleCardTimer = null;
            idleCardIndex++;
            if (idleCardIndex == idleCards.Count)
            {
                Shuffle(idleCards, Idle.Card);
                idleCardIndex = 0;
            }

            ShowIdleCard();
        }

        /// <summary>Leaving Showing things: the card goes, and its turn with it.</summary>
        private void StopIdleCards()
        {
            idleCardTimer?.Dispose();
            idleCardTimer = null;
            Idle.Card = null;
        }

        /// <summary>
        /// Shuffles the cards with <see cref="random"/> (Fisher–Yates); <paramref name="last"/>, the card shown last, is
        /// never first after, so no card shows twice running.
        /// </summary>
        private void Shuffle(List<IdleCard> cards, IdleCard last)
        {
            for (var i = cards.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var swapped = cards[i];
                cards[i] = cards[j];
                cards[j] = swapped;
            }

            if (cards.Count > 1 && cards[0] == last)
            {
                var j = 1 + random.Next(cards.Count - 1);
                cards[0] = cards[j];
                cards[j] = last;
            }
        }

        /// <summary>
        /// Every card of the Idle screen's pool, unshuffled: the nearly-finished games, the last unlocks, the last games
        /// played, then the tips. Without PlayniteAchievements, or before its first read, the achievement kinds are left
        /// out, as the totals are.
        /// </summary>
        private List<IdleCard> IdleCardPool() =>
            (achievements.IsAvailable && achievementsRead ? CloseAchievementsCards().Concat(RecentUnlockCards()) : Enumerable.Empty<IdleCard>())
                .Concat(RecentlyPlayedCards())
                .Concat(IdleTips.Select(t => new IdleCard(IdleCardKind.Tip, t.Text, null, header: "Tip", prompt: "Got it", buttons: t.Buttons)))
                .ToList();

        /// <summary>
        /// The <see cref="IdleCardsOfAKind"/> nearly-finished games, in the library and never hidden: the most
        /// achievements unlocked without having them all, as last read and with unlocks heard live since, those nearer
        /// finished first among as many, then by name. Each card is the game and the locked achievement most players
        /// have, "Hades - Blessed", of unknown rarity only with no other named, the game alone with none: that one's
        /// Score, rarity and description, the game's progress, 42 of 48 unlocked, its art, and Y seeing the game.
        /// PlayniteAchievements has no progress counters, so this is as close as it gets.
        /// </summary>
        private IEnumerable<IdleCard> CloseAchievementsCards()
        {
            var live = new HashSet<string>(liveUnlocks.Select(u => u.Key));
            return recordedAchievements
                .Select(g => new
                {
                    Game = library.Games.FirstOrDefault(x => x.Id == g.GameId && !x.Hidden),
                    Total = g.Achievements.Count,
                    Locked = g.Achievements.Where(a => a.UnlockedAt == null && !live.Contains(UnlockKey(g.GameId, a.Key))).ToList(),
                })
                .Where(g => g.Game != null && g.Locked.Count > 0 && g.Locked.Count < g.Total)
                .OrderByDescending(g => g.Total - g.Locked.Count)
                .ThenByDescending(g => (double)(g.Total - g.Locked.Count) / g.Total)
                .ThenBy(g => g.Game.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(IdleCardsOfAKind)
                .Select(g =>
                {
                    var next = g.Locked
                        .Where(a => !string.IsNullOrEmpty(a.Name))
                        .OrderByDescending(a => a.Rarity ?? double.MinValue)
                        .FirstOrDefault();
                    return new IdleCard(
                        IdleCardKind.CloseAchievements,
                        next == null ? g.Game.Name : g.Game.Name + " - " + next.Name,
                        next?.Description,
                        HeroArtOf(g.Game),
                        label: "Close achievement",
                        score: next == null ? (int?)null : ScoreOf(next),
                        progress: (double)(g.Total - g.Locked.Count) / g.Total,
                        isRare: next != null && IsRare(next),
                        prompt: SeeGame,
                        gameId: g.Game.Id);
                })
                .ToList();
        }

        /// <summary>
        /// The last <see cref="IdleCardsOfAKind"/> unlocks, as last read and heard live since, of games in the library and
        /// never hidden: each with when atop, "Unlocked 2 days ago", its Score and rarity, its game and name, "Hades -
        /// Blessed", its description, its icon and its game's art, with Y seeing the game.
        /// </summary>
        private IEnumerable<IdleCard> RecentUnlockCards() =>
            AllUnlocks()
                .Select(u => new { Unlock = u.Achievement, Game = library.Games.FirstOrDefault(g => g.Id == u.GameId && !g.Hidden) })
                .Where(u => u.Game != null)
                .OrderByDescending(u => u.Unlock.UnlockedAt)
                .Take(IdleCardsOfAKind)
                .Select(u => new IdleCard(
                    IdleCardKind.RecentUnlock,
                    u.Game.Name + " - " + u.Unlock.Name,
                    u.Unlock.Description,
                    HeroArtOf(u.Game),
                    u.Unlock.Icon,
                    header: "Unlocked " + IdleWhen(u.Unlock.UnlockedAt.Value),
                    label: "Achievement unlocked",
                    score: ScoreOf(u.Unlock),
                    isRare: IsRare(u.Unlock),
                    prompt: SeeGame,
                    gameId: u.Game.Id))
                .ToList();

        /// <summary>
        /// The last <see cref="IdleCardsOfAKind"/> games played, never hidden ones: each with its art, when it was last
        /// played atop, "Played yesterday", its name, and its playtime, "12 h played", with Y seeing the game.
        /// </summary>
        private IEnumerable<IdleCard> RecentlyPlayedCards() =>
            PlayedGames().Take(IdleCardsOfAKind).Select(g =>
            {
                var played = PlaytimeWords(g.Playtime);
                return new IdleCard(
                    IdleCardKind.RecentlyPlayed,
                    g.Name,
                    played == null ? null : played + " played",
                    HeroArtOf(g),
                    header: "Played " + IdleWhen(g.LastActivity.Value),
                    label: "Recently played",
                    prompt: SeeGame,
                    gameId: g.Id);
            });

        /// <summary>A playtime in seconds as the card words it, "12 h" or under an hour "45 min"; null under a minute.</summary>
        private static string PlaytimeWords(ulong seconds) =>
            seconds >= 3600 ? seconds / 3600 + " h"
            : seconds >= 60 ? seconds / 60 + " min"
            : null;

        /// <summary>
        /// When something happened, from now by calendar day, as the Idle screen's cards word it: "today", "yesterday",
        /// "3 days ago", "a week ago", "2 weeks ago", "a month ago", "5 months ago", "a year ago", "3 years ago". A time
        /// ahead of now is today.
        /// </summary>
        private string IdleWhen(DateTime at)
        {
            var today = clock.Now.Date;
            var day = at.Date;
            var days = (today - day).Days;
            if (days <= 0)
            {
                return "today";
            }

            if (days == 1)
            {
                return "yesterday";
            }

            if (days < 7)
            {
                return days + " days ago";
            }

            if (days < 30)
            {
                return days < 14 ? "a week ago" : days / 7 + " weeks ago";
            }

            // Whole calendar months, one more only once the day of the month comes round again.
            var months = (today.Year - day.Year) * 12 + today.Month - day.Month - (today.Day < day.Day ? 1 : 0);
            months = Math.Max(1, months);
            if (months < 12)
            {
                return months == 1 ? "a month ago" : months + " months ago";
            }

            var years = months / 12;
            return years == 1 ? "a year ago" : years + " years ago";
        }

        /// <summary>One of the Idle screen's tips: its text, and the buttons it is about.</summary>
        private sealed class IdleTip
        {
            public IdleTip(string text, ControllerButton[] buttons)
            {
                Text = text;
                Buttons = buttons;
            }

            public string Text { get; }
            public ControllerButton[] Buttons { get; }
        }
    }
}
