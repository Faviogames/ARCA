using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace WinMemoryCleaner
{
#pragma warning disable CA1714 // Singular name reads better at every use site; values are combined only for settings.
    /// <summary>1 clic clean categories; stored as flags in the settings.</summary>
    [Flags]
    public enum CleanCategory
    {
        /// <summary>Nothing selected.</summary>
        None = 0,
        /// <summary>User/system temporary files, reports, old dumps and broken shortcuts.</summary>
        Temp = 1,
        /// <summary>Browser, thumbnail and internet caches.</summary>
        Caches = 2,
        /// <summary>Desktop and Start menu shortcuts whose local target is gone.</summary>
        Shortcuts = 4,
        /// <summary>Narrow registry rules, backed up before any change.</summary>
        Registry = 8,
        /// <summary>Disconnected USB device records without a minimum age; driver packages are kept.</summary>
        Usb = 16,
        /// <summary>Recycle bin, opt-in.</summary>
        RecycleBin = 32,
        /// <summary>GPU shader caches, Windows Update leftovers and Delivery Optimization.</summary>
        Extreme = 64
    }
#pragma warning restore CA1714

    /// <summary>Visible state of one category during a run.</summary>
    public enum CleanTaskState
    {
        /// <summary>Not run yet.</summary>
        Idle,
        /// <summary>Queued in the current run.</summary>
        Pending,
        /// <summary>Measuring without changing anything.</summary>
        Analyzing,
        /// <summary>Applying changes.</summary>
        Cleaning,
        /// <summary>Finished.</summary>
        Completed,
        /// <summary>Not applicable now (for example, a pending update reboot).</summary>
        Skipped,
        /// <summary>Unexpected error; the other categories still run.</summary>
        Failed,
        /// <summary>Cancelled by the user.</summary>
        Cancelled
    }

    /// <summary>Measured outcome of one category. In an analysis, the same counters are estimates.</summary>
    public sealed class CleanTaskResult
    {
        private const int MaxDetails = 400;
        private readonly List<string> _details = new List<string>();

        /// <summary>Empty result for a category.</summary>
        public CleanTaskResult(CleanCategory category) { Category = category; }

        /// <summary>Category this result belongs to.</summary>
        public CleanCategory Category { get; private set; }
        /// <summary>Bytes actually deleted (or deletable, in an analysis).</summary>
        public long Bytes { get; internal set; }
        /// <summary>Files deleted.</summary>
        public int Files { get; internal set; }
        /// <summary>Shortcuts, registry entries or devices removed.</summary>
        public int Entries { get; internal set; }
        /// <summary>Items left in place on purpose: in use, long paths, open browsers...</summary>
        public int Skipped { get; internal set; }
        /// <summary>Items that should have been removed but could not be.</summary>
        public int Failed { get; internal set; }
        /// <summary>Why the category was skipped or only partially applied.</summary>
        public string Note { get; internal set; }
        /// <summary>Final state.</summary>
        public CleanTaskState State { get; internal set; }
        /// <summary>Per-item lines for the report file; capped.</summary>
        public IList<string> Details { get { return _details; } }

        internal void AddNote(string note)
        {
            Note = string.IsNullOrEmpty(Note) ? note : Note + " · " + note;
        }

        internal void Detail(string line)
        {
            if (_details.Count < MaxDetails) _details.Add(line);
            else if (_details.Count == MaxDetails) _details.Add("…");
        }

        internal void Add(CleanTaskResult other)
        {
            Bytes += other.Bytes; Files += other.Files; Entries += other.Entries; Skipped += other.Skipped; Failed += other.Failed;
            if (!string.IsNullOrEmpty(other.Note)) AddNote(other.Note);
            foreach (var line in other.Details) Detail(line);
        }
    }

    // A task measures when apply is false and changes the system only when it is true.
    // It fills the given result as it goes, so a cancellation still reports what was done.
    internal interface ICleanTask
    {
        CleanCategory Category { get; }
        void Run(bool apply, CleanTaskResult result, CancellationToken token);
    }

    /// <summary>Formatting shared by the window, the preview and the report.</summary>
    public static class CleanText
    {
        private static readonly CultureInfo _es = new CultureInfo("es-ES");

        /// <summary>Spanish display name of a category.</summary>
        public static string Name(CleanCategory category)
        {
            switch (category)
            {
                case CleanCategory.Temp: return "Temporales";
                case CleanCategory.Caches: return "Cachés";
                case CleanCategory.Shortcuts: return "Accesos directos";
                case CleanCategory.Registry: return "Registro de Windows";
                case CleanCategory.Usb: return "Dispositivos USB";
                case CleanCategory.RecycleBin: return "Papelera";
                case CleanCategory.Extreme: return "Limpieza extrema";
                default: return category.ToString();
            }
        }

        /// <summary>Human size, e.g. "850 MB" or "1,2 GB".</summary>
        public static string Size(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0", _es) + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0", _es) + " MB";
            if (bytes >= 1L << 10) return (bytes / (double)(1L << 10)).ToString("0", _es) + " KB";
            return bytes + " B";
        }

        /// <summary>Card line for one result: what was (or would be) removed.</summary>
        public static string Brief(CleanTaskResult r, bool estimate)
        {
            if (r == null) throw new ArgumentNullException("r");
            var parts = new List<string>();
            if (r.Bytes > 0 || r.Files > 0) parts.Add(Size(r.Bytes));
            if (r.Files > 0) parts.Add(Plural(r.Files, "archivo", "archivos"));
            if (r.Entries > 0) parts.Add(Plural(r.Entries, EntryNoun(r.Category, false), EntryNoun(r.Category, true)));
            if (parts.Count == 0) parts.Add(estimate ? "Nada que limpiar" : "Ya estaba limpio");
            if (r.Skipped > 0) parts.Add(r.Skipped + " omitidos");
            if (r.Failed > 0) parts.Add(r.Failed + " fallos");
            return string.Join(" · ", parts.ToArray());
        }

        /// <summary>One-line summary of a whole run, each kind counted separately.</summary>
        public static string Summary(IEnumerable<CleanTaskResult> results)
        {
            if (results == null) throw new ArgumentNullException("results");
            long bytes = 0; int files = 0, shortcuts = 0, registry = 0, devices = 0, skipped = 0, failed = 0, failedTasks = 0;
            foreach (var r in results)
            {
                bytes += r.Bytes; files += r.Files; skipped += r.Skipped; failed += r.Failed;
                if (r.State == CleanTaskState.Failed) failedTasks++;
                if (r.Category == CleanCategory.Shortcuts || r.Category == CleanCategory.Temp) shortcuts += r.Entries;
                else if (r.Category == CleanCategory.Registry) registry += r.Entries;
                else if (r.Category == CleanCategory.Usb) devices += r.Entries;
            }
            var parts = new List<string> { Size(bytes) + " liberados" };
            parts.Add(Plural(files, "archivo eliminado", "archivos eliminados"));
            if (shortcuts > 0) parts.Add(Plural(shortcuts, "acceso retirado", "accesos retirados"));
            if (registry > 0) parts.Add(Plural(registry, "entrada retirada", "entradas retiradas"));
            if (devices > 0) parts.Add(Plural(devices, "dispositivo retirado", "dispositivos retirados"));
            if (skipped > 0) parts.Add(skipped + " omitidos");
            if (failed > 0) parts.Add(failed + " fallos");
            if (failedTasks > 0) parts.Add(Plural(failedTasks, "categoría con error", "categorías con error"));
            return string.Join(" · ", parts.ToArray());
        }

        private static string EntryNoun(CleanCategory category, bool plural)
        {
            switch (category)
            {
                case CleanCategory.Temp:
                case CleanCategory.Shortcuts: return plural ? "accesos" : "acceso";
                case CleanCategory.Usb: return plural ? "dispositivos" : "dispositivo";
                default: return plural ? "entradas" : "entrada";
            }
        }

        private static string Plural(int n, string one, string many) { return n + " " + (n == 1 ? one : many); }
    }
}
