using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Launch screen: as a game launches, its art covers every window, launchers and splash windows included, until
    /// Playnite says the game started, its launch was cancelled, or it stopped. It steps aside sooner for a window that
    /// needs the user, such as a EULA, a sign-in or an error: one not Playnite's that holds the foreground for
    /// <see cref="LaunchScreenStepsAsideAfter"/>. B dismisses it too, and failing all of those, it gives up.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>How often the Launch screen looks at the window in front while it shows.</summary>
        private static readonly TimeSpan LaunchScreenWatchInterval = TimeSpan.FromSeconds(0.5);

        /// <summary>
        /// How long a launcher's or game's window must hold the foreground before the Launch screen steps aside for it:
        /// one asking for an answer keeps it, where a progress or splash window comes and goes.
        /// </summary>
        private static readonly TimeSpan LaunchScreenStepsAsideAfter = TimeSpan.FromSeconds(5);

        /// <summary>The game the Launch screen is up for, while it shows; null otherwise.</summary>
        private Guid? launchScreenGame;

        /// <summary>The Launch screen's timeout, while it shows; null otherwise.</summary>
        private IDisposable launchScreenTimer;

        /// <summary>The next look at the window in front, while the Launch screen shows; null otherwise.</summary>
        private IDisposable launchScreenWatch;

        /// <summary>The window not Playnite's last seen in front while the Launch screen shows, and since when; zero for none.</summary>
        private IntPtr launchScreenForeign;
        private DateTime launchScreenForeignSince;

        /// <summary>Shows the Launch screen with the game's art, chosen as Home's hero chooses it.</summary>
        private void ShowLaunchScreen(Guid gameId)
        {
            launchScreenGame = gameId;
            launchScreenTimer?.Dispose();
            launchScreenTimer = clock.After(LaunchScreenTimeout, HideLaunchScreen);
            launchScreenForeign = IntPtr.Zero;
            WatchLaunchForeground();
            LaunchScreen.Art = HeroArtOf(GameById(gameId));
            LaunchScreen.IsShown = true;
        }

        /// <summary>Looks again at the window in front in a moment.</summary>
        private void WatchLaunchForeground()
        {
            launchScreenWatch?.Dispose();
            launchScreenWatch = clock.After(LaunchScreenWatchInterval, LookAtLaunchForeground);
        }

        /// <summary>
        /// A window not Playnite's that has held the foreground for <see cref="LaunchScreenStepsAsideAfter"/> needs the
        /// user, so the Launch screen steps aside for it; Playnite's own in front, the Overlay with the Guide among them,
        /// or another window taking its place, starts the count again.
        /// </summary>
        private void LookAtLaunchForeground()
        {
            launchScreenWatch = null;
            var front = windowSystem.ForegroundWindow;
            if (front == IntPtr.Zero || windowSystem.IsPlaynites(front))
            {
                launchScreenForeign = IntPtr.Zero;
            }
            else if (front != launchScreenForeign)
            {
                launchScreenForeign = front;
                launchScreenForeignSince = clock.Now;
            }
            else if (clock.Now - launchScreenForeignSince >= LaunchScreenStepsAsideAfter)
            {
                HideLaunchScreen();
                return;
            }

            WatchLaunchForeground();
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

        /// <summary>The Launch screen goes, whatever it was up for: the game reported, a window needing the user, B, or the timeout.</summary>
        private void HideLaunchScreen()
        {
            launchScreenGame = null;
            launchScreenTimer?.Dispose();
            launchScreenTimer = null;
            launchScreenWatch?.Dispose();
            launchScreenWatch = null;
            launchScreenForeign = IntPtr.Zero;
            LaunchScreen.IsShown = false;
        }
    }
}
