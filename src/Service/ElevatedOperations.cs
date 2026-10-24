using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>Short-lived, explicitly elevated worker. Only fixed ARCA operations cross the pipe.</summary>
    internal static class ElevatedOperations
    {
        private const string WorkerArgument = "--arca-worker";
        private const CleanCategory ElevatedCategories = CleanCategory.Temp | CleanCategory.Registry | CleanCategory.Usb | CleanCategory.Extreme;

        internal static bool IsAdministrator
        {
            get { using (var identity = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
        }

        internal static bool NeedsElevation(CleanCategory categories, bool apply)
        {
            return apply && (categories & ElevatedCategories) != 0;
        }

        internal static bool ValidCategories(int value)
        {
            const int allowed = (int)(CleanCategory.Temp | CleanCategory.Caches | CleanCategory.Registry | CleanCategory.Usb | CleanCategory.RecycleBin | CleanCategory.Extreme);
            return value != 0 && (value & ~allowed) == 0;
        }

        internal static bool ValidMemoryAreas(int value) { return value > 0 && (value & ~255) == 0; }

        internal static bool IsFinalResult(CleanTaskState state)
        {
            return state == CleanTaskState.Completed || state == CleanTaskState.Failed || state == CleanTaskState.Skipped || state == CleanTaskState.Cancelled;
        }

        internal static NamedPipeClientStream CreateWorkerPipe(string name)
        {
            // A synchronous pipe serializes reads and writes: the cancellation read
            // would block the worker's very first progress packet indefinitely.
            return new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        }

        internal static IList<CleanTaskResult> Clean(CleanCategory selected, Action<CleanCategory, CleanTaskState, CleanTaskResult> progress, CancellationToken token)
        {
            var results = new List<CleanTaskResult>();
            Execute("clean", (int)selected, false, reader =>
            {
                var kind = reader.ReadByte();
                if (kind == 0) return false;
                if (kind == 4) throw new InvalidOperationException(reader.ReadString());
                if (kind != 1) throw new InvalidDataException("Respuesta de limpieza no válida.");
                var category = (CleanCategory)reader.ReadInt32();
                var state = (CleanTaskState)reader.ReadInt32();
                var result = reader.ReadBoolean() ? ReadResult(reader, category, state) : null;
                if (result != null && IsFinalResult(state)) results.Add(result);
                if (progress != null) progress(category, state, result);
                return true;
            }, token);
            OneClickCleanService.WriteReport(results);
            return results;
        }

        internal static bool Optimize(Enums.Memory.Areas areas, bool closeBrowsers, Action<byte, string> progress, out BrowserCloseResult browsers)
        {
            var success = false;
            var closed = new BrowserCloseResult();
            Execute("ram", (int)areas, closeBrowsers, reader =>
            {
                var kind = reader.ReadByte();
                if (kind == 0) return false;
                if (kind == 4) throw new InvalidOperationException(reader.ReadString());
                if (kind == 2) { var value = reader.ReadByte(); var step = reader.ReadString(); if (progress != null) progress(value, step); }
                else if (kind == 3) { success = reader.ReadBoolean(); closed.Closed = reader.ReadInt32(); closed.Failed = reader.ReadInt32(); }
                else throw new InvalidDataException("Respuesta de memoria no válida.");
                return true;
            }, CancellationToken.None);
            browsers = closeBrowsers ? closed : null;
            return success;
        }

        internal static int UndoRegistry()
        {
            var restored = 0;
            Execute("undo", 0, false, reader =>
            {
                var kind = reader.ReadByte();
                if (kind == 0) return false;
                if (kind == 4) throw new InvalidOperationException(reader.ReadString());
                if (kind != 5) throw new InvalidDataException("Respuesta de respaldo no válida.");
                restored = reader.ReadInt32();
                return true;
            }, CancellationToken.None);
            return restored;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Reader and writer share the pipe owned and disposed by the enclosing using; .NET 4 has no leaveOpen overload.")]
        private static void Execute(string operation, int selection, bool closeBrowsers, Func<BinaryReader, bool> receive, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var name = "ARCA-" + Guid.NewGuid().ToString("N");
            var security = new PipeSecurity();
            var user = WindowsIdentity.GetCurrent().User;
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
            using (var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security))
            {
                Process worker;
                try
                {
                    worker = Process.Start(new ProcessStartInfo(Helper.GetExecutablePath(), WorkerArgument + " " + name + " " + Process.GetCurrentProcess().Id)
                    { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
                }
                catch (Win32Exception e) { if (e.NativeErrorCode == 1223) throw new OperationCanceledException("Permiso de administrador cancelado."); throw; }
                if (worker == null) throw new InvalidOperationException("No se pudo abrir el auxiliar de limpieza.");
                using (worker)
                {
                    var connection = pipe.BeginWaitForConnection(null, null);
                    using (connection.AsyncWaitHandle)
                    {
                        var deadline = DateTime.UtcNow.AddSeconds(30);
                        while (!connection.AsyncWaitHandle.WaitOne(100))
                        {
                            token.ThrowIfCancellationRequested();
                            if (worker.HasExited || DateTime.UtcNow > deadline) throw new IOException("El auxiliar no pudo conectarse. No se inició la limpieza.");
                        }
                        pipe.EndWaitForConnection(connection);
                    }
                    uint client;
                    if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out client) || client != worker.Id) throw new InvalidDataException("Auxiliar no reconocido.");
                    var writer = new BinaryWriter(pipe);
                    var reader = new BinaryReader(pipe);
                    writer.Write(user.Value); writer.Write(operation); writer.Write(selection); writer.Write(closeBrowsers); writer.Flush();
                    using (token.Register(() => { try { lock (writer) { writer.Write(true); writer.Flush(); } } catch (IOException) { } catch (ObjectDisposedException) { } }))
                        while (receive(reader)) { }
                    // A completed response means the worker has finished service restoration too.
                    worker.WaitForExit(5000);
                }
            }
        }

        /// <summary>Recognized before the UI, migrations or instance mutex are initialized.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Reader and writer share the pipe owned and disposed by the enclosing using; .NET 4 has no leaveOpen overload.")]
        internal static bool TryRunWorker(string[] arguments)
        {
            if (arguments == null || arguments.Length == 0 || arguments[0] != WorkerArgument) return false;
            int parent;
            Guid nonce;
            if (arguments.Length != 3 || !arguments[1].StartsWith("ARCA-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(arguments[1].Substring(5), "N", out nonce) || !int.TryParse(arguments[2], out parent) || !IsAdministrator) return true;
            try
            {
                using (var pipe = CreateWorkerPipe(arguments[1]))
                {
                    pipe.Connect(15000);
                    uint server;
                    if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out server) || server != parent) return true;
                    using (var process = Process.GetProcessById(parent))
                        if (!string.Equals(process.MainModule.FileName, Helper.GetExecutablePath(), StringComparison.OrdinalIgnoreCase)) return true;
                    var reader = new BinaryReader(pipe);
                    var writer = new BinaryWriter(pipe);
                    var owner = reader.ReadString();
                    // Elevating with another account would otherwise clean that account's profile.
                    if (owner != WindowsIdentity.GetCurrent().User.Value)
                    {
                        writer.Write((byte)4); writer.Write("La limpieza debe autorizarse con la misma cuenta de Windows que abrió ARCA."); writer.Write((byte)0); writer.Flush(); return true;
                    }
                    var operation = reader.ReadString();
                    var selected = reader.ReadInt32();
                    var closeBrowsers = reader.ReadBoolean();
                    using (var cancel = new CancellationTokenSource())
                    {
                        var listener = new Thread(() =>
                        {
                            try { reader.ReadBoolean(); }
                            catch (IOException) { }
                            catch (ObjectDisposedException) { }
                            finally { try { cancel.Cancel(); } catch (ObjectDisposedException) { } }
                        });
                        listener.IsBackground = true;
                        listener.Start();
                        try
                        {
                            if (operation == "clean" && ValidCategories(selected))
                            {
                                OneClickCleanService.Run((CleanCategory)selected, true, (c, s, r) =>
                                {
                                    lock (writer)
                                    {
                                        writer.Write((byte)1); writer.Write((int)c); writer.Write((int)s); writer.Write(r != null);
                                        if (r != null) WriteResult(writer, r);
                                        writer.Flush();
                                    }
                                }, cancel.Token);
                            }
                            else if (operation == "ram" && ValidMemoryAreas(selected))
                            {
                                var browsers = BrowserCloseService.CloseIfRequested(closeBrowsers, BrowserCloseService.ForceCloseCurrentSession);
                                var service = new ComputerService();
                                service.OnOptimizeProgressUpdate += (value, step) => { writer.Write((byte)2); writer.Write(value); writer.Write(step ?? ""); writer.Flush(); };
                                service.Optimize(Enums.Memory.Optimization.Reason.Manual, (Enums.Memory.Areas)selected);
                                writer.Write((byte)3); writer.Write(service.LastOptimizationErrorCount == 0 && (browsers == null || browsers.Failed == 0));
                                writer.Write(browsers == null ? 0 : browsers.Closed); writer.Write(browsers == null ? 0 : browsers.Failed);
                            }
                            else if (operation == "undo" && selected == 0) { writer.Write((byte)5); writer.Write(RegistryCleanTask.UndoLast()); }
                            else throw new InvalidDataException("Operación no permitida.");
                        }
                        catch (Exception e) { writer.Write((byte)4); writer.Write(e.Message); }
                        writer.Write((byte)0); writer.Flush();
                        pipe.Dispose();
                        listener.Join(1000);
                    }
                }
            }
            catch (Exception e) { Logger.Error(e); }
            return true;
        }

        internal static void WriteResult(BinaryWriter writer, CleanTaskResult result)
        {
            writer.Write(result.Bytes); writer.Write(result.Files); writer.Write(result.Entries); writer.Write(result.Skipped); writer.Write(result.Failed);
            writer.Write(result.Note ?? ""); writer.Write(result.ProgressMessage ?? ""); writer.Write(result.Details.Count);
            foreach (var detail in result.Details) writer.Write(detail);
        }

        internal static CleanTaskResult ReadResult(BinaryReader reader, CleanCategory category, CleanTaskState state)
        {
            var result = new CleanTaskResult(category) { State = state, Bytes = reader.ReadInt64(), Files = reader.ReadInt32(), Entries = reader.ReadInt32(), Skipped = reader.ReadInt32(), Failed = reader.ReadInt32(), Note = reader.ReadString(), ProgressMessage = reader.ReadString() };
            var count = reader.ReadInt32();
            if (count < 0 || count > 401) throw new InvalidDataException("Informe no válido.");
            for (var i = 0; i < count; i++) result.Detail(reader.ReadString());
            return result;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint processId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint processId);
    }
}
