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
        private Profile profile;
        private ProfileAchievements achievements;
        private string achievementsNote;
        private ControllerBattery battery = ControllerBattery.None;
        private int notificationCount;
        private bool isInDiscordCall;
        private bool isDiscordMuted;
        private bool isDiscordDeafened;
        private bool isDiscordSpeaking;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The profile signed in, which Home's top bar and the Guide's profile card show; null before the first sign-in.</summary>
        public Profile Profile
        {
            get => profile;
            internal set => Set(ref profile, value, nameof(Profile));
        }

        /// <summary>
        /// The achievement totals, the same as Profile &amp; system's (<see cref="GuideState.Achievements"/>), whose Score
        /// Home's top bar shows under the gamertag; null while there are none.
        /// </summary>
        public ProfileAchievements Achievements
        {
            get => achievements;
            internal set => Set(ref achievements, value, nameof(Achievements));
        }

        /// <summary>
        /// Why there are no <see cref="Achievements"/>, which Home's Achievements tile says under its title in the Score's
        /// place: that PlayniteAchievements is needed, or that it has recorded nothing yet. Null with totals, and until
        /// PlayniteAchievements has been looked for and, when it is there, first read.
        /// </summary>
        public string AchievementsNote
        {
            get => achievementsNote;
            internal set => Set(ref achievementsNote, value, nameof(AchievementsNote));
        }

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

        /// <summary>Whether the user is in a Discord call: the Discord icon shows only then.</summary>
        public bool IsInDiscordCall
        {
            get => isInDiscordCall;
            internal set => Set(ref isInDiscordCall, value, nameof(IsInDiscordCall));
        }

        /// <summary>Whether the user has muted themselves in Discord, while in a call; false out of one.</summary>
        public bool IsDiscordMuted
        {
            get => isDiscordMuted;
            internal set
            {
                if (Set(ref isDiscordMuted, value, nameof(IsDiscordMuted)))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DiscordBadge)));
                }
            }
        }

        /// <summary>Whether the user has deafened themselves in Discord, while in a call; false out of one.</summary>
        public bool IsDiscordDeafened
        {
            get => isDiscordDeafened;
            internal set
            {
                if (Set(ref isDiscordDeafened, value, nameof(IsDiscordDeafened)))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DiscordBadge)));
                }
            }
        }

        /// <summary>Whether the user is speaking in the call, which lights the Discord icon; false out of one.</summary>
        public bool IsDiscordSpeaking
        {
            get => isDiscordSpeaking;
            internal set => Set(ref isDiscordSpeaking, value, nameof(IsDiscordSpeaking));
        }

        /// <summary>The badge on the Discord icon: deafened over muted, as deafening mutes too.</summary>
        public DiscordBadge DiscordBadge =>
            isDiscordDeafened ? DiscordBadge.Deafened : isDiscordMuted ? DiscordBadge.Muted : DiscordBadge.None;

        private bool Set<T>(ref T field, T value, string name)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }

    /// <summary>The badge on the status area's Discord icon.</summary>
    public enum DiscordBadge
    {
        /// <summary>Neither muted nor deafened, or not in a call: no badge.</summary>
        None,

        /// <summary>Muted in Discord.</summary>
        Muted,

        /// <summary>Deafened, which wins over muted.</summary>
        Deafened,
    }
}
