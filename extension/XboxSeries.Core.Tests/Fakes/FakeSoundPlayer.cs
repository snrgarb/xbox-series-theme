using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>Records every sound the core asks for, in order.</summary>
    public sealed class FakeSoundPlayer : ISoundPlayer
    {
        public List<UiSound> Played { get; } = new List<UiSound>();

        public void Play(UiSound sound) => Played.Add(sound);
    }
}
