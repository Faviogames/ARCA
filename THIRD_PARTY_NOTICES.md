# Atribución y dependencias

ARCA deriva de **Windows Memory Cleaner**, copyright Igor Mundstein, bajo GPL-3.0. Se conservan el código original y el texto de licencia en `LICENSE`; el historial original puede consultarse en el repositorio de origen. Origen: https://github.com/IgorMundstein/WinMemoryCleaner.

Las modificaciones de ARCA abarcan la interfaz WPF, burbuja de escritorio, limpieza manual, mantenimiento y recursos gráficos. No es una publicación oficial del autor original.

Las dependencias NuGet con versiones están en `src/packages.config`. NUnit 2.6.4 es una dependencia heredada del ensamblado; su runtime y licencia se incluyen en el portable. Los analizadores y referencias se restauran mediante NuGet; sus cachés no se publican en Git.

La clave de nombre seguro `src/WinMemoryCleaner.snk` ya formaba parte del código del proyecto original. No es un certificado Authenticode ni acredita al editor de ARCA.

Los iconos de las categorías se generaron para ARCA a partir de referencias visuales del usuario. No se incluyen binarios ni licencias de aplicaciones estudiadas como inspiración.

