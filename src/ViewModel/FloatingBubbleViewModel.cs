using System;
using System.Globalization;
using System.Windows.Input;

namespace WinMemoryCleaner
{
    /// <summary>
    /// ARCA: floating bubble state (Ready, Cleaning, Completed, Error)
    /// </summary>
    /// <seealso cref="ObservableObject" />
    public class FloatingBubbleViewModel : ObservableObject
    {
        #region Fields

        private OptimizationResult _lastResult;
        private readonly ICommand _optimizeCommand;
        private Enums.FloatingBubble.State _state;
        private int _usedPercentage;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FloatingBubbleViewModel" /> class.
        /// </summary>
        /// <param name="optimizeCommand">The existing optimize command (shared with window, tray, hotkey and schedule).</param>
        public FloatingBubbleViewModel(ICommand optimizeCommand)
        {
            if (optimizeCommand == null)
                throw new ArgumentNullException("optimizeCommand");

            _optimizeCommand = optimizeCommand;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the accessible name / tooltip text.
        /// </summary>
        public string Description
        {
            get
            {
                switch (State)
                {
                    case Enums.FloatingBubble.State.Cleaning:
                        return Text("Limpiando memoria…", "Cleaning memory…");

                    case Enums.FloatingBubble.State.Completed:
                        return string.Format(CultureInfo.CurrentCulture, "{0}{1}{2}", Text("Limpieza completada", "Cleaning completed"), Environment.NewLine, ResultText);

                    case Enums.FloatingBubble.State.Error:
                        return Text("La limpieza no se completó en todas las áreas. Consulta el registro o abre la ventana principal. Clic para reintentar.", "Cleaning did not complete in every area. Check the log or open the main window. Click to retry.");

                    default:
                        var text = string.Format(CultureInfo.CurrentCulture, "{0} · {1} {2}", Text("Limpiar memoria", "Clean memory"), UsedPercentageText, Text("de RAM en uso", "RAM in use"));

                        return _lastResult != null && _lastResult.Succeeded
                            ? string.Format(CultureInfo.CurrentCulture, "{0}{1}{2}", text, Environment.NewLine, ResultText)
                            : text;
                }
            }
        }

        /// <summary>
        /// Gets the "hide bubble" menu text.
        /// </summary>
        public string HideText
        {
            get { return Text("Ocultar burbuja", "Hide bubble"); }
        }

        /// <summary>
        /// Gets the "open app" menu text.
        /// </summary>
        public string OpenText
        {
            get { return Text("Abrir ARCA", "Open ARCA"); }
        }

        /// <summary>
        /// Gets the "exit" menu text.
        /// </summary>
        public string ExitText
        {
            get { return Localizer.String.Exit; }
        }

        /// <summary>
        /// Gets the last measured result text (approximate, can be zero or negative).
        /// </summary>
        public string ResultText
        {
            get
            {
                if (_lastResult == null)
                    return string.Empty;

                var delta = _lastResult.PhysicalFreeDelta;
                var size = Math.Abs(delta).ToMemoryUnit();

                if (size.Key < 0.05)
                    return Text("Sin variación apreciable de RAM libre (aprox.)", "No noticeable change in free RAM (approx.)");

                return string.Format(CultureInfo.CurrentCulture, "{0}: {1}{2:0.#} {3} ({4})", Text("RAM libre", "Free RAM"), delta >= 0 ? "+" : "−", size.Key, size.Value, Text("aprox.", "approx."));
            }
        }

        /// <summary>
        /// Gets the current state.
        /// </summary>
        public Enums.FloatingBubble.State State
        {
            get { return _state; }
            private set
            {
                if (_state == value)
                    return;

                _state = value;

                RaisePropertyChanged();
                RaisePropertyChanged(() => Description);
            }
        }

        /// <summary>
        /// Gets the used physical memory percentage.
        /// </summary>
        public int UsedPercentage
        {
            get { return _usedPercentage; }
            private set
            {
                if (_usedPercentage == value)
                    return;

                _usedPercentage = value;

                RaisePropertyChanged();
                RaisePropertyChanged(() => UsedPercentageText);
                RaisePropertyChanged(() => Description);
            }
        }

        /// <summary>
        /// Gets the used physical memory percentage text.
        /// </summary>
        public string UsedPercentageText
        {
            get { return string.Format(CultureInfo.CurrentCulture, "{0}%", UsedPercentage); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Requests one cleaning with the current options. Ignored while a cleaning is running.
        /// </summary>
        /// <returns><c>true</c> if the request was forwarded to the optimize command.</returns>
        public bool Clean()
        {
            if (State == Enums.FloatingBubble.State.Cleaning || !_optimizeCommand.CanExecute(null))
                return false;

            _optimizeCommand.Execute(null);

            return true;
        }

        /// <summary>
        /// Called when an optimization (from any trigger) finishes.
        /// </summary>
        /// <param name="result">The result.</param>
        public void OnOptimizationFinished(OptimizationResult result)
        {
            if (result == null)
                return;

            _lastResult = result;

            State = result.Succeeded ? Enums.FloatingBubble.State.Completed : Enums.FloatingBubble.State.Error;

            RaisePropertyChanged(() => ResultText);
            RaisePropertyChanged(() => Description);
        }

        /// <summary>
        /// Called when an optimization (from any trigger) starts.
        /// </summary>
        public void OnOptimizationStarted()
        {
            State = Enums.FloatingBubble.State.Cleaning;
        }

        /// <summary>
        /// Returns from a transient state (Completed/Error) to Ready.
        /// </summary>
        public void ResetToReady()
        {
            if (State == Enums.FloatingBubble.State.Completed || State == Enums.FloatingBubble.State.Error)
                State = Enums.FloatingBubble.State.Ready;
        }

        /// <summary>
        /// Picks the Spanish or English text for ARCA strings (not part of the strict localization files).
        /// </summary>
        /// <param name="spanish">The Spanish text.</param>
        /// <param name="english">The English text.</param>
        /// <returns>The text for the current app language.</returns>
        public static string Localize(string spanish, string english)
        {
            var language = Localizer.Language;

            return language != null && language.Name != null && language.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? spanish : english;
        }

        private static string Text(string spanish, string english)
        {
            return Localize(spanish, english);
        }

        /// <summary>
        /// Updates the used physical memory percentage.
        /// </summary>
        /// <param name="memory">The memory.</param>
        public void Update(Memory memory)
        {
            if (memory == null || memory.Physical == null)
                return;

            UsedPercentage = Math.Max(0, Math.Min(100, memory.Physical.Used.Percentage));
        }

        #endregion
    }
}

