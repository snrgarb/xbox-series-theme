using XboxSeries.Extension;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // A pad read through XInput outside Playnite, for the Idle screen to count while a game is in front: whether it is
    // held off its rest now, or was moved since the last look, and never a pad lying still, its sticks drifting inside
    // their deadzones. The deciding is plain C# (PadActivity.cs, compiled in here).
    public class PadActivityTests
    {
        private static readonly PadReading AtRest = new PadReading(0, 0, 0, 0, 0, 0, 0);

        [Fact]
        public void A_pad_at_rest_is_not_off_its_rest_though_its_sticks_and_triggers_drift_inside_their_deadzones()
        {
            Assert.False(PadActivity.IsOffRest(AtRest));
            Assert.False(PadActivity.IsOffRest(new PadReading(0, 30, 30, 7849, 0, 0, -8689)));
            Assert.False(PadActivity.IsOffRest(new PadReading(0, 30, 30, 0, -7849, 8689, 0)));
            Assert.False(PadActivity.IsOffRest(new PadReading(0, 0, 0, 5000, 5000, 6000, -6000)));
        }

        [Fact]
        public void A_button_down_is_off_its_rest()
        {
            Assert.True(PadActivity.IsOffRest(new PadReading(0x1000, 0, 0, 0, 0, 0, 0)));
        }

        [Fact]
        public void A_trigger_pressed_past_its_threshold_is_off_its_rest_so_a_held_accelerator_counts()
        {
            Assert.True(PadActivity.IsOffRest(new PadReading(0, 0, 31, 0, 0, 0, 0)));
            Assert.True(PadActivity.IsOffRest(new PadReading(0, 255, 0, 0, 0, 0, 0)));
        }

        [Fact]
        public void A_stick_pushed_past_its_deadzone_is_off_its_rest_either_way_on_either_axis()
        {
            Assert.True(PadActivity.IsOffRest(new PadReading(0, 0, 0, 7850, 0, 0, 0)));
            Assert.True(PadActivity.IsOffRest(new PadReading(0, 0, 0, 0, -32768, 0, 0)));
            Assert.True(PadActivity.IsOffRest(new PadReading(0, 0, 0, 0, 0, 8690, 0)));
            Assert.True(PadActivity.IsOffRest(new PadReading(0, 0, 0, 0, 0, 0, -8690)));
        }

        [Fact]
        public void A_stick_part_way_on_both_axes_is_off_its_rest_when_its_push_is_past_the_deadzone()
        {
            // 6000 each way is under the deadzone on either axis alone, but about 8485 on the diagonal.
            Assert.True(PadActivity.IsOffRest(new PadReading(0, 0, 0, 6000, 6000, 0, 0)));
        }
        [Fact]
        public void A_pad_lying_still_has_not_moved_though_its_readings_jitter()
        {
            var still = new PadReading(0, 2, 0, 1200, -900, -300, 400);
            var jittered = new PadReading(0, 9, 4, 2400, -1800, 900, -600);

            Assert.False(PadActivity.MovedSince(still, still));
            Assert.False(PadActivity.MovedSince(still, jittered));
        }

        [Fact]
        public void A_button_pressed_or_let_go_since_has_moved()
        {
            Assert.True(PadActivity.MovedSince(AtRest, new PadReading(0x0001, 0, 0, 0, 0, 0, 0)));
            Assert.True(PadActivity.MovedSince(new PadReading(0x1000, 0, 0, 0, 0, 0, 0), AtRest));
        }

        [Fact]
        public void A_trigger_eased_more_than_a_small_step_has_moved_though_it_is_still_inside_its_threshold()
        {
            Assert.True(PadActivity.MovedSince(AtRest, new PadReading(0, 0, 25, 0, 0, 0, 0)));
            Assert.True(PadActivity.MovedSince(new PadReading(0, 200, 0, 0, 0, 0, 0), new PadReading(0, 150, 0, 0, 0, 0, 0)));
        }

        [Fact]
        public void A_stick_moved_more_than_a_small_step_has_moved_though_it_is_still_inside_its_deadzone()
        {
            Assert.True(PadActivity.MovedSince(AtRest, new PadReading(0, 0, 0, 5000, 0, 0, 0)));
            Assert.True(PadActivity.MovedSince(AtRest, new PadReading(0, 0, 0, 0, 0, 0, -5000)));
            Assert.True(PadActivity.MovedSince(new PadReading(0, 0, 0, 30000, 0, 0, 0), new PadReading(0, 0, 0, 20000, 0, 0, 0)));
        }
        // Every pad sampled more often than the core looks, so a tap between two looks isn't missed.
        private readonly PadWatch watch = new PadWatch();

        [Fact]
        public void A_pad_lying_still_is_never_used_however_often_it_is_seen()
        {
            watch.Saw(0, AtRest);
            watch.Saw(0, new PadReading(0, 3, 0, 900, -400, 0, 0));
            Assert.False(watch.UsedSinceLastLook());
            watch.Saw(0, AtRest);
            Assert.False(watch.UsedSinceLastLook());
        }

        [Fact]
        public void A_tap_between_two_looks_is_used_at_the_next_look_alone()
        {
            watch.Saw(0, AtRest);
            watch.Saw(0, new PadReading(0x1000, 0, 0, 0, 0, 0, 0));
            watch.Saw(0, AtRest);

            Assert.True(watch.UsedSinceLastLook());
            watch.Saw(0, AtRest);
            Assert.False(watch.UsedSinceLastLook());
        }

        [Fact]
        public void A_held_accelerator_is_used_at_every_look_while_it_is_held()
        {
            var accelerating = new PadReading(0, 0, 255, 0, 0, 0, 0);
            watch.Saw(0, accelerating);
            Assert.True(watch.UsedSinceLastLook());
            watch.Saw(0, accelerating);
            Assert.True(watch.UsedSinceLastLook());
        }

        [Fact]
        public void A_pad_coming_back_at_rest_is_not_used_though_it_went_with_a_stick_pushed()
        {
            watch.Saw(1, new PadReading(0, 0, 0, 20000, 0, 0, 0));
            watch.UsedSinceLastLook();
            watch.Saw(1, null);
            watch.Saw(1, AtRest);

            Assert.False(watch.UsedSinceLastLook());
        }

        [Fact]
        public void Each_pad_is_compared_with_its_own_last_reading()
        {
            var pushed = new PadReading(0, 0, 0, 0, 0, 0, 6000);
            watch.Saw(0, AtRest);
            watch.Saw(2, pushed);
            watch.UsedSinceLastLook();
            watch.Saw(0, AtRest);
            watch.Saw(2, pushed);

            Assert.False(watch.UsedSinceLastLook());
        }
    }
}
