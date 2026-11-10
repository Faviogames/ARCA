# ARCA v2.2

Esta entrega reúne los dos cambios acordados y sustituye a la v2.1 incompleta.

- **RAM:** Caché de archivos modificados desactivada por defecto, también al migrar desde versiones antiguas. Sigue disponible en Opciones avanzadas y se guarda la elección explícita posterior.
- **Registro:** ampliación integrada en 1 Clic Clean. Añade desinstalaciones huérfanas (instalación y desinstalador inexistentes), Steam cuando faltan la carpeta y el manifiesto de una biblioteca accesible, GameDVR con ejecutable desaparecido, y referencias concretas a archivos, iconos y carpetas inexistentes.
- Mantiene MUICache y App Paths; añade revisión de ambas vistas de HKLM, 32 y 64 bits. Las ubicaciones compartidas se revisan una sola vez.
- Respaldo .reg por operación en su vista original, comprobación del estado después del respaldo y verificación de la retirada. Si no se puede respaldar, no se modifica la entrada.
- Restauración con la herramienta reg.exe de la misma arquitectura que la exportación. Las reglas y exclusiones están documentadas en docs/REGISTRY_RULES.md.

**Descarga:** extrae **ARCA-v2.2-portable.zip** y abre ARCA.exe. Conserva nunit.framework.dll junto al ejecutable y cierra antes la versión antigua desde la bandeja.

## English

Includes both agreed changes: **Modified file cache off by default** and **expanded registry cleaning**. New rules cover verified orphaned uninstall entries, Steam entries with both installation and manifest missing, stale GameDVR records, and explicit missing-file references. Each change requires a backup, revalidation and post-delete verification. Export/import use the original registry view.

Download the complete portable ZIP; the standalone ARCA.exe asset is for the updater. Source and SHA-256 checksums included. GPL-3.0; not Authenticode signed.

Validation runs on Windows in GitHub Actions, including export/delete/import tests using GUID-named HKCU fixture keys only. No cleaning of the user's actual registry, RAM or devices is executed. Broader detection does not mean every key listed by another cleaner is safe to remove.

Still pending: weekly cleaning, the postponed Packages reset, and first-run RAM timing comparisons.
