using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>Which console generation's boot animation plays as Playnite starts in Fullscreen, oldest first; Off plays none.</summary>
    public enum BootVideo
    {
        Off,
        Xbox,
        Xbox360,
        Xbox360Slim,
        Xbox360SpecialEdition,
        XboxOne,
        XboxOneX,
        XboxSeriesS,
        XboxSeriesX,
    }

    /// <summary>Where the Start-up sequence is, in its order; None once Home shows.</summary>
    public enum StartupPhase
    {
        None,

        /// <summary>The chosen <see cref="BootVideo"/>, full screen; a button press skips it.</summary>
        BootVideo,

        /// <summary>Who's playing today?: the profiles in a row, one focused, over the green swirl.</summary>
        WhosPlaying,

        /// <summary>Signing you in: the chosen profile alone, centred.</summary>
        SigningIn,
    }

    /// <summary>
    /// What a Start-up sequence begins with, as the core hands it to the sequence's own thread: the boot video (Off on
    /// waking from sleep), whether Who's playing today? asks, the profiles, and the one signed in last, whom it focuses
    /// and, when it doesn't ask, signs in.
    /// </summary>
    public sealed class StartupPlan
    {
        public StartupPlan(BootVideo bootVideo, bool askWhosPlaying, IReadOnlyList<Profile> profiles, Profile lastProfile)
        {
            BootVideo = bootVideo;
            AskWhosPlaying = askWhosPlaying;
            Profiles = profiles;
            LastProfile = lastProfile;
        }

        public BootVideo BootVideo { get; }

        public bool AskWhosPlaying { get; }

        public IReadOnlyList<Profile> Profiles { get; }

        public Profile LastProfile { get; }
    }

    /// <summary>
    /// One person who plays on this PC: a gamertag, a real name and a picture. It is identity only; the Bundle's options
    /// are shared by every profile.
    /// </summary>
    public sealed class Profile
    {
        public Profile(string id, string gamertag, string realName, string picture)
        {
            Id = id;
            Gamertag = gamertag ?? "";
            RealName = realName ?? "";
            Picture = picture;
        }

        public string Id { get; }

        public string Gamertag { get; }

        /// <summary>The person's real name, shown under the gamertag; may be empty.</summary>
        public string RealName { get; }

        /// <summary>A full path to the profile's picture: a gamerpic or the Windows account picture; null for none.</summary>
        public string Picture { get; }

        /// <summary>Whether the profile has a real name: only then does Home's top bar swap it with the gamertag.</summary>
        public bool HasRealName => RealName.Trim().Length > 0;

        /// <summary>The name "Hi X!" greets: the real name's first word, or the gamertag without one.</summary>
        public string FirstName
        {
            get
            {
                var real = RealName.Trim();
                return real.Length == 0 ? Gamertag : real.Split(' ')[0];
            }
        }
    }

    /// <summary>
    /// The Start-up sequence shown full screen over Playnite as it starts in Fullscreen, and Who's playing today? again on
    /// waking from sleep. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class StartupState : INotifyPropertyChanged
    {
        private StartupPhase phase;
        private BootVideo bootVideo;
        private IReadOnlyList<Profile> profiles = new Profile[0];
        private Profile focusedProfile;
        private int focusedIndex;
        private Profile signingIn;

        public event PropertyChangedEventHandler PropertyChanged;

        public StartupPhase Phase
        {
            get => phase;
            internal set
            {
                if (Set(ref phase, value, nameof(Phase)))
                {
                    Changed(nameof(IsShown));
                }
            }
        }

        /// <summary>Whether the sequence covers the screen.</summary>
        public bool IsShown => phase != StartupPhase.None;

        /// <summary>The generation whose video plays while <see cref="Phase"/> is BootVideo.</summary>
        public BootVideo BootVideo
        {
            get => bootVideo;
            internal set => Set(ref bootVideo, value, nameof(BootVideo));
        }

        /// <summary>Who's playing today?'s profiles, left to right.</summary>
        public IReadOnlyList<Profile> Profiles
        {
            get => profiles;
            internal set => Set(ref profiles, value, nameof(Profiles));
        }

        /// <summary>The profile Who's playing today? has focused, which A signs in.</summary>
        public Profile FocusedProfile
        {
            get => focusedProfile;
            internal set => Set(ref focusedProfile, value, nameof(FocusedProfile));
        }

        /// <summary>
        /// Where <see cref="FocusedProfile"/> is in <see cref="Profiles"/>, counting from 0 on the left, set before it, so
        /// the row can slide sideways to put it at the screen's centre, as the console does.
        /// </summary>
        public int FocusedIndex
        {
            get => focusedIndex;
            internal set => Set(ref focusedIndex, value, nameof(FocusedIndex));
        }

        /// <summary>The profile Signing you in shows.</summary>
        public Profile SigningIn
        {
            get => signingIn;
            internal set => Set(ref signingIn, value, nameof(SigningIn));
        }

        private bool Set<T>(ref T field, T value, string name)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            Changed(name);
            return true;
        }

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    /// <summary>Personalisation, in Settings: the boot video, the profiles, and whether Who's playing today? asks.</summary>
    public sealed class StartUpSettingsState : INotifyPropertyChanged
    {
        private BootVideo bootVideo = BootVideo.XboxSeriesX;
        private bool askWhosPlaying = true;
        private IReadOnlyList<Profile> profiles = new Profile[0];
        private IReadOnlyList<string> gamerpics = new string[0];

        public event PropertyChangedEventHandler PropertyChanged;

        public BootVideo BootVideo
        {
            get => bootVideo;
            internal set => Set(ref bootVideo, value, nameof(BootVideo));
        }

        /// <summary>Whether Who's playing today? asks on every start and on waking from sleep; off signs the last profile in.</summary>
        public bool AskWhosPlaying
        {
            get => askWhosPlaying;
            internal set => Set(ref askWhosPlaying, value, nameof(AskWhosPlaying));
        }

        /// <summary>Every profile, in the order Who's playing today? shows them. There is always at least one.</summary>
        public IReadOnlyList<Profile> Profiles
        {
            get => profiles;
            internal set => Set(ref profiles, value, nameof(Profiles));
        }

        /// <summary>The pictures a profile can wear: the Windows account picture first, then each in the Gamerpics folder.</summary>
        public IReadOnlyList<string> Gamerpics
        {
            get => gamerpics;
            internal set => Set(ref gamerpics, value, nameof(Gamerpics));
        }

        private void Set<T>(ref T field, T value, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
