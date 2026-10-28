using System;
using System.ComponentModel;
using XboxSeries.Core;
using XboxSeries.Shared;

namespace XboxSeries.Extension
{
    /// <summary>
    /// When one Start-up sets the Boot shell's two events (<see cref="BootShellEvents"/>), following its sequence's
    /// <see cref="StartupState"/> on the sequence's thread, apart from WPF so it can be checked on its own: FirstFrame at
    /// its first frame that shows something, which the Start-up window tells it of, and BootVideoEnded as the sequence
    /// leaves its boot video, finished, skipped, failed or past its deadline, or with FirstFrame when there's none. A
    /// boot video that ends before any frame shows it, missing or skipped before it opened, waits for FirstFrame too.
    /// Each is set once.
    /// </summary>
    internal sealed class BootShellSignals
    {
        private readonly StartupState startup;
        private readonly Action<string> setEvent;
        private bool firstFrameShown;
        private bool bootVideoEndedSet;

        /// <param name="setEvent">Sets one of the events by its name, if the Boot shell made it.</param>
        public BootShellSignals(StartupState startup, Action<string> setEvent)
        {
            this.startup = startup;
            this.setEvent = setEvent;
            startup.PropertyChanged += OnStartupChanged;
        }

        /// <summary>Start-up's first frame that shows something has been drawn.</summary>
        public void FirstFrameShown()
        {
            if (firstFrameShown)
            {
                return;
            }

            firstFrameShown = true;
            setEvent(BootShellEvents.FirstFrame);
            SetBootVideoEnded();
        }

        private void OnStartupChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StartupState.Phase))
            {
                SetBootVideoEnded();
            }
        }

        /// <summary>BootVideoEnded, once the sequence is past its boot video, or had none, but never before FirstFrame.</summary>
        private void SetBootVideoEnded()
        {
            if (firstFrameShown && !bootVideoEndedSet && startup.Phase != StartupPhase.BootVideo)
            {
                bootVideoEndedSet = true;
                setEvent(BootShellEvents.BootVideoEnded);
            }
        }
    }
}
