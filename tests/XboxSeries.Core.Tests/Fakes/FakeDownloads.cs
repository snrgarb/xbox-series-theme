using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>The games the client can install; none until added.</summary>
        public HashSet<Guid> Installable { get; } = new HashSet<Guid>();

        public bool CanInstall(Guid gameId) => Installable.Contains(gameId);

        public List<Guid> InstallsAsked { get; } = new List<Guid>();

        public void Install(Guid gameId) => InstallsAsked.Add(gameId);

        /// <summary>The installs the client reports running, whether Playnite knows of them or not.</summary>
        public List<Guid> ClientInstalls { get; } = new List<Guid>();

        public IReadOnlyList<Guid> Installing() => ClientInstalls.ToList();

        public List<Guid> MarkedInstalled { get; } = new List<Guid>();

        public void MarkInstalled(Guid gameId) => MarkedInstalled.Add(gameId);

        public List<Guid> MarkedCancelled { get; } = new List<Guid>();

        public void MarkCancelled(Guid gameId) => MarkedCancelled.Add(gameId);

        /// <summary>The games the client can uninstall; none until added.</summary>
        public HashSet<Guid> Uninstallable { get; } = new HashSet<Guid>();

        public bool CanUninstall(Guid gameId) => Uninstallable.Contains(gameId);

        public List<Guid> UninstallsAsked { get; } = new List<Guid>();

        public void Uninstall(Guid gameId) => UninstallsAsked.Add(gameId);

        public List<Guid> MarkedUninstalled { get; } = new List<Guid>();

        public void MarkUninstalled(Guid gameId) => MarkedUninstalled.Add(gameId);

        public List<Guid> UninstallsUnfinished { get; } = new List<Guid>();

        public void UninstallUnfinished(Guid gameId) => UninstallsUnfinished.Add(gameId);

        /// <summary>The games whose launch the client says waits on the user, as on its EULA; none until added.</summary>
        public HashSet<Guid> LaunchesWaitingOnUser { get; } = new HashSet<Guid>();

        public bool IsLaunchWaitingOnUser(Guid gameId) => LaunchesWaitingOnUser.Contains(gameId);

        public List<Guid> LaunchQuestionsShown { get; } = new List<Guid>();

        public void ShowLaunchQuestion(Guid gameId) => LaunchQuestionsShown.Add(gameId);
    }
}
