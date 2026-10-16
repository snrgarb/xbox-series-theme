using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// Port for games' downloads in their library's client (Steam's): how far each has got, installing a game without the
    /// client's own windows, and the installs it runs that Playnite didn't start.
    /// </summary>
    public interface IDownloads
    {
        /// <summary>How far the game's download has got; null when its library can't say, as for any but Steam.</summary>
        DownloadProgress ProgressOf(Guid gameId);

        /// <summary>Whether <see cref="Install"/> can install the game: a Steam game, with Steam on this PC.</summary>
        bool CanInstall(Guid gameId);

        /// <summary>Asks the client to install the game into its default library, showing none of its own windows.</summary>
        void Install(Guid gameId);

        /// <summary>
        /// The games the client is installing, waiting or downloading, that Playnite lists as not installed: those
        /// <see cref="Install"/> started, or the user started in the client itself.
        /// </summary>
        IReadOnlyList<Guid> Installing();

        /// <summary>
        /// Once the client has installed the game, marks it installed in Playnite, in the folder it went to, as Playnite's
        /// own install would have; does nothing while it hasn't (the install was cancelled, or is still going).
        /// </summary>
        void MarkInstalled(Guid gameId);
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
