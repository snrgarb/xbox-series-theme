using System;
using System.Collections.Generic;
using Xunit;

namespace XboxSeries.BootShell.Tests
{
    // When the Boot shell launches Fullscreen at sign-in, against default audio devices and a clock the test makes up:
    // once Windows' default has stayed the same for 1.5 s, and at the 6 s limit when it hasn't, or there is none.
    public class AudioSettleTests
    {
        private const string Headset = "{0.0.0.00000000}.{headset}";
        private const string Tv = "{0.0.0.00000000}.{tv}";

        private TimeSpan now = TimeSpan.FromSeconds(0.3);
        private readonly List<string> log = new();
        private readonly AudioSettle settle;

        public AudioSettleTests()
        {
            settle = new AudioSettle(() => now, log.Add);
        }

        private void At(double seconds) => now = TimeSpan.FromSeconds(seconds);

        [Fact]
        public void Fullscreen_launches_once_the_default_has_stayed_the_same_for_1_5_s()
        {
            Assert.False(settle.Launch(Tv));

            At(1.7);
            Assert.False(settle.Launch(Tv));

            At(1.8);
            Assert.True(settle.Launch(Tv));
            Assert.Equal("audio settled after 1800 ms", settle.Why);
        }

        [Fact]
        public void A_new_default_starts_the_settle_time_again()
        {
            Assert.False(settle.Launch(Headset));

            At(1.5);
            Assert.False(settle.Launch(Tv));

            At(2.9);
            Assert.False(settle.Launch(Tv));

            At(3);
            Assert.True(settle.Launch(Tv));
        }

        [Fact]
        public void No_default_yet_counts_as_not_settled()
        {
            Assert.False(settle.Launch(null));

            At(2);
            Assert.False(settle.Launch(null));

            At(2.5);
            Assert.False(settle.Launch(Tv));

            At(4);
            Assert.True(settle.Launch(Tv));
        }

        [Fact]
        public void A_default_still_changing_at_the_6_s_limit_launches_anyway()
        {
            Assert.False(settle.Launch(Headset));

            At(5);
            Assert.False(settle.Launch(Tv));

            At(5.9);
            Assert.False(settle.Launch(Tv));

            At(6);
            Assert.True(settle.Launch(Tv));
            Assert.Equal("audio not settled after 6 s; launching anyway", settle.Why);
        }

        [Fact]
        public void No_default_by_the_6_s_limit_launches_anyway()
        {
            Assert.False(settle.Launch(null));

            At(6);
            Assert.True(settle.Launch(null));
            Assert.Equal("no default audio device after 6 s; launching anyway", settle.Why);
        }

        [Fact]
        public void It_says_launch_once()
        {
            settle.Launch(Tv);
            At(2);
            Assert.True(settle.Launch(Tv));

            At(3);
            Assert.False(settle.Launch(Tv));
            At(7);
            Assert.False(settle.Launch(Headset));
        }

        [Fact]
        public void It_logs_the_default_it_sees_and_each_change()
        {
            settle.Launch(null);
            At(0.4);
            settle.Launch(null);
            At(1);
            settle.Launch(Headset);
            At(1.1);
            settle.Launch(Headset);
            At(2);
            settle.Launch(Tv);

            Assert.Equal(new[]
            {
                "audio: no default audio device",
                "audio: default audio device " + Headset,
                "audio: default audio device now " + Tv,
            }, log);
        }
    }
}
