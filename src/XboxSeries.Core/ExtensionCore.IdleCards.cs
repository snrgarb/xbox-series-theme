using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Idle screen's cards: at Showing things, a pool of tips is shown one card at a time in shuffled turn, each
    /// for <see cref="IdleCardShownFor"/>, reshuffled when it runs out; waking hides the card.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>How long each Idle screen card shows before the next.</summary>
        private static readonly TimeSpan IdleCardShownFor = TimeSpan.FromSeconds(10);

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

        /// <summary>Every card of the Idle screen's pool, unshuffled.</summary>
        private List<IdleCard> IdleCardPool() =>
            IdleTips.Select(t => new IdleCard(IdleCardKind.Tip, t, null)).ToList();
    }
}
