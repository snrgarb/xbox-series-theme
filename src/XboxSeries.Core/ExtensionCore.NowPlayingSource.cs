using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace XboxSeries.Core
{
    /// <summary>
    /// Settings' Now Playing source (#75): which app the Now Playing card follows while it has a media session, Spotify
    /// at first, else Windows' current session; Any for Windows' current always. Its choices are Any, Spotify, then every
    /// other app that has had a session this run, by the name people know it by. Shared by every profile, and saved.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// The secret store's name for Theme settings' Guide page's options, as JSON: the Now Playing source. Not secret, but the
        /// store keeps the extension's own options.
        /// </summary>
        private const string GuideSecret = "Guide";

        /// <summary>
        /// The apps other than Spotify that have had a media session this run, by name, in the order seen; a saved source
        /// not seen yet first, so it is a choice before its app has started.
        /// </summary>
        private readonly List<string> mediaApps = new List<string>();

        /// <summary>
        /// The session the card shows: the chosen app's, while it has one, else Windows' current; Windows' current always
        /// with the source Any.
        /// </summary>
        private MediaSession ShownSession()
        {
            var source = Settings.Guide.NowPlayingSource;
            var chosen = source == GuideSettingsState.AnySource
                ? null
                : mediaSessions.Sessions.FirstOrDefault(s => SameApp(s.App, source));
            return chosen ?? mediaSessions.Current;
        }

        /// <summary>
        /// Sets Settings' Now Playing source to <paramref name="source"/>, one of
        /// <see cref="GuideSettingsState.NowPlayingSources"/>: an open Audio flyout's card follows it at once, and it is
        /// saved. Any other is refused.
        /// </summary>
        public void SetNowPlayingSource(string source)
        {
            var choice = Settings.Guide.NowPlayingSources.FirstOrDefault(s => SameApp(s, source));
            if (choice == null || choice == Settings.Guide.NowPlayingSource)
            {
                return;
            }

            Settings.Guide.NowPlayingSource = choice;
            SaveGuideSettings();
            ShowNowPlaying();
        }

        /// <summary>
        /// Notes every app with a media session now as a choice, whether or not the flyout shows, so the dropdown offers
        /// it even after the app has gone.
        /// </summary>
        private void NoteMediaApps()
        {
            var sessions = mediaSessions.Sessions.Concat(new[] { mediaSessions.Current }).Where(s => s != null);
            var added = false;
            foreach (var app in sessions.Select(s => s.App))
            {
                added |= NoteMediaApp(app);
            }

            if (added)
            {
                ShowNowPlayingSources();
            }
        }

        /// <summary>Notes <paramref name="app"/> as a choice; false when it already is one, or has no name.</summary>
        private bool NoteMediaApp(string app)
        {
            if (string.IsNullOrEmpty(app)
                || SameApp(app, GuideSettingsState.AnySource)
                || SameApp(app, GuideSettingsState.DefaultSource)
                || mediaApps.Any(a => SameApp(a, app)))
            {
                return false;
            }

            mediaApps.Add(app);
            return true;
        }

        /// <summary>Settings' dropdown offers Any, Spotify, then the apps noted, in the order noted.</summary>
        private void ShowNowPlayingSources() => Settings.Guide.NowPlayingSources =
            new[] { GuideSettingsState.AnySource, GuideSettingsState.DefaultSource }.Concat(mediaApps).ToList();

        private static bool SameApp(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Theme settings' Guide page's options saved before, read when the core is made, and the apps with a media session
        /// then noted; with none saved, or none readable, Spotify.
        /// </summary>
        private void LoadGuideSettings()
        {
            SavedGuide saved;
            try
            {
                var json = secrets.Read(GuideSecret);
                saved = json == null ? null : JsonConvert.DeserializeObject<SavedGuide>(json);
            }
            catch (JsonException)
            {
                saved = null;
            }

            var source = saved?.NowPlayingSource;
            if (string.IsNullOrWhiteSpace(source))
            {
                source = GuideSettingsState.DefaultSource;
            }
            else if (SameApp(source, GuideSettingsState.AnySource))
            {
                source = GuideSettingsState.AnySource;
            }
            else if (SameApp(source, GuideSettingsState.DefaultSource))
            {
                source = GuideSettingsState.DefaultSource;
            }
            else
            {
                NoteMediaApp(source);
            }

            Settings.Guide.NowPlayingSource = source;
            NoteMediaApps();
            ShowNowPlayingSources();
        }

        private void SaveGuideSettings() => secrets.Write(GuideSecret, JsonConvert.SerializeObject(new SavedGuide
        {
            NowPlayingSource = Settings.Guide.NowPlayingSource,
        }));

        /// <summary>What the secret store keeps of Theme settings' Guide page's options.</summary>
        private sealed class SavedGuide
        {
            public string NowPlayingSource { get; set; }
        }
    }
}
