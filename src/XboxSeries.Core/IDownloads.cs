using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// Port for games' downloads in their library's client (Steam's): how far each has got, installing a game without the
    /// client's own windows, and the installs it runs that Playnite didn't start; and a launch the client holds on a
    /// question for the user, such as the game's EULA.
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
        /// <see cref="Install"/> or Playnite started, or the user started in the client itself.
        /// </summary>
        IReadOnlyList<Guid> Installing();

        /// <summary>
        /// Once the client has installed the game, marks it installed in Playnite, in the folder it went to, as Playnite's
        /// own install would have; does nothing while it hasn't (the install was cancelled, or is still going).
        /// </summary>
        void MarkInstalled(Guid gameId);

        /// <summary>
        /// Once the client has nothing of an install Playnite started (it was cancelled, or never begun), marks the game
        /// no longer installing in Playnite, whose own watch on the install only ends with the game installed; does
        /// nothing while the client has any of it.
        /// </summary>
        void MarkCancelled(Guid gameId);

        /// <summary>
        /// Whether the client's launch of the game waits on the user to answer a question in the client's own window,
        /// such as the game's EULA, a launch option or a CD key: one asked just now, or by an earlier launch and never
        /// answered, which the client folds a new launch into, asking nothing new. False for a game from any other
        /// library, and while the client asks nothing.
        /// </summary>
        bool IsLaunchWaitingOnUser(Guid gameId);

        /// <summary>
        /// Shows the client's window holding the question its launch of the game waits on, in front of Playnite's, as
        /// the client may hold it in a window it keeps hidden, such as Steam's main window under <c>-silent</c>.
        /// </summary>
        void ShowLaunchQuestion(Guid gameId);
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
