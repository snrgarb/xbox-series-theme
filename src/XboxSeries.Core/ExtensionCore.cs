using System;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// The extension's single facade. Controller button events, library snapshots and port events go in;
    /// view state comes out, and commands go out through the ports.
    /// </summary>
    public sealed class ExtensionCore
    {
        private readonly IClock clock;
        private readonly IWindowSystem windowSystem;
        private readonly IPlayniteHost playnite;
        private LibrarySnapshot library = LibrarySnapshot.Empty;
        private IntPtr recordedForeground;
        private ControllerButton? dismissButton;
        private IDisposable releaseFallback;

        public ExtensionCore(IClock clock, IWindowSystem windowSystem, IPlayniteHost playnite)
        {
            this.clock = clock;
            this.windowSystem = windowSystem;
            this.playnite = playnite;
        }

        public GuideState Guide { get; } = new GuideState();

        public void OnLibraryChanged(LibrarySnapshot snapshot)
        {
            library = snapshot;
        }

        public void OnControllerButton(ControllerButton button, ButtonState state)
        {
            if (state == ButtonState.Released)
            {
                if (button == dismissButton)
                {
                    HandFocusBack();
                }

                return;
            }

            if (button == ControllerButton.Guide && !Guide.IsOpen)
            {
                if (Guide.IsOverlayShown)
                {
                    // Reopened before focus was handed back: the Overlay is still in front, so keep the window recorded first.
                    CancelReleaseWait();
                }
                else
                {
                    recordedForeground = windowSystem.ForegroundWindow;
                }

                Guide.IsOverlayShown = true;
                Guide.IsOpen = true;

                // Taking the foreground can fail; the Overlay is Topmost and the Guide follows Playnite's callback either way.
                windowSystem.TakeForeground();
            }
            else if ((button == ControllerButton.Guide || button == ControllerButton.B) && Guide.IsOpen)
            {
                Guide.IsOpen = false;
                dismissButton = button;
                releaseFallback = clock.After(TimeSpan.FromSeconds(1), HandFocusBack);
            }
        }

        /// <summary>Esc, or a click on the dimmed area. No pad button is held, so focus goes back at once.</summary>
        public void DismissGuide()
        {
            if (!Guide.IsOpen)
            {
                return;
            }

            Guide.IsOpen = false;
            HandFocusBack();
        }

        private void HandFocusBack()
        {
            CancelReleaseWait();
            if (windowSystem.Exists(recordedForeground))
            {
                windowSystem.SetForeground(recordedForeground);
            }
            else
            {
                playnite.BringToFront();
            }

            Guide.IsOverlayShown = false;
        }

        private void CancelReleaseWait()
        {
            dismissButton = null;
            releaseFallback?.Dispose();
            releaseFallback = null;
        }
    }

    /// <summary>What the Guide shows. Views bind to it and hold no logic of their own.</summary>
    public sealed class GuideState : INotifyPropertyChanged
    {
        private bool isOpen;
        private bool isOverlayShown;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Whether the Guide panel is in: it slides in when this turns true and out when it turns false.</summary>
        public bool IsOpen
        {
            get => isOpen;
            internal set => Set(ref isOpen, value, nameof(IsOpen));
        }

        /// <summary>
        /// Whether the Overlay window is up. It outlives <see cref="IsOpen"/> until focus has been handed back,
        /// because hiding the focused Overlay early would hand the game focus while the dismiss button is still held.
        /// </summary>
        public bool IsOverlayShown
        {
            get => isOverlayShown;
            internal set => Set(ref isOverlayShown, value, nameof(IsOverlayShown));
        }

        private void Set(ref bool field, bool value, string name)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
