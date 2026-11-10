using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// A full path or URL to an image, decoded at the width given as the converter parameter so large art doesn't
    /// sit in memory at full size. Null, or an image that can't be read, shows nothing. An image from a URL, such as a
    /// Discord icon or avatar, is kept once loaded, so a list rebuilt doesn't download and decode it again. One from a
    /// file, such as a cover, is shared for as long as something still shows it, so a tile rebuilt while its row is on
    /// screen doesn't read and decode its file again.
    /// </summary>
    internal sealed class DecodedImageConverter : IValueConverter
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// The images loaded from URLs, by width and URL, for as long as their thread runs; one that fails to download or
        /// decode is dropped, to be tried again when next shown. Only URLs: a library's covers and backgrounds, from
        /// local files, would add up to too much memory kept this way (<see cref="loadedFromFiles"/> keeps none). Each thread keeps its own, as Playnite's UI thread and the
        /// Start-up sequence's: an image still downloading can't be frozen, so it can't be shown on another thread than
        /// the one that made it, and a plain dictionary per thread needs no lock.
        /// </summary>
        [ThreadStatic]
        private static Dictionary<string, BitmapImage> loadedOnThisThread;

        private static Dictionary<string, BitmapImage> Loaded =>
            loadedOnThisThread ?? (loadedOnThisThread = new Dictionary<string, BitmapImage>());

        /// <summary>
        /// The images loaded from files that something may still hold, by width and the file as it was when read: its
        /// path, when it was last written and its length, so a file written again is read again. Held weakly, so this
        /// keeps none of them in memory: an image nothing shows any more goes as it would have, and its entry with it
        /// the next time the dead ones are cleared out. They are frozen, so every thread shares them, under the lock.
        /// </summary>
        private static readonly Dictionary<string, WeakReference<BitmapImage>> loadedFromFiles =
            new Dictionary<string, WeakReference<BitmapImage>>();

        /// <summary>How many files' images are kept track of before the dead ones are cleared out: twice those alive the last time.</summary>
        private static int clearDeadAt = 64;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            Load(value as string, parameter == null ? 0 : System.Convert.ToInt32(parameter, CultureInfo.InvariantCulture));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        public static BitmapImage Load(string path, int decodeWidth)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var isUrl = path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            var key = decodeWidth.ToString(CultureInfo.InvariantCulture) + " " + path;
            if (isUrl && Loaded.TryGetValue(key, out var kept))
            {
                return kept;
            }

            var fileKey = isUrl ? null : FileKey(path, decodeWidth);
            if (fileKey != null && StillLoaded(fileKey) is BitmapImage shown)
            {
                return shown;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = decodeWidth;

                // A URL, such as a Discord avatar, downloads in the background and shows once it arrives; one that
                // fails stays empty, so whatever is drawn under it shows instead. It can't be frozen until then.
                image.DownloadFailed += (s, e) =>
                {
                    Loaded.Remove(key);
                    logger.Warn(e.ErrorException, $"Can't download image {path}");
                };
                image.DecodeFailed += (s, e) =>
                {
                    Loaded.Remove(key);
                    logger.Warn(e.ErrorException, $"Can't decode image {path}");
                };

                // Kept before it starts, so that a failure, however soon it comes, drops it.
                if (isUrl)
                {
                    Loaded[key] = image;
                }

                image.EndInit();
                if (image.CanFreeze)
                {
                    image.Freeze();
                }

                if (fileKey != null && image.IsFrozen)
                {
                    KeepTrackOf(fileKey, image);
                }

                return image;
            }
            catch (Exception e)
            {
                Loaded.Remove(key);
                logger.Warn(e, $"Can't load image {path}");
                return null;
            }
        }

        /// <summary>
        /// What tells one reading of a file's image from another: the width, and the file's path, last write and length.
        /// Null for what isn't a file that is there to read, which is loaded, or fails, as it always did.
        /// </summary>
        private static string FileKey(string path, int decodeWidth)
        {
            try
            {
                var file = new FileInfo(path);
                return file.Exists
                    ? string.Join(
                        " ",
                        decodeWidth.ToString(CultureInfo.InvariantCulture),
                        file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                        file.Length.ToString(CultureInfo.InvariantCulture),
                        path)
                    : null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException
                                      || e is NotSupportedException || e is SecurityException)
            {
                return null;
            }
        }

        /// <summary>The image already loaded for <paramref name="fileKey"/>, if something still holds it; else null.</summary>
        private static BitmapImage StillLoaded(string fileKey)
        {
            lock (loadedFromFiles)
            {
                return loadedFromFiles.TryGetValue(fileKey, out var reference) && reference.TryGetTarget(out var image) ? image : null;
            }
        }

        /// <summary>Notes a frozen image as loaded for <paramref name="fileKey"/>, clearing the dead entries out once they may be half of them.</summary>
        private static void KeepTrackOf(string fileKey, BitmapImage image)
        {
            lock (loadedFromFiles)
            {
                loadedFromFiles[fileKey] = new WeakReference<BitmapImage>(image);
                if (loadedFromFiles.Count >= clearDeadAt)
                {
                    foreach (var dead in loadedFromFiles.Where(entry => !entry.Value.TryGetTarget(out _)).Select(entry => entry.Key).ToList())
                    {
                        loadedFromFiles.Remove(dead);
                    }

                    clearDeadAt = Math.Max(64, loadedFromFiles.Count * 2);
                }
            }
        }
    }
}
