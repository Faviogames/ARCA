using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WinMemoryCleaner
{
    // This interface keeps tests completely separate from the user's real browsers.
    internal interface IBrowserProcess : IDisposable
    {
        string Name { get; }
        int SessionId { get; }
        bool HasExited { get; }
        void Kill();
        bool WaitForExit(int milliseconds);
    }

    /// <summary>Measured outcome of an explicitly requested browser shutdown.</summary>
    public sealed class BrowserCloseResult
    {
        /// <summary>Browser processes confirmed terminated.</summary>
        public int Closed { get; internal set; }
        /// <summary>Browser processes that could not be confirmed terminated.</summary>
        public int Failed { get; internal set; }
    }

    /// <summary>Forces closure of known browser executables in this Windows session only.</summary>
    public static class BrowserCloseService
    {
        private static readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "msedge", "chrome", "opera", "opera_gx", "firefox", "brave", "vivaldi",
            "chromium", "iexplore", "waterfox", "librewolf", "floorp", "zen", "arc"
        };

        /// <summary>Matches exact browser image names; excludes embedded WebView runtimes and generic launchers.</summary>
        public static bool IsSupportedBrowser(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return _names.Contains(name);
        }

        internal static BrowserCloseResult CloseIfRequested(bool requested, Func<BrowserCloseResult> close)
        {
            return requested ? close() : null;
        }

        /// <summary>Runs only when a manual cleaning has explicitly requested browser closure.</summary>
        public static BrowserCloseResult ForceCloseCurrentSession()
        {
            try
            {
                using (var current = Process.GetCurrentProcess())
                    return Close(Process.GetProcesses().Select(p => (IBrowserProcess)new BrowserProcess(p)), current.SessionId);
            }
            catch (Exception e)
            {
                Logger.Warning("RamPrototype: browser shutdown could not be completed: " + e.Message);
                return new BrowserCloseResult { Failed = 1 };
            }
        }

        internal static BrowserCloseResult Close(IEnumerable<IBrowserProcess> processes, int sessionId)
        {
            var result = new BrowserCloseResult();
            var snapshot = processes.ToList();
            var requested = new List<IBrowserProcess>();
            try
            {
                foreach (var process in snapshot)
                {
                    // A generic browser.exe/launcher.exe or a WebView belongs to arbitrary
                    // applications and is deliberately never included in this allowlist.
                    try
                    {
                        if (process.SessionId != sessionId || !IsSupportedBrowser(process.Name) || process.HasExited)
                            continue;
                    }
                    catch (Exception e) { Logger.Debug(e); continue; }

                    try
                    {
                        process.Kill();
                        requested.Add(process);
                    }
                    catch (Exception e)
                    {
                        // Exiting between enumeration and Kill is an expected race.
                        try { if (process.HasExited) continue; } catch { }
                        result.Failed++;
                        Logger.Warning("RamPrototype: a browser process could not be closed: " + e.Message);
                    }
                }

                // All candidates receive Kill before waiting. Total waiting is bounded,
                // even when a browser has hundreds of renderer processes.
                var elapsed = Stopwatch.StartNew();
                foreach (var process in requested)
                {
                    try
                    {
                        if (process.HasExited || process.WaitForExit(Math.Max(0, 3000 - (int)Math.Min(3000L, elapsed.ElapsedMilliseconds))))
                            result.Closed++;
                        else
                            result.Failed++;
                    }
                    catch { result.Failed++; }
                }
                return result;
            }
            finally
            {
                foreach (var process in snapshot)
                    process.Dispose();
            }
        }

        private sealed class BrowserProcess : IBrowserProcess
        {
            private readonly Process _process;
            public BrowserProcess(Process process) { _process = process; }
            public string Name { get { return _process.ProcessName; } }
            public int SessionId { get { return _process.SessionId; } }
            public bool HasExited { get { return _process.HasExited; } }
            public void Kill() { _process.Kill(); }
            public bool WaitForExit(int milliseconds) { return _process.WaitForExit(milliseconds); }
            public void Dispose() { _process.Dispose(); }
        }
    }
}
