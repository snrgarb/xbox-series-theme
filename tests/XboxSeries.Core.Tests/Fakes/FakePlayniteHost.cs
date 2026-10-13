using System;
using System.Collections.Generic;
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

        /// <summary>The Dashboard page Playnite was last brought to the front on, or null if never.</summary>
        public DashboardPage? Page { get; private set; }

        /// <summary>Every game Playnite was asked to start, in order.</summary>
        public List<Guid> StartedGames { get; } = new List<Guid>();

        public void BringToFront() => windows.SetForeground(Window);

        public void BringToFront(DashboardPage page)
        {
            BringToFront();
            Page = page;
        }

        public void StartGame(Guid gameId) => StartedGames.Add(gameId);
    }
}
