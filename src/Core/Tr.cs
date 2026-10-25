using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace WinMemoryCleaner
{
    /// <summary>
    /// ARCA's own texts. Spanish is the source text and the lookup key; English comes from
    /// <see cref="English"/>. The language follows <see cref="Localizer.Language"/>.
    /// </summary>
    public static class Tr
    {
        private static readonly CultureInfo _spanish = new CultureInfo("es-ES");
        private static readonly CultureInfo _english = new CultureInfo("en-US");

        /// <summary>Forces a language ("es" or "en") regardless of the saved choice; tests only.</summary>
        internal static string Override { get; set; }

        /// <summary>Whether texts are shown in Spanish.</summary>
        public static bool IsSpanish
        {
            get
            {
                var name = Override;
                if (name == null)
                {
                    var language = Localizer.Language;
                    name = language == null ? null : language.Name;
                }
                return name != null && name.StartsWith("es", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>Culture for numbers and dates in ARCA's texts.</summary>
        public static CultureInfo Culture { get { return IsSpanish ? _spanish : _english; } }

        /// <summary>The text in the current language; an untranslated text stays in Spanish.</summary>
        public static string Get(string spanish)
        {
            if (spanish == null || IsSpanish) return spanish;
            string english;
            return English.TryGetValue(spanish, out english) ? english : spanish;
        }

        /// <summary><see cref="Get"/> followed by string.Format with <see cref="Culture"/>.</summary>
        public static string Format(string spanish, params object[] args)
        {
            return string.Format(Culture, Get(spanish), args);
        }

        /// <summary>Spanish source text → English.</summary>
        internal static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "Actualizar", "Update" },
            { "Buscando actualizaciones…", "Checking for updates…" },
            { "ARCA está actualizada.", "ARCA is up to date." },
            { "Hay una nueva versión. Pulsa Actualizar para instalarla.", "A new version is available. Click Update to install it." },
            { "No se pudo comprobar la actualización. Se reintentará más tarde.", "Could not check for updates. ARCA will try again later." },
            { "Descargando actualización…", "Downloading update…" },
            { "No se pudo actualizar. Pulsa Actualizar para reintentar.", "Could not update. Click Update to try again." },
            { "Tu memoria, bajo control", "Your memory, under control" },
            { "Memoria RAM", "RAM memory" },
            { "1 Clic Clean", "1-Click Clean" },
            { "Minimizar al escritorio", "Minimize to the desktop" },
            { "Minimizar", "Minimize" },
            { "Ocultar en la bandeja", "Hide in the tray" },
            { "Ocultar ventana", "Hide window" },
            { "MEMORIA EN USO", "MEMORY IN USE" },
            { "TOP 3 · MAYOR CONSUMO", "TOP 3 · HIGHEST USAGE" },
            { "TOP 3 · NO DISPONIBLE", "TOP 3 · NOT AVAILABLE" },
            { "RAM residente aproximada; procesos agrupados por ejecutable en esta sesión. Las páginas compartidas pueden contarse varias veces. Actualización cada 5 s.", "Approximate resident RAM; processes grouped by executable in this session. Shared pages may be counted more than once. Updated every 5 s." },
            { "Indicador de uso de RAM", "RAM usage indicator" },
            { "{0}: {1} ({2}) · {3} de RAM residente aproximada", "{0}: {1} ({2}) · {3} of approximate resident RAM" },
            { "Explorador", "Explorer" },
            { " en uso", " in use" },
            { " disponibles", " available" },
            { " totales", " total" },
            { "Limpiar memoria", "Clean memory" },
            { "Limpiando memoria…", "Cleaning memory…" },
            { "Limpieza manual · Tú decides cuándo", "Manual cleaning · You decide when" },
            { "Limpieza en curso…", "Cleaning in progress…" },
            { "Limpieza completada · ", "Cleaning completed · " },
            { "Limpieza incompleta. Consulta el registro de la aplicación.", "Cleaning incomplete. Check the application log." },
            { "Limpieza cancelada · permiso no concedido", "Cleaning cancelled · permission not granted" },
            { "No se pudo completar la limpieza", "Cleaning could not be completed" },
            { "Burbuja en el escritorio", "Desktop bubble" },
            { "Visible solo en el escritorio", "Visible only on the desktop" },
            { "Cerrar navegadores", "Close browsers" },
            { "Fuerza el cierre al limpiar, incluso desde la burbuja. Puedes perder trabajo sin guardar.", "Forces them to close when cleaning, even from the bubble. You may lose unsaved work." },
            { "Procesos de navegador cerrados: {0}; sin cerrar: {1}.", "Browser processes closed: {0}; not closed: {1}." },
            { "No se encontraron navegadores abiertos compatibles.", "No supported open browsers were found." },
            { "Procesos de navegador cerrados: {0}.", "Browser processes closed: {0}." },
            { "⚙   Opciones avanzadas", "⚙   Advanced options" },
            { "Opciones avanzadas", "Advanced options" },
            { "Volver  ✕", "Back  ✕" },
            { "Iniciar con Windows", "Start with Windows" },
            { "Abre ARCA al iniciar sesión. No ejecuta limpiezas automáticamente.", "Opens ARCA when you sign in. It never cleans automatically." },
            { "No se pudo cambiar el inicio con Windows: ", "Could not change starting with Windows: " },
            { "Actualizar siempre", "Always update" },
            { "Busca nuevas versiones en GitHub al abrir ARCA y cada 24 horas. Si hay una, la descarga y reabre ARCA actualizado.", "Checks GitHub for new versions when ARCA opens and every 24 hours. If there is one, it downloads it and reopens ARCA updated." },
            { "Idioma:", "Language:" },
            { "Áreas de limpieza", "Cleaning areas" },
            { "Vaciar más áreas no garantiza mayor rendimiento.", "Emptying more areas does not guarantee better performance." },
            { "Procesos excluidos", "Excluded processes" },
            { "Quitar", "Remove" },
            { "Añadir", "Add" },
            { "Salir de ARCA", "Exit ARCA" },
            { "Mantenimiento del equipo, en un solo clic.", "PC maintenance in a single click." },
            { "Listo para comenzar", "Ready to start" },
            { "1 categoría activada", "1 category selected" },
            { "{0} categorías activadas", "{0} categories selected" },
            { " + extrema", " + extreme" },
            { "Limpiar PC", "Clean PC" },
            { "Cancelar", "Cancel" },
            { "Cancelando…", "Cancelling…" },
            { "Cancelando · se termina el elemento en curso…", "Cancelling · finishing the current item…" },
            { "Elige al menos una categoría", "Choose at least one category" },
            { "Analizando qué se puede limpiar · aún no se modifica nada", "Analyzing what can be cleaned · nothing is changed yet" },
            { "Permiso no concedido o limpieza cancelada", "Permission not granted or cleaning cancelled" },
            { "Ya hay una limpieza en curso, o no se pudo iniciar", "A cleaning is already in progress, or it could not start" },
            { "Se puede liberar {0}", "{0} can be freed" },
            { "Análisis cancelado · no se modificó nada", "Analysis cancelled · nothing was changed" },
            { "Revisa la vista previa", "Review the preview" },
            { "Cancelado · ", "Cancelled · " },
            { "No se pudo completar · ", "Could not be completed · " },
            { "Completado · ", "Completed · " },
            { "Limpieza extrema", "Extreme cleaning" },
            { "Limpia shaders, restos de Windows Update y DNS. Los shaders se reconstruirán.", "Cleans shaders, Windows Update leftovers and DNS. Shaders will be rebuilt." },
            { "Shaders de GPU · Restos de Windows Update · DNS", "GPU shaders · Windows Update leftovers · DNS" },
            { "Desactivada · solo se aplica si la activas", "Off · only applied if you turn it on" },
            { "Deberás cargar shaders de nuevo", "Shaders will need to load again" },
            { "Ver detalles", "View details" },
            { "⚙  Opciones", "⚙  Options" },
            { "Vista previa", "Preview" },
            { "Esto es lo que se limpiará. Todavía no se ha modificado nada.", "This is what will be cleaned. Nothing has been changed yet." },
            { "Limpiar ahora", "Clean now" },
            { "Vista previa cerrada · no se modificó nada", "Preview closed · nothing was changed" },
            { "Total estimado: {0}. Los archivos en uso o recientes se conservarán.", "Estimated total: {0}. Files in use or recent files will be kept." },
            { "Opciones y detalles", "Options and details" },
            { "Mostrar vista previa antes de limpiar", "Show preview before cleaning" },
            { "Abrir carpeta de informes", "Open reports folder" },
            { "No se pudo abrir la carpeta de informes: ", "Could not open the reports folder: " },
            { "Deshacer último cambio del registro", "Undo last registry change" },
            { "Reglas: se conservan los archivos en uso y los de menos de 48 h (7 días para volcados); nunca se siguen enlaces ni uniones. Las cachés de cada navegador y de Discord solo se limpian con él cerrado. También en limpieza extrema se conserva el historial de navegación, las cookies, las contraseñas, los marcadores y las sesiones. Temporales incluye los accesos rotos del escritorio e Inicio, que van a la papelera. El registro se exporta antes de cada cambio. Se retiran los registros de dispositivos desconectados sin esperar días, horas ni minutos, también si no tienen fecha. Se comprueba de nuevo su estado antes de retirarlos y se conservan sus controladores.", "Rules: files in use and files newer than 48 h (7 days for dumps) are kept; links and junctions are never followed. Each browser's and Discord's caches are only cleaned while it is closed. Extreme cleaning also keeps browsing history, cookies, passwords, bookmarks and sessions. Temporary files include broken desktop and Start shortcuts, which go to the recycle bin. The registry is exported before every change. Records of disconnected devices are removed without waiting days, hours or minutes, even when they have no date. Their state is checked again before removal and their drivers are kept." },
            { "Aún no hay informe en esta sesión.", "No report in this session yet." },
            { "No hay cambios de registro que deshacer", "There are no registry changes to undo" },
            { "Esperando autorización para restaurar el registro…", "Waiting for authorization to restore the registry…" },
            { "Restauración cancelada o no disponible", "Restore cancelled or not available" },
            { "No se pudo restaurar el último respaldo del registro", "The last registry backup could not be restored" },
            { "Registro restaurado · {0} respaldo importado", "Registry restored · {0} backup imported" },
            { "Registro restaurado · {0} respaldos importados", "Registry restored · {0} backups imported" },
            { "Temporales", "Temporary files" },
            { "Cachés", "Caches" },
            { "Accesos directos", "Shortcuts" },
            { "Registro de Windows", "Windows registry" },
            { "Dispositivos desconectados", "Disconnected devices" },
            { "Papelera", "Recycle bin" },
            { "Temporales, informes de errores y accesos directos rotos", "Temporary files, error reports and broken shortcuts" },
            { "Caché de navegadores y Discord", "Browser and Discord cache" },
            { "Se guarda un respaldo", "A backup is saved" },
            { "Limpia dispositivos desconectados obsoletos", "Cleans stale disconnected devices" },
            { "Se vacía la papelera", "Empties the recycle bin" },
            { "Elimina temporales antiguos del usuario y Windows, informes de errores y volcados antiguos. Los accesos directos rotos se envían a la papelera.", "Deletes old user and Windows temporary files, error reports and old dumps. Broken shortcuts are sent to the recycle bin." },
            { "Las aplicaciones deben estar cerradas.", "The applications must be closed." },
            { "Limpia referencias a programas que ya no existen. Guarda un respaldo antes de modificar el registro.", "Cleans references to programs that no longer exist. Saves a backup before changing the registry." },
            { "Ideal para solucionar problemas con mandos y periféricos.", "Ideal for fixing problems with controllers and peripherals." },
            { "En cola", "Queued" },
            { "Analizando…", "Analyzing…" },
            { "Limpiando…", "Cleaning…" },
            { "Completado", "Completed" },
            { "Omitido", "Skipped" },
            { "Error", "Error" },
            { "Cancelado", "Cancelled" },
            { "Ningún registro de dispositivo retirado", "No device record removed" },
            { "No hay dispositivos desconectados para limpiar", "There are no disconnected devices to clean" },
            { "Nada que limpiar", "Nothing to clean" },
            { "Ya estaba limpio", "Already clean" },
            { "{0} omitidos", "{0} skipped" },
            { "{0} fallos", "{0} failures" },
            { "{0} liberados", "{0} freed" },
            { "archivo", "file" },
            { "archivos", "files" },
            { "acceso", "shortcut" },
            { "accesos", "shortcuts" },
            { "dispositivo", "device" },
            { "dispositivos", "devices" },
            { "entrada", "entry" },
            { "entradas", "entries" },
            { "archivo eliminado", "file deleted" },
            { "archivos eliminados", "files deleted" },
            { "acceso retirado", "shortcut removed" },
            { "accesos retirados", "shortcuts removed" },
            { "entrada retirada", "entry removed" },
            { "entradas retiradas", "entries removed" },
            { "dispositivo retirado", "device removed" },
            { "dispositivos retirados", "devices removed" },
            { "categoría con error", "category with errors" },
            { "categorías con error", "categories with errors" },
            { "{0} abierto", "{0} open" },
            { "Ruta rechazada por seguridad: {0}", "Path rejected for safety: {0}" },
            { "(nula)", "(null)" },
            { "Sin respaldo, no se modificó {0}", "No backup, {0} was not changed" },
            { "Optimización de distribución", "Delivery Optimization" },
            { "Optimización de distribución: ", "Delivery Optimization: " },
            { "Optimización de distribución: tiempo agotado", "Delivery Optimization: timed out" },
            { "Optimización de distribución no disponible", "Delivery Optimization not available" },
            { "Caché DNS: se vaciará", "DNS cache: will be flushed" },
            { "Caché DNS vaciada", "DNS cache flushed" },
            { "Caché DNS no se pudo vaciar", "DNS cache could not be flushed" },
            { "Windows Update: actualización pendiente de reinicio", "Windows Update: update pending a restart" },
            { "Windows Update: instalación en curso", "Windows Update: installation in progress" },
            { "Windows Update: el servicio {0} no se detuvo", "Windows Update: the {0} service did not stop" },
            { "Restos de Windows Update", "Windows Update leftovers" },
            { "Revisando dispositivos desconectados de Windows…", "Checking Windows for disconnected devices…" },
            { "No se pudo leer la lista de dispositivos de Windows", "Could not read the Windows device list" },
            { "No se pudo completar la revisión de dispositivos · error {0}", "The device check could not be completed · error {0}" },
            { "  (dispositivo desconectado · sin espera mínima)", "  (disconnected device · no minimum wait)" },
            { "Omitido: no se pudo identificar {0}", "Skipped: {0} could not be identified" },
            { "Ya no estaba registrado: {0}", "No longer registered: {0}" },
            { "Retirando {0}/{1} · {2}", "Removing {0}/{1} · {2}" },
            { "Omitido: ya no se confirma la desconexión de {0}", "Skipped: {0} is no longer confirmed as disconnected" },
            { "Retirado y verificado: {0}", "Removed and verified: {0}" },
            { "Retirada no confirmada: {0} · el registro sigue apareciendo en Windows", "Removal not confirmed: {0} · the record still appears in Windows" },
            { "Retirada no confirmada: {0} · no se pudo verificar la lista de Windows", "Removal not confirmed: {0} · the Windows list could not be checked" },
            { "No se pudo retirar: {0} · error {1}", "Could not remove: {0} · error {1}" },
            { "Cancelación pendiente · {0} · {1} s", "Cancellation pending · {0} · {1} s" },
            { "Esperando a Windows · {0} · {1} s", "Waiting for Windows · {0} · {1} s" },
            { "Dispositivo", "Device" },
            { "Respuesta de limpieza no válida.", "Invalid cleaning response." },
            { "Respuesta de memoria no válida.", "Invalid memory response." },
            { "Respuesta de respaldo no válida.", "Invalid backup response." },
            { "Permiso de administrador cancelado.", "Administrator permission cancelled." },
            { "No se pudo abrir el auxiliar de limpieza.", "The cleaning helper could not be opened." },
            { "El auxiliar no pudo conectarse. No se inició la limpieza.", "The helper could not connect. Cleaning was not started." },
            { "Auxiliar no reconocido.", "Unrecognized helper." },
            { "La limpieza debe autorizarse con la misma cuenta de Windows que abrió ARCA.", "Cleaning must be authorized with the same Windows account that opened ARCA." },
            { "Operación no permitida.", "Operation not allowed." },
            { "Informe no válido.", "Invalid report." },
        };
    }

    /// <summary>XAML text that follows the language: <c>{local:Tr 'Texto en español'}</c>.</summary>
    [MarkupExtensionReturnType(typeof(object))]
    public sealed class TrExtension : MarkupExtension
    {
        /// <summary>For XAML.</summary>
        public TrExtension() { }

        /// <summary>Spanish source text.</summary>
        public TrExtension(string text) { Text = text; }

        /// <summary>Spanish source text.</summary>
        [ConstructorArgument("text")]
        public string Text { get; set; }

        /// <summary>A one-way binding that is refreshed whenever the language changes.</summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding("Revision") { Source = TrSource.Instance, Mode = BindingMode.OneWay, Converter = TrSource.Instance, ConverterParameter = Text };
            return binding.ProvideValue(serviceProvider);
        }
    }

    /// <summary>Binding source whose only job is to announce a language change.</summary>
    public sealed class TrSource : INotifyPropertyChanged, IValueConverter
    {
        /// <summary>Shared instance.</summary>
        public static readonly TrSource Instance = new TrSource();

        private int _revision;

        private TrSource()
        {
            Localizer.StaticPropertyChanged += (sender, e) => { _revision++; Raise(); };
        }

        /// <summary>Raised when the language changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Changes with every language switch.</summary>
        public int Revision { get { return _revision; } }

        private void Raise()
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs("Revision"));
        }

        /// <summary>Translates the converter parameter.</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Tr.Get(parameter as string);
        }

        /// <summary>Not supported.</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
