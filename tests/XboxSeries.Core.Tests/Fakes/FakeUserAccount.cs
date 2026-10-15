using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>The Windows user signed in, with a name and account picture a test can set.</summary>
    public sealed class FakeUserAccount : IUserAccount
    {
        public string Name { get; set; } = "Gabe";

        public string Picture { get; set; } = @"C:\Users\Public\AccountPictures\gabe.png";

        public string RealName { get; set; } = "Gabe Smith";

        public IReadOnlyList<string> Gamerpics { get; set; } = new[] { @"C:\Gamerpics\a.png", @"C:\Gamerpics\b.png" };
    }
}
