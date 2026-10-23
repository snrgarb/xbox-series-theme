using System;
using System.Runtime.InteropServices;
using static XboxSeries.BootShell.NativeMethods;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// Reads Windows' default audio device, the eRender eConsole one that SDL2's WASAPI opens for Playnite, through
    /// IMMDeviceEnumerator. NativeAOT has no built-in COM interop, so it calls through each object's vtable by hand.
    /// </summary>
    internal sealed unsafe class DefaultAudio : IDisposable
    {
        // IMMDeviceEnumerator: IUnknown's three, EnumAudioEndpoints, then GetDefaultAudioEndpoint.
        private const int GetDefaultAudioEndpoint = 4;

        // IMMDevice: IUnknown's three, Activate, OpenPropertyStore, then GetId.
        private const int GetId = 5;

        private const int Release = 2;

        private nint enumerator;

        /// <summary>Makes the device enumerator; throws when COM won't.</summary>
        public DefaultAudio()
        {
            // Already initialized for this thread, in either apartment, is as good.
            var hr = CoInitializeEx(0, COINIT_MULTITHREADED);
            if (hr < 0 && hr != RPC_E_CHANGED_MODE)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            Marshal.ThrowExceptionForHR(CoCreateInstance(CLSID_MMDeviceEnumerator, 0, CLSCTX_ALL, IID_IMMDeviceEnumerator, out enumerator));
        }

        /// <summary>The default audio device's id, or null when Windows has none; throws on any other failure.</summary>
        public string? Id()
        {
            nint device;
            var hr = ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Slot(enumerator, GetDefaultAudioEndpoint))(enumerator, eRender, eConsole, &device);
            if (hr == E_NOTFOUND)
            {
                return null;
            }

            Marshal.ThrowExceptionForHR(hr);
            try
            {
                char* id;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char**, int>)Slot(device, GetId))(device, &id));
                try
                {
                    return new string(id);
                }
                finally
                {
                    CoTaskMemFree(id);
                }
            }
            finally
            {
                ReleaseObject(device);
            }
        }

        public void Dispose()
        {
            if (enumerator != 0)
            {
                ReleaseObject(enumerator);
                enumerator = 0;
            }
        }

        /// <summary>The method in <paramref name="slot"/> of a COM object's vtable.</summary>
        private static nint Slot(nint instance, int slot) => (*(nint**)instance)[slot];

        private static void ReleaseObject(nint instance) => ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(instance, Release))(instance);
    }
}
