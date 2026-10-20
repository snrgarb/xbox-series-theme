using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>The Idle screen's stages, as no input goes on: awake, the dim, then the things it shows over the dim.</summary>
    public enum IdleStage
    {
        Awake,

        /// <summary>The screen dims, after <see cref="IdleState.DimAfter"/> with no input.</summary>
        Dimmed,

        /// <summary>The Idle screen shows its things over the dim, after <see cref="IdleState.ShowThingsAfter"/> with no input.</summary>
        ShowingThings,
    }

    /// <summary>
    /// The Idle screen: with no input for a while the screen dims, over every window but Toasts, and later shows things
    /// over the dim. The first input after only wakes it. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class IdleState : INotifyPropertyChanged
    {
        private IdleStage stage;
        private IdleCard card;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>How long with no input before the screen dims; null for Never, which switches the stage off.</summary>
        public TimeSpan? DimAfter { get; internal set; } = TimeSpan.FromMinutes(2);

        /// <summary>How long with no input before the Idle screen shows things; null for Never, which switches the stage off.</summary>
        public TimeSpan? ShowThingsAfter { get; internal set; } = TimeSpan.FromMinutes(5);

        public IdleStage Stage
        {
            get => stage;
            internal set
            {
                if (stage != value)
                {
                    stage = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stage)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDimmed)));
                }
            }
        }

        /// <summary>Whether the dim shows: while Dimmed or Showing things.</summary>
        public bool IsDimmed => stage != IdleStage.Awake;

        /// <summary>
        /// The card on the idle panel, the right half of the screen flown in from the right over the dim, while Showing
        /// things, each for a while in shuffled turn; null while Awake or Dimmed, or with nothing to show. The view
        /// crossfades from one to the next and hides it with the dim.
        /// </summary>
        public IdleCard Card
        {
            get => card;
            internal set
            {
                if (card != value)
                {
                    card = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Card)));
                }
            }
        }
    }

    /// <summary>What an Idle screen card tells of.</summary>
    public enum IdleCardKind
    {
        /// <summary>A nearly-finished game: the most achievements unlocked without having them all.</summary>
        CloseAchievements,

        /// <summary>One of the last unlocks.</summary>
        RecentUnlock,

        /// <summary>One of the last games played.</summary>
        RecentlyPlayed,

        /// <summary>A tip, text only.</summary>
        Tip,
    }

    /// <summary>
    /// One Idle screen card, the console's idle panel flown in from the right: a top line (a Score and a progress, or a
    /// header), a bold title, a label and a description, a game's art or an achievement's icon in the lower half, and a
    /// Y prompt. Whatever is null isn't shown.
    /// </summary>
    public sealed class IdleCard
    {
        internal IdleCard(
            IdleCardKind kind,
            string title,
            string description,
            HeroArt art = null,
            string icon = null,
            string header = null,
            string label = null,
            int? score = null,
            double? progress = null,
            bool isRare = false,
            string prompt = null,
            Guid? gameId = null,
            IReadOnlyList<ControllerButton> buttons = null)
        {
            Kind = kind;
            Title = title;
            Description = description;
            Art = art;
            Icon = icon;
            Header = header;
            Label = label;
            Score = score;
            Progress = progress;
            IsRare = isRare;
            Prompt = prompt;
            GameId = gameId;
            Buttons = buttons ?? new ControllerButton[0];
        }

        public IdleCardKind Kind { get; }

        /// <summary>
        /// The bold title: "Halo 5: Guardians - Kraken Lackin'", a game's name, or a tip's text, which names a button by
        /// its <see cref="ControllerButton"/> in braces, "{Y}", for the view to draw as the button's icon.
        /// </summary>
        public string Title { get; }

        /// <summary>The line under the label: an achievement's description, or a game's playtime; null for none.</summary>
        public string Description { get; }

        /// <summary>A game's art, falling back as Home's hero does, its cover for missing background art; null for none.</summary>
        public HeroArt Art { get; }

        /// <summary>A full path or URI to an achievement's icon; null for none.</summary>
        public string Icon { get; }

        /// <summary>The light top line in place of a Score: "Unlocked 2 days ago", "Played yesterday", "Tip"; null for none.</summary>
        public string Header { get; }

        /// <summary>The bold line under the title: "Close achievement", "Achievement unlocked", "Recently played"; null for none.</summary>
        public string Label { get; }

        /// <summary>The achievement's Score, by the G at the top; null for none.</summary>
        public int? Score { get; }

        /// <summary>How far along, 0 to 1: the top right's percentage and the bar under it; null for none.</summary>
        public double? Progress { get; }

        /// <summary>Whether the achievement is rare, fewer than 10% of players having it: the diamond by the Score.</summary>
        public bool IsRare { get; }

        /// <summary>The Y prompt's words, "See game" or "Got it"; null for none.</summary>
        public string Prompt { get; }

        /// <summary>The game Y opens the Game hub of; null where Y only wakes the screen.</summary>
        public Guid? GameId { get; }

        /// <summary>The buttons a tip is about, highlighted on its controller; empty for none.</summary>
        public IReadOnlyList<ControllerButton> Buttons { get; }
    }
}
