using System;
using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Installing Steam games through Steam's own command line, which the running client takes without a window.</summary>
    internal sealed partial class SteamDownloads
    {
        public bool CanInstall(Guid gameId) => false;

        public void Install(Guid gameId)
        {
        }

        public IReadOnlyList<Guid> Installing() => new Guid[0];

        public void MarkInstalled(Guid gameId)
        {
        }
    }
}
