# Cambios de ARCA

## 2.0 — 25 de octubre de 2026

- Compilación y ensamblado renombrados a ARCA.exe; recursos WPF y scripts adaptados.
- Identificación ARCA v2.0 en el pie de ambas secciones.
- Actualización manual: botón rojo solo cuando hay una versión nueva, deshabilitado sin novedades o sin comprobación válida. Se retira la casilla de actualización automática.
- El inicio existente se reescribe con la ruta del ejecutable actual y /Tray.
- 159 pruebas focalizadas superadas, incluidos identidad, comparación de versiones y disponibilidad del botón.
- No se incluyen limpiezas semanales ni se cambia la lógica de mantenimiento.

## 1.0.3 — 25 de octubre de 2026

- Ayuda de Limpieza extrema más corta; se mantiene el aviso «Deberás cargar shaders de nuevo».
- Eliminada la casilla «Iniciar minimizado». El acceso de Inicio abre ARCA en la bandeja (`/Tray`); los accesos de versiones anteriores se actualizan al abrir la app. Abrirla a mano muestra siempre la ventana.
- Interfaz en inglés: selector «Idioma: Español / English» en Opciones avanzadas, con cambio inmediato y sin reiniciar. Cubre la ventana, 1 Clic Clean, la burbuja, la bandeja, ayudas, estados, errores propios e informes; los números siguen el idioma. Los nombres de aplicaciones y dispositivos y los mensajes devueltos por Windows no se traducen. Si la preferencia guardada no es español, ARCA usa inglés.
- Eliminado el atajo global de limpieza de RAM. Si estaba activado, se desactiva y deja de registrarse; restablecer la configuración tampoco lo vuelve a registrar.

La limpieza semanal sigue pendiente de definir e implementar; esta versión no ejecuta mantenimiento automático.

## 1.0.2 — 25 de octubre de 2026

- Textos de interfaz: «1 Clic Clean», «Caché de navegadores y Discord» y ayuda breve para recordar que las aplicaciones deben estar cerradas.
- Cachés incluye Discord, Discord PTB y Discord Canary (Cache, Code Cache y GPUCache, también los GIF de favoritos). Se omite si Discord está abierto, incluso en la bandeja. Se conservan cookies, sesión e historial.
- Limpieza extrema vacía la caché DNS (equivalente a `ipconfig /flushdns`). No libera espacio; aparece en el informe.

- Nueva opción «Actualizar siempre» (Memoria RAM → Opciones avanzadas): busca la última release de GitHub al abrir ARCA y cada 24 horas; si hay una versión nueva, descarga `ARCA.exe`, comprueba su versión y reabre la app actualizada. Desactivada por defecto.

- «Dispositivos USB» pasa a llamarse «Dispositivos desconectados».
- El análisis y la retirada abarcan todas las clases de Windows, incluidos HID, AudioEndpoint, Monitor, SoftwareComponent y SoftwareDevice; sin filtro por enumerador USB ni por antigüedad.
- Se mantienen la comprobación de desconexión antes de retirar y la confirmación posterior; no se eliminan paquetes del almacén de controladores.
- 151 pruebas focalizadas correctas y comparación de solo lectura con los dispositivos presentes de Windows.

## 1.0.1 — 25 de octubre de 2026

- Corregido un bloqueo del auxiliar anterior a la limpieza: una lectura síncrona de cancelación impedía enviar el primer progreso. Reproducido con una prueba sin dispositivos reales; corregido con un canal asíncrono en ambos extremos.
- Progreso USB por dispositivo y aviso con tiempo transcurrido cuando Windows sigue procesando una retirada.
- Verificación después de cada retirada: solo se cuentan registros que ya no aparecen en la lista de Windows; nombres y errores quedan en el informe.
- Resumen USB específico y distinción entre «no hay registros desconectados» y una retirada que falló.
- 144 pruebas focalizadas, sin ejecutar retiradas reales durante la validación.

## 1.0 — 25 de octubre de 2026

Primera versión de lanzamiento. La numeración 4.x correspondía a prototipos internos.

- Interfaz y burbuja sin administrador; autorización al limpiar mediante un auxiliar temporal.
- Inicio con Windows por acceso directo del usuario; sin limpieza automática.
- Preferencias en HKCU, con importación de la configuración anterior en lectura.
- Ayudas revisadas: cachés resumida, USB con una frase y papelera sin tooltip.
- Cancelar UAC no limpia ni cierra navegadores.
- 128 pruebas focalizadas y paquetes portable/código fuente con SHA-256.

## Prototipos internos

### 4.4 — 24 de octubre de 2026

- Retirados el módulo experimental de historial, las referencias SQLite y sus pruebas específicas.
- Historial, cookies, contraseñas, marcadores y sesiones se conservan también en extrema.
- Reorganizados repositorio, documentación y paquetes para publicar ARCA.
- Retirados los workflows de distribución pertenecientes al proyecto original.

## 4.3

- Iconos de 256 px, ayudas y textos simplificados, botón «Limpiar PC».
- Módulo experimental de historial, retirado en 4.4 tras una detección heurística cuya causa no se confirmó.

## 4.2

- Familia de iconos y cinco tarjetas. Accesos directos se integra en Temporales.

## 4.1

- Registros USB desconectados sin antigüedad mínima, con comprobación antes de retirar.

## 4.0 y prototipos

- Sección «1 Clic Clean», interfaz en forma de RAM, top 3 por consumo y burbuja circular exclusiva del escritorio.
- Base: Windows Memory Cleaner 3.0.8, de Igor Mundstein. Se conserva su atribución; el historial original está disponible en el repositorio de origen.

