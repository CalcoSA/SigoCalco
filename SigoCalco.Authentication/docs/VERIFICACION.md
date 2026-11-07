# Verificación de alcance y pruebas

Verificación realizada el 5 de noviembre de 2026.

Este documento conserva la evidencia de la entrega inicial. La [revisión del 6 de noviembre](REVISION_TECNICA_FINAL.md) registra las 164 pruebas actuales, las correcciones y los pendientes. El TRX local fue reemplazado por esa ejecución más reciente.

- Origen: C:\Users\juan.zapata\source\repos\sitraCalco, rama feature/jp2, HEAD 66b2a5dd8417cee16877446faec7602c90ba2821.
- Destino: C:\Users\juan.zapata\source\repos\SigoCalco, rama feature/initproject, HEAD eb45f48be73a06b2c9bd2e66a19884233314b32c.
- Git del origen limpio antes y después.
- Las ramas y los HEAD no cambiaron. No se hicieron commits ni push.
- Ninguna conexión ni script ejecutado en MySQL o WordPress.

Se compararon antes y después las rutas y los SHA-256 de todos los archivos rastreados y no ignorados. Los archivos previamente eliminados se representaron como MISSING. Se excluyen los archivos ignorados, los metadatos internos de Git y las salidas de compilación.

| Alcance | Archivos | SHA-256 agregado antes y después |
|---|---:|---|
| Repositorio sitraCalco completo | 438 | 08B83615BD90217599E5A669F1ACD3824A0705042FFF060BCB40DB4363504620 |
| SigoCalco fuera de SigoCalco.Authentication | 86 | 5396D7116EB0BA08E5889857691917E49572ED7A26940E32E222936ACBBD0CC9 |

Ambas huellas coinciden exactamente. Esto incluye los cambios locales que Meetings ya tenía al iniciar el trabajo; se conservaron.

Se verificaron los cinco proyectos y sus referencias: todos resuelven dentro de SigoCalco.Authentication. No quedan referencias operativas al contexto, conexión o tablas de SitraCalco.

Compilación Release: SDK 10.0.401; 0 errores y 0 advertencias.
Suite completa: 132 aprobadas, 0 fallidas, 0 omitidas; counters contrastados con el TRX.
Reporte local: ../.artifacts/test-results/authentication.trx (ignorado por Git).

Las pruebas usan SQLite en memoria, servicios simulados y metadatos de MySQL/Pomelo sin conexión. No se ejecutaron pruebas de integración con MySQL/WordPress reales ni el script de instalación.
