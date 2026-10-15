using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: the console's UI sounds, played through Playnite Fullscreen's own audio engine, so they share its device
    /// and its interface volume. The engine isn't in the SDK, so it is reached by reflection; if any of it is missing
    /// (Desktop mode, a Playnite that has changed, no audio device) the Bundle simply stays silent.
    /// </summary>
    internal sealed class PlayniteSounds : ISoundPlayer
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>Each sound's files, in Sounds\ next to the extension. Navigate's four take turns, as on the console.</summary>
        private static readonly Dictionary<UiSound, string[]> Files = new Dictionary<UiSound, string[]>
        {
            [UiSound.Navigate] = new[] { "Navigate1.wav", "Navigate2.wav", "Navigate3.wav", "Navigate4.wav" },
            [UiSound.Enter] = new[] { "Enter.wav" },
            [UiSound.OpenMenu] = new[] { "OpenMenu.wav" },
            [UiSound.PromptOpen] = new[] { "PromptOpen.wav" },
            [UiSound.PromptClose] = new[] { "PromptClose.wav" },
            [UiSound.Login] = new[] { "Login.wav" },
            [UiSound.PowerOff] = new[] { "PowerOff.wav" },
            [UiSound.Notification] = new[] { "Notification.wav" },
        };

        private readonly Engine engine;
        private readonly string directory;
        private readonly Dictionary<string, IntPtr> loaded = new Dictionary<string, IntPtr>();
        private readonly Dictionary<UiSound, int> turns = new Dictionary<UiSound, int>();

        public PlayniteSounds(IPlayniteAPI api)
        {
            directory = Path.Combine(Path.GetDirectoryName(typeof(PlayniteSounds).Assembly.Location), "Sounds");
            engine = Engine.Find(api);
            if (engine == null)
            {
                return;
            }

            // Loaded up front, so the first Guide over a game doesn't wait on the disk.
            foreach (var file in Files.Values.SelectMany(f => f))
            {
                Load(file);
            }
        }

        public void Play(UiSound sound) => PlayFile(Next(sound));

        /// <summary>
        /// Silences Playnite's own navigation and activation sounds, the Dashboard's audio\navigation.wav and
        /// activation.wav, which Playnite plays as focus lands on its buttons and lists, or puts them back at its interface
        /// volume. Start-up silences them while it covers Home, as Home takes its first focus behind it. Only the volume of
        /// Playnite's loaded sounds changes, never its settings, so nothing of it can outlast Playnite; the Bundle's own
        /// sounds, the Start-up's among them, play on.
        /// </summary>
        public void SilenceThemeSounds(bool silent)
        {
            if (engine == null)
            {
                return;
            }

            try
            {
                var volume = silent ? 0 : engine.InterfaceVolume();
                foreach (var sound in engine.ThemeSounds())
                {
                    if (sound != IntPtr.Zero)
                    {
                        engine.SetSoundVolume(sound, volume);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to {(silent ? "silence" : "restore")} Playnite's own sounds");
            }
        }

        /// <summary>Plays <paramref name="sound"/> and waits for it to finish, as when Playnite is about to close.</summary>
        public void PlayToEnd(UiSound sound)
        {
            var file = Next(sound);
            if (PlayFile(file))
            {
                Thread.Sleep(WavLength(Path.Combine(directory, file)));
            }
        }

        private string Next(UiSound sound)
        {
            var files = Files[sound];
            turns.TryGetValue(sound, out var turn);
            turns[sound] = (turn + 1) % files.Length;
            return files[turn];
        }

        /// <summary>Plays one file at the current interface volume; false when nothing played.</summary>
        private bool PlayFile(string file)
        {
            if (engine == null)
            {
                return false;
            }

            try
            {
                // Playnite skips its own sounds at zero rather than playing them silently.
                var volume = engine.InterfaceVolume();
                var chunk = Load(file);
                if (volume <= 0 || chunk == IntPtr.Zero)
                {
                    return false;
                }

                // Set on every play, so a change in Playnite's audio settings applies at once.
                engine.SetSoundVolume(chunk, volume);
                engine.PlaySound(chunk);
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to play {file}");
                return false;
            }
        }

        /// <summary>The file's sound, loaded once. A load that fails, as with the audio device closed, is tried again next time.</summary>
        private IntPtr Load(string file)
        {
            if (loaded.TryGetValue(file, out var chunk))
            {
                return chunk;
            }

            try
            {
                chunk = engine.LoadSound(Path.Combine(directory, file));
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to load {file}");
                chunk = IntPtr.Zero;
            }

            if (chunk != IntPtr.Zero)
            {
                loaded[file] = chunk;
            }

            return chunk;
        }

        /// <summary>How long a PCM WAV file plays: its data's size over its byte rate.</summary>
        private static TimeSpan WavLength(string path)
        {
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    reader.ReadBytes(12);
                    var byteRate = 0;
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        var id = new string(reader.ReadChars(4));
                        var size = reader.ReadInt32();
                        if (id == "fmt ")
                        {
                            reader.ReadBytes(8);
                            byteRate = reader.ReadInt32();
                            reader.ReadBytes(size - 12);
                        }
                        else if (id == "data")
                        {
                            return byteRate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)size / byteRate);
                        }
                        else
                        {
                            reader.ReadBytes(size + (size & 1));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to read the length of {path}");
            }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Playnite Fullscreen's audio engine (FullscreenApplication.Audio), its interface volume setting, and its own
        /// navigation and activation sounds as it loaded them from the theme. Playnite's own PlaySound reopens the audio
        /// device when Playnite has closed it after a while without sound, as it does while a game runs.
        /// </summary>
        private sealed class Engine
        {
            public Func<string, IntPtr> LoadSound { get; private set; }
            public Action<IntPtr, float> SetSoundVolume { get; private set; }
            public Action<IntPtr> PlaySound { get; private set; }
            public Func<float> InterfaceVolume { get; private set; }

            /// <summary>Playnite's navigation and activation sounds, read afresh; <see cref="IntPtr.Zero"/> for one the theme lacks.</summary>
            public Func<IntPtr[]> ThemeSounds { get; private set; }

            public static Engine Find(IPlayniteAPI api)
            {
                try
                {
                    var app = Assembly.GetEntryAssembly()?.GetType("Playnite.FullscreenApp.FullscreenApplication");
                    var audio = app?.GetProperty("Audio", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (audio == null)
                    {
                        logger.Info("No Playnite Fullscreen audio engine: the Bundle's sounds are off");
                        return null;
                    }

                    // The SDK's Fullscreen settings wrap Playnite's own, which hold the interface volume.
                    var wrapper = api.ApplicationSettings.Fullscreen;
                    var settings = wrapper.GetType()
                        .GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(wrapper);

                    var navigate = app.GetProperty("NavigateSound", BindingFlags.Public | BindingFlags.Static);
                    var activate = app.GetProperty("ActivateSound", BindingFlags.Public | BindingFlags.Static);
                    return new Engine
                    {
                        LoadSound = Bind<Func<string, IntPtr>>(audio, "LoadSound"),
                        SetSoundVolume = Bind<Action<IntPtr, float>>(audio, "SetSoundVolume"),
                        PlaySound = Bind<Action<IntPtr>>(audio, "PlaySound"),
                        InterfaceVolume = (Func<float>)Delegate.CreateDelegate(
                            typeof(Func<float>), settings, settings.GetType().GetProperty("InterfaceVolume").GetGetMethod()),
                        // Missing from some Playnite, they are simply left as they are.
                        ThemeSounds = () => new[] { navigate, activate }.Where(p => p != null).Select(p => (IntPtr)p.GetValue(null)).ToArray(),
                    };
                }
                catch (Exception e)
                {
                    logger.Error(e, "Playnite's audio engine has changed: the Bundle's sounds are off");
                    return null;
                }
            }

            private static T Bind<T>(object target, string method) where T : Delegate =>
                (T)Delegate.CreateDelegate(typeof(T), target, method);
        }
    }
}
