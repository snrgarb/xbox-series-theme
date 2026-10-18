using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// Search: games, never hidden ones, and Apps found by name as the user types on the console keyboard. With nothing
    /// typed it shows the games most recently added; typing shows a Games row and an Apps row, each only with something
    /// found. It takes the place of My games &amp; apps and Settings, as they take each other's.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        public SearchState Search { get; } = new SearchState();

        /// <summary>Opens Search from Home's top bar, empty, the keyboard up; B goes back to the top bar.</summary>
        public void OpenSearch()
        {
        }

        /// <summary>A on the page's search box: the keyboard comes up again, the text as it was.</summary>
        public void ShowSearchKeyboard()
        {
        }

        /// <summary>A on a key of the console keyboard.</summary>
        public void PressKey(KeyboardKey key)
        {
        }

        /// <summary>
        /// A pad shortcut with the keyboard up: X backspace, Y space, LB and RB the cursor left and right, the left stick's
        /// click Shift, and Start submits. Returns whether the button was the keyboard's, so the view marks it handled.
        /// </summary>
        public bool PressKeyboardShortcut(ControllerButton button) => false;

        /// <summary>Text typed on a physical keyboard, as the console keyboard would type it, at the cursor.</summary>
        public void TypeSearchText(string text)
        {
        }

        /// <summary>A on a game the page found, or a recently added one: its Game hub.</summary>
        public void ChooseSearchGame(Guid gameId)
        {
        }

        /// <summary>A on an App the page found: starts it, as My games &amp; apps' Apps does.</summary>
        public void StartSearchApp(Guid appId)
        {
        }

        /// <summary>B on the page: puts the keyboard away first, then goes back to where the page was opened from.</summary>
        public void LeaveSearch()
        {
        }
    }
}
