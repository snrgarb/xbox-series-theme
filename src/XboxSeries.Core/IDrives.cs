namespace XboxSeries.Core
{
    /// <summary>Port for the PC's drives, for My games &amp; apps' storage ring.</summary>
    public interface IDrives
    {
        /// <summary>The space on the drive at <paramref name="root"/>, such as C:\; null when it can't be read.</summary>
        DriveSpace SpaceOf(string root);
    }

    /// <summary>A drive's free and total space, in bytes.</summary>
    public sealed class DriveSpace
    {
        public DriveSpace(long freeBytes, long totalBytes)
        {
            FreeBytes = freeBytes;
            TotalBytes = totalBytes;
        }

        public long FreeBytes { get; }
        public long TotalBytes { get; }
    }
}
