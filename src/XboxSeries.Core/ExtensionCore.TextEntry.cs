namespace XboxSeries.Core
{
    /// <summary>
    /// Text entry over the page (<see cref="TextEntryState"/>): every text the Bundle asks for outside Search is typed on
    /// the Console keyboard here, in place of Playnite's own keyboards. While it is up it has the pad: the page under it,
    /// and a tile focused there, hear nothing.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        public TextEntryState TextEntry { get; } = new TextEntryState();

        /// <summary>Start, or the keyboard's Enter: the text is kept, and text entry goes.</summary>
        public void ConfirmTextEntry()
        {
        }

        /// <summary>B: the text is dropped, anything it changed as it was typed put back, and text entry goes.</summary>
        public void CancelTextEntry()
        {
        }

        /// <summary>Settings' Add profile: a gamertag for the new profile, which is added once it is kept.</summary>
        public void TypeNewProfile()
        {
        }

        /// <summary>A profile's Gamertag box: its gamertag, from what it is now.</summary>
        public void TypeProfileGamertag(string id)
        {
        }

        /// <summary>A profile's Real name box: its real name, from what it is now.</summary>
        public void TypeProfileRealName(string id)
        {
        }

        /// <summary>Discord's Client ID: typed from what is saved, and saved once it is kept.</summary>
        public void TypeDiscordClientId()
        {
        }

        /// <summary>Discord's Client Secret: typed from empty, shown as dots, and saved once it is kept.</summary>
        public void TypeDiscordClientSecret()
        {
        }
    }
}
