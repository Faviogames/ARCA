using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace WinMemoryCleaner
{
    /// <summary>One category tile of the 1 Clic Clean section.</summary>
    public sealed class CleanCard : ObservableObject
    {
        private readonly Action _changed;
        private bool _isChecked;
        private CleanTaskState _state;
        private string _status;

        internal CleanCard(CleanCategory category, string subtitle, string icon, bool isChecked, Action changed)
        {
            Category = category; Title = CleanText.Name(category); Subtitle = subtitle; Icon = icon;
            _isChecked = isChecked; _changed = changed;
        }

        /// <summary>Category.</summary>
        public CleanCategory Category { get; private set; }
        /// <summary>Card title.</summary>
        public string Title { get; private set; }
        /// <summary>Resting description.</summary>
        public string Subtitle { get; private set; }

        /// <summary>Contextual help shown when hovering a category.</summary>
        public string Help
        {
            get
            {
                switch (Category)
                {
                    case CleanCategory.Temp: return "Elimina temporales antiguos del usuario y Windows, informes de errores y volcados antiguos. Los accesos directos rotos se envían a la papelera.";
                    case CleanCategory.Caches: return "Las aplicaciones deben estar cerradas.";
                    case CleanCategory.Registry: return "Limpia referencias a programas que ya no existen. Guarda un respaldo antes de modificar el registro.";
                    case CleanCategory.Usb: return "Ideal para solucionar problemas con mandos y periféricos.";
                    case CleanCategory.RecycleBin: return null;
                    default: return Subtitle;
                }
            }
        }
        /// <summary>Stroke icon geometry.</summary>
        public string Icon { get; private set; }

        /// <summary>Embedded PNG from the approved icon family.</summary>
        public string IconImage
        {
            get
            {
                string file;
                switch (Category)
                {
                    case CleanCategory.Temp: file = "temporales"; break;
                    case CleanCategory.Caches: file = "caches"; break;
                    case CleanCategory.Registry: file = "registro"; break;
                    case CleanCategory.Usb: file = "usb"; break;
                    case CleanCategory.RecycleBin: file = "papelera"; break;
                    default: return null;
                }
                return "pack://application:,,,/WinMemoryCleaner;component/Resources/Images/Clean/" + file + ".png";
            }
        }

        /// <summary>Selected for the next run; persisted.</summary>
        public bool IsChecked
        {
            get { return _isChecked; }
            set { if (_isChecked == value) return; _isChecked = value; RaisePropertyChanged(); _changed(); }
        }

        /// <summary>State in the current or last run.</summary>
        public CleanTaskState State
        {
            get { return _state; }
            internal set { _state = value; RaisePropertyChanged(); RaisePropertyChanged(() => Line); }
        }

        /// <summary>Second line: description at rest, progress or result otherwise.</summary>
        public string Line { get { return _state == CleanTaskState.Idle || string.IsNullOrEmpty(_status) ? Subtitle : _status; } }

        internal void Show(CleanTaskState state, string status)
        {
            _status = status;
            State = state;
        }
    }

    /// <summary>A preview row: what one category would remove.</summary>
    public sealed class CleanPreviewRow
    {
        /// <summary>Category name.</summary>
        public string Name { get; internal set; }
        /// <summary>Estimate.</summary>
        public string Text { get; internal set; }
    }

    /// <summary>State and commands of the 1 Clic Clean section. Independent from the RAM cleaning and the bubble.</summary>
    public sealed class OneClickCleanViewModel : ObservableObject, IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource _cancel;
        private bool _isRunning, _isCancelling, _isPreviewOpen, _isAnalyzing;
        private string _status = "Listo para comenzar", _report, _extremeStatus;
        private CleanTaskState _extremeState;

        /// <summary>Creates the section state from the saved preferences.</summary>
        public OneClickCleanViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            var saved = (CleanCategory)Settings.OneClickCategories;
            Action changed = SelectionChanged;
            Cards = new ObservableCollection<CleanCard>
            {
                new CleanCard(CleanCategory.Temp, "Temporales, informes de errores y accesos directos rotos", "M6,2 H18 L24,8 V30 H6 Z M18,2 V8 H24 M10,15 H20 M10,20 H20 M10,25 H16", (saved & CleanCategory.Temp) != 0, changed),
                new CleanCard(CleanCategory.Caches, "Caché de navegadores y Discord", "M4,7 A10,4 0 1 0 24,7 A10,4 0 1 0 4,7 M4,7 V25 A10,4 0 0 0 24,25 V7 M4,13 A10,4 0 0 0 24,13 M4,19 A10,4 0 0 0 24,19", (saved & CleanCategory.Caches) != 0, changed),
                new CleanCard(CleanCategory.Registry, "Se guarda un respaldo", "M14,3 L24,8 V20 L14,25 L4,20 V8 Z M4,8 L14,13 L24,8 M14,13 V25", (saved & CleanCategory.Registry) != 0, changed),
                new CleanCard(CleanCategory.Usb, "Limpia dispositivos desconectados obsoletos", "M10,2 H18 V9 H10 Z M8,9 H20 V21 A6,6 0 0 1 8,21 Z M12.5,4.5 V6.5 M15.5,4.5 V6.5", (saved & CleanCategory.Usb) != 0, changed),
                new CleanCard(CleanCategory.RecycleBin, "Se vacía la papelera", "M4,7 H24 M10,7 V4 H18 V7 M6,7 L8,28 H20 L22,7 M12,12 V23 M16,12 V23", (saved & CleanCategory.RecycleBin) != 0, changed)
            };
            Extreme = (saved & CleanCategory.Extreme) != 0;
            PreviewRows = new ObservableCollection<CleanPreviewRow>();
            StartCommand = new RelayCommand(StartOrCancel, () => !_isCancelling && !_isPreviewOpen && !_isAnalyzing);
            ConfirmPreviewCommand = new RelayCommand(() => { ClosePreview(); Settings.OneClickPreview = false; Save(); RaisePropertyChanged(() => AlwaysPreview); Begin(true); });
            ClosePreviewCommand = new RelayCommand(() => { ClosePreview(); Status = "Vista previa cerrada · no se modificó nada"; ResetCards(); });
            OpenReportsCommand = new RelayCommand(OpenReports);
            UndoRegistryCommand = new RelayCommand(UndoRegistry, () => !_isRunning);
        }

        /// <summary>The five category tiles; shortcuts are included in Temp.</summary>
        public ObservableCollection<CleanCard> Cards { get; private set; }
        /// <summary>Estimates shown before the first run.</summary>
        public ObservableCollection<CleanPreviewRow> PreviewRows { get; private set; }
        /// <summary>Starts a run, or cancels the one in progress.</summary>
        public ICommand StartCommand { get; private set; }
        /// <summary>Accepts the preview and cleans.</summary>
        public ICommand ConfirmPreviewCommand { get; private set; }
        /// <summary>Closes the preview without changes.</summary>
        public ICommand ClosePreviewCommand { get; private set; }
        /// <summary>Opens the reports folder.</summary>
        public ICommand OpenReportsCommand { get; private set; }
        /// <summary>Re-imports the last registry backup.</summary>
        public ICommand UndoRegistryCommand { get; private set; }

        private bool _extreme;

        /// <summary>Opt-in deep clean; persisted with the categories.</summary>
        public bool Extreme
        {
            get { return _extreme; }
            set { if (_extreme == value) return; _extreme = value; RaisePropertyChanged(); SelectionChanged(); }
        }

        /// <summary>Shows the analysis before cleaning. On until the first confirmed run.</summary>
        public bool AlwaysPreview
        {
            get { return Settings.OneClickPreview; }
            set { Settings.OneClickPreview = value; Save(); RaisePropertyChanged(); }
        }

        /// <summary>Header chip.</summary>
        public string ActiveText
        {
            get
            {
                var n = Cards.Count(c => c.IsChecked);
                var text = n == 1 ? "1 categoría activada" : n + " categorías activadas";
                return Extreme ? text + " + extrema" : text;
            }
        }

        /// <summary>Line under the main button.</summary>
        public string Status
        {
            get { return _status; }
            private set { _status = value; RaisePropertyChanged(); }
        }

        /// <summary>Extreme strip line during and after a run.</summary>
        public string ExtremeStatus
        {
            get { return _extremeStatus; }
            private set { _extremeStatus = value; RaisePropertyChanged(); }
        }

        /// <summary>Extreme state, for its strip colour.</summary>
        public CleanTaskState ExtremeState
        {
            get { return _extremeState; }
            private set { _extremeState = value; RaisePropertyChanged(); }
        }

        /// <summary>Text of the last run's report.</summary>
        public string Report
        {
            get { return _report; }
            private set { _report = value; RaisePropertyChanged(); RaisePropertyChanged(() => HasReport); }
        }

        /// <summary>Whether a report can be shown.</summary>
        public bool HasReport { get { return !string.IsNullOrEmpty(_report); } }

        /// <summary>A run or an analysis is in progress.</summary>
        public bool IsRunning
        {
            get { return _isRunning; }
            private set { _isRunning = value; RaisePropertyChanged(); RaisePropertyChanged(() => ButtonText); RaisePropertyChanged(() => IsIdle); CommandManager.InvalidateRequerySuggested(); }
        }

        /// <summary>Selections can change.</summary>
        public bool IsIdle { get { return !_isRunning && !_isPreviewOpen; } }

        /// <summary>Main button caption.</summary>
        public string ButtonText
        {
            get { return _isCancelling ? "Cancelando…" : _isRunning ? (_isAnalyzing ? "Analizando…" : "Cancelar") : "Limpiar PC"; }
        }

        /// <summary>The preview overlay is visible.</summary>
        public bool IsPreviewOpen
        {
            get { return _isPreviewOpen; }
            private set { _isPreviewOpen = value; RaisePropertyChanged(); RaisePropertyChanged(() => IsIdle); CommandManager.InvalidateRequerySuggested(); }
        }

        /// <summary>Total line of the preview.</summary>
        public string PreviewTotal { get; private set; }

        private CleanCategory Selected
        {
            get
            {
                var flags = Cards.Where(c => c.IsChecked).Aggregate(CleanCategory.None, (all, c) => all | c.Category);
                return Extreme ? flags | CleanCategory.Extreme : flags;
            }
        }

        private void SelectionChanged()
        {
            Settings.OneClickCategories = (int)Selected;
            Save();
            RaisePropertyChanged(() => ActiveText);
        }

        private static void Save()
        {
            try { Settings.Save(); }
            catch (Exception e) { Logger.Warning("ARCA: no se pudieron guardar las preferencias de 1 Clic Clean: " + e.Message); }
        }

        private void StartOrCancel()
        {
            if (_isRunning)
            {
                if (_cancel != null) _cancel.Cancel();
                _isCancelling = true;
                RaisePropertyChanged(() => ButtonText);
                CommandManager.InvalidateRequerySuggested();
                Status = "Cancelando · se termina el elemento en curso…";
                return;
            }
            if (Selected == CleanCategory.None) { Status = "Elige al menos una categoría"; return; }
            Begin(!Settings.OneClickPreview);
        }

        private void Begin(bool apply)
        {
            var selected = Selected;
            ResetCards();
            if (_cancel != null) _cancel.Dispose();
            _cancel = new CancellationTokenSource();
            var token = _cancel.Token;
            _isAnalyzing = !apply;
            IsRunning = true;
            Status = apply ? "Limpieza en curso…" : "Analizando qué se puede limpiar · aún no se modifica nada";
            Action<CleanCategory, CleanTaskState, CleanTaskResult> progress = (c, s, r) => _dispatcher.BeginInvoke((Action)(() => Show(c, s, r, !apply)));
            Task.Factory.StartNew(() =>
            {
                try { return OneClickCleanService.Run(selected, apply, progress, token); }
                catch (OperationCanceledException)
                {
                    return (IList<CleanTaskResult>)OneClickCleanService.Order.Where(c => (selected & c) != 0)
                        .Select(c => new CleanTaskResult(c) { State = CleanTaskState.Cancelled, Note = "Permiso no concedido o limpieza cancelada" }).ToList();
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    return (IList<CleanTaskResult>)OneClickCleanService.Order.Where(c => (selected & c) != 0)
                        .Select(c => new CleanTaskResult(c) { State = CleanTaskState.Failed, Note = e.Message }).ToList();
                }
            },
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t => _dispatcher.BeginInvoke((Action)(() => Finished(t.IsFaulted || t.IsCanceled ? null : t.Result, apply))), TaskScheduler.Default);
        }

        private void Show(CleanCategory category, CleanTaskState state, CleanTaskResult result, bool estimate)
        {
            string text;
            if (result == null) text = OneClickCleanService.StateText(state);
            else if (!ElevatedOperations.IsFinalResult(state) && !string.IsNullOrEmpty(result.ProgressMessage)) text = result.ProgressMessage;
            else if (state == CleanTaskState.Skipped || state == CleanTaskState.Failed) text = OneClickCleanService.StateText(state) + (string.IsNullOrEmpty(result.Note) ? string.Empty : " · " + result.Note);
            else if (state == CleanTaskState.Cancelled && result.Bytes == 0 && result.Entries == 0) text = "Cancelado";
            else text = (estimate ? "Se puede liberar " : string.Empty) + CleanText.Brief(result, estimate) + (string.IsNullOrEmpty(result.Note) ? string.Empty : " · " + result.Note);

            if (category == CleanCategory.Extreme) { ExtremeState = state; ExtremeStatus = text; return; }
            var card = Cards.FirstOrDefault(c => c.Category == category);
            if (card != null) card.Show(state, text);
            if (category == CleanCategory.Usb && !ElevatedOperations.IsFinalResult(state) && result != null && !_isCancelling)
                Status = text;
        }

        private void Finished(IList<CleanTaskResult> results, bool apply)
        {
            var cancelled = _isCancelling;
            _isCancelling = false;
            _isAnalyzing = false;
            IsRunning = false;
            if (results == null) { Status = "Ya hay una limpieza en curso, o no se pudo iniciar"; return; }
            if (!apply)
            {
                if (cancelled) { Status = "Análisis cancelado · no se modificó nada"; return; }
                PreviewRows.Clear();
                foreach (var r in results)
                    PreviewRows.Add(new CleanPreviewRow
                    {
                        Name = CleanText.Name(r.Category),
                        Text = (r.State == CleanTaskState.Failed || r.State == CleanTaskState.Skipped ? OneClickCleanService.StateText(r.State) : CleanText.Brief(r, true))
                            + (string.IsNullOrEmpty(r.Note) ? string.Empty : " · " + r.Note)
                    });
                PreviewTotal = "Total estimado: " + CleanText.Size(results.Sum(r => r.Bytes)) + ". Los archivos en uso o recientes se conservarán.";
                RaisePropertyChanged(() => PreviewTotal);
                Status = "Revisa la vista previa";
                IsPreviewOpen = true;
                return;
            }
            Report = OneClickCleanService.Report(results);
            cancelled = cancelled || results.Any(r => r.State == CleanTaskState.Cancelled);
            var summary = results.Count == 1 && results[0].Category == CleanCategory.Usb ? CleanText.Brief(results[0], false) : CleanText.Summary(results);
            Status = (cancelled ? "Cancelado · " : results.All(r => r.State == CleanTaskState.Failed) ? "No se pudo completar · " : "Completado · ") + summary;
        }

        private void ClosePreview() { IsPreviewOpen = false; }

        /// <summary>Cancels a run in progress when the window goes away.</summary>
        public void Dispose()
        {
            if (_cancel == null) return;
            _cancel.Cancel();
            _cancel.Dispose();
            _cancel = null;
        }

        private void ResetCards()
        {
            foreach (var card in Cards) card.Show(CleanTaskState.Idle, null);
            ExtremeState = CleanTaskState.Idle;
            ExtremeStatus = null;
        }

        private void OpenReports()
        {
            try
            {
                Directory.CreateDirectory(OneClickCleanService.ReportFolder);
                var last = OneClickCleanService.LastReport;
                if (last != null && File.Exists(last)) Process.Start("explorer.exe", "/select,\"" + last + "\"");
                else Process.Start("explorer.exe", "\"" + OneClickCleanService.ReportFolder + "\"");
            }
            catch (Exception e) { Status = "No se pudo abrir la carpeta de informes: " + e.Message; }
        }

        private void UndoRegistry()
        {
            if (!Directory.Exists(RegistryCleanTask.BackupRoot) || !Directory.EnumerateFiles(RegistryCleanTask.BackupRoot, "*.reg", SearchOption.AllDirectories).Any())
            { Status = "No hay cambios de registro que deshacer"; return; }
            IsRunning = true;
            Status = "Esperando autorización para restaurar el registro…";
            Task.Factory.StartNew(() => ElevatedOperations.IsAdministrator ? RegistryCleanTask.UndoLast() : ElevatedOperations.UndoRegistry(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t => _dispatcher.BeginInvoke((Action)(() =>
                {
                    IsRunning = false;
                    if (t.IsFaulted || t.IsCanceled) { Status = "Restauración cancelada o no disponible"; return; }
                    var restored = t.Result;
                    Status = restored < 0 ? "No hay cambios de registro que deshacer"
                        : restored == 0 ? "No se pudo restaurar el último respaldo del registro"
                        : "Registro restaurado · " + restored + (restored == 1 ? " respaldo importado" : " respaldos importados");
                })), TaskScheduler.Default);
        }
    }
}



