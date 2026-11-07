# SigoCalco.Authentication

Microservicio .NET 10 reutilizado de Authentication, con las mismas capas, repositorios, controladores, respuestas y mecanismos de seguridad.

Revisión final del 6 de noviembre de 2026: **Release con 0 errores y 0 advertencias; 164 pruebas aprobadas, 0 fallidas y 0 omitidas**. La [matriz de revisión técnica](docs/REVISION_TECNICA_FINAL.md) documenta correcciones, evidencia ejecutada, licencia de AutoMapper e integraciones pendientes; no certifica el despliegue en producción.

## Procedencia y alcance

- Origen leído: `C:\Users\juan.zapata\source\repos\sitraCalco\sitraCalco.Authentication`.
- Destino: `C:\Users\juan.zapata\source\repos\SigoCalco\SigoCalco.Authentication`.
- Rama de destino: `feature/initproject`; no se creó ni cambió de rama.
- No se hicieron commits ni push. No se ejecutaron scripts ni cambios sobre bases de datos.
- No se copiaron appsettings del origen, secretos, bin, obj, .vs, archivos .user ni perfiles personales.
- Se conservaron los cambios locales previos de Meetings y el contenido de Reservations.
- Las huellas del origen y de los archivos ajenos al destino se documentan en [VERIFICACION.md](docs/VERIFICACION.md).
- El inventario completo de archivos está en [ARCHIVOS.md](docs/ARCHIVOS.md).

## Estructura

```text
SigoCalco.Authentication/
├── SigoCalco.Authentication.sln
├── 01.Authentication.Api/Authentication.Api/
├── 02.Authentication.Application/Authentication.Application/
├── 03.Authentication.Domain/Authentication.Domain/
├── 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/
├── 05.Authentication.Test/Authentication.Test/
├── scripts/Verify.ps1
└── docs/
```

Los namespaces y ensamblados usan `SigoCalco.Authentication.*`. El contexto es `SigoCalcoContext`; todas las referencias entre proyectos resuelven dentro de esta carpeta. Se corrigió el nombre de infraestructura de `Persistance` a `Persistence` en el destino. Se conserva EF Core 9.0.17 con Pomelo 9.0.0 del original sobre net10.0.

## Comportamiento conservado

- Login con usuario y contraseña de WordPress. El usuario debe existir previamente en Authentication y estar activo, con opciones de menú asignadas.
- Verificación WordPress bcrypt, bcrypt estándar, phpass y MD5 legado, sin cambios en sus algoritmos.
- SSO de intranet mediante HMAC-SHA256, ventana temporal y comparación constante.
- JWT HS256 con los claims anteriores, validación de emisor, audiencia, firma, vencimiento y sesión.
- Sesiones independientes, expiración por inactividad, actividad con limitación de frecuencia, logout y rotación del hash del refresh token. Renovar un token no cuenta como actividad.
- Roles, opciones de menú, jerarquía de menú, código funcional PDV protegido y catálogo de centros de solo consulta.
- Administración de usuarios, paginación, filtro por login, auditoría de operaciones exitosas y logs.
- Contrato HTTP `ResponseApi { isSuccess, message, result }` y tratamiento original de excepciones.

Las acciones administrativas conservan la autorización `[Authorize]` existente; no se agregó una política nueva por rol. La auditoría conserva el actor obtenido de `userLogin`, separado del usuario afectado.

## Correo y prioridad local

`User.Email` se almacena en `users.email VARCHAR(254) NOT NULL`. No se agregó unicidad: los identificadores únicos siguen siendo `userLogin` y `wordpressUserId`. Se recortan espacios exteriores, se conserva el uso de mayúsculas y se rechazan valores vacíos, formatos inválidos, nombres de presentación, espacios interiores y valores mayores de 254 caracteres.

En `POST /api/user`, `email` es opcional en la solicitud, pero el valor final es obligatorio:

1. Si se envía, se valida y guarda como correo local.
2. Si se omite o llega como null, se consulta WordPress por login y se comprueba que el ID coincida con `wordpressUserId`.
3. Se obtiene `wp_users.user_email AS wordpressUserEmail` y se valida antes de guardar.
4. Si WordPress no ofrece un correo válido, la creación devuelve 400; el administrador puede enviar un correo local válido.

La consulta parametrizada y su lector incluyen `wordpressUserEmail`. El hash `wordpressUserPass` conserva `[JsonIgnore]`.

WordPress solo aporta el valor inicial. Login y SSO consultan WordPress para autenticar, pero no escriben el correo local. Refresh consulta el usuario local. No existe sincronización posterior ni escritura sobre WordPress.

El correo aparece en `UserDto`, `UserAdministrationDto`, las consultas de usuarios, `result.user` de login/SSO/refresh y el resultado de creación/actualización.

## Centro de Soluciones

El centro permanece en la relación `Userrole`, respetando el modelo original:

- Creación: `solutionCenterId` obligatorio, positivo y existente, para cualquier rol.
- Actualización: los valores omitidos o null conservan el valor actual; la asignación final debe tener un rol y centro válidos.
- Cambiar a un rol distinto de PDV conserva el centro si no se envía otro.
- `Userrole.SolutionCenterId` y su columna SQL son obligatorios, con FK al catálogo propio.
- Los DTOs administrativos muestran el centro para todos los roles.
- Se mantienen las transacciones y los bloqueos de usuario, rol y catálogo. La combinación de campos se realiza contra el usuario leído bajo bloqueo.
- Se rechaza eliminar un rol con usuarios asignados. La FK hacia rol también usa RESTRICT, para evitar usuarios sin asignación. La protección especial del rol funcional PDV se conserva.
- Login, SSO y refresh rechazan usuarios históricos sin asignación de centro.

El modelo conserva la unicidad original `(IdUser, IdRole)`; no agrega unicidad al centro. Varias personas pueden usar el mismo centro. La API mantiene una asignación por usuario. Si un usuario histórico tiene varias, su actualización exige indicar explícitamente rol y centro antes de reemplazarlas.

La base impide centros nulos/inexistentes en las asignaciones; crear el usuario y su asignación es una operación transaccional del repositorio. Un operador con SQL directo debe respetar también esa regla: una FK desde Userrole no obliga por sí sola a que todo usuario tenga una fila hija. No se añadieron triggers ni se rediseñó el modelo.

## Endpoints

Se conservan las rutas y métodos originales. Las rutas por controlador también aceptan el uso habitual en minúsculas.

| Método | Ruta | Comportamiento |
|---|---|---|
| POST | /api/auth/login | Login WordPress y autorización local; incluye correo local |
| GET | /api/auth/intranet-access?userLogin=...&ts=...&sig=... | SSO; incluye correo local |
| POST | /api/auth/refresh | Rota refresh y emite JWT con correo local actual |
| POST | /api/auth/activity | Actualiza actividad de la sesión autenticada |
| POST | /api/auth/logout | Revoca la sesión autenticada |
| GET | /api/user?page=1&take=10&search=... | Usuarios paginados con correo y centro |
| GET | /api/user/{idUser} | Usuario con correo y centro |
| POST | /api/user | Alta con correo inicial y centro obligatorio |
| PUT | /api/user | Actualización parcial de email, rol, centro y estado |
| DELETE | /api/user/{idUser} | Elimina usuario y relaciones |
| GET | /api/role | Lista de roles |
| GET | /api/role/{idRole} | Rol con opciones de menú |
| POST | /api/role | Crea rol y permisos de menú |
| PUT | /api/role | Modifica rol y permisos de menú |
| DELETE | /api/role/{idRole} | Elimina rol no protegido y sin usuarios asignados |
| GET | /api/menuoption | Opciones de menú |
| GET | /api/menuoption/role/{idRole} | Menús por rol |
| GET | /api/wordpressuser/{userLogin} | Usuario WordPress, ahora con wordpressUserEmail |
| GET | /api/Authentication/SolutionCenters | Catálogo de centros |
| GET | /api/log | Auditoría paginada, con filtros originales |

Login, intranet-access y refresh son anónimos y validan sus respectivas credenciales. Los demás endpoints requieren autenticación. No se añadió un endpoint `me`: la información del usuario autenticado sigue en las respuestas de autenticación y en sus claims.

### DTO de creación

```json
{
  "wordpressUserId": 123,
  "userLogin": "usuario",
  "userName": "Nombre del usuario",
  "statusUser": true,
  "idRole": 7,
  "solutionCenterId": 101
}
```

En este ejemplo se obtiene el correo inicial de WordPress. Los IDs son ilustrativos y deben corresponder a datos reales. También puede enviarse `"email": "usuario@example.com"`.

### DTO de actualización

```json
{
  "idUser": 42,
  "email": "nuevo@example.com"
}
```

Se conserva `PUT /api/user`. `idUser` es obligatorio. `email`, `idRole`, `solutionCenterId` y `statusUser` son opcionales; omitidos o null significan conservar. `statusUser: false` sí cambia el estado. Un correo vacío o un ID cero/negativo es inválido. No se puede quitar un centro: incluso una edición de correo debe terminar con una asignación válida.

Los nombres reales son `idRole` y `statusUser`, como en el original; no se renombraron a `roleId` o `status`.

POST y PUT conservan el envoltorio y sus mensajes, pero ahora `result` contiene `UserAdministrationDto` con el correo persistido, en lugar del objeto vacío previo. GET por ID y listado también incluyen `email`.

## JWT para Meetings y Reservations

Claims de identidad conservados: `sub`, `sid`, `idUser`, `wordpressUserId`, `userLogin`, `userName`, `idRole`, `nameRole`.

Nuevo claim exacto: **`email`**, obtenido de la tabla local. Se mantienen `iss`, `aud` y `exp`, la firma HS256 y los parámetros de expiración. No se agregaron claims de permisos ni se cambió el significado de los anteriores.

Los JWT emitidos después de actualizar el usuario contienen el correo nuevo, tanto por login como por SSO o refresh. Los tokens ya emitidos conservan su contenido hasta que dejen de ser válidos.

La API conserva `MapInboundClaims = false`; puede leerse con `User.FindFirst("email")?.Value` o con la nueva extensión `GetEmail()`, que solo considera identidades autenticadas y rechaza claims duplicados. Los consumidores deberán configurar emisor, audiencia y clave propios de SigoCalco. Si usan roles de ASP.NET, deberán mapear los claims existentes según su política.

La validación exclusiva de firma/vencimiento no reproduce logout ni expiración por inactividad. La integración futura de Meetings/Reservations debe validar también la sesión `sid` mediante el mecanismo que se acuerde. Esos microservicios no fueron modificados.

Referencia sobre nombres de claims: [Microsoft: MapInboundClaims](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-10.0).

## Base de datos y despliegue manual

Script: [CreateSigoCalcoAuthentication.sql](04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Scripts/CreateSigoCalcoAuthentication.sql).

Usa explícitamente `bdcalco_sigocalco`, que debe existir, y crea en orden las ocho tablas:

```text
sigocalco_authentication_log
sigocalco_authentication_menuoption
sigocalco_authentication_role
sigocalco_authentication_solution_center
sigocalco_authentication_users
sigocalco_authentication_rolemenuoption
sigocalco_authentication_sessions
sigocalco_authentication_userrole
```

Incluye PK, FK, índices, restricciones de nulabilidad, longitudes, defaults y charset. Los nombres de restricciones se limitan a Authentication. No contiene ALTER/DROP de tablas ni DML sobre datos existentes. No es una migración ni es idempotente: detenerse ante cualquier error y no ejecutarlo sobre una instalación existente.

No hay migraciones ni `EnsureCreated` en producción. Las pruebas usan `EnsureCreated` exclusivamente en SQLite en memoria. El script se contrastó con el modelo MySQL/Pomelo sin abrir conexiones; no fue ejecutado en MySQL.

Se deben cargar manualmente los centros, roles, menús, relaciones de permisos y el primer usuario autorizados. El código original no contiene un catálogo corporativo completo ni un usuario inicial seguro; no se inventaron. Si se usa PDV, debe conservarse `role_code = 'PDV'`, sin asumir el ID 6 del sistema anterior.

Antes de importar históricos, revisar en una copia de los datos: correo ausente/inválido, usuario sin rol, centro ausente/inexistente, varias asignaciones y duplicados de login/WordPress ID. Resolverlos mediante una lista aprobada, conservando registros y trazabilidad. No rellenar centros por el nombre del rol ni reemplazar correos locales con WordPress. No se ejecutó limpieza, importación ni migración automática.

Referencia para el comportamiento RESTRICT: [MySQL: foreign key constraints](https://dev.mysql.com/doc/refman/8.0/en/create-table-foreign-keys.html).

## Configuración pendiente

`appsettings.json` es una plantilla sin credenciales. Completar mediante variables de entorno o configuración local excluida de Git:

Los valores de desarrollo de esta estación se conservan en `appsettings.Development.json`, ignorado por Git y excluido de `dotnet publish`. En Production deben proporcionarse mediante la configuración externa del entorno. AutoMapper 16.1.1 es la versión efectiva de la API; su modalidad de licencia y la configuración de `LicenseKey`, cuando corresponda, quedan pendientes según la revisión técnica.

| Variable | Valor requerido |
|---|---|
| ConnectionStrings__SigoCalcoDatabase | Conexión propia a MySQL con Database=bdcalco_sigocalco; se valida el nombre del esquema |
| ConnectionStrings__WordPressDatabase | Conexión de lectura a WordPress y su tabla wp_users |
| Jwt__Secret | Clave propia de SigoCalco, al menos 32 bytes, compartida de forma segura con los validadores |
| Jwt__Issuer | Emisor; plantilla: SigoCalco.Authentication |
| Jwt__Audience | Audiencia; plantilla: SigoCalco |
| Jwt__ExpirationMinutes | Plantilla: 480, conserva el default del emisor original |
| Sso__Secret | Secreto propio acordado con la integración de intranet |
| Sso__ExpirationSeconds | Opcional; default original: 300 |
| Session__IdleTimeoutMinutes | Plantilla: 5, conserva el default de SessionOptions |
| Cors__AllowedOrigins__0 | Origen real del frontend; agregar índices para otros orígenes |
| ASPNETCORE_ENVIRONMENT | Development para Swagger local; Production en despliegue |

Configurar también URL/puerto, HTTPS, certificados y permisos de conexión según el entorno. No se copió launchSettings del origen. WordPress solo requiere lectura; la cuenta de ejecución de Authentication no necesita ejecutar DDL.

MySqlConnector se actualizó de 2.6.0 a **2.6.1** al detectar NuGet una vulnerabilidad heredada, sin cambiar las demás versiones: [aviso GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8).

## Compilación y pruebas

Desde esta carpeta, con PowerShell y SDK .NET 10:

```powershell
.\scripts\Verify.ps1 -Action Test
```

El script compila en Release, ejecuta la suite y conserva SDK/cache/temporales/resultados dentro de `.artifacts`, excluido de Git. También admite `-Action Build`.

Resultado del 5 de noviembre de 2026: **compilación con 0 errores y 0 advertencias; 132 pruebas aprobadas, 0 fallidas, 0 omitidas**. Se ejecutaron con SDK 10.0.401. Reporte TRX local: `.artifacts/test-results/authentication.trx`.

Última ejecución, 6 de noviembre de 2026: **164 pruebas aprobadas, 0 fallidas y 0 omitidas**, después de corregir el alta con `statusUser: false` y alinear el default de auditoría con el SQL. Añade 28 casos con la canalización HTTP real, 3 regresiones de `StatusUser` y 1 comparación completa SQL/modelo. El TRX local corresponde a esta ejecución más reciente.

La suite incluye las pruebas heredadas de sesión, auditoría, autorización de acceso y permisos, adaptadas solo donde cambió el contrato. Añade correo inicial WordPress, mapeo de lectura, validación, creación explícita, correos compartidos, PUT parcial, persistencia/rollback, centro obligatorio para PDV y otros roles, centros inexistentes, eliminación de roles asignados, datos históricos, JWT con correo actualizado y conservación de claims, SSO y refresh.

Las pruebas de persistencia usan SQLite en memoria; un interceptor elimina únicamente la sintaxis de bloqueo MySQL y comprueba que se solicite dentro de una transacción. Eso **no valida bloqueos concurrentes, collation ni ejecución DDL en MySQL real**. La lectura de WordPress se prueba con el mapeador compartido y servicios simulados; **no se probó una conexión real a WordPress**. La integración externa, certificados, credenciales, catálogo aprobado, primer usuario y aceptación con React/Meetings/Reservations quedan pendientes del entorno.
