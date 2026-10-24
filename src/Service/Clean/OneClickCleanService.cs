using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>Runs the selected 1 clic clean categories one after another, one run at a time.</summary>
    public static class OneClickCleanService
    {
        // Recycle bin first, so shortcuts recycled later in the same run stay restorable.
        internal static readonly CleanCategory[] Order =
        {
            CleanCategory.RecycleBin, CleanCategory.Temp, CleanCategory.Caches, CleanCategory.Extreme,
            CleanCategory.Registry, CleanCategory.Usb
        };

        private static int _running;

        /// <summary>Folder for per-run text reports.</summary>
        public static string ReportFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ARCA\Informes"); }
        }

        /// <summary>Whether a run is in progress.</summary>
        public static bool IsRunning { get { return Thread.VolatileRead(ref _running) != 0; } }

        internal static ICleanTask Create(CleanCategory category)
        {
            switch (category)
            {
                case CleanCategory.Temp: return new TemporaryCleanTask();
                case CleanCategory.Caches: return new FileCleanTask(category, CleanCatalog.Caches);
                case CleanCategory.Extreme: return new ExtremeCleanTask();
                case CleanCategory.Shortcuts: return new ShortcutCleanTask();
                case CleanCategory.Registry: return new RegistryCleanTask();
                case CleanCategory.Usb: return new UsbCleanTask();
                case CleanCategory.RecycleBin: return new RecycleBinTask();
                default: throw new ArgumentOutOfRangeException("category");
            }
        }

        /// <summary>
        /// Analyses (apply = false) or cleans the selected categories. A failing category never
        /// stops the others. Returns null when another run is already in progress.
        /// </summary>
        public static IList<CleanTaskResult> Run(CleanCategory selected, bool apply, Action<CleanCategory, CleanTaskState, CleanTaskResult> progress, CancellationToken token)
        {
            return Run(Order.Where(c => (selected & c) != 0).Select(c => Create(c)).ToList(), apply, progress, token);
        }

        internal static IList<CleanTaskResult> Run(IList<ICleanTask> tasks, bool apply, Action<CleanCategory, CleanTaskState, CleanTaskResult> progress, CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return null;
            var results = new List<CleanTaskResult>();
            try
            {
                foreach (var task in tasks) Notify(progress, task.Category, CleanTaskState.Pending, null);
                foreach (var task in tasks)
                {
                    var result = new CleanTaskResult(task.Category);
                    if (token.IsCancellationRequested) result.State = CleanTaskState.Cancelled;
                    else
                    {
                        Notify(progress, task.Category, apply ? CleanTaskState.Cleaning : CleanTaskState.Analyzing, null);
                        try
                        {
                            task.Run(apply, result, token);
                            result.State = result.Failed > 0 && result.Bytes == 0 && result.Files == 0 && result.Entries == 0 ? CleanTaskState.Failed
                                : !string.IsNullOrEmpty(result.Note) && result.Bytes == 0 && result.Entries == 0 ? CleanTaskState.Skipped
                                : CleanTaskState.Completed;
                        }
                        // Counters keep what was really removed before the cancellation.
                        catch (OperationCanceledException) { result.State = CleanTaskState.Cancelled; }
                        catch (Exception e)
                        {
                            Logger.Error(e, "ARCA 1 clic clean: " + task.Category);
                            result.State = CleanTaskState.Failed;
                            result.AddNote(e.Message);
                        }
                    }
                    results.Add(result);
                    Notify(progress, task.Category, result.State, result);
                }
                if (apply) WriteReport(results);
                return results;
            }
            finally { Interlocked.Exchange(ref _running, 0); }
        }

        private static void Notify(Action<CleanCategory, CleanTaskState, CleanTaskResult> progress, CleanCategory category, CleanTaskState state, CleanTaskResult result)
        {
            if (progress == null) return;
            try { progress(category, state, result); }
            catch (Exception e) { Logger.Debug(e); }
        }

        /// <summary>Path of the most recent report, or null.</summary>
        public static string LastReport { get; private set; }

        /// <summary>Plain-text report of a run, also shown in the details panel.</summary>
        public static string Report(IList<CleanTaskResult> results)
        {
            if (results == null) throw new ArgumentNullException("results");
            var text = new StringBuilder();
            text.AppendLine("ARCA · 1 clic clean · " + DateTime.Now.ToString("g", new CultureInfo("es-ES")));
            text.AppendLine(CleanText.Summary(results));
            foreach (var r in results)
            {
                text.AppendLine();
                text.AppendLine("■ " + CleanText.Name(r.Category) + " — " + StateText(r.State) + " · " + CleanText.Brief(r, false));
                if (!string.IsNullOrEmpty(r.Note)) text.AppendLine("  " + r.Note);
                foreach (var line in r.Details) text.AppendLine("  " + line);
            }
            return text.ToString();
        }

        internal static string StateText(CleanTaskState state)
        {
            switch (state)
            {
                case CleanTaskState.Pending: return "En cola";
                case CleanTaskState.Analyzing: return "Analizando…";
                case CleanTaskState.Cleaning: return "Limpiando…";
                case CleanTaskState.Completed: return "Completado";
                case CleanTaskState.Skipped: return "Omitido";
                case CleanTaskState.Failed: return "Error";
                case CleanTaskState.Cancelled: return "Cancelado";
                default: return string.Empty;
            }
        }

        private static void WriteReport(IList<CleanTaskResult> results)
        {
            try
            {
                Directory.CreateDirectory(ReportFolder);
                var file = Path.Combine(ReportFolder, "1clic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
                File.WriteAllText(file, Report(results), Encoding.UTF8);
                LastReport = file;
                // Keep the 30 most recent reports.
                foreach (var old in Directory.GetFiles(ReportFolder, "1clic-*.txt").OrderByDescending(f => f, StringComparer.Ordinal).Skip(30))
                    File.Delete(old);
            }
            catch (Exception e) { Logger.Warning("ARCA: no se pudo guardar el informe de 1 clic clean: " + e.Message); }
        }
    }
}
