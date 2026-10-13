using System;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>Playnite Fullscreen as one window on a <see cref="FakeWindowSystem"/>.</summary>
    public sealed class FakePlayniteHost : IPlayniteHost
    {
        private readonly FakeWindowSystem windows;

        public FakePlayniteHost(FakeWindowSystem windows)
        {
            this.windows = windows;
            Window = windows.OpenWindow();
        }

        /// <summary>Playnite Fullscreen's main window.</summary>
        public IntPtr Window { get; }

        public void BringToFront() => windows.SetForeground(Window);
    }
}
