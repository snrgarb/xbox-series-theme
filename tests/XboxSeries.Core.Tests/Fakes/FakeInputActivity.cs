using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>Input outside Playnite's windows, as a test makes it: <see cref="Use"/> is seen by the next look.</summary>
    public sealed class FakeInputActivity : IInputActivity
    {
        private bool used;

        /// <summary>Someone used the keyboard, the mouse or a pad, or holds a pad off its rest, until the next look.</summary>
        public void Use() => used = true;

        /// <summary>Someone holds a pad off its rest: every look sees input while it is true.</summary>
        public bool IsHeld { get; set; }

        public bool InputSinceLastLook()
        {
            var seen = used || IsHeld;
            used = false;
            return seen;
        }
    }
}
