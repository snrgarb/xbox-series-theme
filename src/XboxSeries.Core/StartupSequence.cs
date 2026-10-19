using System;
using System.Collections.Generic;

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
        private readonly IClock clock;
        private readonly ISoundPlayer sounds;

        public StartupSequence(IClock clock, ISoundPlayer sounds)
        {
            this.clock = clock;
            this.sounds = sounds;
        }

        /// <summary>A profile signed in, raised on the sequence's thread as Signing you in shows.</summary>
        public event Action<Profile> SignedIn;

        /// <summary>What the Start-up window shows.</summary>
        public StartupState State { get; } = new StartupState();

        /// <summary>The boot video, or Who's playing today? (or, when it doesn't ask, the last profile signing in) with it Off.</summary>
        public void Begin(StartupPlan plan)
        {
        }

        /// <summary>The boot video finished, or couldn't play: the sequence goes on.</summary>
        public void OnBootVideoEnded()
        {
        }

        /// <summary>A button: the pad's, read on this thread, a real key standing in for one, or the Guide button from Playnite.</summary>
        public void OnButton(ControllerButton button, ButtonState state)
        {
        }

        /// <summary>The pad's buttons held down now, read on this thread: each newly down is a press, each let go a release.</summary>
        public void OnPadReading(IReadOnlyCollection<ControllerButton> down)
        {
        }

        /// <summary>Signing you in has shown: the sequence ends, and its window fades out to Home.</summary>
        public void End()
        {
        }
    }
}
