using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Start-up sequence (boot video, Who's playing today?, Signing you in, then "Hi X!") and Settings' Start up
    /// category, which chooses the boot video, keeps the profiles, and says whether Who's playing today? asks.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// The secret store's name for the Start up options, as JSON: the boot video, whether Who's playing today? asks,
        /// the profiles and the one last signed in. They aren't secret, but the store keeps the extension's own options.
        /// </summary>
        private const string StartUpSecret = "StartUp";

        /// <summary>How long Signing you in shows before "Hi X!" and Home.</summary>
        private static readonly TimeSpan SigningInDuration = TimeSpan.FromSeconds(3);

        /// <summary>The longest any boot video is let run before the sequence goes on without it; each is under 15 s.</summary>
        private static readonly TimeSpan BootVideoDeadline = TimeSpan.FromSeconds(20);

        private List<Profile> profiles = new List<Profile>();

        /// <summary>The profile signed in last, whom Who's playing today? focuses and, when it doesn't ask, signs in; null for the first.</summary>
        private string lastProfileId;

        private IDisposable signingInTimer;

        /// <summary>The boot video's deadline, while it plays; null otherwise.</summary>
        private IDisposable bootVideoTimer;

        /// <summary>The greeting's Toast showing, until it goes; null otherwise.</summary>
        private IDisposable greetingTimer;

        /// <summary>The call's Toast, held back while Start-up or the greeting shows, to show as the greeting goes; null for none.</summary>
        private Action waitingRingToast;

        /// <summary>The Start-up sequence, shown full screen over Playnite.</summary>
        public StartupState Startup { get; } = new StartupState();

        /// <summary>
        /// Playnite started in Fullscreen: the sequence begins with the chosen boot video, or Who's playing today? with
        /// it Off. A video that runs past <see cref="BootVideoDeadline"/> ends as one that finished, so nothing it shows
        /// can hold Playnite up.
        /// </summary>
        public void OnStartup()
        {
            if (Startup.IsShown)
            {
                return;
            }

            CoverForStartup();
            Startup.BootVideo = Settings.StartUp.BootVideo;
            if (Startup.BootVideo == BootVideo.Off)
            {
                AfterBootVideo();
            }
            else
            {
                Startup.Phase = StartupPhase.BootVideo;
                bootVideoTimer = clock.After(BootVideoDeadline, () =>
                {
                    bootVideoTimer = null;
                    OnBootVideoEnded();
                });
            }
        }

        /// <summary>The boot video finished, or couldn't play: the sequence goes on.</summary>
        public void OnBootVideoEnded()
        {
            if (Startup.Phase == StartupPhase.BootVideo)
            {
                bootVideoTimer?.Dispose();
                bootVideoTimer = null;
                AfterBootVideo();
            }
        }

        /// <summary>
        /// Windows woke from sleep: Who's playing today? asks again, if it asks, without the boot video. The Guide, and
        /// any Toast, go first.
        /// </summary>
        public void OnResumedFromSleep()
        {
            if (!Settings.StartUp.AskWhosPlaying || Startup.IsShown)
            {
                return;
            }

            CoverForStartup();
            AskWhosPlaying();
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

            PutBackShownNotification();
            ringToastTimer?.Dispose();
            ringToastTimer = null;
            greetingTimer?.Dispose();
            greetingTimer = null;
            Toast.IsShown = false;
        }

        /// <summary>After the boot video, or at once with it Off: Who's playing today? if it asks, else the last profile signs in.</summary>
        private void AfterBootVideo()
        {
            if (Settings.StartUp.AskWhosPlaying)
            {
                AskWhosPlaying();
            }
            else
            {
                SignIn(LastProfile);
            }
        }

        /// <summary>Who's playing today?, focus on the profile signed in last.</summary>
        private void AskWhosPlaying()
        {
            Startup.SigningIn = null;
            Startup.FocusedProfile = LastProfile;
            Startup.Phase = StartupPhase.WhosPlaying;
        }

        /// <summary>
        /// A button while Start-up shows, which nothing else sees: any press skips the boot video; on Who's playing
        /// today?, left and right move along the profiles, held at either end, and A signs the focused one in. B and the
        /// Guide button do nothing: someone has to sign in.
        /// </summary>
        private void OnStartupButton(ControllerButton button, ButtonState state)
        {
            if (state != ButtonState.Pressed)
            {
                return;
            }

            if (Startup.Phase == StartupPhase.BootVideo)
            {
                OnBootVideoEnded();
            }
            else if (Startup.Phase == StartupPhase.WhosPlaying && (button == ControllerButton.DPadLeft || button == ControllerButton.DPadRight))
            {
                var index = profiles.FindIndex(p => p.Id == Startup.FocusedProfile?.Id);
                var next = profiles[Clamp(index + (button == ControllerButton.DPadLeft ? -1 : 1), profiles.Count)];
                if (next != Startup.FocusedProfile)
                {
                    sounds.Play(UiSound.Navigate);
                    Startup.FocusedProfile = next;
                }
            }
            else if (Startup.Phase == StartupPhase.WhosPlaying && button == ControllerButton.A)
            {
                sounds.Play(UiSound.Enter);
                SignIn(Startup.FocusedProfile ?? LastProfile);
            }
        }

        /// <summary>
        /// Signing you in: the profile is signed in at once, on the status area and saved as the last, with the console's
        /// sign-in sound; after <see cref="SigningInDuration"/> Home shows with "Hi X!".
        /// </summary>
        private void SignIn(Profile profile)
        {
            Startup.SigningIn = profile;
            Startup.Phase = StartupPhase.SigningIn;
            Status.Profile = profile;
            lastProfileId = profile.Id;
            SaveStartUp();
            sounds.Play(UiSound.Login);
            signingInTimer?.Dispose();
            signingInTimer = clock.After(SigningInDuration, () =>
            {
                signingInTimer = null;
                Startup.Phase = StartupPhase.None;
                Greet(Status.Profile ?? profile);
            });
        }

        /// <summary>
        /// The greeting's Toast, "Hi X!" with the profile's picture and the notification sound, for as long as a
        /// notification's; the call's Toast and Playnite's notifications held back meanwhile follow it.
        /// </summary>
        private void Greet(Profile profile)
        {
            Toast.Text = "Hi " + profile.FirstName + "!";
            Toast.Kind = ToastKind.Greeting;
            Toast.Image = profile.Picture;
            Toast.IsShown = true;
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
        /// The Start up options saved before, read when the core is made; with none, or none readable, the Xbox Series X
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
            ShowProfiles();
            RefreshGamerpics();
        }

        private void SaveStartUp() => secrets.Write(StartUpSecret, JsonConvert.SerializeObject(new SavedStartUp
        {
            BootVideo = Settings.StartUp.BootVideo,
            AskWhosPlaying = Settings.StartUp.AskWhosPlaying,
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

        /// <summary>A profile edited: the new one takes its place, and wherever it shows; an unknown id changes nothing.</summary>
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
            if (Startup.FocusedProfile?.Id == id)
            {
                Startup.FocusedProfile = edited;
            }

            if (Startup.SigningIn?.Id == id)
            {
                Startup.SigningIn = edited;
            }

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

        /// <summary>Settings and Who's playing today? show the profiles as they are now.</summary>
        private void ShowProfiles()
        {
            var list = profiles.ToList();
            Settings.StartUp.Profiles = list;
            Startup.Profiles = list;
        }

        /// <summary>What the secret store keeps of the Start up options.</summary>
        private sealed class SavedStartUp
        {
            public BootVideo BootVideo { get; set; } = BootVideo.XboxSeriesX;

            public bool AskWhosPlaying { get; set; } = true;

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
