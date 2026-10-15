using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: Windows' master volume, on the default output device, and the output and input devices to switch
    /// between, through the Core Audio API. The device is looked up on every call, so the volume follows the user
    /// switching outputs. When there is no output device, or Windows refuses, the volume reads as 0 and unmuted, the
    /// devices as none, and changes do nothing. <see cref="DevicesChanged"/> is raised, on a thread of Windows' own,
    /// whenever a device comes or goes, turns on or off, or becomes a default, until the adapter is disposed.
    /// </summary>
    internal sealed partial class WindowsAudio : IAudio, IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // Windows answers GetDefaultAudioEndpoint with this when there is no device of that flow.
        private const int ElementNotFound = unchecked((int)0x80070490);

        // Held for the adapter's lifetime: Windows calls back on the client only while it stays registered with this
        // enumerator, and the client must outlive the registration.
        private IMMDeviceEnumerator notifyingEnumerator;
        private NotificationClient notificationClient;

        public WindowsAudio()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                var client = new NotificationClient(this);
                var hr = enumerator.RegisterEndpointNotificationCallback(client);
                if (hr < 0)
                {
                    Release(enumerator);
                    Marshal.ThrowExceptionForHR(hr);
                }

                notifyingEnumerator = enumerator;
                notificationClient = client;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Warn(e, "Couldn't listen for Windows' audio devices changing");
            }
        }

        /// <summary>Raised on a thread of Windows' own, often several times over for one change.</summary>
        public event Action DevicesChanged;

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

        public IReadOnlyList<AudioDevice> Devices(AudioDeviceKind kind)
        {
            var devices = new List<AudioDevice>();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(FlowOf(kind), DeviceStateActive, out collection));
                Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        Marshal.ThrowExceptionForHR(collection.Item(i, out device));
                        Marshal.ThrowExceptionForHR(device.GetId(out var id));
                        devices.Add(new AudioDevice(id, FriendlyName(device) ?? id, kind));
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException))
                    {
                        // One device Windows won't describe is left out rather than losing the rest.
                        logger.Warn(e, $"Couldn't read Windows' audio {Describe(kind)} device {i}");
                    }
                    finally
                    {
                        Release(device);
                    }
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Warn(e, $"Couldn't list Windows' audio {Describe(kind)} devices");
            }
            finally
            {
                Release(collection);
                Release(enumerator);
            }

            return devices;
        }

        public string DefaultDeviceId(AudioDeviceKind kind)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                var hr = enumerator.GetDefaultAudioEndpoint(FlowOf(kind), ERole.Multimedia, out device);
                if (hr == ElementNotFound)
                {
                    return null;
                }

                Marshal.ThrowExceptionForHR(hr);
                Marshal.ThrowExceptionForHR(device.GetId(out var id));
                return id;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Warn(e, $"Couldn't read Windows' default audio {Describe(kind)} device");
                return null;
            }
            finally
            {
                Release(device);
                Release(enumerator);
            }
        }

        public void SetDefaultDevice(AudioDevice device)
        {
            if (device == null)
            {
                return;
            }

            // Known risk: Windows has no public API to change the default device. IPolicyConfig is undocumented, the
            // interface the Sound control panel itself uses; it has held steady since Windows 7, but an update could
            // change or remove it, which would show here as a failure logged and the default left as it was.
            IPolicyConfig policy = null;
            try
            {
                policy = (IPolicyConfig)new PolicyConfigClient();

                // Console and multimedia, as Sound settings' own default does; communications is left alone, so a
                // headset kept for calls stays so.
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(device.Id, ERole.Console));
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(device.Id, ERole.Multimedia));
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Warn(e, $"Couldn't make {device.Name} Windows' default audio {Describe(device.Kind)} device");
            }
            finally
            {
                Release(policy);
            }
        }

        /// <summary>Stops listening for devices changing. Safe to call more than once.</summary>
        public void Dispose()
        {
            var enumerator = notifyingEnumerator;
            var client = notificationClient;
            notifyingEnumerator = null;
            notificationClient = null;
            if (enumerator == null)
            {
                return;
            }

            try
            {
                Marshal.ThrowExceptionForHR(enumerator.UnregisterEndpointNotificationCallback(client));
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                logger.Warn(e, "Couldn't stop listening for Windows' audio devices changing");
            }
            finally
            {
                Release(enumerator);
            }
        }

        private void RaiseDevicesChanged()
        {
            try
            {
                DevicesChanged?.Invoke();
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // Nothing may escape back into Windows' audio service through the callback.
                logger.Warn(e, "Couldn't pass on Windows' audio devices changing");
            }
        }

        private static EDataFlow FlowOf(AudioDeviceKind kind) =>
            kind == AudioDeviceKind.Input ? EDataFlow.Capture : EDataFlow.Render;

        private static string Describe(AudioDeviceKind kind) => kind == AudioDeviceKind.Input ? "input" : "output";

        /// <summary>The name Windows shows for <paramref name="device"/>, or null when it has none.</summary>
        private static string FriendlyName(IMMDevice device)
        {
            IPropertyStore store = null;
            var value = default(PropVariant);
            try
            {
                Marshal.ThrowExceptionForHR(device.OpenPropertyStore(StgmRead, out store));
                var key = DeviceFriendlyName;
                Marshal.ThrowExceptionForHR(store.GetValue(ref key, out value));
                return value.vt == VtLpwstr && value.pointer != IntPtr.Zero ? Marshal.PtrToStringUni(value.pointer) : null;
            }
            finally
            {
                PropVariantClear(ref value);
                Release(store);
            }
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

        /// <summary>
        /// Windows' callback for devices changing. Every change but a property's (which come in floods, for things
        /// like a device's volume) raises <see cref="DevicesChanged"/>; the core re-reads the devices on each.
        /// </summary>
        private sealed class NotificationClient : IMMNotificationClient
        {
            private readonly WindowsAudio audio;

            public NotificationClient(WindowsAudio audio)
            {
                this.audio = audio;
            }

            public int OnDeviceStateChanged(string deviceId, int newState)
            {
                audio.RaiseDevicesChanged();
                return 0;
            }

            public int OnDeviceAdded(string deviceId)
            {
                audio.RaiseDevicesChanged();
                return 0;
            }

            public int OnDeviceRemoved(string deviceId)
            {
                audio.RaiseDevicesChanged();
                return 0;
            }

            public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)
            {
                audio.RaiseDevicesChanged();
                return 0;
            }

            public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
        }
    }
}
