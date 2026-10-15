using System;
using System.Runtime.InteropServices;

namespace XboxSeries.Extension
{
    /// <summary>The Core Audio API's COM interfaces <see cref="WindowsAudio"/> calls, declared in their vtable order.</summary>
    internal sealed partial class WindowsAudio
    {
        private const int DeviceStateActive = 0x1;
        private const int StgmRead = 0x0;
        private const ushort VtLpwstr = 31;

        // PKEY_Device_FriendlyName: the name Windows shows for a device, such as "Speakers (Realtek(R) Audio)".
        private static readonly PropertyKey DeviceFriendlyName =
            new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant value);

        private enum EDataFlow
        {
            Render = 0,
            Capture = 1,
        }

        private enum ERole
        {
            Console = 0,
            Multimedia = 1,
            Communications = 2,
        }

        private static class ClsCtx
        {
            public const int All = 0x17;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public int pid;

            public PropertyKey(Guid fmtid, int pid)
            {
                this.fmtid = fmtid;
                this.pid = pid;
            }
        }

        // A PROPVARIANT: its type, three reserved words, then a union two pointers wide (16 bytes on x86, 24 on x64).
        // Only a string's pointer is read; PropVariantClear frees whatever it holds.
        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public ushort vt;
            public ushort reserved1;
            public ushort reserved2;
            public ushort reserved3;
            public IntPtr pointer;
            public IntPtr pointer2;
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
            int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

            [PreserveSig]
            int RegisterEndpointNotificationCallback(IMMNotificationClient client);

            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
        }

        [ComImport]
        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig]
            int GetCount(out uint count);

            [PreserveSig]
            int Item(uint index, out IMMDevice device);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object activated);

            [PreserveSig]
            int OpenPropertyStore(int access, out IPropertyStore properties);

            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

            [PreserveSig]
            int GetState(out int state);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig]
            int GetCount(out uint count);

            [PreserveSig]
            int GetAt(uint index, out PropertyKey key);

            [PreserveSig]
            int GetValue(ref PropertyKey key, out PropVariant value);

            [PreserveSig]
            int SetValue(ref PropertyKey key, ref PropVariant value);

            [PreserveSig]
            int Commit();
        }

        [ComImport]
        [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMNotificationClient
        {
            [PreserveSig]
            int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);

            [PreserveSig]
            int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

            [PreserveSig]
            int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

            [PreserveSig]
            int OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);

            [PreserveSig]
            int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
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

        // Known risk: CPolicyConfigClient and IPolicyConfig are undocumented, with no public API to set the default
        // audio device in their place. Only SetDefaultEndpoint is called; the slots before it are declared so it lands
        // at its place in the vtable, and their parameters are left as raw pointers.
        [ComImport]
        [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
        private class PolicyConfigClient
        {
        }

        [ComImport]
        [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig]
            int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr format);

            [PreserveSig]
            int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int isDefault, IntPtr format);

            [PreserveSig]
            int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

            [PreserveSig]
            int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr endpointFormat, IntPtr mixFormat);

            [PreserveSig]
            int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int isDefault, IntPtr defaultPeriod, IntPtr minimumPeriod);

            [PreserveSig]
            int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr period);

            [PreserveSig]
            int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);

            [PreserveSig]
            int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);

            [PreserveSig]
            int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr key, IntPtr value);

            [PreserveSig]
            int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr key, IntPtr value);

            [PreserveSig]
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

            [PreserveSig]
            int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int isVisible);
        }
    }
}
