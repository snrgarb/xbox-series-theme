using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>Port for the Windows user signed in, the first profile's defaults, and the gamerpics a profile can wear.</summary>
    public interface IUserAccount
    {
        /// <summary>The user's name.</summary>
        string Name { get; }

        /// <summary>A full path to the user's account picture; null when there is none.</summary>
        string Picture { get; }

        /// <summary>The user's full name, as Windows records it; empty when there is none.</summary>
        string RealName { get; }

        /// <summary>Full paths to the images in the extension's Gamerpics folder, by name; empty when there are none.</summary>
        IReadOnlyList<string> Gamerpics { get; }
    }
}
