# Validación de ARCA 1.0.2

25 de octubre de 2026.

- Compilación Release correcta con .NET SDK 10. Persiste MSB3884 por el archivo heredado AllRules.ruleset ausente.
- Entrega 1.0.2 preparada para publicación, con portable, fuentes, ejecutable de actualización y SHA-256.
- 151 pruebas focalizadas, 0 errores y 0 fallos (build/test.ps1).
- Se enumeran todas las clases instaladas con DIGCF_ALLCLASSES y enumerador nulo, incluidos HID, audio, monitores y dispositivos/componentes de software. Solo CR_NO_SUCH_DEVINST confirma que una instancia está desconectada; los estados presentes o desconocidos no se retiran.
- Cinco casos nuevos prueban la conservación de nodos presentes o de estado desconocido. Continúan pasando las pruebas de reconexión antes de retirar y de verificación posterior a la retirada.
- El análisis real de la build local coincide con la auditoría independiente de todas las clases y la lista mostrada por la otra aplicación. Esta validación fue de solo lectura; no se retiraron dispositivos del usuario.
- Regresión de comunicación real por pipe sin elevación: reproduce el bloqueo de 1.0 al enviar progreso mientras se espera cancelación y pasa con el canal asíncrono. No retira dispositivos.
- Pruebas de progreso sin duplicar resultados y de verificación posterior: una solicitud aceptada cuyo registro sigue presente o no se puede comprobar no cuenta como retirada.
- Auditoría local de solo lectura: la detección de ARCA coincide con la diferencia entre registros instalados y presentes (DIGCF_PRESENT). No se borraron dispositivos durante esta auditoría.
- Las 20 pruebas nuevas cubren selección de operaciones elevadas, rechazo de bits desconocidos, conservación de resultados, argumentos inválidos y creación/eliminación de un acceso directo en una carpeta temporal. No modifican la carpeta Inicio real.
- Las anteriores cubren mantenimiento con datos de ensayo, USB simulado, top 3, cierre simulado de navegadores y estados/ciclo de vida de la burbuja.
- Render WPF real de 1 Clic Clean, opciones avanzadas y burbuja, sin ejecutar limpiezas.
- Manifiesto asInvoker. El auxiliar usa runas, una conexión de un solo uso con identificador aleatorio, ACL por usuario/administradores y comprobación de los procesos de ambos extremos. Solo acepta operaciones fijas de ARCA, sin comandos ni rutas de borrado arbitrarios.
- Se rechaza elevar con otra cuenta para evitar limpiar otro perfil. El auxiliar termina después de la operación. La cancelación es cooperativa y Windows Update restaura sus servicios en finally.
- Preferencias en HKCU\Software\ARCA; importación de las anteriores del equipo en lectura.
- Sin SQLite ni limpieza de historial; iconos de 256 px con transparencia.

Los avisos del sandbox por no poder guardar preferencias o informes no provocaron fallos. La suite heredada completa no está certificada.

## Comprobaciones manuales pendientes

No se ha aceptado UAC para ejecutar una limpieza real en el equipo del usuario. Falta verificar el recorrido real: abrir sin elevación, cancelar UAC sin cambios ni cierre de navegadores, aceptar una limpieza y comprobar que termina el auxiliar. También falta probar el inicio tras iniciar sesión, retirada real de USB, restauración del registro, servicios de Windows Update y escenarios con varios monitores/juegos.

La causa de la detección heurística del prototipo 4.3 no se confirmó. ARCA 1.0.2 sigue sin firma Authenticode; las pruebas funcionales no certifican ausencia de detecciones. No se han desactivado protecciones ni creado exclusiones.

El test de lectura de la versión desde la API de releases se incluye en build/test.ps1. La sustitución y reapertura real mediante el actualizador opcional no se ha ejecutado durante esta validación. No se ha vaciado DNS ni limpiado Discord del usuario al compilar o probar.
