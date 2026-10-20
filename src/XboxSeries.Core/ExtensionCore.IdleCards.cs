using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Idle screen's cards: at Showing things, a pool of the last games played and tips is shown one card at a time in shuffled turn, each
    /// for <see cref="IdleCardShownFor"/>, reshuffled when it runs out; waking hides the card.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>How long each Idle screen card shows before the next.</summary>
        private static readonly TimeSpan IdleCardShownFor = TimeSpan.FromSeconds(10);

        /// <summary>How many cards the pool holds of each kind but tips, which are all in it.</summary>
        private const int IdleCardsOfAKind = 5;

        /// <summary>The Idle screen's tips, every one in the pool, text only.</summary>
        private static readonly string[] IdleTips =
        {
            "Press the Guide button anywhere, even in a game, to open the Guide.",
            "Press Menu on a game tile to play it, favourite it or manage it.",
            "Press Y on Home to search your games and apps.",
            "Customise Home from a tile's menu, or Settings › Theme settings › Personalisation.",
            "Flag a filter preset in Playnite to show it as a Group on Home.",
            "On the keyboard, X is backspace, Y is space, Start is Enter.",
            "See what's downloading in My games & apps › Manage › Queue.",
            "Your Score adds up achievements across every platform.",
            "Connect Discord in Settings to see your calls in the Guide.",
            "Rejoin a recent Discord call in one press from the Discord tab.",
            "See what's playing in the Guide's Audio, under Now Playing.",
            "Pick a boot video, original Xbox to Series X, in Personalisation › Start up.",
        };

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

        /// <summary>Every card of the Idle screen's pool, unshuffled: the last games played, then the tips.</summary>
        private List<IdleCard> IdleCardPool() =>
            RecentlyPlayedCards()
                .Concat(IdleTips.Select(t => new IdleCard(IdleCardKind.Tip, t, null)))
                .ToList();

        /// <summary>
        /// The last <see cref="IdleCardsOfAKind"/> games played, never hidden ones: each with its art, its name, and its
        /// playtime and when it was last played, "12 h played · yesterday", or only when, "Yesterday", with no playtime.
        /// </summary>
        private IEnumerable<IdleCard> RecentlyPlayedCards() =>
            PlayedGames().Take(IdleCardsOfAKind).Select(g =>
            {
                var when = IdleWhen(g.LastActivity.Value);
                var played = PlaytimeWords(g.Playtime);
                return new IdleCard(
                    IdleCardKind.RecentlyPlayed,
                    g.Name,
                    played == null ? Capitalised(when) : played + " played · " + when,
                    HeroArtOf(g));
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

        private static string Capitalised(string words) => char.ToUpperInvariant(words[0]) + words.Substring(1);
    }
}
