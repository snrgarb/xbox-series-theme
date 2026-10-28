using System;
using System.IO;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the PC's drives, as Windows reports them.</summary>
    internal sealed class WindowsDrives : IDrives
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public DriveSpace SpaceOf(string root)
        {
            try
            {
                var drive = new DriveInfo(root);
                return drive.IsReady ? new DriveSpace(drive.AvailableFreeSpace, drive.TotalSize) : null;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                logger.Warn(e, $"Couldn't read the space on {root}");
                return null;
            }
        }
    }
}
