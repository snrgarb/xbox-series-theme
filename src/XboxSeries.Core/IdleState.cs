using System;
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
        /// The card centred on the dim while Showing things, each for a while in shuffled turn; null while Awake or
        /// Dimmed, or with nothing to show. The view crossfades from one to the next and hides it with the dim.
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

    /// <summary>One Idle screen card: art or an icon, a title line and a detail line.</summary>
    public sealed class IdleCard
    {
        internal IdleCard(IdleCardKind kind, string title, string detail, HeroArt art = null, string icon = null)
        {
            Kind = kind;
            Title = title;
            Detail = detail;
            Art = art;
            Icon = icon;
        }

        public IdleCardKind Kind { get; }

        public string Title { get; }

        public string Detail { get; }

        /// <summary>A game's art, falling back as Home's hero does, its cover for missing background art; null for none.</summary>
        public HeroArt Art { get; }

        /// <summary>A full path or URI to an achievement's icon; null for none.</summary>
        public string Icon { get; }
    }
}
