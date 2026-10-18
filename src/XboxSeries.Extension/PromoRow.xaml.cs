using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The four placeholder tiles under Home's Recent row. It holds no logic: <see cref="RecentRowControl"/>, which hosts
    /// it, moves focus into, along and out of it.
    /// </summary>
    public partial class PromoRow : UserControl
    {
        public PromoRow()
        {
            InitializeComponent();
        }

        /// <summary>The tiles, left to right.</summary>
        internal IReadOnlyList<Button> AllTiles => Tiles.Children.OfType<Button>().ToList();

        /// <summary>The tile nearest below <paramref name="from"/>, by their centres across the screen.</summary>
        internal Button NearestTo(FrameworkElement from)
        {
            var centre = from.TranslatePoint(new Point(from.ActualWidth / 2, 0), this).X;
            return AllTiles.OrderBy(tile => System.Math.Abs(tile.TranslatePoint(new Point(tile.ActualWidth / 2, 0), this).X - centre)).First();
        }
    }
}
