using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// After a game finishes downloading, every integration's library is updated and metadata downloaded for that game and
    /// any the update added, filling blank fields only, ending with the Library updated Toast; and Settings' Download
    /// metadata box, which does the metadata alone for every game missing it.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>Update after downloads, on or off, saved at once.</summary>
        public void SetUpdateAfterDownloads(bool on)
        {
        }

        /// <summary>The Download metadata box: metadata for every game missing it, with Playnite's progress bar and no Toast.</summary>
        public void DownloadMissingMetadata()
        {
        }

        /// <summary>B or Cancel while the Download metadata box's run is going: stops it.</summary>
        public void CancelMetadataDownload()
        {
        }
    }
}
