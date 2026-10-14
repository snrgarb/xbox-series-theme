namespace XboxSeries.Core
{
    /// <summary>Port for the Windows user signed in, shown on the Guide's profile card.</summary>
    public interface IUserAccount
    {
        /// <summary>The user's name.</summary>
        string Name { get; }

        /// <summary>A full path to the user's account picture; null when there is none.</summary>
        string Picture { get; }
    }
}
