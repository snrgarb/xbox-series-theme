using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Home's game tiles, merged into each of Home's rows.</summary>
    public partial class HomeTiles : ResourceDictionary
    {
        public HomeTiles()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// A tile's install state, from what the tile shows: its game's <see cref="HomeTile.Status"/>, or, with the parameter
    /// "Visibility", whether there is one to show. A tile showing anything else, as Play history's collage, has none.
    /// </summary>
    internal sealed class TileStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = (value as HomeTile)?.Status;
            return (parameter as string) == "Visibility"
                ? (string.IsNullOrEmpty(status) ? Visibility.Collapsed : Visibility.Visible)
                : (object)status;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
