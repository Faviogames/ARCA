using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Removes confirmed nonpresent device records across all Windows device classes.
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

        internal static bool RemovalConfirmed(bool requestSucceeded, bool? stillRegistered)
        {
            return requestSucceeded && stillRegistered.HasValue && !stillRegistered.Value;
        }

        private static bool IsPresent(int devInst)
        {
            int status, problem;
            // Unknown/error states are retained; only a confirmed absent devnode is eligible.
            return !ShouldRemoveFromStatus(CM_Get_DevNode_Status(out status, out problem, devInst, 0));
        }

        internal static bool ShouldRemoveFromStatus(int configurationResult)
        {
            return configurationResult == CR_NO_SUCH_DEVINST;
        }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            // A null enumerator includes HID, SWD audio/software components, monitors,
            // USB and other classes. The persisted category bit keeps its legacy name.
            const string enumerator = null;
            {
                result.Progress(Tr.Get("Revisando dispositivos desconectados de Windows…"));
                var set = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DIGCF_ALLCLASSES);
                if (set == INVALID_HANDLE_VALUE) { result.Failed++; result.AddNote(Tr.Get("No se pudo leer la lista de dispositivos de Windows")); return; }
                try
                {
                    var devices = new List<SP_DEVINFO_DATA>();
                    for (var i = 0; ; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var item = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) };
                        if (!SetupDiEnumDeviceInfo(set, i, ref item))
                        {
                            var error = Marshal.GetLastWin32Error();
                            if (error != 259) { result.Failed++; result.AddNote(Tr.Format("No se pudo completar la revisión de dispositivos · error {0}", error)); }
                            break;
                        }
                        if (ShouldRemove(IsPresent(item.DevInst))) devices.Add(item);
                        else result.Skipped++;
                    }
                    // Capture the list first so removals do not shift enumeration indices.
                    for (var i = 0; i < devices.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var data = devices[i];
                        if (!ShouldRemove(IsPresent(data.DevInst))) continue;

                        var name = Name(set, ref data);
                        var instanceId = InstanceId(set, ref data);
                        var line = name + Tr.Get("  (dispositivo desconectado · sin espera mínima)");
                        if (instanceId == null) { result.Skipped++; result.Detail(Tr.Format("Omitido: no se pudo identificar {0}", name)); continue; }
                        if (!apply) { result.Entries++; result.Detail(line); continue; }
                        if (IsRegistered(enumerator, instanceId) == false)
                        { result.Skipped++; result.Detail(Tr.Format("Ya no estaba registrado: {0}", name)); continue; }
                        token.ThrowIfCancellationRequested();
                        result.Progress(Tr.Format("Retirando {0}/{1} · {2}", i + 1, devices.Count, name));
                        var error = 0;
                        var removed = RemoveWithProgress(result, name, () => IsPresent(data.DevInst), () =>
                        {
                            var success = SetupDiCallClassInstaller(DIF_REMOVE, set, ref data);
                            if (!success) error = Marshal.GetLastWin32Error();
                            return success;
                        }, token);
                        if (!removed.HasValue) { result.Skipped++; result.Detail(Tr.Format("Omitido: ya no se confirma la desconexión de {0}", name)); }
                        else if (removed.Value)
                        {
                            var registered = IsRegistered(enumerator, instanceId);
                            if (RemovalConfirmed(true, registered)) { result.Entries++; result.Detail(Tr.Format("Retirado y verificado: {0}", line)); }
                            else
                            {
                                result.Failed++;
                                result.Detail(Tr.Format(registered == true ? "Retirada no confirmada: {0} · el registro sigue apareciendo en Windows" : "Retirada no confirmada: {0} · no se pudo verificar la lista de Windows", name));
                            }
                        }
                        else { result.Failed++; result.Detail(Tr.Format("No se pudo retirar: {0} · error {1}", line, error)); }
                    }
                }
                finally { SetupDiDestroyDeviceInfoList(set); }
            }
        }

        // Re-open the installed-device list: an installer success code alone does
        // not prove the record disappeared. Errors stay unconfirmed, never success.
        private static bool? IsRegistered(string enumerator, string instanceId)
        {
            var set = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DIGCF_ALLCLASSES);
            if (set == INVALID_HANDLE_VALUE) return null;
            try
            {
                for (var i = 0; ; i++)
                {
                    var data = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) };
                    if (!SetupDiEnumDeviceInfo(set, i, ref data)) return Marshal.GetLastWin32Error() == 259 ? (bool?)false : null;
                    var id = InstanceId(set, ref data);
                    if (id == null) return null;
                    if (string.Equals(id, instanceId, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }

        private static string InstanceId(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            var id = new StringBuilder(1024);
            int size;
            return SetupDiGetDeviceInstanceIdW(set, ref data, id, id.Capacity, out size) ? id.ToString() : null;
        }

        // A driver installer cannot be safely aborted mid-call. Keep the UI informed
        // while Windows finishes, then honor cancellation before the next device.
        private static bool? RemoveWithProgress(CleanTaskResult result, string name, Func<bool> present, Func<bool> remove, CancellationToken token)
        {
            var gate = new object();
            var waiting = true;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            using (var timer = new Timer(_ =>
            {
                lock (gate)
                    if (waiting) result.Progress(Tr.Format(token.IsCancellationRequested ? "Cancelación pendiente · {0} · {1} s" : "Esperando a Windows · {0} · {1} s", name, (int)elapsed.Elapsed.TotalSeconds));
            }, null, 8000, 5000))
            {
                try { return RemoveIfDisconnected(present, remove); }
                finally { lock (gate) waiting = false; }
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
            return SetupDiGetDeviceInstanceIdW(set, ref data, id, id.Capacity, out size) ? id.ToString() : Tr.Get("Dispositivo");
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
