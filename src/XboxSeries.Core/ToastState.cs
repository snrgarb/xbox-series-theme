using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What a Toast tells of, which picks the image on its tile.</summary>
    public enum ToastKind
    {
        /// <summary>One of Playnite's notifications: the bell.</summary>
        Notification,

        /// <summary>A Playnite notification about a screenshot: a camera.</summary>
        Screenshot,

        /// <summary>A known DM ringing: the caller's avatar, or Discord's mark.</summary>
        Call,

        /// <summary>"Hi X!" as a profile finishes signing in: the profile's picture, with no "Press ⓧ to view".</summary>
        Greeting,

        /// <summary>"Queued" as a game joins the queue: the game's cover; ⓧ goes to Manage Queue rather than the Guide.</summary>
        Queued,
    }

    /// <summary>
    /// The Toast at the bottom of the screen: a new Playnite notification, or a known DM ringing, shown above every
    /// window without taking focus. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class ToastState : INotifyPropertyChanged
    {
        private string text = "";
        private bool isShown;
        private ToastKind kind;
        private string image;

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
        /// What the Toast tells of, which picks its tile's image. A call's <see cref="Text"/> is "X is calling", and the
        /// view fades it with "Press ⓧ to view", as it does every Toast's. Like <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public ToastKind Kind
        {
            get => kind;
            internal set
            {
                if (kind != value)
                {
                    kind = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Kind)));
                }
            }
        }

        /// <summary>
        /// The image the tile shows in place of its kind's own, as the caller's avatar for a call; null for the kind's
        /// own. Like <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public string Image
        {
            get => image;
            internal set
            {
                if (image != value)
                {
                    image = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
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
