using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Start-up sequence (boot video, Who's playing today?, Signing you in, then "Hi X!") and Theme settings'
    /// Personalisation page, which chooses the boot video, keeps the profiles, and says whether Who's playing
    /// today? asks.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// The secret store's name for the Personalisation options, as JSON: the boot video, whether Who's playing today? asks,
        /// Background delay, the profiles and the one last signed in. They aren't secret, but the store keeps the extension's own options.
        /// </summary>
        private const string StartUpSecret = "StartUp";

        /// <summary>How long Signing you in shows before "Hi X!" and Home.</summary>
        private static readonly TimeSpan SigningInDuration = TimeSpan.FromSeconds(3);

        private List<Profile> profiles = new List<Profile>();

        /// <summary>The profile signed in last, whom Who's playing today? focuses and, when it doesn't ask, signs in; null for the first.</summary>
        private string lastProfileId;

        /// <summary>Background delay as the player set it; null while they never have, and the default stands.</summary>
        private bool? backgroundDelayChosen;

        /// <summary>Signing you in's <see cref="SigningInDuration"/>, while it shows; null otherwise.</summary>
        private IDisposable signingInTimer;

        private bool isStartupShown;

        /// <summary>The greeting's Toast showing, until it goes; null otherwise.</summary>
        private IDisposable greetingTimer;

        /// <summary>The call's Toast, held back while Start-up or the greeting shows, to show as the greeting goes; null for none.</summary>
        private Action waitingRingToast;

        /// <summary>
        /// Whether a Start-up sequence covers the screen, from its beginning until Signing you in has shown. The sequence
        /// runs on a thread of its own; while it shows, the buttons Playnite hands the core reach nothing.
        /// </summary>
        public bool IsStartupShown
        {
            get => isStartupShown;
            private set
            {
                if (isStartupShown != value)
                {
                    isStartupShown = value;
                    RefreshHints();
                    LookAtIdlePause();
                }
            }
        }

        /// <summary>Signing you in has shown: the Start-up window fades out to Home as "Hi X!" shows. Raised on the UI thread.</summary>
        public event Action StartupEnded;

        /// <summary>Who's playing today? asked from the Guide's Switch profile: the plan for a Start-up sequence to begin with.</summary>
        public event Action<StartupPlan> WhosPlayingAsked;

        /// <summary>
        /// The sequence's thread signed a profile in, marshalled to the UI thread: the core's own profile of that id is
        /// signed in at once, on the status area and saved as the last; the sequence has played the sign-in sound. After
        /// <see cref="SigningInDuration"/> Start-up ends and Home shows with "Hi X!". Out of turn, it changes nothing.
        /// </summary>
        public void OnStartupSignedIn(Profile profile)
        {
            if (!IsStartupShown || signingInTimer != null || profile == null)
            {
                return;
            }

            var signedIn = profiles.FirstOrDefault(p => p.Id == profile.Id) ?? profile;
            Status.Profile = signedIn;
            lastProfileId = signedIn.Id;
            SaveStartUp();
            signingInTimer = clock.After(SigningInDuration, () =>
            {
                signingInTimer = null;
                IsStartupShown = false;
                StartupEnded?.Invoke();
                Home.Reveal();
                Greet(Status.Profile ?? signedIn);
            });
        }

        /// <summary>
        /// Start-up covers the screen, the Guide and any Toast going first, and the plan its sequence begins with is
        /// made; null while Start-up already shows.
        /// </summary>
        private StartupPlan BeginStartup(BootVideo video, bool askWhosPlaying)
        {
            if (IsStartupShown)
            {
                return null;
            }

            CoverForStartup();
            IsStartupShown = true;
            return new StartupPlan(video, askWhosPlaying, profiles.ToList(), LastProfile);
        }

        /// <summary>
        /// Playnite started in Fullscreen: Start-up covers the screen, and the plan its sequence begins with, the chosen
        /// boot video first, is handed to the sequence's thread. Null while Start-up already shows.
        /// </summary>
        public StartupPlan OnStartup() => BeginStartup(Settings.StartUp.BootVideo, Settings.StartUp.AskWhosPlaying);

        /// <summary>
        /// Windows woke from sleep: Who's playing today? asks again, if it asks, without the boot video. The Guide, and
        /// any Toast, go first.
        /// </summary>
        public StartupPlan OnResumedFromSleep() =>
            Settings.StartUp.AskWhosPlaying ? BeginStartup(BootVideo.Off, true) : null;

        /// <summary>
        /// The Guide's Switch profile: Who's playing today? asks, without the boot video, even when it is turned off for
        /// start and waking; the Guide goes first. A Guide entry hands nothing back, so the plan goes out on
        /// <see cref="WhosPlayingAsked"/> rather than as a return value.
        /// </summary>
        private void SwitchProfile()
        {
            var plan = BeginStartup(BootVideo.Off, true);
            if (plan != null)
            {
                WhosPlayingAsked?.Invoke(plan);
            }
        }

        /// <summary>Settings' Boot video page: the generation that plays at start, or Off. Saved at once.</summary>
        public void SetBootVideo(BootVideo video)
        {
            Settings.StartUp.BootVideo = video;
            SaveStartUp();
        }

        /// <summary>Settings' Who's playing today? box: whether it asks on every start and wake. Saved at once.</summary>
        public void SetAskWhosPlaying(bool ask)
        {
            Settings.StartUp.AskWhosPlaying = ask;
            SaveStartUp();
        }

        /// <summary>
        /// Settings' Background delay box: whether Home's hero waits for focus to rest on a Recent-row game before showing
        /// its art. Saved at once, and in force from the next tile focused.
        /// </summary>
        public void SetBackgroundDelay(bool on)
        {
            backgroundDelayChosen = on;
            Settings.StartUp.BackgroundDelay = on;
            SaveStartUp();
        }

        /// <summary>Settings' Profiles page: a new profile, after the others, wearing the Windows account picture.</summary>
        public Profile AddProfile(string gamertag)
        {
            var trimmed = gamertag?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            var added = new Profile(Guid.NewGuid().ToString(), trimmed, "", account.Picture);
            profiles.Add(added);
            ProfilesChanged();
            return added;
        }

        /// <summary>A profile's gamertag, typed on the keyboard; trimmed, and empty is refused.</summary>
        public void SetProfileGamertag(string id, string gamertag)
        {
            var trimmed = gamertag?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                ReplaceProfile(id, p => new Profile(p.Id, trimmed, p.RealName, p.Picture));
            }
        }

        /// <summary>A profile's real name, typed on the keyboard; trimmed, and may be empty.</summary>
        public void SetProfileRealName(string id, string realName)
        {
            ReplaceProfile(id, p => new Profile(p.Id, p.Gamertag, realName?.Trim() ?? "", p.Picture));
        }

        /// <summary>A profile's picture, one of <see cref="StartUpSettingsState.Gamerpics"/>.</summary>
        public void SetProfilePicture(string id, string picture)
        {
            RefreshGamerpics();
            if (picture != null && Settings.StartUp.Gamerpics.Contains(picture))
            {
                ReplaceProfile(id, p => new Profile(p.Id, p.Gamertag, p.RealName, picture));
            }
        }

        /// <summary>Forgets a profile; the last one is never removed. The one signed in last gone, the first is signed in next.</summary>
        public void RemoveProfile(string id)
        {
            if (profiles.Count < 2 || profiles.RemoveAll(p => p.Id == id) == 0)
            {
                return;
            }

            if (lastProfileId == id)
            {
                lastProfileId = null;
            }

            ProfilesChanged();
        }

        /// <summary>The profile signed in last, or the first when that one has gone or none has signed in yet.</summary>
        private Profile LastProfile => profiles.FirstOrDefault(p => p.Id == lastProfileId) ?? profiles[0];

        /// <summary>
        /// Start-up is about to cover the screen and take the pad: the Guide closes, a Guide button press or D-pad repeat
        /// under way is forgotten, and the Toasts are held back until the greeting has gone, a notification's showing to
        /// show again in its turn.
        /// </summary>
        private void CoverForStartup()
        {
            CancelHold();
            guideTapTimer?.Dispose();
            guideTapTimer = null;
            StopRepeat();
            CloseTileMenu();
            if (Guide.IsOpen)
            {
                DismissAtOnce(ReturnToRecordedWindow);
            }

            PutBackShownToast();
            ringToastTimer?.Dispose();
            ringToastTimer = null;
            greetingTimer?.Dispose();
            greetingTimer = null;
            Toast.IsShown = false;
        }

        /// <summary>
        /// The greeting's Toast, "Hi X!" with the profile's picture and the notification sound, for as long as a
        /// notification's; the call's Toast and Playnite's notifications held back meanwhile follow it.
        /// </summary>
        private void Greet(Profile profile)
        {
            Toast.Show("Hi " + profile.FirstName + "!", ToastKind.Greeting, profile.Picture);
            sounds.Play(UiSound.Notification);
            greetingTimer = clock.After(ToastDuration, () =>
            {
                greetingTimer = null;
                Toast.IsShown = false;
                var ring = waitingRingToast;
                waitingRingToast = null;
                if (ring != null)
                {
                    ring();
                }
                else if (waitingToasts.Count > 0)
                {
                    toastTimer = clock.After(ToastGap, ShowNextToast);
                }
            });
        }

        /// <summary>
        /// The Personalisation options saved before, read when the core is made; with none, or none readable, the Xbox Series X
        /// boot video, asking Who's playing today?, and one profile of the Windows user.
        /// </summary>
        private void LoadStartUp()
        {
            SavedStartUp saved;
            try
            {
                var json = secrets.Read(StartUpSecret);
                saved = json == null ? null : JsonConvert.DeserializeObject<SavedStartUp>(json);
            }
            catch (JsonException)
            {
                saved = null;
            }

            profiles = (saved?.Profiles ?? new List<SavedProfile>())
                .Where(p => !string.IsNullOrEmpty(p?.Id) && !string.IsNullOrWhiteSpace(p.Gamertag))
                .Select(p => new Profile(p.Id, p.Gamertag, p.RealName, p.Picture))
                .ToList();
            if (profiles.Count == 0)
            {
                profiles.Add(new Profile(Guid.NewGuid().ToString(), account.Name, account.RealName, account.Picture));
            }

            lastProfileId = saved?.LastProfileId;
            Settings.StartUp.BootVideo = saved != null && Enum.IsDefined(typeof(BootVideo), saved.BootVideo) ? saved.BootVideo : BootVideo.XboxSeriesX;
            Settings.StartUp.AskWhosPlaying = saved?.AskWhosPlaying ?? true;
            backgroundDelayChosen = saved?.BackgroundDelay;
            Settings.StartUp.BackgroundDelay = backgroundDelayChosen ?? StartUpSettingsState.BackgroundDelayByDefault;
            ShowProfiles();
            RefreshGamerpics();
        }

        private void SaveStartUp() => secrets.Write(StartUpSecret, JsonConvert.SerializeObject(new SavedStartUp
        {
            BootVideo = Settings.StartUp.BootVideo,
            AskWhosPlaying = Settings.StartUp.AskWhosPlaying,
            BackgroundDelay = backgroundDelayChosen,
            Profiles = profiles.Select(p => new SavedProfile { Id = p.Id, Gamertag = p.Gamertag, RealName = p.RealName, Picture = p.Picture }).ToList(),
            LastProfileId = lastProfileId,
        }));

        /// <summary>The pictures a profile can wear, read afresh: the Windows account picture, then the Gamerpics folder's.</summary>
        private void RefreshGamerpics()
        {
            var pictures = new List<string>();
            if (account.Picture != null)
            {
                pictures.Add(account.Picture);
            }

            pictures.AddRange((account.Gamerpics ?? new string[0]).Where(p => !pictures.Contains(p)));
            if (!pictures.SequenceEqual(Settings.StartUp.Gamerpics))
            {
                Settings.StartUp.Gamerpics = pictures;
            }
        }

        /// <summary>
        /// A profile edited: the new one takes its place, and wherever it shows; an unknown id changes nothing. Start-up
        /// never shows meanwhile: it covers Settings, where profiles are edited.
        /// </summary>
        private void ReplaceProfile(string id, Func<Profile, Profile> edit)
        {
            var index = profiles.FindIndex(p => p.Id == id);
            if (index < 0)
            {
                return;
            }

            var edited = edit(profiles[index]);
            profiles[index] = edited;
            ProfilesChanged();
            if (Status.Profile?.Id == id)
            {
                Status.Profile = edited;
                ShowProfileInGuide();
            }
        }

        /// <summary>The picture Profile &amp; system wears: the profile signed in's, or the Windows user's before the first sign-in.</summary>
        private string ProfilePicture => Status.Profile != null ? Status.Profile.Picture : account.Picture;

        /// <summary>The profile signed in was edited with the Guide open: its tab icon and card show it, focus kept where it was.</summary>
        private void ShowProfileInGuide()
        {
            if (!Guide.IsOpen)
            {
                return;
            }

            RefreshTabs();
            if (Guide.Tab == GuideTabKind.ProfileAndSystem && Guide.Flyout == null)
            {
                var index = Guide.Items.ToList().IndexOf(Guide.FocusedItem);
                ShowProfileTab();
                Guide.FocusedItem = Guide.Items[Clamp(index, Guide.Items.Count)];
            }
        }

        /// <summary>The profiles changed: saved at once, and shown afresh.</summary>
        private void ProfilesChanged()
        {
            SaveStartUp();
            ShowProfiles();
        }

        /// <summary>Settings shows the profiles as they are now; a Start-up sequence is handed them as it begins.</summary>
        private void ShowProfiles() => Settings.StartUp.Profiles = profiles.ToList();

        /// <summary>What the secret store keeps of the Personalisation options.</summary>
        private sealed class SavedStartUp
        {
            public BootVideo BootVideo { get; set; } = BootVideo.XboxSeriesX;

            public bool AskWhosPlaying { get; set; } = true;

            /// <summary>Null until the player sets it, so the default stays the default's to give.</summary>
            public bool? BackgroundDelay { get; set; }

            public List<SavedProfile> Profiles { get; set; }

            public string LastProfileId { get; set; }
        }

        private sealed class SavedProfile
        {
            public string Id { get; set; }

            public string Gamertag { get; set; }

            public string RealName { get; set; }

            public string Picture { get; set; }
        }
    }
}
