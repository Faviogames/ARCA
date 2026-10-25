using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace WinMemoryCleaner
{
    /// <summary>A read-only sample of resident memory in one process.</summary>
    internal sealed class AppMemorySample
    {
        internal string Name;
        internal long Bytes;
    }

    /// <summary>Read-only process sampling; never changes or closes processes.</summary>
    internal static class TopMemoryApps
    {
        internal static IList<AppMemorySample> Capture()
        {
            var samples = new List<AppMemorySample>();
            int session;
            using (var current = Process.GetCurrentProcess()) session = current.SessionId;
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.SessionId == session && !process.HasExited)
                            samples.Add(new AppMemorySample { Name = process.ProcessName, Bytes = process.WorkingSet64 });
                    }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException) { }
                    catch (NotSupportedException) { }
                }
            }
            return Rank(samples);
        }

        // Group executable names so a browser's renderer processes share one chip.
        // Shared pages can be counted more than once: this is not private working set.
        internal static IList<AppMemorySample> Rank(IEnumerable<AppMemorySample> samples)
        {
            return samples.Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name) && s.Bytes > 0)
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new AppMemorySample { Name = g.Key, Bytes = g.Sum(s => s.Bytes) })
                .OrderByDescending(s => s.Bytes).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Take(3).ToList();
        }

        internal static string DisplayName(string name)
        {
            switch (name.ToUpperInvariant())
            {
                case "MSEDGE": return "Edge";
                case "CHROME": return "Chrome";
                case "FIREFOX": return "Firefox";
                case "OPERA": return "Opera";
                case "OPERA_GX": return "Opera GX";
                case "BRAVE": return "Brave";
                case "EXPLORER": return Tr.Get("Explorador");
                case "WINMEMORYCLEANER":
                case "RAMPROTOTYPE":
                case "ARCA": return "ARCA";
                default: return name;
            }
        }

        internal static string FormatBytes(long bytes)
        {
            return bytes >= 1073741824L
                ? (bytes / 1073741824d).ToString("0.0", Tr.Culture) + " GB"
                : (bytes / 1048576d).ToString("0", Tr.Culture) + " MB";
        }
    }

    /// <summary>A chip combines usage indication with an optional top-application label.</summary>
    public sealed class RamChip : ObservableObject
    {
        private bool _isLit;
        private string _name = string.Empty;
        private string _usage = string.Empty;
        private string _rank = string.Empty;
        /// <summary>Whether the occupancy meter reaches this chip.</summary>
        public bool IsLit { get { return _isLit; } set { if (_isLit == value) return; _isLit = value; RaisePropertyChanged(); } }
        /// <summary>Application label, or empty for unassigned chips.</summary>
        public string Name { get { return _name; } }
        /// <summary>Formatted resident memory.</summary>
        public string Usage { get { return _usage; } }
        /// <summary>Rank label.</summary>
        public string Rank { get { return _rank; } }
        /// <summary>Full accessible description, including the executable name.</summary>
        public string Description { get; private set; }
        internal void Update(AppMemorySample sample, int rank)
        {
            _name = sample == null ? string.Empty : TopMemoryApps.DisplayName(sample.Name);
            _usage = sample == null ? string.Empty : TopMemoryApps.FormatBytes(sample.Bytes);
            _rank = sample == null ? string.Empty : "TOP " + rank;
            Description = sample == null ? Tr.Get("Indicador de uso de RAM") : Tr.Format("{0}: {1} ({2}) · {3} de RAM residente aproximada", _rank, _name, sample.Name, _usage);
            RaisePropertyChanged("Name"); RaisePropertyChanged("Usage"); RaisePropertyChanged("Rank"); RaisePropertyChanged("Description");
        }
    }
}
