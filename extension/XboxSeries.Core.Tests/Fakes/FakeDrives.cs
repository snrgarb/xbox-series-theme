using System;
using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>The PC's drives, by root; any other drive can't be read.</summary>
    public sealed class FakeDrives : IDrives
    {
        private readonly Dictionary<string, DriveSpace> drives = new Dictionary<string, DriveSpace>(StringComparer.OrdinalIgnoreCase);

        public FakeDrives WithDrive(string root, long freeBytes, long totalBytes)
        {
            drives[root] = new DriveSpace(freeBytes, totalBytes);
            return this;
        }

        public DriveSpace SpaceOf(string root) => drives.TryGetValue(root, out var space) ? space : null;
    }
}
