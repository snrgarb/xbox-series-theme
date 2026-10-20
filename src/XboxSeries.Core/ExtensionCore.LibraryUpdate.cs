using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace XboxSeries.Core
{
    /// <summary>
    /// After a game finishes downloading, every integration's library is updated and metadata downloaded for that game and
    /// any the update added, filling blank fields only, ending with the Library updated Toast; and Settings' Download
    /// metadata box, which does the metadata alone for every game missing it.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// The secret store's name for the Playnite settings category's options, as JSON: Update after downloads. Not
        /// secret, but the store keeps the extension's own options.
        /// </summary>
        private const string PlayniteSettingsSecret = "PlayniteSettings";

        /// <summary>The Library updated Toast's line.</summary>
        private const string LibraryUpdated = "Library updated";

        /// <summary>
        /// How long after the last game finished downloading the library update waits, so several finishing close
        /// together give one update.
        /// </summary>
        private static readonly TimeSpan UpdateAfterDownloadsQuiet = TimeSpan.FromSeconds(10);

        /// <summary>
        /// How often the library update, held off while a game is played or launching or while Playnite runs a library
        /// task of its own, looks again whether it can start.
        /// </summary>
        private static readonly TimeSpan HeldUpdateRecheck = TimeSpan.FromSeconds(5);

        /// <summary>The games seen to finish downloading that the next library update is for, in the order they finished.</summary>
        private readonly List<Guid> finishedDownloads = new List<Guid>();

        /// <summary>The wait before the library update starts; null with none.</summary>
        private IDisposable libraryUpdateTimer;

        /// <summary>The wait before the metadata download after the library update looks again whether it can start; null with none.</summary>
        private IDisposable heldMetadataTimer;

        /// <summary>
        /// Whether the library update after downloads, or the metadata download that follows it, is going or held off for a
        /// game.
        /// </summary>
        private bool libraryUpdateRunning;

        /// <summary>Update after downloads, on or off, saved at once.</summary>
        public void SetUpdateAfterDownloads(bool on)
        {
            Settings.Playnite.UpdateAfterDownloads = on;
            secrets.Write(PlayniteSettingsSecret, JsonConvert.SerializeObject(new SavedPlayniteSettings { UpdateAfterDownloads = on }));

            // Turned off, games already finished are let go: turning it back on doesn't bring their update back.
            if (!on)
            {
                finishedDownloads.Clear();
                libraryUpdateTimer?.Dispose();
                libraryUpdateTimer = null;
            }
        }

        /// <summary>
        /// The Download metadata box: metadata for every game missing it, with Playnite's progress bar and no Toast. It
        /// does nothing while it is already going, or beside the update after downloads or a library task of Playnite's
        /// own, whose progress bar is already up.
        /// </summary>
        public void DownloadMissingMetadata()
        {
            if (Settings.Playnite.IsDownloadingMetadata || libraryUpdateRunning || playnite.IsLibraryBusy)
            {
                return;
            }

            Settings.Playnite.IsDownloadingMetadata = true;
            playnite.DownloadMissingMetadata(_ => Settings.Playnite.IsDownloadingMetadata = false);
        }

        /// <summary>
        /// B or Cancel while the Download metadata box's run is going: stops it. It reads as going until Playnite says it
        /// has stopped.
        /// </summary>
        public void CancelMetadataDownload()
        {
            if (Settings.Playnite.IsDownloadingMetadata)
            {
                playnite.CancelLibraryTask();
            }
        }

        /// <summary>
        /// The Playnite settings category's options saved before, read when the core is made; with none saved, or none
        /// readable, Update after downloads is on.
        /// </summary>
        private void LoadPlayniteSettings()
        {
            SavedPlayniteSettings saved;
            try
            {
                var json = secrets.Read(PlayniteSettingsSecret);
                saved = json == null ? null : JsonConvert.DeserializeObject<SavedPlayniteSettings>(json);
            }
            catch (JsonException)
            {
                saved = null;
            }

            Settings.Playnite.UpdateAfterDownloads = saved?.UpdateAfterDownloads ?? true;
        }

        /// <summary>
        /// A game seen to finish downloading, with Update after downloads on: the library update waits <see cref="UpdateAfterDownloadsQuiet"/> from now,
        /// for any others finishing with it.
        /// </summary>
        private void NoteFinishedDownload(Guid gameId)
        {
            if (!Settings.Playnite.UpdateAfterDownloads)
            {
                return;
            }

            if (!finishedDownloads.Contains(gameId))
            {
                finishedDownloads.Add(gameId);
            }

            WaitForQuietThenUpdate();
        }

        /// <summary>The library update starts <see cref="UpdateAfterDownloadsQuiet"/> from now, unless another game finishes first.</summary>
        private void WaitForQuietThenUpdate()
        {
            libraryUpdateTimer?.Dispose();
            libraryUpdateTimer = clock.After(UpdateAfterDownloadsQuiet, StartLibraryUpdate);
        }

        /// <summary>
        /// The library update after downloads starts, with Playnite's progress bar, once no game is played and Playnite
        /// has no library task of its own going; then metadata for the games that finished and those it added; then the
        /// Library updated Toast waits its turn. Cancelled or failed, either step ends the run with no Toast.
        /// </summary>
        private void StartLibraryUpdate()
        {
            libraryUpdateTimer = null;

            // Games finishing while one runs are for the next, which waits for this one's end.
            if (libraryUpdateRunning || finishedDownloads.Count == 0)
            {
                return;
            }

            // Never while a game is played, or about to be, nor beside the Download metadata box's run or a library task
            // of Playnite's own, whose progress bar is up: it looks again a little later.
            if (runningGame != null || IsQuittingOrLaunching || Settings.Playnite.IsDownloadingMetadata || playnite.IsLibraryBusy)
            {
                libraryUpdateTimer = clock.After(HeldUpdateRecheck, StartLibraryUpdate);
                return;
            }

            var finished = finishedDownloads.ToList();
            finishedDownloads.Clear();
            libraryUpdateRunning = true;
            playnite.UpdateLibrary(gamesAdded =>
            {
                // Cancelled, failed or not run: no metadata and no Toast, and the games that finished are let go rather
                // than tried again.
                if (gamesAdded == null)
                {
                    EndLibraryUpdate();
                    return;
                }

                StartMetadataAfterUpdate(finished.Concat(gamesAdded).Distinct().ToList(), gamesAdded.Count);
            });
        }

        /// <summary>
        /// Metadata for <paramref name="games"/>, the games that finished and those the library update added, once no game
        /// is played or launching, as the update itself waits; then the Library updated Toast, saying the
        /// <paramref name="gamesAdded"/>, waits its turn.
        /// </summary>
        private void StartMetadataAfterUpdate(IReadOnlyList<Guid> games, int gamesAdded)
        {
            heldMetadataTimer = null;

            // A game started while the library updated: it looks again a little later, until its Play session has ended.
            if (runningGame != null || IsQuittingOrLaunching)
            {
                heldMetadataTimer = clock.After(HeldUpdateRecheck, () => StartMetadataAfterUpdate(games, gamesAdded));
                return;
            }

            playnite.DownloadMetadata(games, completed =>
            {
                // Cancelled or failed, it doesn't say the library was updated.
                if (completed)
                {
                    EnqueueToast(new WaitingToast(LibraryUpdated, ToastKind.LibraryUpdated, null, detail: GamesAddedLine(gamesAdded)));
                }

                EndLibraryUpdate();
            });
        }

        /// <summary>The update after downloads has ended; games that finished while it ran get one of their own.</summary>
        private void EndLibraryUpdate()
        {
            libraryUpdateRunning = false;
            if (finishedDownloads.Count > 0)
            {
                WaitForQuietThenUpdate();
            }
        }

        /// <summary>The Library updated Toast's second line: "&lt;n&gt; games added", "1 game added", or none for none.</summary>
        private static string GamesAddedLine(int count) =>
            count == 0 ? null : count == 1 ? "1 game added" : count + " games added";

        /// <summary>The Playnite settings category's options as saved.</summary>
        private sealed class SavedPlayniteSettings
        {
            public bool? UpdateAfterDownloads { get; set; }
        }
    }
}
