using System.Collections.Generic;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Game hub's backdrop with the extension (#207, spec #201's Game hub > Backdrop): a snapshot of the screen the
    // hub was opened from, taken once as it opens and let go as it closes, so twenty openings hold no more than one.
    // The snapshot itself is the extension's (a picture of Playnite's page); here it is any object.
    public class HubBackdropTests
    {
        private readonly HubBackdropState backdrop = new HubBackdropState();
        private readonly List<object> taken = new List<object>();

        private object Take()
        {
            var snapshot = new object();
            taken.Add(snapshot);
            return snapshot;
        }

        [Fact]
        public void Before_any_hub_opens_there_is_no_snapshot()
        {
            Assert.Null(backdrop.Snapshot);
            Assert.False(backdrop.HasSnapshot);
        }

        [Fact]
        public void Opening_the_hub_takes_a_snapshot_and_shows_it()
        {
            var changed = new List<string>();
            backdrop.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            Assert.True(backdrop.Open(Take));

            var snapshot = Assert.Single(taken);
            Assert.Same(snapshot, backdrop.Snapshot);
            Assert.True(backdrop.HasSnapshot);
            Assert.Contains(nameof(HubBackdropState.Snapshot), changed);
            Assert.Contains(nameof(HubBackdropState.HasSnapshot), changed);
        }

        [Fact]
        public void Told_again_while_the_hub_is_open_it_keeps_the_snapshot_it_has()
        {
            backdrop.Open(Take);
            var first = backdrop.Snapshot;

            Assert.False(backdrop.Open(Take));

            Assert.Single(taken);
            Assert.Same(first, backdrop.Snapshot);
        }

        [Fact]
        public void Closing_the_hub_lets_the_snapshot_go()
        {
            backdrop.Open(Take);
            var changed = new List<string>();
            backdrop.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            Assert.True(backdrop.Close());

            Assert.Null(backdrop.Snapshot);
            Assert.False(backdrop.HasSnapshot);
            Assert.Contains(nameof(HubBackdropState.Snapshot), changed);
            Assert.Contains(nameof(HubBackdropState.HasSnapshot), changed);
        }

        [Fact]
        public void Closing_with_no_snapshot_lets_nothing_go()
        {
            Assert.False(backdrop.Close());

            backdrop.Open(Take);
            backdrop.Close();
            Assert.False(backdrop.Close());
        }

        [Fact]
        public void Each_opening_takes_a_snapshot_of_its_own()
        {
            backdrop.Open(Take);
            backdrop.Close();
            backdrop.Open(Take);

            Assert.Equal(2, taken.Count);
            Assert.Same(taken[1], backdrop.Snapshot);
            Assert.NotSame(taken[0], taken[1]);
        }

        [Fact]
        public void With_nothing_to_take_the_hub_has_no_snapshot_and_falls_back_to_the_games_art()
        {
            Assert.False(backdrop.Open(() => null));

            Assert.Null(backdrop.Snapshot);
            Assert.False(backdrop.HasSnapshot);
            Assert.False(backdrop.Close());
        }

        [Fact]
        public void Twenty_openings_and_closings_take_and_free_twenty_and_hold_none_after()
        {
            int took = 0, freed = 0;
            for (var i = 0; i < 20; i++)
            {
                if (backdrop.Open(Take))
                {
                    took++;
                }

                // Told twice of each, as a hub's visibility can be.
                backdrop.Open(Take);
                if (backdrop.Close())
                {
                    freed++;
                }

                backdrop.Close();
            }

            Assert.Equal(20, took);
            Assert.Equal(20, freed);
            Assert.Equal(20, taken.Count);
            Assert.Null(backdrop.Snapshot);
        }
    }
}
