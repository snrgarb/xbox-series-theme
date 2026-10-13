namespace XboxSeries.Core
{
    /// <summary>
    /// The extension's single facade. Controller button events, library snapshots and port events go in;
    /// view state comes out, and commands go out through the ports.
    /// </summary>
    public sealed class ExtensionCore
    {
        private readonly IClock clock;
        private LibrarySnapshot library = LibrarySnapshot.Empty;

        public ExtensionCore(IClock clock)
        {
            this.clock = clock;
        }

        public GuideState Guide { get; } = new GuideState();

        public void OnLibraryChanged(LibrarySnapshot snapshot)
        {
            library = snapshot;
        }

        public void OnControllerButton(ControllerButton button, ButtonState state)
        {
        }
    }

    /// <summary>What the Guide shows.</summary>
    public sealed class GuideState
    {
        public bool IsOpen { get; internal set; }
    }
}
