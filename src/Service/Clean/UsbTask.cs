using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Removes disconnected USB device records without a minimum age or a last-seen date.
    /// Driver packages stay in the driver store. Presence is checked again before each removal.
    /// </summary>
    internal sealed class UsbCleanTask : ICleanTask
    {
        public CleanCategory Category { get { return CleanCategory.Usb; } }

        internal static bool ShouldRemove(bool present)
        {
            return !present;
        }

        // A null outcome means that disconnection could no longer be confirmed.
        internal static bool? RemoveIfDisconnected(Func<bool> isPresent, Func<bool> remove)
        {
            return ShouldRemove(isPresent()) ? (bool?)remove() : null;
        }

        private static bool IsPresent(int devInst)
        {
            int status, problem;
            // Unknown/error states are retained; only a confirmed absent devnode is eligible.
            return CM_Get_DevNode_Status(out status, out problem, devInst, 0) != CR_NO_SUCH_DEVINST;
        }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            foreach (var enumerator in new[] { "USB", "USBSTOR" })
            {
                var set = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DIGCF_ALLCLASSES);
                if (set == INVALID_HANDLE_VALUE) { result.AddNote("No se pudo leer la lista de dispositivos " + enumerator); continue; }
                try
                {
                    var devices = new List<SP_DEVINFO_DATA>();
                    for (var i = 0; ; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var item = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) };
                        if (!SetupDiEnumDeviceInfo(set, i, ref item)) break;
                        devices.Add(item);
                    }
                    // Capture the list first so removals do not shift enumeration indices.
                    for (var i = 0; i < devices.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var data = devices[i];
                        if (!ShouldRemove(IsPresent(data.DevInst))) continue;

                        var name = Name(set, ref data);
                        var line = name + "  (USB desconectado · sin espera mínima)";
                        if (!apply) { result.Entries++; result.Detail(line); continue; }
                        token.ThrowIfCancellationRequested();
                        var removed = RemoveIfDisconnected(() => IsPresent(data.DevInst), () => SetupDiCallClassInstaller(DIF_REMOVE, set, ref data));
                        if (!removed.HasValue) { result.Skipped++; result.Detail("Omitido: ya no se confirma la desconexión de " + name); }
                        else if (removed.Value) { result.Entries++; result.Detail(line); }
                        else { result.Failed++; result.Detail("No se pudo retirar: " + line); }
                    }
                }
                finally { SetupDiDestroyDeviceInfoList(set); }
            }
        }

        private static string Name(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            foreach (var property in new[] { SPDRP_FRIENDLYNAME, SPDRP_DEVICEDESC })
            {
                var buffer = new byte[1024];
                int type, required;
                if (SetupDiGetDeviceRegistryPropertyW(set, ref data, property, out type, buffer, buffer.Length, out required))
                {
                    var text = Encoding.Unicode.GetString(buffer, 0, Math.Max(0, Math.Min(required, buffer.Length))).TrimEnd('\0');
                    if (text.Length > 0) return text;
                }
            }
            var id = new StringBuilder(512);
            int size;
            return SetupDiGetDeviceInstanceIdW(set, ref data, id, id.Capacity, out size) ? id.ToString() : "Dispositivo USB";
        }

        private const int DIGCF_ALLCLASSES = 0x4, DIF_REMOVE = 0x5, CR_NO_SUCH_DEVINST = 0xD;
        private const int SPDRP_DEVICEDESC = 0x0, SPDRP_FRIENDLYNAME = 0xC;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public int DevInst; public IntPtr Reserved; }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr set, int index, ref SP_DEVINFO_DATA data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA data, StringBuilder id, int size, out int required);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SP_DEVINFO_DATA data, int property, out int type, byte[] buffer, int size, out int required);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiCallClassInstaller(int installFunction, IntPtr set, ref SP_DEVINFO_DATA data);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_DevNode_Status(out int status, out int problem, int devInst, int flags);
    }
}
