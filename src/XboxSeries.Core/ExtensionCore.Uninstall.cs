using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Uninstalling a game through its client (Steam) without the client's own windows or confirmation, in place of
    /// Playnite's own Uninstall, followed until the client has finished, then told of with a Toast.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The Uninstalled Toast's line, after the game's name.</summary>
        private const string WasUninstalled = " was uninstalled";

        /// <summary>How often a game being uninstalled is marked uninstalled again, until the client has finished with it.</summary>
        private static readonly TimeSpan UninstallPoll = TimeSpan.FromSeconds(1);

        /// <summary>
        /// How long an uninstall can go unfinished before it is given up on. Steam deletes a big game's files in a minute
        /// or two, so one still there ten minutes on was refused, or the client never got the command.
        /// </summary>
        private static readonly TimeSpan AskedUninstallGivesUpAfter = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The games <see cref="UninstallGame"/> asked the client to uninstall that no snapshot has shown uninstalled
        /// yet, with when each was asked.
        /// </summary>
        private readonly Dictionary<Guid, DateTime> uninstallsAsked = new Dictionary<Guid, DateTime>();

        /// <summary>
        /// The next mark of the games being uninstalled, while any are; null otherwise. It is its own timer, not the
        /// queue's, which stops whenever nothing is installing.
        /// </summary>
        private IDisposable uninstallTimer;

        /// <summary>
        /// Whether Playnite's game menu's Uninstall uninstalls the game through its client without the client's own
        /// windows, in place of Playnite's own Uninstall: an installed game its client can uninstall, not running. One
        /// already being uninstalled stays here too, so pressing Uninstall again before the client is done asks nothing
        /// rather than fall back on Playnite's, which raises the client's own dialog. Any other game keeps Playnite's.
        /// </summary>
        public bool CanUninstallSilently(Guid gameId)
        {
            var game = library.Games.FirstOrDefault(g => g.Id == gameId);

            // A game installing is Playnite's (or the queue's) to finish first; the running one has its files in use.
            return game != null
                && game.Installed
                && !game.Installing
                && gameId != runningGame
                && downloads.CanUninstall(gameId);
        }

        /// <summary>
        /// The Tile menu's Uninstall, confirmed: as Playnite's game menu's, through the client without its windows while
        /// <see cref="CanUninstallSilently"/>, otherwise Playnite's own Uninstall.
        /// </summary>
        private void UninstallThroughTheme(Guid gameId)
        {
            if (CanUninstallSilently(gameId))
            {
                UninstallGame(gameId);
            }
            else
            {
                playnite.UninstallGame(gameId);
            }
        }

        /// <summary>
        /// The game menu's Uninstall, for a game <see cref="CanUninstallSilently"/>: asks its client to uninstall it,
        /// follows it until the client has, and then tells of it with a "X was uninstalled" Toast. One already being
        /// uninstalled is asked once only.
        /// </summary>
        public void UninstallGame(Guid gameId)
        {
            if (!CanUninstallSilently(gameId) || uninstallsAsked.ContainsKey(gameId))
            {
                return;
            }

            downloads.Uninstall(gameId);
            uninstallsAsked[gameId] = clock.Now;
            FollowUninstalls();
        }

        /// <summary>
        /// Reads the uninstalls again in a second while any is followed, and stops reading once none is.
        /// </summary>
        private void FollowUninstalls()
        {
            if (uninstallsAsked.Count == 0)
            {
                uninstallTimer?.Dispose();
                uninstallTimer = null;
            }
            else if (uninstallTimer == null)
            {
                uninstallTimer = clock.After(UninstallPoll, PollUninstalls);
            }
        }

        /// <summary>
        /// The uninstalls' second is up: each one followed is marked uninstalled, which does nothing until the client has
        /// uninstalled it, as Playnite never sees an uninstall it didn't start. The snapshot that follows shows it
        /// uninstalled, and only that tells of it.
        /// </summary>
        private void PollUninstalls()
        {
            uninstallTimer = null;
            foreach (var asked in uninstallsAsked.ToList())
            {
                // One the client is still on long after it was asked won't finish: the log hears of it, and no Toast does.
                if (clock.Now - asked.Value >= AskedUninstallGivesUpAfter)
                {
                    uninstallsAsked.Remove(asked.Key);
                    downloads.UninstallUnfinished(asked.Key);
                    continue;
                }

                downloads.MarkUninstalled(asked.Key);
            }

            FollowUninstalls();
        }

        /// <summary>
        /// Each game the client was asked to uninstall that a snapshot shows not installed has finished: it tells of it
        /// with an Uninstalled Toast, once, in turn behind any showing. One gone from the library altogether, as its
        /// library plugin dropped it, is no longer followed, and tells of nothing.
        /// </summary>
        private void ToastFinishedUninstalls()
        {
            var listed = new HashSet<Guid>(library.Games.Select(g => g.Id));
            foreach (var gone in uninstallsAsked.Keys.Where(id => !listed.Contains(id)).ToList())
            {
                uninstallsAsked.Remove(gone);
            }

            foreach (var finished in library.Games.Where(g => !g.Installed && uninstallsAsked.ContainsKey(g.Id)).ToList())
            {
                uninstallsAsked.Remove(finished.Id);
                EnqueueToast(new WaitingToast(finished.Name + WasUninstalled, ToastKind.Uninstalled, finished.CoverImage));
            }

            FollowUninstalls();
        }
    }
}
