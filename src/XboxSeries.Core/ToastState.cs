using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Toast at the bottom of the screen: a new Playnite notification, shown above every window without taking
    /// focus. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class ToastState : INotifyPropertyChanged
    {
        private string text = "";
        private bool isShown;

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
