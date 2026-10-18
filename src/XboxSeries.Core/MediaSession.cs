using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>One app's media, as Windows' media flyout shows it: the track, its art, the app and whether it plays.</summary>
    public sealed class MediaSession
    {
        /// <summary>
        /// Apps by a word their raw ids carry, as an AUMID (SpotifyAB.SpotifyMusic_…!Spotify), an executable
        /// (chrome.exe) or a registered id (MSEdge), and the names people know them by; the first match wins.
        /// </summary>
        private static readonly KeyValuePair<string, string>[] KnownApps =
        {
            Known("spotify", "Spotify"),
            Known("msedge", "Edge"),
            Known("microsoftedge", "Edge"),
            Known("chrome", "Chrome"),
            Known("firefox", "Firefox"),

            // Firefox's AUMID: a hash of its install directory, the same for every default install.
            Known("308046b0af4a39cb", "Firefox"),
            Known("brave", "Brave"),
            Known("opera", "Opera"),
            Known("vivaldi", "Vivaldi"),
            Known("zunemusic", "Media Player"),
            Known("zunevideo", "Films & TV"),
            Known("wmplayer", "Windows Media Player"),
            Known("vlc", "VLC"),
            Known("applemusic", "Apple Music"),
            Known("itunes", "iTunes"),
            Known("amazonmusic", "Amazon Music"),
            Known("tidal", "TIDAL"),
            Known("deezer", "Deezer"),
            Known("musicbee", "MusicBee"),
            Known("aimp", "AIMP"),
            Known("discord", "Discord"),
        };

        public MediaSession(string appId, string title, string artist, bool isPlaying, object art = null)
        {
            AppId = appId ?? "";
            Title = title ?? "";
            Artist = string.IsNullOrEmpty(artist) ? null : artist;
            IsPlaying = isPlaying;
            Art = art;
        }

        /// <summary>The app's raw id, as Windows has it: an AUMID, an executable's name or path.</summary>
        public string AppId { get; }

        public string Title { get; }

        /// <summary>The artist; null for none, as for many a video.</summary>
        public string Artist { get; }

        /// <summary>Whether it is playing, rather than paused or stopped.</summary>
        public bool IsPlaying { get; }

        /// <summary>
        /// The album art as the extension has decoded it, ready to show, which the core only passes on; null for none.
        /// </summary>
        public object Art { get; }

        /// <summary>The app as people know it, Spotify, Chrome or Edge, rather than its raw id.</summary>
        public string App => FriendlyAppName(AppId);

        /// <summary>
        /// <paramref name="appId"/>, an AUMID, an executable's name or path, as people know the app: a known app by its
        /// name, any other by its package's or executable's name, its dots as spaces and its first letter capital.
        /// </summary>
        public static string FriendlyAppName(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                return "";
            }

            var squashed = new string(appId.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            foreach (var known in KnownApps)
            {
                if (squashed.Contains(known.Key))
                {
                    return known.Value;
                }
            }

            // A packaged app's AUMID, Publisher.Name_hash!App: its package's name. Else an executable's name or path.
            var name = appId;
            var bang = name.IndexOf('!');
            if (bang >= 0)
            {
                name = name.Substring(0, bang);
                var underscore = name.IndexOf('_');
                if (underscore > 0)
                {
                    name = name.Substring(0, underscore);
                }
            }
            else
            {
                name = name.Substring(name.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - 4);
                }
            }

            name = name.Replace('.', ' ').Trim();
            return name.Length == 0 ? appId : char.ToUpper(name[0], CultureInfo.InvariantCulture) + name.Substring(1);
        }

        private static KeyValuePair<string, string> Known(string word, string name) => new KeyValuePair<string, string>(word, name);
    }
}
