using System;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// The <b>Game hub</b>'s backdrop with the extension: a snapshot of the screen the hub was opened from, which the
    /// Dashboard shows, darkened, above and below the hub's band (<see cref="DashboardBindings.HubBackdrop"/>). It is
    /// taken once as the hub opens and let go as it closes, so however often the hub opens, at most one is held. With
    /// none, the Dashboard shows the game's art there instead, as it does with the theme alone.
    /// The snapshot is the extension's (a small picture of Playnite's page); here it is any object.
    /// </summary>
    public sealed class HubBackdropState : INotifyPropertyChanged
    {
        private object snapshot;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The screen the open hub was opened from; null while no hub is open, or none could be taken.</summary>
        public object Snapshot
        {
            get => snapshot;
            private set
            {
                if (snapshot != value)
                {
                    var had = HasSnapshot;
                    snapshot = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Snapshot)));
                    if (had != HasSnapshot)
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSnapshot)));
                    }
                }
            }
        }

        /// <summary>Whether there is a <see cref="Snapshot"/> to show.</summary>
        public bool HasSnapshot => snapshot != null;

        /// <summary>
        /// The hub has opened: <paramref name="take"/> takes the snapshot, which may be null when there's nothing to take.
        /// Told again before the hub has closed, it keeps the one it has. Whether a snapshot was taken.
        /// </summary>
        public bool Open(Func<object> take)
        {
            if (HasSnapshot)
            {
                return false;
            }

            Snapshot = take();
            return HasSnapshot;
        }

        /// <summary>The hub has closed: the snapshot is let go. Whether there was one.</summary>
        public bool Close()
        {
            if (!HasSnapshot)
            {
                return false;
            }

            Snapshot = null;
            return true;
        }
    }
}
