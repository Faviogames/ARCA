using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Windows.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// RamPrototype: decides whether the floating bubble may be shown.
    /// The bubble is only allowed while the desktop itself is the foreground window.
    /// </summary>
    /// <seealso cref="IDisposable" />
    [SuppressMessage("Usage", "CA2216:Disposable types should declare finalizer", Justification = "UnhookWinEvent must run on the UI thread that installed the hook; the OS releases it when that thread exits")]
    public sealed class DesktopVisibilityController : IDisposable
    {
        #region Fields

        private static readonly TimeSpan _showDelay = TimeSpan.FromMilliseconds(200);

        private readonly Dispatcher _dispatcher;
        private bool _disposed;
        private IntPtr _hook;
        private bool _isEnabled;
        private bool _isMainWindowShown;
        private bool _isSessionLocked;
        private readonly int _ownProcessId;
        private readonly DispatcherTimer _showTimer;
        private readonly DispatcherTimer _reconcileTimer;
        private readonly NativeMethods.WinEventDelegate _winEventProc; // Kept alive while the hook exists

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DesktopVisibilityController" /> class.
        /// Must be created on the UI thread (the hook is delivered through its message loop).
        /// </summary>
        /// <param name="dispatcher">The UI dispatcher.</param>
        public DesktopVisibilityController(Dispatcher dispatcher)
        {
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            _dispatcher = dispatcher;

            using (var process = Process.GetCurrentProcess())
                _ownProcessId = process.Id;

            _showTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher) { Interval = _showDelay };
            _showTimer.Tick += OnShowTimerTick;

            // Shell commands such as Win+D can hide/minimize a window without changing
            // our logical visibility. Reconcile only while the main dashboard is hidden.
            _reconcileTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            _reconcileTimer.Tick += OnReconcileTimerTick;

            _winEventProc = OnWinEvent;
            _hook = NativeMethods.SetWinEventHook
            (
                Constants.Windows.WinEvent.SystemForeground,
                Constants.Windows.WinEvent.SystemForeground,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                Constants.Windows.WinEvent.OutOfContext
            );

            if (_hook == IntPtr.Zero)
                Logger.Warning("RamPrototype: foreground hook could not be installed. The floating bubble will stay hidden.");

            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the bubble must be shown (<c>true</c>) or hidden (<c>false</c>). Raised on the UI thread.
        /// </summary>
        public event Action<bool> VisibilityChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the bubble feature is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                Evaluate();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the main window is open (not minimized nor hidden).
        /// </summary>
        public bool IsMainWindowShown
        {
            get { return _isMainWindowShown; }
            set
            {
                _isMainWindowShown = value;
                Evaluate();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the bubble is currently allowed to be visible.
        /// </summary>
        public bool IsVisible { get; private set; }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases the foreground hook, timer and system event subscriptions.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
            }
            catch
            {
                // ignored
            }

            _showTimer.Stop();
            _showTimer.Tick -= OnShowTimerTick;
            _reconcileTimer.Stop();
            _reconcileTimer.Tick -= OnReconcileTimerTick;

            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }

            SetVisible(false);
            VisibilityChanged = null;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Classifies a foreground window. Class names are hints confirmed by the shell process identity.
        /// </summary>
        /// <param name="className">Class name of the foreground root window.</param>
        /// <param name="processId">Process that owns the foreground window.</param>
        /// <param name="shellProcessId">Process that owns the shell desktop window (0 if there is no shell).</param>
        /// <param name="ownProcessId">This app process.</param>
        /// <param name="hasDefView">Whether the window hosts the desktop icon view.</param>
        /// <returns>The foreground kind.</returns>
        public static Enums.Desktop.Foreground Classify(string className, int processId, int shellProcessId, int ownProcessId, bool hasDefView)
        {
            if (processId == 0 || string.IsNullOrEmpty(className))
                return Enums.Desktop.Foreground.Unknown;

            if (processId == ownProcessId)
                return Enums.Desktop.Foreground.OwnProcess;

            // Without a shell (e.g. Explorer restarting) the desktop cannot be confirmed
            if (shellProcessId == 0)
                return Enums.Desktop.Foreground.Unknown;

            if (processId == shellProcessId)
            {
                if (className.Equals(Constants.Windows.Desktop.ProgmanClass, StringComparison.Ordinal))
                    return Enums.Desktop.Foreground.Desktop;

                // After Win+D the desktop icon view may live in a WorkerW window
                if (className.Equals(Constants.Windows.Desktop.WorkerWClass, StringComparison.Ordinal) && hasDefView)
                    return Enums.Desktop.Foreground.Desktop;
            }

            // Includes Explorer folders, taskbar, Alt+Tab, Task View and Start
            return Enums.Desktop.Foreground.Other;
        }

        /// <summary>
        /// Decides whether the bubble may be visible.
        /// </summary>
        /// <param name="isEnabled">Whether the feature is enabled.</param>
        /// <param name="isMainWindowShown">Whether the main window is open.</param>
        /// <param name="isSessionLocked">Whether the session is locked or disconnected.</param>
        /// <param name="foreground">The foreground kind.</param>
        /// <param name="isCurrentlyVisible">Whether the bubble is visible right now.</param>
        /// <returns><c>true</c> if the bubble may be visible.</returns>
        public static bool ShouldShow(bool isEnabled, bool isMainWindowShown, bool isSessionLocked, Enums.Desktop.Foreground foreground, bool isCurrentlyVisible)
        {
            if (!isEnabled || isMainWindowShown || isSessionLocked)
                return false;

            switch (foreground)
            {
                case Enums.Desktop.Foreground.Desktop:
                    return true;

                case Enums.Desktop.Foreground.OwnProcess:
                    // Own popups (e.g. the bubble menu) keep the current state
                    return isCurrentlyVisible;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Re-evaluates the visibility. Hides immediately; shows only after a short, re-confirmed delay.
        /// </summary>
        public void Evaluate()
        {
            if (_disposed)
                return;

            _reconcileTimer.IsEnabled = _isEnabled && !_isMainWindowShown && !_isSessionLocked && _hook != IntPtr.Zero;

            if (_hook == IntPtr.Zero)
            {
                SetVisible(false);
                return;
            }

            if (ShouldShow(_isEnabled, _isMainWindowShown, _isSessionLocked, GetForeground(), IsVisible))
            {
                if (!IsVisible)
                {
                    // Restart the delay so stale show requests (e.g. during Alt+Tab) are dropped
                    _showTimer.Stop();
                    _showTimer.Start();
                }
                else
                {
                    var handler = VisibilityChanged;
                    if (handler != null)
                        handler(true); // Also repair shell-driven minimization of the bubble.
                }
            }
            else
            {
                _showTimer.Stop();
                SetVisible(false);
            }
        }

        private Enums.Desktop.Foreground GetForeground()
        {
            try
            {
                var foreground = NativeMethods.GetForegroundWindow();

                if (foreground == IntPtr.Zero)
                    return Enums.Desktop.Foreground.Unknown;

                var root = NativeMethods.GetAncestor(foreground, Constants.Windows.Window.GetAncestorRoot);

                if (root == IntPtr.Zero)
                    root = foreground;

                int processId;

                if (NativeMethods.GetWindowThreadProcessId(root, out processId) == 0)
                    return Enums.Desktop.Foreground.Unknown;

                var shellProcessId = 0;
                var shell = NativeMethods.GetShellWindow();

                if (shell != IntPtr.Zero && NativeMethods.GetWindowThreadProcessId(shell, out shellProcessId) == 0)
                    shellProcessId = 0;

                var className = new StringBuilder(256);

                if (NativeMethods.GetClassName(root, className, className.Capacity) == 0)
                    return Enums.Desktop.Foreground.Unknown;

                var hasDefView = NativeMethods.FindWindowEx(root, IntPtr.Zero, Constants.Windows.Desktop.DefViewClass, null) != IntPtr.Zero;

                return Classify(className.ToString(), processId, shellProcessId, _ownProcessId, hasDefView);
            }
            catch (Exception e)
            {
                Logger.Debug(e);

                return Enums.Desktop.Foreground.Unknown;
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            bool? isLocked;

            switch (e.Reason)
            {
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                    isLocked = true;
                    break;

                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.SessionUnlock:
                    isLocked = false;
                    break;

                default:
                    isLocked = null;
                    break;
            }

            if (isLocked == null)
                return;

            // SystemEvents are raised on a dedicated thread
            _dispatcher.BeginInvoke((Action)(() =>
            {
                _isSessionLocked = isLocked.Value;
                Evaluate();
            }));
        }

        private void OnShowTimerTick(object sender, EventArgs e)
        {
            _showTimer.Stop();

            if (!_disposed && ShouldShow(_isEnabled, _isMainWindowShown, _isSessionLocked, GetForeground(), IsVisible))
                SetVisible(true);
        }

        private void OnReconcileTimerTick(object sender, EventArgs e)
        {
            Evaluate();
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Out-of-context hooks are delivered on the thread that installed them (UI thread)
            Evaluate();
        }

        private void SetVisible(bool value)
        {
            if (IsVisible == value)
                return;

            IsVisible = value;

            var handler = VisibilityChanged;

            if (handler != null)
                handler(value);
        }

        #endregion
    }
}
