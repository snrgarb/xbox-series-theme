namespace XboxSeries.Core
{
    /// <summary>
    /// Port for input anywhere in Windows, for the Idle screen to count while a game or another app is in front, where
    /// Playnite's windows see none of it: the keyboard, the mouse, and every pad, sticks and triggers included.
    /// </summary>
    public interface IInputActivity
    {
        /// <summary>
        /// Whether anyone has used the keyboard, the mouse or a pad since the last call, or holds a pad's button, trigger
        /// or stick off its rest now, so a held accelerator counts.
        /// </summary>
        bool InputSinceLastLook();
    }

    /// <summary>No input outside Playnite, ever: for a core given no port.</summary>
    internal sealed class NoInputActivity : IInputActivity
    {
        public static readonly NoInputActivity Instance = new NoInputActivity();

        public bool InputSinceLastLook() => false;
    }
}
