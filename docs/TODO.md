# Pendientes de ARCA — propuesta para discutir

25 de octubre de 2026. Los apartados marcados están implementados para 1.0.3. La limpieza semanal sigue pendiente.

- [x] Acortar la ayuda de Limpieza extrema a: «Limpia shaders, restos de Windows Update y DNS. Los shaders se reconstruirán.» Mantener el aviso visible «Deberás cargar shaders de nuevo».
- [ ] Añadir la casilla «Limpiar cada semana» en 1 Clic Clean, desactivada por defecto. Propuesta de ayuda: «Limpia cada 7 días mientras ARCA esté abierta. No incluye Extrema.»
- [ ] La tarea semanal ejecutará las categorías normales seleccionadas, excluyendo Extrema de forma obligatoria aunque esté activada para una limpieza manual. No ejecutará la limpieza de RAM ni forzará el cierre de navegadores. Respetará las aplicaciones abiertas y las mismas verificaciones y respaldos del modo manual.
- [ ] Guardar la fecha de la última ejecución semanal completada. Propuesta: primera ejecución 7 días después de activar; si ARCA estuvo cerrada al vencer el plazo, dejar una ejecución pendiente para la próxima sesión, sin acumular varias limpiezas. Posponer durante juegos/otras aplicaciones en primer plano y durante otra limpieza. No instalar una tarea o servicio que ejecute mantenimiento con ARCA cerrada.
- [ ] Resolver antes de implementar la interacción con UAC: la interfaz actual no está elevada; temporales del sistema, registro y dispositivos requieren autorización. Propuesta para discutir: ejecutar al volver al escritorio y pedir autorización si corresponde. Si se cancela, no repetir avisos continuamente ni contar la ejecución como completada. No prometer mantenimiento completamente silencioso con este modelo de permisos.
- [x] Traducir la interfaz completa al inglés y añadir un selector pequeño «Idioma: Español / English» en Opciones avanzadas. Cubrir RAM, 1 Clic Clean, burbuja, bandeja, ayudas, estados, errores propios e informes. Centralizar textos usando la infraestructura de localización existente y persistir la elección por usuario. No traducir nombres de aplicaciones o dispositivos ni prometer traducir mensajes devueltos por Windows.
- [x] Eliminar la casilla «Iniciar minimizado». El acceso directo de Inicio llevará un argumento específico para abrir siempre en bandeja; abrir ARCA manualmente seguirá mostrando su ventana. La burbuja seguirá dependiendo del ajuste del usuario y de la visibilidad del escritorio.
- [x] Eliminar «Activar atajo de limpieza» de la interfaz y desregistrar/desactivar los atajos guardados para que no sigan actuando de forma invisible. Ese ajuste actualmente permite limpiar RAM mediante una combinación global de teclas.
- [x] Compilar y probar los textos de ambos idiomas y el argumento /Tray del acceso de inicio. Mantener la prueba de inicio de sesión real como comprobación manual.
- [ ] Cuando se implemente la limpieza semanal, verificar periodicidad con un reloj simulado, persistencia, cancelación, exclusión de Extrema, UAC y sesiones cerradas.

Decisiones pendientes: categorías de la tarea semanal, recuperación de una semana vencida y comportamiento del aviso de administrador. No modificar el modelo de permisos sin discutirlo.
