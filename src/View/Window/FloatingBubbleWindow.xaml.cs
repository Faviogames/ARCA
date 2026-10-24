using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace WinMemoryCleaner
{
    /// <summary>
    /// RamPrototype: floating bubble. Not topmost, not in the taskbar/Alt+Tab, and never takes focus.
    /// </summary>
    /// <seealso cref="Window" />
    public partial class FloatingBubbleWindow
    {
        #region Fields

        private const double ScreenMargin = 24;

        private static readonly TimeSpan _completedDuration = TimeSpan.FromSeconds(2.5);
        private static readonly TimeSpan _errorDuration = TimeSpan.FromSeconds(6);

        private bool _isDragging;
        private bool _isMouseDown;
        private Point _mouseDownScreenPoint;
        private readonly DispatcherTimer _resetTimer;
        private readonly Storyboard _spinStoryboard;
        private double _startLeft;
        private double _startTop;
        private readonly FloatingBubbleViewModel _viewModel;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FloatingBubbleWindow" /> class.
        /// </summary>
        /// <param name="viewModel">The view model.</param>
        public FloatingBubbleWindow(FloatingBubbleViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException("viewModel");

            InitializeComponent();

            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            DataContext = _viewModel;

            _resetTimer = new DispatcherTimer();
            _resetTimer.Tick += (sender, e) =>
            {
                _resetTimer.Stop();
                _viewModel.ResetToReady();
            };

            _spinStoryboard = (Storyboard)Resources["SpinStoryboard"];

            // Create the handle now to apply the non-activating styles before the first show
            new WindowInteropHelper(this).EnsureHandle();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the user asks to hide the bubble (disables the feature).
        /// </summary>
        public event Action HideRequested;

        /// <summary>
        /// Occurs when the user asks to open the main window.
        /// </summary>
        public event Action OpenRequested;

        #endregion

        #region Methods

        /// <summary>Whether the shell has hidden or minimized the desktop surface.</summary>
        internal bool NeedsDesktopRestore
        {
            get
            {
                var handle = new WindowInteropHelper(this).Handle;
                return !IsVisible || WindowState == WindowState.Minimized || handle == IntPtr.Zero || !NativeMethods.IsWindowVisible(handle);
            }
        }

        private double GetDeviceScale()
        {
            var source = PresentationSource.FromVisual(this);

            return source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M11 : 1;
        }

        /// <summary>
        /// Hides the bubble and closes its menu.
        /// </summary>
        public void HideBubble()
        {
            if (ContextMenu != null)
                ContextMenu.IsOpen = false;

            CancelMouseInteraction();

            _spinStoryboard.Stop(this);

            Hide();
        }

        /// <summary>
        /// Moves the bubble back into a visible work area if its saved position is off-screen
        /// (e.g. a monitor was disconnected or the resolution changed).
        /// </summary>
        public void EnsureVisiblePosition()
        {
            var scale = GetDeviceScale();
            var left = Settings.FloatingBubbleLeft;
            var top = Settings.FloatingBubbleTop;

            var screen = double.IsNaN(left) || double.IsNaN(top)
                ? null
                : Screen.AllScreens.FirstOrDefault(s => s.WorkingArea.Contains((int)((left + Width / 2) * scale), (int)((top + Height / 2) * scale)));

            if (screen == null)
            {
                // Default: bottom-right corner of the primary work area
                var workArea = SystemParameters.WorkArea;

                Left = workArea.Right - Width - ScreenMargin;
                Top = workArea.Bottom - Height - ScreenMargin;
                return;
            }

            // Keep the whole bubble inside that screen
            var area = screen.WorkingArea;

            Left = Math.Max(area.Left / scale, Math.Min(left, area.Right / scale - Width));
            Top = Math.Max(area.Top / scale, Math.Min(top, area.Bottom / scale - Height));
        }

        private void CancelMouseInteraction()
        {
            _isDragging = false;
            _isMouseDown = false;

            if (IsMouseCaptured)
                ReleaseMouseCapture();
        }

        private void OnExitMenuItemClick(object sender, RoutedEventArgs e)
        {
            App.Shutdown();
        }

        private void OnHideMenuItemClick(object sender, RoutedEventArgs e)
        {
            var handler = HideRequested;

            if (handler != null)
                handler();
        }

        /// <inheritdoc/>
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);

            _isDragging = false;
            _isMouseDown = false;
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e == null)
                return;

            base.OnMouseLeftButtonDown(e);

            _isMouseDown = true;
            _isDragging = false;
            _mouseDownScreenPoint = PointToScreen(e.GetPosition(this));
            _startLeft = Left;
            _startTop = Top;

            CaptureMouse();

            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (e == null)
                return;

            base.OnMouseLeftButtonUp(e);

            if (!_isMouseDown)
                return;

            var wasDragging = _isDragging;

            CancelMouseInteraction();

            if (wasDragging)
            {
                // Drag never cleans; persist the new position
                Settings.FloatingBubbleLeft = Left;
                Settings.FloatingBubbleTop = Top;
                EnsureVisiblePosition();
                Settings.FloatingBubbleLeft = Left;
                Settings.FloatingBubbleTop = Top;
                Settings.Save();
            }
            else
                _viewModel.Clean();

            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e == null)
                return;

            base.OnMouseMove(e);

            if (!_isMouseDown || e.LeftButton != MouseButtonState.Pressed)
                return;

            var scale = GetDeviceScale();
            var point = PointToScreen(e.GetPosition(this));
            var deltaX = (point.X - _mouseDownScreenPoint.X) / scale;
            var deltaY = (point.Y - _mouseDownScreenPoint.Y) / scale;

            // Threshold in DIPs, so it follows the DPI scale
            if (!_isDragging && (Math.Abs(deltaX) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(deltaY) >= SystemParameters.MinimumVerticalDragDistance))
                _isDragging = true;

            if (_isDragging)
            {
                Left = _startLeft + deltaX;
                Top = _startTop + deltaY;
            }
        }

        private void OnOpenMenuItemClick(object sender, RoutedEventArgs e)
        {
            var handler = OpenRequested;

            if (handler != null)
                handler();
        }

        /// <inheritdoc/>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);

            // Tool window: hidden from the taskbar and Alt+Tab. No-activate: clicks never steal the focus
            var exStyle = NativeMethods.GetWindowLong(helper.Handle, Constants.Windows.Window.GwlExStyle);
            if (NativeMethods.SetWindowLong(helper.Handle, Constants.Windows.Window.GwlExStyle, exStyle | Constants.Windows.Window.WsExToolWindow | Constants.Windows.Window.WsExNoActivate) == 0)
                Logger.Warning("RamPrototype: the floating bubble window styles could not be applied.");

            var source = HwndSource.FromHwnd(helper.Handle);

            if (source != null)
                source.AddHook(WndProc);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "State")
                return;

            var state = _viewModel.State;

            Spinner.Visibility = state == Enums.FloatingBubble.State.Cleaning ? Visibility.Visible : Visibility.Collapsed;
            CompletedBadge.Visibility = state == Enums.FloatingBubble.State.Completed ? Visibility.Visible : Visibility.Collapsed;
            ErrorBadge.Visibility = state == Enums.FloatingBubble.State.Error ? Visibility.Visible : Visibility.Collapsed;

            if (state == Enums.FloatingBubble.State.Cleaning && SystemParameters.ClientAreaAnimation)
                _spinStoryboard.Begin(this, true);
            else
                _spinStoryboard.Stop(this);

            _resetTimer.Stop();

            if (state == Enums.FloatingBubble.State.Completed || state == Enums.FloatingBubble.State.Error)
            {
                _resetTimer.Interval = state == Enums.FloatingBubble.State.Completed ? _completedDuration : _errorDuration;
                _resetTimer.Start();
            }
        }

        /// <summary>
        /// Shows the bubble without activating it.
        /// </summary>
        public void ShowBubble()
        {
            EnsureVisiblePosition();
            WindowState = WindowState.Normal;
            Show();
            // WPF's IsVisible can remain true when the shell hides a native HWND.
            // SW_SHOWNOACTIVATE repairs that case without taking focus.
            NativeMethods.ShowWindowAsync(new WindowInteropHelper(this).Handle, 4);
            if (_viewModel.State == Enums.FloatingBubble.State.Cleaning && SystemParameters.ClientAreaAnimation)
                _spinStoryboard.Begin(this, true);
        }

        /// <inheritdoc/>
        protected override void OnClosed(EventArgs e)
        {
            _resetTimer.Stop();
            _spinStoryboard.Stop(this);
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            var handle = new WindowInteropHelper(this).Handle;
            var source = handle != IntPtr.Zero ? HwndSource.FromHwnd(handle) : null;
            if (source != null)
                source.RemoveHook(WndProc);
            base.OnClosed(e);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Constants.Windows.Window.WmMouseActivate)
            {
                handled = true;
                return new IntPtr(Constants.Windows.Window.MaNoActivate);
            }

            return IntPtr.Zero;
        }

        #endregion
    }
}
