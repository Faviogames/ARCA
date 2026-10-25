# ARCA

**Another RAM Cleaner Application** — utilidad gratuita para Windows con limpieza manual de RAM y mantenimiento mediante **1 Clic Clean**.

Basada en [Windows Memory Cleaner](https://github.com/IgorMundstein/WinMemoryCleaner), de Igor Mundstein. ARCA es una versión modificada independiente bajo GPL-3.0.

![1 Clic Clean](docs/1-clic-clean.png)

## Descargar y ejecutar

Descarga el portable desde [Releases](https://github.com/Faviogames/ARCA/releases/latest), extráelo y abre `ARCA.exe`. Conserva `nunit.framework.dll` junto al ejecutable: el proyecto heredado incluye sus pruebas en el ensamblado. Cierra la versión anterior desde la bandeja antes de abrir una nueva.

Entrega local: `releases/ARCA-v1.0.2-portable.zip` y `releases/ARCA-v1.0.2-source.zip`. Los paquetes se distribuyen como archivos de release, no como binarios dentro del código Git.

ARCA abre sin pedir administrador. La limpieza completa de RAM, los temporales del sistema, el registro, los dispositivos desconectados y Extrema solicitan autorización de Windows al ejecutarse. Un auxiliar de la misma aplicación realiza la operación y termina; no se instala ningún servicio. Cancelar el aviso cancela esa operación. Las cachés del usuario y la papelera pueden limpiarse sin elevar la app.

La autorización debe hacerse con la misma cuenta de Windows que abrió ARCA; no se admite usar las credenciales de otra cuenta para limpiar su perfil. Si la app se abre manualmente como administrador, mantiene esos permisos durante esa sesión.

En **Memoria RAM → Opciones avanzadas**, activa **Iniciar con Windows** para crear un acceso directo en la carpeta Inicio de tu usuario. **Iniciar minimizado** es independiente. Arrancar ARCA no ejecuta ninguna limpieza. Conserva el portable en una ubicación estable; si lo mueves, desactiva y vuelve a activar esta opción desde la nueva ubicación.

## Funciones

- **RAM:** uso físico, top 3 por consumo, limpieza manual y exclusiones.
- **Burbuja circular:** aparece al minimizar u ocultar ARCA y volver al escritorio; se oculta al activar otras aplicaciones, incluidos juegos en ventana. Clic para limpiar RAM, arrastre y menú contextual.
- **Temporales:** temporales del usuario/Windows, informes de errores, volcados antiguos y accesos directos rotos. Los accesos se envían a la papelera.
- **Cachés:** navegadores y Discord cerrados, INetCache y miniaturas. Conserva historial, cookies, contraseñas, marcadores y sesiones.
- **Registro:** reglas acotadas para referencias a programas inexistentes, con exportación previa de claves y restauración.
- **Dispositivos desconectados:** registros no presentes de todas las clases de Windows, sin antigüedad mínima; conserva los paquetes de controladores, comprueba la desconexión antes de retirar y verifica que el registro desapareció.
- **Papelera:** vaciado opcional.
- **Extrema:** shaders de GPU, restos de Windows Update, optimización de distribución y vaciado de la caché DNS. Los juegos necesitan reconstruir los shaders después. También conserva el historial.

Se recuerdan las categorías. La primera ejecución ofrece vista previa; después puede limpiarse con un clic. Hay cancelación e informes locales; un fallo no detiene las demás tareas.

La casilla opcional «Cerrar navegadores» pertenece a la limpieza de RAM y fuerza su cierre cuando se solicita esa limpieza; puede perderse trabajo sin guardar. El mantenimiento de cachés omite los navegadores abiertos.

## Versión 1.0.2

Dispositivos desconectados de todas las clases de Windows, cachés de Discord y vaciado de caché DNS en Extrema. La interfaz usa «1 Clic Clean» y «Caché de navegadores y Discord»; la ayuda recuerda cerrar las aplicaciones.

«Actualizar siempre» es opcional y está desactivado por defecto. Comprueba las releases de Faviogames/ARCA al abrir y cada 24 horas, descarga ARCA.exe, comprueba su versión y reinicia la aplicación. Para instalar por primera vez, utiliza el ZIP completo con sus dependencias.

## Versión 1.0.1

Corrige el bloqueo de comunicación del auxiliar que podía impedir empezar una limpieza en 1.0. USB muestra el registro en curso, comprueba después de cada retirada que desapareció de la lista de Windows y guarda nombres y errores en el informe. No se cuentan las retiradas cuya eliminación no se confirma.

## Lanzamiento 1.0

Primera versión de lanzamiento de ARCA. Los números 4.x identificaban prototipos internos. Incluye inicio con Windows por usuario, permisos solicitados al limpiar y ayudas breves por categoría. Conserva el diseño de RAM, la burbuja circular, los iconos de 256 px y el botón «Limpiar PC».

Se retiró completamente el módulo experimental de historial del prototipo 4.3: ya no se distribuye código SQLite de acceso a perfiles.

La 4.3 recibió una detección heurística genérica de Malwarebytes cuya causa no se confirmó. Retirar el módulo no garantiza que otros antivirus dejen de detectar el ejecutable. ARCA todavía no tiene firma Authenticode; no desactives la protección para instalarla.

## Compilar y probar

Requisitos: Windows 10/11, .NET SDK 10 y NuGet CLI en PATH, o variable `NUGET` con la ruta a `nuget.exe`. El proyecto usa C# y WPF sobre .NET Framework 4.0; las referencias y analizadores se restauran desde `src/packages.config`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build/build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build/test.ps1
```

Con paquetes ya restaurados, añade `-SkipRestore` al primer comando. El resultado es `src/bin/Release/WinMemoryCleaner.exe`; el nombre interno se conserva por compatibilidad de los recursos WPF. El portable lo entrega como `ARCA.exe`.

Las pruebas focalizadas usan datos de ensayo y no limpian la RAM del usuario, cierran sus navegadores ni retiran dispositivos reales. La suite heredada completa no está certificada. Consulta [docs/VALIDATION.md](docs/VALIDATION.md).

Las operaciones elevadas, retirada real de USB, restauración del registro y escenarios con varios monitores/juegos deben comprobarse en el equipo de destino. Más RAM libre o menos caché no garantizan más FPS. Informes y respaldos: `%LOCALAPPDATA%\ARCA`.

Se mantienen la configuración y la exclusión de instancia originales: no ejecutes ambas variantes a la vez. No hay actualización automática desde el proyecto original.

## Licencia y créditos

GPL-3.0. Consulta [LICENSE](LICENSE) y [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). No se hereda la firma del autor original. Los iconos se generaron para ARCA y están en `src/Resources/Images/Clean/`.
