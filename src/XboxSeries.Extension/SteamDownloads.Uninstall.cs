using System;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Uninstalling Steam games through the running client, the way <see cref="Install"/> installs them, and marking
    /// them not installed in Playnite once Steam's manifest says it has finished.
    /// </summary>
    internal sealed partial class SteamDownloads
    {
        public bool CanUninstall(Guid gameId) => false;

        public void Uninstall(Guid gameId)
        {
        }

        public void MarkUninstalled(Guid gameId)
        {
        }

        public void UninstallUnfinished(Guid gameId)
        {
        }
    }
}
