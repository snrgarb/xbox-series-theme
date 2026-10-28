using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// Text entry over the page: the Console keyboard, docked at the foot of the dimmed Dashboard, under a caption and the
    /// field it types into. It names a profile, types Discord's Client ID or Secret, or narrows Full library by name.
    /// Start or Enter keeps the text; B cancels it. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class TextEntryState : INotifyPropertyChanged
    {
        private bool isShown;
        private string caption;
        private bool isSecret;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Whether text entry is up, over the page, with the pad.</summary>
        public bool IsShown
        {
            get => isShown;
            internal set
            {
                if (isShown != value)
                {
                    isShown = value;
                    Changed(nameof(IsShown));
                }
            }
        }

        /// <summary>What is being typed, above the field: "Gamertag", "Client Secret".</summary>
        public string Caption
        {
            get => caption;
            internal set
            {
                if (caption != value)
                {
                    caption = value;
                    Changed(nameof(Caption));
                }
            }
        }

        /// <summary>Whether the field shows dots for the text, as for Discord's Client Secret.</summary>
        public bool IsSecret
        {
            get => isSecret;
            internal set
            {
                if (isSecret != value)
                {
                    isSecret = value;
                    Changed(nameof(IsSecret));
                }
            }
        }

        /// <summary>The keyboard, whose text is the field's.</summary>
        public ConsoleKeyboard Keyboard { get; } = new ConsoleKeyboard();

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
