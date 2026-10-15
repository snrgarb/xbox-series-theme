namespace XboxSeries.Core
{
    /// <summary>Port for the controller's vibration.</summary>
    public interface IControllerRumble
    {
        /// <summary>One short buzz, as the console gives when a held Guide button opens Power.</summary>
        void Pulse();
    }
}
