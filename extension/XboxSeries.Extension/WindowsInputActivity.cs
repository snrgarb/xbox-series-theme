using System;
using System.Runtime.InteropServices;
using System.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Input anywhere in Windows, for the Idle screen to count while a game or another app is in front: the keyboard and
    /// the mouse from Windows' last input time, and every pad read through <see cref="XInput"/>, which keeps reading while
    /// another app has the foreground. The pads are read more often than the core looks, on a timer's thread, so a tap
    /// between two looks isn't missed (<see cref="PadWatch"/>). Nothing here throws: without XInput, the pads go unread.
    /// </summary>
    internal sealed class WindowsInputActivity : IInputActivity, IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>How often the pads are read: quicker than a tap, so one between two looks is still seen.</summary>
        private const int ReadEveryMs = 50;

        private readonly PadWatch pads = new PadWatch();
        private readonly Timer timer;

        private bool readFailedLogged;
        private volatile bool disposed;

        /// <summary>Windows' tick at the last keyboard or mouse input the core was told of; null before the first look.</summary>
        private uint? lastInputTick;

        public WindowsInputActivity()
        {
            timer = new Timer(_ => ReadPads(), null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>Starts reading the pads, once the extension runs on the Dashboard: dormant, it never reads them.</summary>
        public void Start()
        {
            if (!disposed)
            {
                timer.Change(ReadEveryMs, Timeout.Infinite);
            }
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
                for (var slot = 0; slot < PadWatch.Slots; slot++)
                {
                    pads.Saw(slot, XInput.Read(slot));
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

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    }
}
