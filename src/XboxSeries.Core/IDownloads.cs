using System;

namespace XboxSeries.Core
{
    /// <summary>Port for the progress of games Playnite is installing, as their library's client reports it (Steam's downloads).</summary>
    public interface IDownloads
    {
        /// <summary>How far the game's download has got; null when its library can't say, as for any but Steam.</summary>
        DownloadProgress ProgressOf(Guid gameId);
    }

    /// <summary>One game's download, as its library's client last wrote it.</summary>
    public sealed class DownloadProgress
    {
        public DownloadProgress(bool isActive, long bytesDownloaded, long bytesTotal)
        {
            IsActive = isActive;
            BytesDownloaded = bytesDownloaded;
            BytesTotal = bytesTotal;
        }

        /// <summary>Whether the client is downloading it now, rather than holding it in its queue.</summary>
        public bool IsActive { get; }

        public long BytesDownloaded { get; }

        /// <summary>The download's whole size; 0 while the client doesn't know it yet.</summary>
        public long BytesTotal { get; }
    }
}
