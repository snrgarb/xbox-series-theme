using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The battery percentage Windows keeps for a Bluetooth LE device, the one Settings shows. Windows.Gaming.Input's own
    /// battery report for an Xbox pad on Bluetooth LE is a fixed placeholder, so the pad's percentage is read here.
    /// </summary>
    internal static class BluetoothBattery
    {
        // A Bluetooth LE HID device: BTHLEDEVICE\{00001812-...}_DEV_VID&02045E_PID&0B13_REV&0520_987A14D29A9D\...
        private static readonly Regex HidDevice = new Regex(
            @"^BTHLEDEVICE\\\{00001812-[^}]*\}_DEV_VID&02(?<vid>[0-9A-F]{4})_PID&(?<pid>[0-9A-F]{4})_REV&[0-9A-F]{4}_(?<address>[0-9A-F]{12})\\",
            RegexOptions.IgnoreCase);

        private static DEVPROPKEY batteryKey = new DEVPROPKEY(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
        private static DEVPROPKEY connectedKey = new DEVPROPKEY(new Guid("83DA6326-97A6-4088-9453-A1923F573B29"), 15);

        /// <summary>
        /// The percentage left on the first connected Bluetooth LE controller with this vendor and product, or null when
        /// none is connected or Windows has no percentage for it.
        /// </summary>
        public static int? PercentOf(ushort vendorId, ushort productId)
        {
            var addresses = new List<string>();
            ForEachDevice("BTHLEDEVICE", (set, data, id) =>
            {
                var match = HidDevice.Match(id);
                if (match.Success
                    && Convert.ToUInt16(match.Groups["vid"].Value, 16) == vendorId
                    && Convert.ToUInt16(match.Groups["pid"].Value, 16) == productId)
                {
                    addresses.Add(match.Groups["address"].Value.ToUpperInvariant());
                }
            });

            int? percent = null;
            ForEachDevice("BTHLE", (set, data, id) =>
            {
                if (percent != null || !addresses.Exists(a => id.StartsWith(@"BTHLE\DEV_" + a + @"\", StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (ByteProperty(set, ref data, ref connectedKey) is byte connected && connected != 0
                    && ByteProperty(set, ref data, ref batteryKey) is byte battery)
                {
                    percent = battery;
                }
            });
            return percent;
        }

        private static void ForEachDevice(string enumerator, DeviceAction action)
        {
            const uint DIGCF_PRESENT = 0x2;
            const uint DIGCF_ALLCLASSES = 0x4;
            var set = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
            if (set == new IntPtr(-1))
            {
                return;
            }

            try
            {
                var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) };
                var id = new StringBuilder(512);
                for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref data); i++)
                {
                    if (SetupDiGetDeviceInstanceIdW(set, ref data, id, id.Capacity, out _))
                    {
                        action(set, data, id.ToString());
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }
        }

        /// <summary>A one-byte property (a byte or a boolean), or null when the device doesn't have it.</summary>
        private static byte? ByteProperty(IntPtr set, ref SP_DEVINFO_DATA data, ref DEVPROPKEY key)
        {
            var buffer = new byte[4];
            return SetupDiGetDevicePropertyW(set, ref data, ref key, out _, buffer, buffer.Length, out var size, 0) && size >= 1
                ? buffer[0]
                : (byte?)null;
        }

        private delegate void DeviceAction(IntPtr set, SP_DEVINFO_DATA data, string instanceId);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;

            public DEVPROPKEY(Guid fmtid, uint pid)
            {
                this.fmtid = fmtid;
                this.pid = pid;
            }
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SP_DEVINFO_DATA data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA data, StringBuilder id, int size, out int required);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref SP_DEVINFO_DATA data, ref DEVPROPKEY key, out uint type, byte[] buffer, int size, out int required, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    }
}
