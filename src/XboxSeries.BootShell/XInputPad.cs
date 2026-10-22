using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// Reads every pad on a thread of its own, every 30 ms, so a quick Guide tap between the Boot shell's 100 ms polls
    /// isn't missed: <c>XInputGetStateEx</c>, xinput1_4's ordinal 100, which unlike XInputGetState has the Guide button.
    /// A slot with no pad is tried once a second, as XInput is slow to say so. What <see cref="PadWatch"/> makes of the
    /// buttons waits in <see cref="Presses"/> for the Boot shell's next poll.
    /// </summary>
    internal sealed unsafe class XInputPad
    {
        private const int XInputGetStateEx = 100;
        private static readonly TimeSpan Every = TimeSpan.FromMilliseconds(30);
        private static readonly TimeSpan Probe = TimeSpan.FromSeconds(1);

        private readonly PadWatch watch;
        private readonly Func<TimeSpan> elapsed;
        private readonly SignInLog log;
        private readonly bool[] connected = new bool[PadWatch.Pads];
        private readonly TimeSpan[] nextProbe = new TimeSpan[PadWatch.Pads];

        /// <param name="elapsed">The time since the Boot shell started.</param>
        public XInputPad(Func<TimeSpan> elapsed, SignInLog log)
        {
            this.elapsed = elapsed;
            this.log = log;
            watch = new PadWatch(elapsed, log.Write);
        }

        /// <summary>The presses read since the Boot shell last took them.</summary>
        public ConcurrentQueue<PadPresses> Presses { get; } = new();

        public void Start() => new Thread(Run) { IsBackground = true, Name = "pad" }.Start();

        private void Run()
        {
            try
            {
                var library = NativeLibrary.Load("xinput1_4.dll");
                var getState = (delegate* unmanaged<uint, byte*, uint>)GetProcAddress(library, XInputGetStateEx);
                if (getState == null)
                {
                    log.Write("ERROR: pad: xinput1_4.dll has no XInputGetStateEx; the pad isn't read");
                    return;
                }

                log.Write($"pad: reading every pad through XInputGetStateEx every {Every.TotalMilliseconds} ms");
                while (true)
                {
                    for (var pad = 0; pad < PadWatch.Pads; pad++)
                    {
                        if (Read(getState, pad) is var presses and not PadPresses.None)
                        {
                            Presses.Enqueue(presses);
                        }
                    }

                    Thread.Sleep(Every);
                }
            }
            catch (Exception e)
            {
                // The Boot shell carries on without the pad rather than going down with it.
                log.Write($"ERROR: pad: {e}");
            }
        }

        private PadPresses Read(delegate* unmanaged<uint, byte*, uint> getState, int pad)
        {
            var now = elapsed();
            if (!connected[pad] && now < nextProbe[pad])
            {
                return PadPresses.None;
            }

            // XINPUT_STATE is 16 bytes, its buttons at 4; the Ex call is given room past it.
            var state = stackalloc byte[64];
            new Span<byte>(state, 64).Clear();
            var up = getState((uint)pad, state) == 0;
            if (up != connected[pad])
            {
                connected[pad] = up;
                log.Write($"pad {pad}: " + (up ? "connected" : "disconnected"));
            }

            if (!up)
            {
                nextProbe[pad] = now + Probe;
            }

            return watch.Read(pad, up ? *(ushort*)(state + 4) : null);
        }
    }
}
