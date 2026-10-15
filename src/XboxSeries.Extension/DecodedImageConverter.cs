using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// A full path or URL to an image, decoded at the width given as the converter parameter so large art doesn't
    /// sit in memory at full size. Null, or an image that can't be read, shows nothing.
    /// </summary>
    internal sealed class DecodedImageConverter : IValueConverter
    {
        private static readonly ILogger logger = LogManager.GetLogger();

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

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = decodeWidth;

                // A URL, such as a Discord avatar, downloads in the background and shows once it arrives; one that
                // fails stays empty, so whatever is drawn under it shows instead. It can't be frozen until then.
                image.DownloadFailed += (s, e) => logger.Warn(e.ErrorException, $"Can't download image {path}");
                image.DecodeFailed += (s, e) => logger.Warn(e.ErrorException, $"Can't decode image {path}");
                image.EndInit();
                if (image.CanFreeze)
                {
                    image.Freeze();
                }

                return image;
            }
            catch (Exception e)
            {
                logger.Warn(e, $"Can't load image {path}");
                return null;
            }
        }
    }
}
