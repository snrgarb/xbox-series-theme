using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Start-up sequence itself (boot video, Who's playing today?, Signing you in), run on a thread of its own so it
    /// animates and answers the pad whatever Playnite's UI thread is doing. It owns the <see cref="StartupState"/> its
    /// window binds, and touches nothing of the core: it begins from a <see cref="StartupPlan"/>, tells the core who
    /// signed in through <see cref="SignedIn"/>, and is ended by the core once Signing you in has shown.
    /// </summary>
    public sealed class StartupSequence
    {
        /// <summary>The longest any boot video is let run before the sequence goes on without it; each is under 15 s.</summary>
        private static readonly TimeSpan BootVideoDeadline = TimeSpan.FromSeconds(20);

        private readonly IClock clock;
        private readonly ISoundPlayer sounds;

        private StartupPlan plan;

        /// <summary>Whether <see cref="End"/> has been called: a sequence runs once, and nothing moves it after.</summary>
        private bool ended;

        /// <summary>The boot video's deadline, while it plays; null otherwise.</summary>
        private IDisposable bootVideoTimer;

        /// <summary>The pad's buttons down at its last reading; null before the first since the sequence began.</summary>
        private HashSet<ControllerButton> padDown;

        public StartupSequence(IClock clock, ISoundPlayer sounds)
        {
            this.clock = clock;
            this.sounds = sounds;
        }

        /// <summary>A profile signed in, raised on the sequence's thread as Signing you in shows.</summary>
        public event Action<Profile> SignedIn;

        /// <summary>What the Start-up window shows.</summary>
        public StartupState State { get; } = new StartupState();

        /// <summary>
        /// The boot video, or Who's playing today? (or, when it doesn't ask, the last profile signing in) with it Off. A
        /// video that runs past <see cref="BootVideoDeadline"/> ends as one that finished, so nothing it shows can hold
        /// the sequence up.
        /// </summary>
        public void Begin(StartupPlan plan)
        {
            if (ended || this.plan != null)
            {
                return;
            }

            this.plan = plan;
            State.Profiles = plan.Profiles;
            State.BootVideo = plan.BootVideo;
            if (plan.BootVideo == BootVideo.Off)
            {
                AfterBootVideo();
            }
            else
            {
                State.Phase = StartupPhase.BootVideo;
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
            if (!ended && State.Phase == StartupPhase.BootVideo)
            {
                bootVideoTimer?.Dispose();
                bootVideoTimer = null;
                AfterBootVideo();
            }
        }

        /// <summary>
        /// A button: the pad's, read on this thread, a real key standing in for one, or the Guide button from Playnite.
        /// Any press skips the boot video; on Who's playing today?, left and right move along the profiles, held at
        /// either end, and A signs the focused one in. B and the Guide button do nothing: someone has to sign in.
        /// </summary>
        public void OnButton(ControllerButton button, ButtonState state)
        {
            if (ended || state != ButtonState.Pressed)
            {
                return;
            }

            if (State.Phase == StartupPhase.BootVideo)
            {
                OnBootVideoEnded();
            }
            else if (State.Phase == StartupPhase.WhosPlaying && (button == ControllerButton.DPadLeft || button == ControllerButton.DPadRight))
            {
                var profiles = State.Profiles.ToList();
                var index = profiles.FindIndex(p => p.Id == State.FocusedProfile?.Id);
                var next = profiles[Math.Max(0, Math.Min(profiles.Count - 1, index + (button == ControllerButton.DPadLeft ? -1 : 1)))];
                if (next != State.FocusedProfile)
                {
                    sounds.Play(UiSound.Navigate);
                    Focus(next);
                }
            }
            else if (State.Phase == StartupPhase.WhosPlaying && button == ControllerButton.A)
            {
                sounds.Play(UiSound.Enter);
                SignIn(State.FocusedProfile ?? plan.LastProfile);
            }
        }

        /// <summary>
        /// The pad's buttons held down now, read on this thread: each newly down is a press, each let go a release. The
        /// first reading once the sequence has begun only says what is held, so a button held from before Start-up (the
        /// A that launched Playnite, say) is no press until it is let go and pressed again.
        /// </summary>
        public void OnPadReading(IReadOnlyCollection<ControllerButton> down)
        {
            if (ended || plan == null)
            {
                return;
            }

            var now = new HashSet<ControllerButton>(down);
            var before = padDown;
            padDown = now;
            if (before == null)
            {
                return;
            }

            foreach (var button in before.Where(b => !now.Contains(b)))
            {
                OnButton(button, ButtonState.Released);
            }

            foreach (var button in down.Where(b => !before.Contains(b)).Distinct())
            {
                OnButton(button, ButtonState.Pressed);
            }
        }

        /// <summary>Signing you in has shown: the sequence ends, and its window fades out to Home.</summary>
        public void End()
        {
            if (ended)
            {
                return;
            }

            ended = true;
            bootVideoTimer?.Dispose();
            bootVideoTimer = null;
            State.Phase = StartupPhase.None;
        }

        /// <summary>After the boot video, or at once with it Off: Who's playing today? if it asks, else the last profile signs in.</summary>
        private void AfterBootVideo()
        {
            if (plan.AskWhosPlaying)
            {
                State.SigningIn = null;
                Focus(plan.LastProfile);
                State.Phase = StartupPhase.WhosPlaying;
            }
            else
            {
                SignIn(plan.LastProfile);
            }
        }

        /// <summary>Focuses a profile on Who's playing today?, its place in the row first, so the row slides as it grows.</summary>
        private void Focus(Profile profile)
        {
            State.FocusedIndex = Math.Max(0, State.Profiles.ToList().FindIndex(p => p.Id == profile?.Id));
            State.FocusedProfile = profile;
        }

        /// <summary>
        /// Signing you in: the profile shows alone with the console's sign-in sound, and the core is told at once; it
        /// ends the sequence once Signing you in has shown.
        /// </summary>
        private void SignIn(Profile profile)
        {
            State.SigningIn = profile;
            State.Phase = StartupPhase.SigningIn;
            sounds.Play(UiSound.Login);
            SignedIn?.Invoke(profile);
        }
    }
}
