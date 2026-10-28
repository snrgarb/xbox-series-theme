using System;
using System.Linq;

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

        /// <summary>What the text is for once kept; null with text entry away.</summary>
        private Action<string> keepText;

        /// <summary>What the text changes as it is typed, on every key; null for nothing.</summary>
        private Action<string> typedText;

        /// <summary>What puts back what the text changed as it was typed, when it is dropped; null for nothing.</summary>
        private Action dropText;

        /// <summary>The page text entry is up over, which takes it away, dropping the text, as the page goes.</summary>
        private TextEntryPage textEntryPage;

        /// <summary>The pages text entry opens over.</summary>
        private enum TextEntryPage
        {
            Settings,
            FullLibrary,
        }

        /// <summary>
        /// Start, or the keyboard's Enter: the text is kept, and text entry goes. A menu, prompt or the Guide still
        /// showing over it answers the pad instead.
        /// </summary>
        public void ConfirmTextEntry()
        {
            if (TextEntryHasThePad)
            {
                EndTextEntry(keep: true);
            }
        }

        /// <summary>
        /// B: the text is dropped, anything it changed as it was typed put back, and text entry goes. A menu, prompt or
        /// the Guide still showing over it answers the B instead.
        /// </summary>
        public void CancelTextEntry()
        {
            if (TextEntryHasThePad)
            {
                EndTextEntry(keep: false);
            }
        }

        /// <summary>Settings' Add profile: a gamertag for the new profile, which is added once it is kept.</summary>
        public void TypeNewProfile()
        {
            if (Settings.IsShown)
            {
                ShowTextEntry(TextEntryPage.Settings, "Gamertag", string.Empty, keep: gamertag => AddProfile(gamertag));
            }
        }

        /// <summary>A profile's Gamertag box: its gamertag, from what it is now.</summary>
        public void TypeProfileGamertag(string id)
        {
            var profile = SettingsProfile(id);
            if (profile != null)
            {
                ShowTextEntry(TextEntryPage.Settings, "Gamertag", profile.Gamertag, keep: gamertag => SetProfileGamertag(id, gamertag));
            }
        }

        /// <summary>A profile's Real name box: its real name, from what it is now.</summary>
        public void TypeProfileRealName(string id)
        {
            var profile = SettingsProfile(id);
            if (profile != null)
            {
                ShowTextEntry(TextEntryPage.Settings, "Real name", profile.RealName, keep: realName => SetProfileRealName(id, realName));
            }
        }

        /// <summary>Discord's Client ID: typed from what is saved, and saved once it is kept.</summary>
        public void TypeDiscordClientId()
        {
            if (Settings.IsShown)
            {
                ShowTextEntry(TextEntryPage.Settings, "Client ID", Settings.Discord.ClientId, keep: SetDiscordClientId);
            }
        }

        /// <summary>
        /// Discord's Client Secret: typed from empty, as the saved one is never shown, in dots, and saved once it is kept.
        /// Kept empty, the saved one stays.
        /// </summary>
        public void TypeDiscordClientSecret()
        {
            if (Settings.IsShown)
            {
                ShowTextEntry(TextEntryPage.Settings, "Client Secret", string.Empty, isSecret: true, keep: secret =>
                {
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        SetDiscordClientSecret(secret);
                    }
                });
            }
        }

        /// <summary>
        /// Puts text entry up over <paramref name="page"/>, its keyboard as the console's comes up over <paramref name="text"/>:
        /// the cursor at its end, Shift on only with nothing typed, the letters showing. <paramref name="keep"/> has the
        /// text once kept; <paramref name="typed"/> has it on every key, and <paramref name="drop"/> puts back what that
        /// changed if it is dropped. Nothing opens while text entry is already up.
        /// </summary>
        private void ShowTextEntry(TextEntryPage page, string caption, string text, Action<string> keep, bool isSecret = false,
            Action<string> typed = null, Action drop = null)
        {
            if (TextEntry.IsShown)
            {
                return;
            }

            TextEntry.Keyboard.Reset(text);
            TextEntry.Caption = caption;
            TextEntry.IsSecret = isSecret;
            keepText = keep;
            typedText = typed;
            dropText = drop;
            textEntryPage = page;
            TextEntry.IsShown = true;
        }

        /// <summary>
        /// Takes text entry away, then keeps the text, or drops it, putting back what it changed. The keyboard forgets the
        /// text either way, so a Client Secret typed isn't left in it.
        /// </summary>
        private void EndTextEntry(bool keep)
        {
            var kept = keepText;
            var dropped = dropText;
            var text = TextEntry.Keyboard.Text;
            keepText = null;
            typedText = null;
            dropText = null;
            TextEntry.IsShown = false;
            TextEntry.Keyboard.Reset();
            TextEntry.IsSecret = false;
            if (keep)
            {
                kept?.Invoke(text);
            }
            else
            {
                dropped?.Invoke();
            }
        }

        /// <summary>
        /// <paramref name="page"/> is going, by the Guide's Home, another page taking its place or a game exiting: text
        /// entry up over it goes too, the text dropped.
        /// </summary>
        private void DropTextEntryOver(TextEntryPage page)
        {
            if (TextEntry.IsShown && textEntryPage == page)
            {
                EndTextEntry(keep: false);
            }
        }

        /// <summary>The profile <paramref name="id"/> with Settings up to type into it; null otherwise.</summary>
        private Profile SettingsProfile(string id) =>
            Settings.IsShown ? Settings.StartUp.Profiles.FirstOrDefault(p => p.Id == id) : null;

        /// <summary>Whether text entry is up with nothing over it: a menu or prompt still showing, or the Guide, which can open over it, answers the pad instead.</summary>
        private bool TextEntryHasThePad => TextEntry.IsShown && ActiveDialog == null && !Guide.IsOverlayShown;

        /// <summary>A on a key of text entry's keyboard: Enter keeps the text, any other key edits it.</summary>
        private void PressTextEntryKey(KeyboardKey key)
        {
            if (!TextEntryHasThePad)
            {
                return;
            }

            if (TextEntry.Keyboard.Press(key))
            {
                EndTextEntry(keep: true);
            }
            else
            {
                OnTextEntryTyped();
            }
        }

        /// <summary>A key edited the text: whatever follows it as it is typed hears it.</summary>
        private void OnTextEntryTyped() => typedText?.Invoke(TextEntry.Keyboard.Text);
    }
}
