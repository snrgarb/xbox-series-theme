using System;
using System.Runtime.InteropServices;
using System.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Input anywhere in Windows, for the Idle screen to count while a game or another app is in front: the keyboard and
    /// the mouse from Windows' last input time, and every pad read through XInput, as Playnite reads them, which keeps
    /// reading while another app has the foreground. The pads are read more often than the core looks, on a timer's
    /// thread, so a tap between two looks isn't missed (<see cref="PadWatch"/>); a slot with no pad is tried again only
    /// every few seconds, as reading an empty one is slow. Nothing here throws: without XInput, the pads go unread.
    /// </summary>
    internal sealed class WindowsInputActivity : IInputActivity, IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>How often the pads are read: quicker than a tap, so one between two looks is still seen.</summary>
        private const int ReadEveryMs = 50;

        /// <summary>How long a slot with no pad goes untried.</summary>
        private const int EmptySlotUntriedMs = 3000;

        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

        private readonly PadWatch pads = new PadWatch();
        private readonly int[] emptySince = new int[PadWatch.Slots];
        private readonly bool[] empty = new bool[PadWatch.Slots];
        private readonly Timer timer;

        /// <summary>The XInput the pads are read through, found on the first read; null once neither DLL loads.</summary>
        private GetState getState;
        private bool xinputFailed;
        private bool readFailedLogged;
        private volatile bool disposed;

        /// <summary>Windows' tick at the last keyboard or mouse input the core was told of; null before the first look.</summary>
        private uint? lastInputTick;

        public WindowsInputActivity()
        {
            timer = new Timer(_ => ReadPads(), null, ReadEveryMs, Timeout.Infinite);
        }

        public bool InputSinceLastLook()
        {
            var used = false;
            try
            {
                // Compared for a change, not for a later tick, as the tick wraps round every 49.7 days.
                var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (GetLastInputInfo(ref info))
                {
                    used = lastInputTick.HasValue && info.dwTime != lastInputTick.Value;
                    lastInputTick = info.dwTime;
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Couldn't read Windows' last input time for the Idle screen");
            }

            return pads.UsedSinceLastLook() | used;
        }

        public void Dispose()
        {
            disposed = true;
            timer.Dispose();
        }

        /// <summary>Every slot read once, then the next read set going, one at a time, so a slow read never piles them up.</summary>
        private void ReadPads()
        {
            try
            {
                if (getState == null && !xinputFailed)
                {
                    getState = FindXInput();
                    xinputFailed = getState == null;
                }

                if (getState == null)
                {
                    return;
                }

                var now = Environment.TickCount;
                for (var slot = 0; slot < PadWatch.Slots; slot++)
                {
                    if (empty[slot] && unchecked(now - emptySince[slot]) < EmptySlotUntriedMs)
                    {
                        continue;
                    }

                    var result = getState((uint)slot, out var state);
                    if (result == ERROR_SUCCESS)
                    {
                        empty[slot] = false;
                        var pad = state.Gamepad;
                        pads.Saw(slot, new PadReading(pad.wButtons, pad.bLeftTrigger, pad.bRightTrigger, pad.sThumbLX, pad.sThumbLY, pad.sThumbRX, pad.sThumbRY));
                    }
                    else
                    {
                        empty[slot] = result == ERROR_DEVICE_NOT_CONNECTED;
                        emptySince[slot] = now;
                        pads.Saw(slot, null);
                    }
                }
            }
            catch (Exception e)
            {
                if (!readFailedLogged)
                {
                    readFailedLogged = true;
                    logger.Error(e, "Couldn't read the pads through XInput for the Idle screen");
                }
            }
            finally
            {
                if (!disposed)
                {
                    try
                    {
                        timer.Change(ReadEveryMs, Timeout.Infinite);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        /// <summary>XInput 1.4, Windows' own since 8, or 9.1.0, which every Windows has; null, logged, when neither loads.</summary>
        private static GetState FindXInput()
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
                logger.Error(e, "No XInput: the Idle screen can't see the pads outside Playnite");
                return null;
            }
        }

        private delegate uint GetState(uint userIndex, out XINPUT_STATE state);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

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

        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] private static extern uint XInputGetState14(uint userIndex, out XINPUT_STATE state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] private static extern uint XInputGetState910(uint userIndex, out XINPUT_STATE state);
    }
}
