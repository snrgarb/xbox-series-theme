using System;
using System.Globalization;
using System.Windows.Data;

namespace XboxSeries.Extension
{
    /// <summary>
    /// A place in a track, or its length, as a media player shows it: minutes and seconds, 3:07, or past an hour hours
    /// too, 1:02:07, the seconds as they have passed rather than rounded up. Anything but a time shows nothing.
    /// </summary>
    internal sealed class MediaTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is TimeSpan time ? Format(time) : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        public static string Format(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
            {
                time = TimeSpan.Zero;
            }

            var hours = (int)time.TotalHours;
            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, time.Minutes, time.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", time.Minutes, time.Seconds);
        }
    }
}
