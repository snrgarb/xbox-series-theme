using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Home is one page that scrolls, as the console's (#206): where it rests is a function of where focus is. On the
    /// Recent row it is as it always was; on the wide tiles it rises until the first Group's header and the top third of
    /// its cards show at the screen's foot, the top bar staying; on a Group, that Group's header is 64 from the screen's
    /// top, the top bar gone above it, the next Group whole and the one after it cut by the screen's foot.
    /// </summary>
    public class HomeRestsTests
    {
        // Home at 1920x1080: the first Group's header 1092 down the page, each Group 435 under the one before, its
        // cards 61 under its header's top and 330 tall.
        private static readonly HomePageLayout Layout = new HomePageLayout(1080, new double[] { 1092, 1527, 1962, 2397 }, 61, 330);

        [Fact]
        public void On_the_Recent_row_Home_is_as_it_was_and_the_top_bar_where_it_was()
        {
            var rest = HomeRests.At(HomeFocus.RecentRow, Layout);

            Assert.Equal(0, rest.PageOffset);
            Assert.Equal(0, rest.TopBarOffset);
        }

        [Fact]
        public void On_the_wide_tiles_the_page_rises_until_the_first_Groups_header_and_a_third_of_its_cards_show_and_the_top_bar_stays()
        {
            var rest = HomeRests.At(HomeFocus.WideTiles, Layout);

            // 1092 + 61 + 330 / 3 - 1080: the header 909 down the screen, its cards from 970 to the foot, 110 of their 330.
            Assert.Equal(183, rest.PageOffset, 6);
            Assert.Equal(0, rest.TopBarOffset);
        }

        [Fact]
        public void On_the_wide_tiles_of_a_screen_tall_enough_to_show_the_Groups_already_the_page_stays()
        {
            var tall = new HomePageLayout(1400, new double[] { 1092 }, 61, 330);

            Assert.Equal(0, HomeRests.At(HomeFocus.WideTiles, tall).PageOffset);
        }

        [Fact]
        public void With_no_Groups_the_wide_tiles_leave_the_page_where_it_is()
        {
            var none = new HomePageLayout(1080, new double[0], 0, 0);

            Assert.Equal(0, HomeRests.At(HomeFocus.WideTiles, none).PageOffset);
        }

        [Theory]
        [InlineData(0, 1028)]
        [InlineData(1, 1463)]
        [InlineData(3, 2333)]
        public void On_a_Group_its_header_is_64_from_the_screens_top(int group, double offset)
        {
            Assert.Equal(offset, HomeRests.At(HomeFocus.Group(group), Layout).PageOffset, 6);
        }

        [Fact]
        public void On_a_Group_the_next_shows_whole_and_the_one_after_is_cut_by_the_screens_foot()
        {
            var offset = HomeRests.At(HomeFocus.Group(1), Layout).PageOffset;

            // The third Group from 499 to 890 down the screen; the fourth from 934, past the foot at 1080.
            Assert.Equal(499, 1962 - offset, 6);
            Assert.True(1962 - offset + 61 + 330 <= 1080);
            Assert.InRange(2397 - offset, 0, 1080);
            Assert.True(2397 - offset + 61 + 330 > 1080);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void On_a_Group_the_top_bar_has_gone_up_with_the_page_from_the_wide_tiles(int group)
        {
            var wide = HomeRests.At(HomeFocus.WideTiles, Layout);
            var rest = HomeRests.At(HomeFocus.Group(group), Layout);

            // Everything moves together from the wide tiles down: 845 for the first Group, so the bar (84 to 144) is
            // far above the screen.
            Assert.Equal(rest.PageOffset - wide.PageOffset, rest.TopBarOffset, 6);
            Assert.True(rest.TopBarOffset >= HomeRests.TopBarBottom);
        }

        [Fact]
        public void On_a_Group_the_top_bar_is_above_the_screen_however_the_page_is_laid_out()
        {
            // Cards so tall that the wide tiles' rest nearly reaches the first Group's.
            var odd = new HomePageLayout(1080, new double[] { 1092 }, 61, 2700);

            Assert.Equal(HomeRests.TopBarBottom, HomeRests.At(HomeFocus.Group(0), odd).TopBarOffset);
        }

        [Fact]
        public void A_Group_past_the_last_rests_on_the_last()
        {
            Assert.Equal(2333, HomeRests.At(HomeFocus.Group(9), Layout).PageOffset, 6);
        }

        [Fact]
        public void Focus_on_a_Group_past_the_last_laid_out_rests_on_and_is_named_as_the_last()
        {
            Assert.Equal(HomeFocus.Group(3), HomeRests.RestingFocus(HomeFocus.Group(9), Layout));
            Assert.Equal(HomeFocus.Group(1), HomeRests.RestingFocus(HomeFocus.Group(1), Layout));
            Assert.Equal(HomeFocus.WideTiles, HomeRests.RestingFocus(HomeFocus.WideTiles, Layout));
        }

        [Fact]
        public void The_top_bar_is_off_the_screen_only_on_the_Groups()
        {
            Assert.True(HomeRests.TopBarShows(HomeRests.At(HomeFocus.RecentRow, Layout)));
            Assert.True(HomeRests.TopBarShows(HomeRests.At(HomeFocus.WideTiles, Layout)));
            Assert.False(HomeRests.TopBarShows(HomeRests.At(HomeFocus.Group(0), Layout)));
        }

        [Fact]
        public void The_Groups_are_off_the_screen_only_while_the_first_starts_below_its_foot()
        {
            // At rest on the Recent row the first Group starts at 1092, under the foot at 1080; on the wide tiles at 909.
            Assert.False(HomeRests.GroupsShow(HomeRests.At(HomeFocus.RecentRow, Layout), Layout));
            Assert.True(HomeRests.GroupsShow(HomeRests.At(HomeFocus.WideTiles, Layout), Layout));
            var narrow = new HomePageLayout(1080, new double[] { 1005 }, 52, 280);
            Assert.True(HomeRests.GroupsShow(HomeRests.At(HomeFocus.RecentRow, narrow), narrow));
        }

        [Fact]
        public void The_Recent_row_and_wide_tiles_are_gone_on_every_Group()
        {
            // On the first Group the wide tiles' foot would still be 16 down the screen, over the header at 64: they
            // fade out rather than show that sliver, so they are gone there too.
            Assert.True(HomeRests.RowsShow(HomeFocus.RecentRow));
            Assert.True(HomeRests.RowsShow(HomeFocus.WideTiles));
            Assert.False(HomeRests.RowsShow(HomeFocus.Group(0)));
            Assert.False(HomeRests.RowsShow(HomeFocus.Group(3)));
        }

        [Fact]
        public void The_log_says_each_resting_point_for_the_end_to_end_tests()
        {
            Assert.Equal("Home rests at RecentRow", HomeRests.Says(HomeFocus.RecentRow, null));
            Assert.Equal("Home rests at WideTiles", HomeRests.Says(HomeFocus.WideTiles, null));
            Assert.Equal("Home rests at Group My Picks", HomeRests.Says(HomeFocus.Group(0), "My Picks"));
        }

        [Fact]
        public void Focus_on_the_same_place_is_the_same_resting_point()
        {
            Assert.Equal(HomeFocus.Group(2), HomeFocus.Group(2));
            Assert.NotEqual(HomeFocus.Group(1), HomeFocus.Group(2));
            Assert.NotEqual(HomeFocus.RecentRow, HomeFocus.WideTiles);
        }
    }
}
