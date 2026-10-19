using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// How wide a row of Search's tiles is: only whole tiles, 7 at most, so none peeks in at the right edge. The page is
    /// 1080 tall but only as wide as the screen's aspect makes it, so a narrower screen shows fewer; the rest are reached
    /// by moving right, as the row slides.
    /// </summary>
    public static class SearchRowWidth
    {
        public const double TileWidth = 240;
        public const double Gap = 15;
        public const int MostTiles = 7;

        /// <summary>The list's room before its first tile, which its negative margin cancels, for the ring and glow.</summary>
        public const double Gutter = 24;

        /// <summary>How many whole tiles fit in the width the row has (the page's, less its margins): never less than 1.</summary>
        public static int TilesShown(double available) =>
            Math.Max(1, Math.Min(MostTiles, (int)Math.Floor((available + Gap) / (TileWidth + Gap))));

        /// <summary>
        /// The row's list's width: its gutter, then the tiles shown with a gap after each, so it ends exactly where the
        /// next tile begins. That tile is then cut off wholly, while the last one's ring, 8 out from it, fits in the gap.
        /// </summary>
        public static double ListWidth(double available) => Gutter + TilesShown(available) * (TileWidth + Gap);
    }
}
