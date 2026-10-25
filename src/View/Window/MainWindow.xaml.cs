using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Input;

namespace WinMemoryCleaner
{
    /// <summary>RamPrototype's manual cleaning dashboard.</summary>
    public partial class MainWindow : View
    {
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _dashboardTimer;
        private DateTime _lastSample = DateTime.MinValue;
        private int _sampling;
        private bool _closed;

        /// <summary>Eight memory chips; the first three also show the largest applications.</summary>
        public ObservableCollection<RamChip> Chips { get; private set; }

        /// <summary>State of the 1 Clic Clean section.</summary>
        public OneClickCleanViewModel OneClick { get; private set; }

        /// <summary>Creates the dashboard.</summary>
        public MainWindow()
        {
            Chips = new ObservableCollection<RamChip>();
            for (var i = 0; i < 8; i++) Chips.Add(new RamChip());
            OneClick = new OneClickCleanViewModel();
            InitializeComponent();
            UseLanguageForNumbers();
            _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _dashboardTimer.Tick += (sender, e) => RefreshDashboard();
            IsVisibleChanged += OnDashboardVisibilityChanged;
            StateChanged += (sender, e) => UpdateDashboardMonitoring();
            _viewModel = DataContext as MainViewModel;
            if (_viewModel == null) return;
            _viewModel.PropertyChanged += OnModelChanged;
            _viewModel.OnOptimizationFinished += OnOptimizationFinished;
            _viewModel.OnAddProcessToExclusionListCommandCompleted += ScrollExclusions;
            Closed += OnDashboardClosed;
            Localizer.StaticPropertyChanged += OnLanguageChanged;
            Loaded += (sender, e) =>
            {
                var area = SystemParameters.WorkArea;
                var scale = Math.Min(1, Math.Min((area.Width - 24) / Width, (area.Height - 24) / Height));
                if (scale < 1) { Width *= scale; Height *= scale; Left = area.Left + (area.Width - Width) / 2; Top = area.Top + (area.Height - Height) / 2; }
                UpdateDashboardMonitoring();
                Optimize.Focus();
            };
        }

        private void OnDashboardVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { UpdateDashboardMonitoring(); }

        private void UpdateDashboardMonitoring()
        {
            if (_dashboardTimer == null) return;
            if (!_closed && IsVisible && WindowState != WindowState.Minimized)
            {
                RefreshDashboard();
                _dashboardTimer.Start();
            }
            else _dashboardTimer.Stop();
        }

        private void RefreshDashboard()
        {
            if (_closed || _viewModel == null) return;
            var percentage = _viewModel.Computer.Memory.Physical.Used.Percentage;
            for (var i = 0; i < Chips.Count; i++) Chips[i].IsLit = percentage > i * 12.5;
            if (DateTime.UtcNow - _lastSample < TimeSpan.FromSeconds(5) || Interlocked.CompareExchange(ref _sampling, 1, 0) != 0) return;
            _lastSample = DateTime.UtcNow;
            ThreadPool.QueueUserWorkItem(state =>
            {
                IList<AppMemorySample> top = null;
                try { top = TopMemoryApps.Capture(); }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                finally { Interlocked.Exchange(ref _sampling, 0); }
                if (Dispatcher.HasShutdownStarted || _closed) return;
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (_closed || !IsVisible || WindowState == WindowState.Minimized) return;
                    for (var i = 0; i < 3; i++) Chips[i].Update(top != null && i < top.Count ? top[i] : null, i + 1);
                    TopAppsStatus.Text = Tr.Get(top == null ? "TOP 3 · NO DISPONIBLE" : "TOP 3 · MAYOR CONSUMO");
                    TopAppsStatus.ToolTip = Tr.Get("RAM residente aproximada; procesos agrupados por ejecutable en esta sesión. Las páginas compartidas pueden contarse varias veces. Actualización cada 5 s.");
                }), DispatcherPriority.Background);
            });
        }

        private void OnAdvancedClose(object sender, RoutedEventArgs e) { AdvancedToggle.IsChecked = false; }
        private void OnCleanDetails(object sender, RoutedEventArgs e) { CleanDetailsPanel.Visibility = Visibility.Visible; }
        private void OnCleanDetailsClose(object sender, RoutedEventArgs e) { CleanDetailsPanel.Visibility = Visibility.Collapsed; }

        private void OnTitleDrag(object sender, MouseButtonEventArgs e)
        {
            // Buttons handle their own press, so dragging the title area only moves the window.
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void OnCloseButtonClick(object sender, RoutedEventArgs e) { Hide(); }
        private void OnExitButtonClick(object sender, RoutedEventArgs e) { App.Shutdown(); }
        private void OnProcessesDropDownOpened(object sender, EventArgs e) { _viewModel.RaisePropertyChanged(() => _viewModel.Processes); }
        private void ScrollExclusions() { ProcessExclusionScrollViewer.ScrollToEnd(); }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "IsOptimizationRunning") return;
            Dispatcher.BeginInvoke((Action)(() =>
            {
                CleanButtonLabel.Text = Tr.Get(_viewModel.IsOptimizationRunning ? "Limpiando memoria…" : "Limpiar memoria");
                if (_viewModel.IsOptimizationRunning) CleaningStatus.Text = Tr.Get("Limpieza en curso…");
            }));
        }

        // XAML StringFormat (memory totals) formats with the element language, not the thread culture.
        private void UseLanguageForNumbers()
        {
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(Tr.Culture.IetfLanguageTag);
        }

        // Texts set from code do not follow the language binding; reset them and resample the top apps.
        private void OnLanguageChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (_closed || _viewModel == null) return;
                UseLanguageForNumbers();
                CleanButtonLabel.Text = Tr.Get(_viewModel.IsOptimizationRunning ? "Limpiando memoria…" : "Limpiar memoria");
                CleaningStatus.Text = Tr.Get(_viewModel.IsOptimizationRunning ? "Limpieza en curso…" : "Limpieza manual · Tú decides cuándo");
                _lastSample = DateTime.MinValue;
            }));
        }

        private void OnOptimizationFinished(OptimizationResult result)
        {
            // Never restore or focus the main window after cleaning from the bubble.
            var formatter = new FloatingBubbleViewModel(_viewModel.OptimizeCommand);
            formatter.OnOptimizationFinished(result);
            CleaningStatus.Text = result.Succeeded ? Tr.Get("Limpieza completada · ") + formatter.ResultText : Tr.Get("Limpieza incompleta. Consulta el registro de la aplicación.");
            if (!string.IsNullOrEmpty(_viewModel.BrowserCloseSummary))
                CleaningStatus.Text += Environment.NewLine + _viewModel.BrowserCloseSummary;
        }

        private void OnDashboardClosed(object sender, EventArgs e)
        {
            _closed = true;
            Localizer.StaticPropertyChanged -= OnLanguageChanged;
            OneClick.Dispose();
            _dashboardTimer.Stop();
            IsVisibleChanged -= OnDashboardVisibilityChanged;
            if (_viewModel == null) return;
            _viewModel.PropertyChanged -= OnModelChanged;
            _viewModel.OnOptimizationFinished -= OnOptimizationFinished;
            _viewModel.OnAddProcessToExclusionListCommandCompleted -= ScrollExclusions;
        }
    }
}
