namespace XboxSeries.Core
{
    /// <summary>Port for the controller, for the battery the status area shows.</summary>
    public interface IController
    {
        /// <summary>The battery of pad 1.</summary>
        ControllerBattery Battery { get; }
    }

    /// <summary>A controller's battery, in the console's steps.</summary>
    public enum ControllerBattery
    {
        /// <summary>No controller, or one that doesn't report its battery: the status area shows none.</summary>
        None,

        /// <summary>Plugged in by cable, so it has no battery to show.</summary>
        Wired,

        Empty,
        Low,
        Medium,
        Full,
    }
}
