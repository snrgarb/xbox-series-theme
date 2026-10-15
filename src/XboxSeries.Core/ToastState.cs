using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Toast at the bottom of the screen: a new Playnite notification, or a known DM ringing, shown above every
    /// window without taking focus. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class ToastState : INotifyPropertyChanged
    {
        private string text = "";
        private bool isShown;
        private bool isCall;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The notification's text. It stays as the Toast hides, so it doesn't blank while sliding away.</summary>
        public string Text
        {
            get => text;
            internal set
            {
                if (text != value)
                {
                    text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        /// <summary>
        /// Whether the Toast tells of a call ringing ("X is calling — press Guide to answer"), drawn with Discord's
        /// glyph, rather than a notification. Like <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public bool IsCall
        {
            get => isCall;
            internal set
            {
                if (isCall != value)
                {
                    isCall = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCall)));
                }
            }
        }

        public bool IsShown
        {
            get => isShown;
            internal set
            {
                if (isShown != value)
                {
                    isShown = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsShown)));
                }
            }
        }
    }
}
