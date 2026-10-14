namespace XboxSeries.Core
{
    /// <summary>Port for the console's UI sounds, played at Playnite's Fullscreen interface volume.</summary>
    public interface ISoundPlayer
    {
        void Play(UiSound sound);
    }

    /// <summary>The console's UI sounds the extension plays. The Dashboard ships Navigate and Enter for Playnite's own screens.</summary>
    public enum UiSound
    {
        /// <summary>Focus moved. The console varies between several; the player picks one.</summary>
        Navigate,

        /// <summary>A: a Guide entry or a menu option was chosen.</summary>
        Enter,

        /// <summary>B: the Guide closed.</summary>
        Back,

        /// <summary>The Guide opened.</summary>
        OpenMenu,

        /// <summary>A menu or prompt opened.</summary>
        PromptOpen,

        /// <summary>A prompt was answered either way, or a menu closed without a choice.</summary>
        PromptClose,

        /// <summary>Playnite Fullscreen started.</summary>
        Startup,

        /// <summary>Playnite is closing.</summary>
        PowerOff,
    }
}
