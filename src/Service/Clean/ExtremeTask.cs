using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Opt-in deep clean: GPU shader caches, downloaded Windows Update packages and the
    /// Delivery Optimization cache, plus a DNS resolver cache flush. WinSxS and DISM /ResetBase are deliberately never touched.
    /// </summary>
    internal sealed class ExtremeCleanTask : ICleanTask
    {
        private static readonly string[] _updateServices = { "wuauserv", "bits" };

        public CleanCategory Category { get { return CleanCategory.Extreme; } }

        private static string Windows { get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); } }
        private static string UpdateDownloads { get { return Path.Combine(Windows, @"SoftwareDistribution\Download"); } }
        private static string DeliveryCache { get { return Path.Combine(Windows, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"); } }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            new FileCleanTask(Category, r => CleanCatalog.Shaders()).Run(apply, result, token);

            token.ThrowIfCancellationRequested();
            // Sub-results keep each part's own size line; finally keeps partial work on cancellation.
            var update = new CleanTaskResult(Category);
            try { WindowsUpdate(apply, update, token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { update.Failed++; update.AddNote("Windows Update: " + e.Message); }
            finally { result.Add(update); }

            token.ThrowIfCancellationRequested();
            var delivery = new CleanTaskResult(Category);
            try { DeliveryOptimization(apply, delivery); }
            catch (Exception e) { delivery.Failed++; delivery.AddNote("Optimización de distribución: " + e.Message); }
            result.Add(delivery);

            token.ThrowIfCancellationRequested();
            // Frees no disk space; it only shows up as a report line, or a failure.
            if (!apply) result.Detail("Caché DNS: se vaciará");
            else if (NativeMethods.DnsFlushResolverCache()) result.Detail("Caché DNS vaciada");
            else { result.Failed++; result.AddNote("Caché DNS no se pudo vaciar"); }
        }

        /// <summary>Why update leftovers must stay right now, or null when it is safe to remove them.</summary>
        internal static string UpdateBlocker(Func<string, bool> keyExists, Func<string, bool> isRunning)
        {
            if (keyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") ||
                keyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                return "Windows Update: actualización pendiente de reinicio";
            // TiWorker is the servicing worker; TrustedInstaller alone often idles after updates.
            if (isRunning("TiWorker"))
                return "Windows Update: instalación en curso";
            return null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Each controller is disposed in the restart loop or immediately when skipped.")]
        private static void WindowsUpdate(bool apply, CleanTaskResult result, CancellationToken token)
        {
            if (!Directory.Exists(UpdateDownloads)) return;
            var blocker = UpdateBlocker(KeyExists, IsRunning);
            if (blocker != null) { result.AddNote(blocker); return; }

            var target = new CleanTarget(UpdateDownloads, TimeSpan.Zero);
            if (!apply) { FileCleaner.Clean(target, false, result, token); Label(result, "Restos de Windows Update"); return; }

            // The download folder is only consistent while both services are stopped.
            var restart = new List<ServiceController>();
            try
            {
                foreach (var name in _updateServices)
                {
                    var service = new ServiceController(name);
                    try
                    {
                        if (service.Status == ServiceControllerStatus.Stopped) { service.Dispose(); continue; }
                        restart.Add(service);
                        if (service.Status != ServiceControllerStatus.StopPending) service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    catch (Exception e)
                    {
                        if (!restart.Contains(service)) service.Dispose();
                        if (!(e is InvalidOperationException) && !(e is System.ServiceProcess.TimeoutException)) throw;
                        result.Skipped++;
                        result.AddNote("Windows Update: el servicio " + name + " no se detuvo");
                        return;
                    }
                }
                FileCleaner.Clean(target, true, result, token);
                Label(result, "Restos de Windows Update");
            }
            finally
            {
                // Restarted even after a cancellation or error, in reverse order (bits before wuauserv).
                for (var i = restart.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        restart[i].Refresh();
                        if (restart[i].Status == ServiceControllerStatus.Stopped) restart[i].Start();
                    }
                    catch (Exception e) { Logger.Warning("ARCA: no se pudo reiniciar " + restart[i].ServiceName + ": " + e.Message); }
                    finally { restart[i].Dispose(); }
                }
            }
        }

        private static void DeliveryOptimization(bool apply, CleanTaskResult result)
        {
            var before = FileCleaner.Measure(DeliveryCache);
            if (before == 0) return;
            if (!apply) { result.Bytes += before; result.Detail(CleanText.Size(before) + "  Optimización de distribución"); return; }

            // The supported cmdlet keeps the service's own bookkeeping consistent, unlike a raw delete.
            var shell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(shell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Delete-DeliveryOptimizationCache -Force\"")
            {
                CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
            };
            using (var process = Process.Start(info))
            {
                if (!process.WaitForExit(120000))
                {
                    try { process.Kill(); } catch (Exception) { }
                    result.Failed++; result.AddNote("Optimización de distribución: tiempo agotado");
                    return;
                }
                if (process.ExitCode != 0) { result.Failed++; result.AddNote("Optimización de distribución no disponible"); return; }
            }
            var freed = Math.Max(0, before - FileCleaner.Measure(DeliveryCache));
            result.Bytes += freed;
            result.Detail(CleanText.Size(freed) + "  Optimización de distribución");
        }

        private static void Label(CleanTaskResult result, string what)
        {
            if (result.Bytes > 0) result.Detail(CleanText.Size(result.Bytes) + "  " + what);
        }

        private static bool KeyExists(string path)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(path)) return key != null;
        }

        private static bool IsRunning(string name)
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var p in processes) p.Dispose();
            return processes.Length > 0;
        }
    }
}
