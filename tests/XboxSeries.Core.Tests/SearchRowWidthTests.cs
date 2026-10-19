using Xunit;

namespace XboxSeries.Core.Tests
{
    // Search's rows show only whole tiles, 7 at most, so no tile peeks in at the right edge: the page is 1080 tall but only
    // as wide as the screen's aspect makes it, so on a narrower screen a row shows fewer.
    public class SearchRowWidthTests
    {
        [Theory]
        [InlineData(1772, 7)] // 1920 wide, less the page's 74 either side.
        [InlineData(5000, 7)]
        [InlineData(1515, 6)] // About 1663 wide: 6 tiles exactly fill it.
        [InlineData(1514, 5)]
        [InlineData(240, 1)]
        [InlineData(100, 1)]
        [InlineData(0, 1)]
        public void A_row_shows_as_many_whole_tiles_as_fit_and_at_most_7(double available, int tiles)
        {
            Assert.Equal(tiles, SearchRowWidth.TilesShown(available));
        }

        [Fact]
        public void A_rows_list_ends_where_the_tile_after_the_last_shown_begins()
        {
            // 24 in to the first tile, 7 tiles of 240 and their 15 gaps: the 8th tile starts at the list's edge, so it is
            // cut off wholly, while the 7th's ring, 8 out from it, still shows.
            Assert.Equal(24 + 7 * 255, SearchRowWidth.ListWidth(1772));
            Assert.Equal(24 + 6 * 255, SearchRowWidth.ListWidth(1663 - 148));
        }
    }
}
