using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Launch screen: the launching game's art, full screen over every window, hiding launchers and splash windows
    /// until the game is running, as on the console. Shown without taking focus. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class LaunchScreenState : INotifyPropertyChanged
    {
        private bool isShown;
        private HeroArt art;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The game's art, chosen as Home's hero chooses it: background art as it is, or a cover to blur and darken; null
        /// for none. It stays as the Launch screen hides, so it doesn't blank while fading out.
        /// </summary>
        public HeroArt Art
        {
            get => art;
            internal set
            {
                if (!Equals(art, value))
                {
                    art = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Art)));
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
