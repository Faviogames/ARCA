# Validación local de ARCA 4.4

24 de octubre de 2026.

- Compilación Release correcta mediante `build/build.ps1 -SkipRestore` y .NET SDK 10. Persiste la advertencia heredada MSB3884 por un conjunto de reglas ausente.
- **108 pruebas focalizadas**, 0 errores y 0 fallos. Se ejecutan con `build/test.ps1`.
- Cubren mantenimiento, agrupación de temporales y accesos directos, USB con retirada simulada, top 3 de memoria, cierre simulado de navegadores y clasificación/estados/ciclo de vida de la burbuja.
- Render WPF real de la pantalla `1 clic clean` y de la burbuja, sin solicitar limpieza ni cierre de aplicaciones.
- Retirados `BrowserHistoryTask.cs`, sus pruebas, contadores y referencias del proyecto. La compilación actual no tiene llamadas a `winsqlite3.dll` ni código para borrar historial.
- Los iconos incluidos tienen 256 × 256 píxeles y transparencia.

No se ha ejecutado una limpieza real elevada, retirado periféricos del usuario ni certificado la suite heredada completa. Los escenarios de juegos, varios monitores, escritorios virtuales, configuración HKLM, restauración del registro y tareas de Windows Update requieren pruebas en el equipo de destino. Los avisos por no poder guardar configuración/logs desde el sandbox no provocaron fallos en las pruebas.

Malwarebytes detectó heurísticamente la build experimental 4.3. No se conoce su causa exacta ni se ha confirmado que esta 4.4 deje de detectarse. Las pruebas funcionales no son un análisis antimalware. La aplicación permanece sin firma Authenticode.

GitHub Actions está preparado para compilar y ejecutar las mismas pruebas. No se declara CI remoto correcto hasta que el workflow haya terminado en el repositorio publicado.
