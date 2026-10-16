using System;
using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>Downloads' progress, by game; any other game's library can't say.</summary>
    public sealed class FakeDownloads : IDownloads
    {
        private readonly Dictionary<Guid, DownloadProgress> progress = new Dictionary<Guid, DownloadProgress>();

        public FakeDownloads With(Guid gameId, bool isActive, long bytesDownloaded, long bytesTotal)
        {
            progress[gameId] = new DownloadProgress(isActive, bytesDownloaded, bytesTotal);
            return this;
        }

        public FakeDownloads Without(Guid gameId)
        {
            progress.Remove(gameId);
            return this;
        }

        public DownloadProgress ProgressOf(Guid gameId) => progress.TryGetValue(gameId, out var p) ? p : null;
    }
}
