using System;
using System.Runtime.InteropServices;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: Windows' master volume, on the default output device, through the Core Audio API. The device is looked
    /// up on every call, so the volume follows the user switching outputs. When there is no output device, or Windows
    /// refuses, the volume reads as 0 and unmuted and changes do nothing.
    /// </summary>
    internal sealed class WindowsAudio : IAudio
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public int Volume
        {
            get
            {
                var volume = 0f;
                With(endpoint => Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out volume)));
                return (int)Math.Round(volume * 100);
            }
        }

        public bool IsMuted
        {
            get
            {
                var muted = false;
                With(endpoint => Marshal.ThrowExceptionForHR(endpoint.GetMute(out muted)));
                return muted;
            }
        }

        public void SetVolume(int volume)
        {
            var context = Guid.Empty;
            With(endpoint => Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(volume / 100f, ref context)));
        }

        public void SetMuted(bool muted)
        {
            var context = Guid.Empty;
            With(endpoint => Marshal.ThrowExceptionForHR(endpoint.SetMute(muted, ref context)));
        }

        private static void With(Action<IAudioEndpointVolume> use)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioEndpointVolume endpoint = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device));
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out var activated));
                endpoint = (IAudioEndpointVolume)activated;
                use(endpoint);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // Marshal turns a failing HRESULT into whichever exception it maps to, not only COMException; none
                // should reach the UI thread over a volume change.
                logger.Warn(e, "Couldn't reach Windows' master volume");
            }
            finally
            {
                Release(endpoint);
                Release(device);
                Release(enumerator);
            }
        }

        private static void Release(object com)
        {
            if (com != null)
            {
                Marshal.ReleaseComObject(com);
            }
        }

        private enum EDataFlow
        {
            Render = 0,
        }

        private enum ERole
        {
            Multimedia = 1,
        }

        private static class ClsCtx
        {
            public const int All = 0x17;
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig]
            int RegisterControlChangeNotify(IntPtr notify);

            [PreserveSig]
            int UnregisterControlChangeNotify(IntPtr notify);

            [PreserveSig]
            int GetChannelCount(out uint channelCount);

            [PreserveSig]
            int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

            [PreserveSig]
            int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

            [PreserveSig]
            int GetMasterVolumeLevel(out float levelDb);

            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float level);

            [PreserveSig]
            int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);

            [PreserveSig]
            int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);

            [PreserveSig]
            int GetChannelVolumeLevel(uint channel, out float levelDb);

            [PreserveSig]
            int GetChannelVolumeLevelScalar(uint channel, out float level);

            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }
    }
}
