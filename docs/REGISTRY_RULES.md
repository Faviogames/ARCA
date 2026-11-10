# Registro de ARCA v2.2

La categoría Registro de Windows conserva su casilla y su respaldo. La nueva detección se integra en análisis, limpieza, progreso e informe de 1 Clic Clean.

| Regla | Evidencia necesaria | Cambio |
|---|---|---|
| MUICache | El ejecutable de la entrada ya no existe en un disco fijo disponible | Retirar ese valor |
| App Paths | Falta el ejecutable indicado | Retirar la entrada concreta |
| Desinstalación ordinaria | DisplayName presente; carpeta de instalación y ejecutable del desinstalador ausentes | Retirar esa entrada de Uninstall |
| Steam | AppID válido, carpeta en steamapps/common ausente, biblioteca accesible y appmanifest del AppID ausente | Retirar esa entrada Steam App |
| GameDVR | MatchedExeFullPath presente y ejecutable ausente | Retirar ese hijo concreto de GameConfigStore/Children |
| Referencias explícitas | Ruta absoluta reconocida, en disco fijo disponible, confirmada ausente | Retirar solo el valor concreto |

Referencias específicas: Discord overlay (x86/x64), Brave Promo, carpetas de plugins/presets iZotope RX11, OneDrive (parche/iconos), AIMP, Unreal Engine 4.0, SmokePatch Football Life 2027, PotPlayer (rutas recientes inexistentes y carpeta de apertura), SplitCam y StartIsBack. No se vacían historiales enteros ni se modifica una ruta que siga existiendo.

Se conservan las entradas MSI, componentes del sistema, actualizaciones, hijos de otro producto y registros sin identidad suficiente. No se consideran inválidos los datos solo por estar vacíos o porque una aplicación no aparezca en una lista de programas.

Las claves de proveedor completas como HKCU/Software/Khronos, Opera Software o ArcBrowser no se borran solo por su nombre. No se cambian referencias de Windows/Active Setup, datos de anticheat, telemetría ni cachés de instaladores cuya función no se ha verificado.

Antes de aplicar, se exporta la clave en su vista original. Después se compara el valor tipado que se va a retirar, o la clave completa con sus subclaves cuando se va a eliminar entera, se comprueba de nuevo la evidencia y se verifica la retirada. Sin respaldo o con estado desconocido, se conserva. Un cambio que Windows no confirma como eliminado no se cuenta como retirado.

Los respaldos usan nombres únicos y registran 32/64 bits en el nombre del archivo. Deshacer importa en esa misma vista. Una restauración incompleta conserva la carpeta para poder reintentarla.

Las pruebas de integración usan claves HKCU y carpetas temporales con GUID creadas por la suite; no borran configuraciones reales de aplicaciones.

Las eliminaciones de varios valores de una misma clave comparten un único respaldo completo previo. Solo se retiran valores presentes en el lote inicial y cuya firma tipada siga coincidiendo; los nuevos o cambiados se conservan.
