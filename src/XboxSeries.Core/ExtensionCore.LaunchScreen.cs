using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Launch screen: as a game launches, its art covers every window, launchers and splash windows included, until
    /// Playnite says the game started, its launch was cancelled, or it stopped; or, failing all of those, until it gives up.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The game the Launch screen is up for, while it shows; null otherwise.</summary>
        private Guid? launchScreenGame;

        /// <summary>The Launch screen's timeout, while it shows; null otherwise.</summary>
        private IDisposable launchScreenTimer;

        /// <summary>Shows the Launch screen with the game's art, chosen as Home's hero chooses it.</summary>
        private void ShowLaunchScreen(Guid gameId)
        {
            launchScreenGame = gameId;
            launchScreenTimer?.Dispose();
            launchScreenTimer = clock.After(LaunchScreenTimeout, HideLaunchScreen);
            LaunchScreen.Art = HeroArtOf(GameById(gameId));
            LaunchScreen.IsShown = true;
        }

        /// <summary>
        /// Playnite has said the game started, its launch was cancelled, or it stopped: the Launch screen hides if it was
        /// up for that game. Its art stays, so it doesn't blank while fading out.
        /// </summary>
        private void HideLaunchScreenFor(Guid gameId)
        {
            if (launchScreenGame == gameId)
            {
                HideLaunchScreen();
            }
        }

        private void HideLaunchScreen()
        {
            launchScreenGame = null;
            launchScreenTimer?.Dispose();
            launchScreenTimer = null;
            LaunchScreen.IsShown = false;
        }
    }
}
