using System;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: Steam's downloads, read from its app manifests, for the games Playnite is installing from Steam.</summary>
    internal sealed class SteamDownloads : IDownloads
    {
        private readonly IPlayniteAPI api;

        public SteamDownloads(IPlayniteAPI api)
        {
            this.api = api;
        }

        public DownloadProgress ProgressOf(Guid gameId) => null;
    }
}
