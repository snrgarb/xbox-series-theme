using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// A full path or URL to an image, decoded at the width given as the converter parameter so large art doesn't
    /// sit in memory at full size. Null, or an image that can't be read, shows nothing. An image from a URL, such as a
    /// Discord icon or avatar, is kept once loaded, so a list rebuilt doesn't download and decode it again.
    /// </summary>
    internal sealed class DecodedImageConverter : IValueConverter
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// The images loaded from URLs, by width and URL, for as long as Playnite runs; one that fails to download or
        /// decode is dropped, to be tried again when next shown. Only URLs: a library's covers and backgrounds, from
        /// local files, would add up to too much memory kept. The converter only runs on the UI thread, so a plain
        /// dictionary does.
        /// </summary>
        private static readonly Dictionary<string, BitmapImage> loaded = new Dictionary<string, BitmapImage>();

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
            if (isUrl && loaded.TryGetValue(key, out var kept))
            {
                return kept;
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
                    loaded.Remove(key);
                    logger.Warn(e.ErrorException, $"Can't download image {path}");
                };
                image.DecodeFailed += (s, e) =>
                {
                    loaded.Remove(key);
                    logger.Warn(e.ErrorException, $"Can't decode image {path}");
                };

                // Kept before it starts, so that a failure, however soon it comes, drops it.
                if (isUrl)
                {
                    loaded[key] = image;
                }

                image.EndInit();
                if (image.CanFreeze)
                {
                    image.Freeze();
                }

                return image;
            }
            catch (Exception e)
            {
                loaded.Remove(key);
                logger.Warn(e, $"Can't load image {path}");
                return null;
            }
        }
    }
}
