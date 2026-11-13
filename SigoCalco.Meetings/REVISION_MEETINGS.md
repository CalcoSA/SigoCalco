# Meetings: cierre de implementación

Nota de continuidad: este documento registra el cierre de la migración anterior. La ampliación posterior de respuestas uniformes, quinto endpoint y proveedores está documentada en [docs/ACTAS_Y_CALENDARIO.md](docs/ACTAS_Y_CALENDARIO.md), con 91 pruebas aprobadas. No se repitió la migración.

Estado comprobado el 11/11/2026.

- La migración ya estaba completa al retomar: 25 tablas en bdcalco_sigocalco (8 Authentication + 17 Meetings). No se repitió DDL.
- Se eliminaron las nueve tablas antiguas vacías tras comprobar dependencias y respaldo: attendee, commitment, meeting, meeting_attendee, minute, minute_attendee, minute_template, minute_type y topic, todas con prefijo sigocalco_meetings_. La tabla meeting actual corresponde al modelo nuevo.
- scripts/sql/02_create_model.sql conserva el modelo definitivo: 17 CREATE TABLE y 30 índices. Se verificaron columnas, tipos, nulabilidad, FK e índices reales contra EF Core.
- El respaldo anterior a la sustitución está en artifacts/mysql-backup-before-replacement. Las ocho copias de Authentication coinciden exactamente por SHA256 con sus tablas actuales, incluidos datos.
- Eliminados 50 archivos obsoletos: entidades, mappings, repositorios, servicios, interfaces, DTO, excepciones y controladores del modelo antiguo, además del ejemplo WeatherForecast. Se conservan las 17 entidades y los cuatro endpoints terminados.
- No hay código activo apuntando a tablas eliminadas. Authentication y Reservations no tienen cambios de código. Sin commit ni push.

Validación final:

- Release: 0 errores, 0 advertencias.
- 59 pruebas distintas aprobadas: 58 de la suite y la prueba real MySQL.
- La prueba MySQL pasó por los cuatro endpoints HTTP, con persistencia y autorización reales, y obtuvo versiones concurrentes 1 y 2.
- Se crearon y eliminaron únicamente una plantilla, dos versiones y dos secciones temporales. Conteos finales de esas tres tablas: 0.
- La primera conexión MySQL de pruebas falló por TLS del entorno restringido, antes de escribir. Solo se repitió esa prueba fuera del entorno restringido y pasó.
- Swagger de Meetings responde 200 en https://localhost:7298/swagger y muestra las cuatro operaciones con autorización Bearer.
- Los informes están en 05.Meetings.Test/Meetings.Test/TestResults/meetings-final.trx y meetings-mysql-final.trx; el segundo resuelve el fallo ambiental del primero.

JWT y sesiones:

- Compatibilidad probada usando el código original JwtTokenApplication de Authentication, enlazado solo en el proyecto de pruebas y sin modificarlo. No se ejecutó login ni se creó una sesión de usuario.
- Comprobada la igualdad de configuración local Secret/Issuer/Audience sin mostrar sus valores. Se mantienen firma HS256, expiración JWT, identidad, rol, centro y permisos.
- Inactividad, logout y revocación de sesión siguen pendientes de integración. Meetings todavía no consulta el estado de sid.
- Propuesta mínima pendiente de aprobación: Authentication expone GET /api/Auth/session usando su validación existente; Meetings lo consulta tras validar el JWT y rechaza sesiones vencidas/revocadas. La consulta no renovaría actividad; POST /api/Auth/activity y POST /api/Auth/logout seguirían siendo responsabilidad de Authentication. El nuevo endpoint y cualquier ajuste en Authentication requieren autorización previa.

Inicio desde dos terminales PowerShell:

    dotnet run --project "C:\Users\juan.zapata\source\repos\SigoCalco\SigoCalco.Authentication\01.Authentication.Api\Authentication.Api\Authentication.Api.csproj" --launch-profile Authentication.Api
    dotnet run --project "C:\Users\juan.zapata\source\repos\SigoCalco\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj" -c Release --launch-profile https

Swagger Authentication: https://localhost:64845/swagger
Swagger Meetings: https://localhost:7298/swagger

Prueba manual: obtener el accessToken mediante POST /api/Auth/login, pegarlo en Authorize de Meetings y ejecutar GET /api/v1/acta-types, POST /api/v1/templates, POST /api/v1/templates/{templateId}/versions y GET /api/v1/templates. Utilizar los identificadores devueltos. Los bodies completos están en 01.Meetings.Api/Meetings.Api/Meetings.Api.http.

No volver a ejecutar Invoke-Migration.ps1 sobre el esquema final: conserva las precondiciones del esquema de origen para impedir una repetición accidental. Para verificar el resultado utilizar Verify-Meetings.ps1 y Verify-Authentication.ps1.
