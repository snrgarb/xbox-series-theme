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
    }
}
