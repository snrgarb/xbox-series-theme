using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>A Playnite library built up by a test, then handed to the core as a <see cref="LibrarySnapshot"/>.</summary>
    public sealed class FakeLibrary
    {
        private readonly List<LibraryGame> games = new List<LibraryGame>();
        private readonly List<FilterPreset> filterPresets = new List<FilterPreset>();
        private readonly List<App> apps = new List<App>();
        private bool appsAvailable = true;

        public FakeLibrary WithGame(
            string name,
            DateTime? lastActivity = null,
            DateTime? added = null,
            bool hidden = false,
            bool installed = true,
            string[] series = null,
            string[] genres = null,
            string source = null)
        {
            games.Add(new LibraryGame(
                Guid.NewGuid(),
                name,
                lastActivity,
                added,
                hidden,
                installed,
                series ?? new string[0],
                genres ?? new string[0],
                source));
            return this;
        }

        public FakeLibrary WithFilterPreset(string name, bool showInFullscreenQuickSelection = false)
        {
            filterPresets.Add(new FilterPreset(Guid.NewGuid(), name, showInFullscreenQuickSelection));
            return this;
        }

        public FakeLibrary WithApp(string name, string path, string arguments = null)
        {
            apps.Add(new App(Guid.NewGuid(), name, path, arguments));
            return this;
        }

        /// <summary>Simulates a Playnite build where reading Software tools fails.</summary>
        public FakeLibrary WithoutApps()
        {
            appsAvailable = false;
            return this;
        }

        /// <summary>The id of the game called <paramref name="name"/>.</summary>
        public Guid Id(string name) => games.Single(g => g.Name == name).Id;

        public LibrarySnapshot Snapshot() =>
            new LibrarySnapshot(games.ToList(), filterPresets.ToList(), appsAvailable ? apps.ToList() : null);
    }
}
