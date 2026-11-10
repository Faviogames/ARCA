# ARCA v2.1

- «Caché de archivos modificados» queda desactivada por defecto para evitar forzar el volcado de búferes de todas las unidades fijas en cada limpieza.
- La migración desde versiones anteriores también la desactiva inicialmente. Puedes activarla en Memoria RAM → Opciones avanzadas → Áreas de limpieza; tu elección se conserva.
- Las demás áreas de RAM mantienen su configuración. Este cambio no cuantifica ni garantiza una mejora de tiempo o FPS: faltan mediciones reales por etapa.
- Se mantiene la instalación de actualizaciones solo al pulsar Actualizar.

## Download / Descarga

Extract **ARCA-v2.1-portable.zip** and run ARCA.exe. Keep all included files together, including nunit.framework.dll. The standalone ARCA.exe is intended for the in-app updater. GPL-3.0 source and SHA-256 checksums are included. Not Authenticode signed.

**Modified file cache** is now off by default, including upgrades from older releases. Enable it explicitly under RAM memory → Advanced options → Cleaning areas if wanted. Subsequent choices persist.

Build and focused tests run in GitHub Actions. No real memory cleaning, device removal, registry cleaning or updater replacement is performed during validation.

Pending for a later version: broader registry rules (unfinished local work), weekly cleaning, and the explicitly postponed Packages reset. None is included in v2.1.
