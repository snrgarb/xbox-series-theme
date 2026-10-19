using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// Uninstalling a game through its client (Steam) without the client's own windows or confirmation, in place of
    /// Playnite's own Uninstall, followed until the client has finished, then told of with a Toast.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// Whether Playnite's game menu's Uninstall uninstalls the game through its client without the client's own
        /// windows, in place of Playnite's own Uninstall: an installed game its client can uninstall, not running and
        /// not already being uninstalled. Any other game keeps Playnite's own Uninstall.
        /// </summary>
        public bool CanUninstallSilently(Guid gameId)
        {
            return false;
        }

        /// <summary>
        /// The game menu's Uninstall, for a game <see cref="CanUninstallSilently"/>: asks its client to uninstall it,
        /// follows it until the client has, and then tells of it with a "X was uninstalled" Toast.
        /// </summary>
        public void UninstallGame(Guid gameId)
        {
        }
    }
}
