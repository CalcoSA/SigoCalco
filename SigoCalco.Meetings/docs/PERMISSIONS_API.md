# Administración de los seis permisos

Los tres endpoints requieren un JWT válido y la política `MeetingsPermissionAdministrator`. Esta política utiliza `IMeetingsIdentity`: comprueba la asignación vigente del usuario/rol, su estado activo y el nombre real en Authentication, y exige **Administrador**. No basta con incluir ese nombre en un JWT sin la asociación real. Se conserva el contrato JWT existente. Un JWT ausente/inválido/vencido produce 401; una identidad autenticada sin autorización administrativa produce 403. La capa Application vuelve a exigir la misma regla, reutilizando la identidad ya validada por solicitud.

La política administrativa no depende de `permission_value` ni constituye una séptima clave. Vaciar permisos funcionales no elimina la capacidad administrativa del rol real Administrador. No hay ninguna decisión pendiente sobre quién administra estos endpoints.

## Contratos

Todos los resultados conservan `isSuccess`, `message`, `result`. Se mantienen las seis claves actuales. No se crea ni renombra ningún permiso, ni se modifica el esquema.

### GET `/api/v1/permissions`

Sin body. Respuesta 200, ordenada por ID; extracto ilustrativo:

```json
{
  "isSuccess": true,
  "message": "Permisos consultados correctamente.",
  "result": [
    {"permissionId":32,"permissionKey":"PLANTILLAS_GLOBALES_GESTION","roles":["Administrador"]},
    {"permissionId":33,"permissionKey":"PLANTILLAS_CENTRO_GESTION","roles":["Administrador"]},
    {"permissionId":34,"permissionKey":"PLANTILLAS_GLOBALES_LECTURA","roles":[]},
    {"permissionId":35,"permissionKey":"PLANTILLAS_CENTRO_LECTURA","roles":[]},
    {"permissionId":36,"permissionKey":"ACTAS_GESTION","roles":[]},
    {"permissionId":37,"permissionKey":"ACTAS_LECTURA","roles":[]}
  ]
}
```

El esquema actual no admite NULL en `permission_value`, pero el lector también tolera un valor NULL y devuelve `[]` sin requerir una migración. El CSV se presenta como arreglo, sin entradas vacías ni roles repetidos.

### POST `/api/v1/permissions/{permissionKey}/roles`

```json
{"roleName":"Administrador"}
```

Agrega un rol existente y activo del catálogo Authentication, conservando las asignaciones anteriores. Normaliza espacios externos, equivalencias Unicode y mayúsculas para comparar; guarda el nombre canónico del catálogo para el rol agregado. Si ya está asignado, devuelve 409 sin cambios.

Ejemplo 200 al agregar a ACTAS_GESTION vacío:

```json
{"isSuccess":true,"message":"Rol agregado correctamente.","result":{"permissionId":36,"permissionKey":"ACTAS_GESTION","roles":["Administrador"]}}
```

### PUT `/api/v1/permissions/{permissionKey}`

```json
{"roles":["Administrador","OTRO_ROL_EXISTENTE"]}
```

Todos los nombres deben existir y estar activos. `OTRO_ROL_EXISTENTE` es un marcador, no una concesión: actualmente el catálogo local solo contiene Administrador. Una solicitud válida con el catálogo actual es `{"roles":["Administrador"]}`. `{"roles":[]}` vacía el permiso. Ausencia/null de `roles` no equivale a un arreglo vacío.

Ejemplo 200 para vaciar:

```json
{"isSuccess":true,"message":"Roles actualizados correctamente.","result":{"permissionId":36,"permissionKey":"ACTAS_GESTION","roles":[]}}
```

Se valida la lista completa antes de escribir. Duplicados, roles desconocidos/inactivos, nombres vacíos, comas/caracteres de control, propiedades adicionales o valores que excedan la capacidad del CSV producen 400. Se preservan ID y clave.

## Errores y concurrencia

| HTTP | Motivo |
|---|---|
| 200 | Consulta o modificación confirmada |
| 400 | Body/rol/lista inválidos; no se aplica parte de la lista |
| 401 | JWT inválido, ausente o vencido; identidad incompleta |
| 403 | Usuario/rol no autorizado o asociación real deshabilitada |
| 404 | Clave inexistente o fuera de las seis definidas |
| 409 | POST repetido, deadlock o tiempo de espera del bloqueo MySQL |
| 500 | Fallo inesperado, sin detalles internos ni secretos |

```json
{"isSuccess":false,"message":"El rol ya está asignado al permiso.","result":null}
```

POST y PUT usan una transacción y `SELECT ... FOR UPDATE` sobre la misma fila. POST lee el valor vigente después de obtener el bloqueo y agrega el rol; las solicitudes concurrentes no sobrescriben asignaciones agregadas por otras solicitudes. PUT sustituye deliberadamente toda la lista: las sustituciones concurrentes se serializan y la última confirmada queda vigente. No es una edición con versión/ETag.

La autorización funcional de plantillas y actas sigue leyendo `permission_value` por solicitud. Una operación iniciada después del commit utiliza la nueva configuración, sin recompilar ni reiniciar.

## Implementación y verificación

Se reutilizan `MeetingsPermission`, `MeetingsDbContext`, normalización de roles, `IMeetingsIdentity`, `AuthenticationDirectory` y el formato de respuestas. Se agregan controlador, política, DTOs, aplicación y repositorio de permisos. Authentication solo recibe SELECT; no se añaden entidades Authentication a EF ni se cambia Authentication/Reservations/frontend. No hay migración.

Las pruebas HTTP aisladas cubren permisos vacíos/NULL, agregados, reemplazos, duplicados, validación completa, límites, claves inexistentes, JWT y denegación administrativa. También verifican que agregar/vaciar permisos mediante HTTP habilita/revoca inmediatamente creación de plantillas y actas.

Resultado del 19/11/2026: **Release con 0 errores y 0 advertencias; 155 pruebas aprobadas, 0 fallidas y 0 omitidas**, incluidas las dos pruebas MySQL y la comprobación del contrato Swagger de los tres endpoints. Se compararon las 26 tablas con la exportación previa: datos y estructura conservados. Authentication y la tabla de permisos permanecen idénticas, incluidos sus contadores. Solo avanzaron secuencias de las fixtures temporales de la prueba MySQL anterior. No quedó ninguna base temporal. Evidencia privada ignorada por Git: `artifacts/permissions-api-20261119/permissions-final.trx`, `preserved-tables.json` y `verification.json` en ese mismo directorio.

La prueba `PermissionMySqlTests` crea una base temporal `sigocalco_permissions_test_<GUID>` en la instancia local validada y copia únicamente la estructura de `sigocalco_meetings_permission`. Ejecuta los endpoints con el emisor JWT de Authentication y su directorio real de solo lectura. Comprueba persistencia, POST simultáneos, bloqueo de PUT y doce agregados distintos concurrentes. Los nombres artificiales de este último caso se usan exclusivamente como fixtures del repositorio aislado; no se crean roles en Authentication ni se envían esos nombres al API como concesiones válidas. El bloque finally elimina exclusivamente la base temporal creada por la prueba y compara las asignaciones reales antes/después.

Desde la raíz del monorepositorio:

```powershell
dotnet build .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release
dotnet test .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release
# Prueba MySQL aislada (requiere permiso local CREATE/DROP DATABASE):
$env:MEETINGS_APPROVED_PERMISSION_MYSQL='1'
$env:MEETINGS_MYSQL_LOCAL_SETTINGS=(Resolve-Path .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\appsettings.Local.json).Path
dotnet test .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release --filter FullyQualifiedName~PermissionMySqlTests
dotnet run --project .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj -c Release --no-build --launch-profile https
```

Swagger: `https://localhost:7298/swagger`. Usar Authorize con un JWT de Administrador. `Meetings.Api.http` incluye solicitudes y respuestas de los tres endpoints y errores. POST/PUT ejecutados manualmente sí cambian el permiso indicado; los ejemplos no fueron aplicados sobre las asignaciones reales para probar esta entrega. No se cambian configuraciones locales ni se envían correos.
