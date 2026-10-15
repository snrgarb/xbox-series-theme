using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// What the status area shows, on Home's top bar and at the Guide's top right alike. Views bind to it and hold no
    /// logic of their own.
    /// </summary>
    public sealed class StatusState : INotifyPropertyChanged
    {
        private string time = "";
        private ControllerBattery battery;
        private int notificationCount;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The time to the minute, in the user's own short format.</summary>
        public string Time
        {
            get => time;
            internal set => Set(ref time, value, nameof(Time));
        }

        /// <summary>The controller's battery.</summary>
        public ControllerBattery Battery
        {
            get => battery;
            internal set => Set(ref battery, value, nameof(Battery));
        }

        /// <summary>How many of Playnite's own notifications there are.</summary>
        public int NotificationCount
        {
            get => notificationCount;
            internal set => Set(ref notificationCount, value, nameof(NotificationCount));
        }

        private void Set<T>(ref T field, T value, string name)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
