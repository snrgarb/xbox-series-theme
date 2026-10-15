namespace XboxSeries.Core
{
    /// <summary>
    /// The Start-up sequence (boot video, Who's playing today?, Signing you in, then "Hi X!") and Settings' Start up
    /// category, which chooses the boot video, keeps the profiles, and says whether Who's playing today? asks.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The Start-up sequence, shown full screen over Playnite.</summary>
        public StartupState Startup { get; } = new StartupState();

        /// <summary>
        /// Playnite started in Fullscreen: the sequence begins with the chosen boot video, or Who's playing today? with
        /// it Off.
        /// </summary>
        public void OnStartup()
        {
        }

        /// <summary>The boot video finished, or couldn't play: the sequence goes on.</summary>
        public void OnBootVideoEnded()
        {
        }

        /// <summary>Windows woke from sleep: Who's playing today? asks again, if it asks.</summary>
        public void OnResumedFromSleep()
        {
        }

        /// <summary>Settings' Boot video page: the generation that plays at start, or Off. Saved at once.</summary>
        public void SetBootVideo(BootVideo video)
        {
        }

        /// <summary>Settings' Who's playing today? box: whether it asks on every start and wake. Saved at once.</summary>
        public void SetAskWhosPlaying(bool ask)
        {
        }

        /// <summary>Settings' Profiles page: a new profile, after the others, wearing the Windows account picture.</summary>
        public Profile AddProfile(string gamertag)
        {
            return null;
        }

        /// <summary>A profile's gamertag, typed on the keyboard; trimmed, and empty is refused.</summary>
        public void SetProfileGamertag(string id, string gamertag)
        {
        }

        /// <summary>A profile's real name, typed on the keyboard; trimmed, and may be empty.</summary>
        public void SetProfileRealName(string id, string realName)
        {
        }

        /// <summary>A profile's picture, one of <see cref="StartUpSettingsState.Gamerpics"/>.</summary>
        public void SetProfilePicture(string id, string picture)
        {
        }

        /// <summary>Forgets a profile; the last one is never removed.</summary>
        public void RemoveProfile(string id)
        {
        }
    }
}
