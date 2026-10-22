using System;
using System.Runtime.InteropServices;
using Playnite.SDK;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The pads in XInput's four slots, read as Playnite reads them, for the Idle screen and the Start-up sequence alike.
    /// XInput keeps reading while another program has the foreground, as Windows.Gaming.Input doesn't: it reads every pad
    /// at rest for a program that isn't in front. XInput 1.4, Windows' own since 8, or 9.1.0, which every Windows has, is
    /// found on the first read. A slot found empty is tried again only every few seconds, as reading an empty one is
    /// slow. Any thread may read.
    /// </summary>
    internal static class XInput
    {
        /// <summary>How long a slot with no pad goes untried.</summary>
        private const int EmptySlotUntriedMs = 3000;

        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>The XInput the pads are read through; null when neither DLL loads.</summary>
        private static readonly Lazy<GetState> getState = new Lazy<GetState>(Find);

        // Shared by every thread that reads, unlocked: two reads crossing cost no more than an extra read of an empty slot,
        // or a pad that has just connected going untried as if it had connected a moment later.
        private static readonly int[] emptySince = new int[PadWatch.Slots];
        private static readonly bool[] empty = new bool[PadWatch.Slots];

        /// <summary>The pad in <paramref name="slot"/>, 0 to 3, now; null with no pad there or without XInput.</summary>
        public static PadReading? Read(int slot)
        {
            var get = getState.Value;
            var now = Environment.TickCount;
            if (get == null || (empty[slot] && unchecked(now - emptySince[slot]) < EmptySlotUntriedMs))
            {
                return null;
            }

            var result = get((uint)slot, out var state);
            if (result != ERROR_SUCCESS)
            {
                empty[slot] = result == ERROR_DEVICE_NOT_CONNECTED;
                emptySince[slot] = now;
                return null;
            }

            empty[slot] = false;
            var pad = state.Gamepad;
            return new PadReading(pad.wButtons, pad.bLeftTrigger, pad.bRightTrigger, pad.sThumbLX, pad.sThumbLY, pad.sThumbRX, pad.sThumbRY);
        }

        /// <summary>XInput 1.4, or 9.1.0; null, logged, when neither loads.</summary>
        private static GetState Find()
        {
            try
            {
                XInputGetState14(0, out _);
                return XInputGetState14;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                logger.Info($"No XInput 1.4 ({e.Message}); trying XInput 9.1.0");
            }

            try
            {
                XInputGetState910(0, out _);
                return XInputGetState910;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                logger.Error(e, "No XInput: Start-up reads no pad, and the Idle screen none outside Playnite");
                return null;
            }
        }

        private delegate uint GetState(uint userIndex, out XINPUT_STATE state);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] private static extern uint XInputGetState14(uint userIndex, out XINPUT_STATE state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] private static extern uint XInputGetState910(uint userIndex, out XINPUT_STATE state);
    }
}
