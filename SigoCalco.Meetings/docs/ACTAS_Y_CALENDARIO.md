# Actas y calendario — uso local

Los cinco endpoints usan `{ "isSuccess": true, "message": "...", "result": ... }`. Los errores conservan su código HTTP y usan `isSuccess: false`, mensaje en español y `result: null`. Los recursos de documentación Swagger/OpenAPI conservan su formato estándar.

| Método y ruta | Resultado |
|---|---|
| GET `/api/v1/acta-types` | 200, catálogo |
| POST `/api/v1/templates` | 201, identificador de plantilla |
| POST `/api/v1/templates/{templateId}/versions` | 201, versión y secciones |
| GET `/api/v1/templates` | 200, listado paginado con versiones |
| POST `/api/v1/actas` | 201, acta y reunión opcional; requiere `Idempotency-Key` UUID |

`POST /api/v1/meetings` no existe: no se crean reuniones independientes. Los IDs de los ejemplos son ilustrativos; use los devueltos por su instancia. Los participantes de ejemplo no reciben correos.

## Ejemplos completos

Todos los requests llevan `Authorization: Bearer <JWT de SigoCalco.Authentication>`. Los POST llevan `Content-Type: application/json`. No utilice un token de Microsoft Graph en Swagger.

**1. GET `/api/v1/acta-types`**, sin body. Respuesta 200:

```json
{
  "isSuccess": true,
  "message": "Operación realizada correctamente.",
  "result": [
    { "actaTypeId": 1, "name": "Acta de reunión" },
    { "actaTypeId": 2, "name": "Acta disciplinaria" },
    { "actaTypeId": 3, "name": "Acta administrativa" }
  ]
}
```

**2. POST `/api/v1/templates`**, body:

```json
{
  "actaTypeId": 1,
  "name": "Seguimiento de equipo",
  "scope": "GLOBAL",
  "solutionCenterCode": null
}
```

Respuesta 201:

```json
{"isSuccess":true,"message":"Plantilla creada correctamente.","result":{"templateId":1}}
```

**3. POST `/api/v1/templates/1/versions`**, body:

```json
{
  "sections": [
    {
      "title": "Información general",
      "contentType": "FORMULARIO",
      "sortOrder": 1,
      "isRequired": true,
      "configurationJson": {
        "fields": [
          {"key":"fecha","label":"Fecha","type":"date","required":true},
          {"key":"hora","label":"Hora","type":"time","required":true},
          {"key":"lugar","label":"Lugar","type":"text","required":true}
        ]
      }
    }
  ]
}
```

Respuesta 201:

```json
{
  "isSuccess": true,
  "message": "Versión creada correctamente.",
  "result": {
    "templateVersionId": 1,
    "versionNumber": 1,
    "createdByUser": 1,
    "createdAt": "2026-11-11T15:00:00Z",
    "sections": [
      {
        "templateSectionId": 1,
        "title": "Información general",
        "contentType": "FORMULARIO",
        "sortOrder": 1,
        "isRequired": true,
        "configurationJson": {
          "fields": [
            {"key":"fecha","label":"Fecha","type":"date","required":true},
            {"key":"hora","label":"Hora","type":"time","required":true},
            {"key":"lugar","label":"Lugar","type":"text","required":true}
          ]
        }
      }
    ]
  }
}
```

**4. GET `/api/v1/templates?page=1&pageSize=20&actaTypeId=1&scope=GLOBAL`**, sin body. Respuesta 200:

```json
{
  "isSuccess": true,
  "message": "Operación realizada correctamente.",
  "result": {
    "items": [
      {
        "templateId": 1,
        "actaTypeId": 1,
        "name": "Seguimiento de equipo",
        "scope": "GLOBAL",
        "solutionCenterCode": null,
        "createdByUser": 1,
        "createdAt": "2026-11-11T14:59:00Z",
        "versions": [
          {
            "templateVersionId": 1,
            "versionNumber": 1,
            "createdByUser": 1,
            "createdAt": "2026-11-11T15:00:00Z",
            "sections": [
              {
                "templateSectionId": 1,
                "title": "Información general",
                "contentType": "FORMULARIO",
                "sortOrder": 1,
                "isRequired": true,
                "configurationJson": {
                  "fields": [
                    {"key":"fecha","label":"Fecha","type":"date","required":true},
                    {"key":"hora","label":"Hora","type":"time","required":true},
                    {"key":"lugar","label":"Lugar","type":"text","required":true}
                  ]
                }
              }
            ]
          }
        ]
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

**5a. POST `/api/v1/actas` sin reunión**, header adicional `Idempotency-Key: 11111111-1111-4111-8111-111111111111`. Body:

```json
{
  "templateVersionId": 1,
  "title": "Seguimiento mensual",
  "documentDate": "2026-12-01",
  "solutionCenterCode": "R01",
  "sections": [
    {"templateSectionId":1,"contentJson":{"fecha":"2026-12-01","hora":"10:00","lugar":"Bogotá"}}
  ],
  "participants": [
    {"userName":"Persona de prueba","email":"person@example.invalid","participantType":"EXTERNO","userLogin":null}
  ],
  "meeting": null
}
```

Respuesta 201, una vez autorizado el permiso de creación:

```json
{
  "isSuccess":true,
  "message":"Acta creada. Consulte integration para conocer el estado externo; una simulación no envía invitaciones ni confirma reservas.",
  "result":{"actaId":1,"actaCode":"ACT-11111111111141118111111111111111","meetingId":null,"modality":null,"integration":null}
}
```

**5b. POST `/api/v1/actas` presencial**, header `Idempotency-Key: 22222222-2222-4222-8222-222222222222`. Body:

```json
{
  "templateVersionId":1,
  "title":"Seguimiento presencial",
  "documentDate":"2026-12-01",
  "solutionCenterCode":"R01",
  "sections":[{"templateSectionId":1,"contentJson":{"fecha":"2026-12-01","hora":"10:00","lugar":"Sala principal"}}],
  "participants":[{"userName":"Persona de prueba","email":"person@example.invalid","participantType":"EXTERNO","userLogin":null}],
  "meeting":{
    "modality":"PRESENCIAL",
    "startAt":"2026-12-01T10:00:00-05:00",
    "endAt":"2026-12-01T11:00:00-05:00",
    "timeZone":"America/Bogota",
    "locationDescription":"Sala principal",
    "externalRoomId":"ROOM-1"
  }
}
```

Respuesta 201:

```json
{
  "isSuccess":true,
  "message":"Acta creada. Consulte integration para conocer el estado externo; una simulación no envía invitaciones ni confirma reservas.",
  "result":{
    "actaId":2,"actaCode":"ACT-22222222222242228222222222222222","meetingId":1,"modality":"PRESENCIAL",
    "integration":{"provider":"RESERVATIONS","state":"SIMULADO","simulated":true,"externalId":"SIMULATED:ACT-22222222222242228222222222222222","teamsJoinUrl":null,"error":null}
  }
}
```

**5c. POST `/api/v1/actas` virtual**, header `Idempotency-Key: 33333333-3333-4333-8333-333333333333`. Body:

```json
{
  "templateVersionId":1,
  "title":"Seguimiento virtual",
  "documentDate":"2026-12-01",
  "solutionCenterCode":"R01",
  "sections":[{"templateSectionId":1,"contentJson":{"fecha":"2026-12-01","hora":"10:00","lugar":"Microsoft Teams"}}],
  "participants":[{"userName":"Persona de prueba","email":"person@example.invalid","participantType":"EXTERNO","userLogin":null}],
  "meeting":{
    "modality":"VIRTUAL",
    "startAt":"2026-12-01T10:00:00-05:00",
    "endAt":"2026-12-01T11:00:00-05:00",
    "timeZone":"America/Bogota",
    "locationDescription":null,
    "externalRoomId":null
  }
}
```

Respuesta 201 en modo simulado:

```json
{
  "isSuccess":true,
  "message":"Acta creada. Consulte integration para conocer el estado externo; una simulación no envía invitaciones ni confirma reservas.",
  "result":{
    "actaId":3,"actaCode":"ACT-33333333333343338333333333333333","meetingId":2,"modality":"VIRTUAL",
    "integration":{"provider":"MICROSOFT_GRAPH","state":"SIMULADO","simulated":true,"externalId":"SIMULATED:ACT-33333333333343338333333333333333","teamsJoinUrl":null,"error":null}
  }
}
```

En modo real, `state` será `SINCRONIZADO`, `simulated` será `false`, `externalId` será el ID recibido de Graph y `teamsJoinUrl` contendrá exclusivamente el enlace HTTPS recibido de `onlineMeeting.joinUrl`. No se inventa un enlace de ejemplo. El creador obtiene esa respuesta después de verificar JWT, identidad activa, rol, permiso de creación y centro. No se ofrece todavía un endpoint de lectura de actas compartidas.

Si falla el proveedor, el acta local ya existe: la respuesta continúa siendo 201 con `isSuccess:true`, `integration.state:"ERROR"`, `teamsJoinUrl:null` y una descripción segura en `integration.error`. No indica que la invitación o la reserva se hayan confirmado. Si el evento existe pero Teams aún no devuelve enlace, se conserva su ID en estado `PENDIENTE`.

Ejemplo completo de error 409 al reutilizar la clave de una operación ya guardada:

```json
{"isSuccess":false,"message":"La Idempotency-Key ya fue utilizada. El acta existente no se modifica ni se repite la integración.","result":null}
```

Los errores 400 (JSON/datos), 401 (JWT), 403 (permisos/centro), 404 (versión/ruta) y 500 (inesperado) conservan esa misma estructura. Las respuestas 405 y 415 también quedan uniformadas. Los errores 500 no exponen excepciones ni configuración.

## Permisos y contenido

La consulta de MySQL confirmó vacíos `ACTA_REUNION_CREACION`, `ACTA_DISCIPLINARIA_CREACION` y `ACTA_ADMINISTRATIVA_CREACION`. **No se cambiaron.** Propuesta pendiente: asignar `Administrador` únicamente a esas tres claves si se aprueba probar los tres tipos. No se conceden lectura, edición, acceso a otros centros ni permisos de administrador implícitos. Las pruebas conceden estos permisos solo en SQLite efímero.

El tipo se obtiene de la versión; el creador y organizador local proceden del JWT validado. El buzón Graph procede exclusivamente de configuración del servidor. Todo acta requiere un centro asignado al actor, incluso con plantilla GLOBAL, porque el modelo vigente exige `solution_center_code`. Una plantilla CENTRO debe coincidir con él. Disciplinaria exige GLOBAL.

Se conserva el contrato JSON anterior: FORMULARIO valida campos configurados, tipos, obligatoriedad, fechas/horas y claves desconocidas/duplicadas. TEXTO, LISTA, TABLA y PARTICIPANTES siguen reservados sin un contrato de contenido definido: solo permiten contenido nulo y sección no obligatoria. No se inventó un formato nuevo para ellos. Para crear actas con contenido utilice FORMULARIO; esos otros contratos requieren definición posterior.

Participantes: hasta 100, correo normalizado y único; se reutiliza el registro por correo sin sobrescribir su nombre o metadatos históricos. INTERNO requiere `userLogin`; EXTERNO lo requiere nulo. Esa clasificación es metadato del participante y no autentica a la persona ni concede acceso a actas. Los permisos de acceso compartido quedan fuera de este POST.

## Persistencia y recuperación

Sin cambios de esquema ni SQL incremental. Se conservan 17 tablas de Meetings y 8 de Authentication.

Se consultan `acta_type`, `template`, `template_version`, `template_section`, `permission` y los catálogos de estado. Se escriben `acta`, `acta_section`, `participant` y `acta_participant`; con reunión también `meeting` y, según modalidad, `meeting_reservation` o `meeting_calendar`. Los errores se registran en `log`. Todos estos nombres llevan prefijo `sigocalco_meetings_`. `acta_status` empieza en BORRADOR y `meeting_status` en PROGRAMADA: este último describe la programación local, no confirma un servicio externo.

Una transacción local guarda el conjunto completo. Después del commit, `IMeetingIntegrationService` llama al proveedor sin transacción MySQL abierta. El ID de operación se conserva en `acta_code`, protegido por índice único. Reintentar el POST con la misma clave da 409 y no repite efectos; cambiar la clave representa una nueva acta. Las colisiones concurrentes del catálogo de participantes se releen dentro de una nueva transacción.

El sincronizador adquiere un bloqueo MySQL `GET_LOCK` por reunión para coordinar varias instancias. Graph recibe el mismo `transactionId` en los reintentos; si existe `external_event_id`, se hace GET sobre ese evento, no otro POST. Los registros SIMULATED no se convierten en envíos reales al cambiar la configuración. La simulación persiste reserva SOLICITADA o calendario PENDIENTE, con ID prefijado `SIMULATED:`, sin fecha de confirmación ni enlace.

No hay reintentos automáticos ni envíos al iniciar el backend. Un operador con acceso a la configuración local puede recuperar exclusivamente una reunión identificada:

```powershell
Set-Location 'C:\Users\juan.zapata\source\repos\SigoCalco'
dotnet run --project .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj -c Release --no-build --launch-profile https -- --retry-meeting 123
```

El comando no inicia el servidor HTTP. Revisa el estado actual, respeta cancelaciones y no repite integraciones terminadas. Devuelve estado y código de salida 1 si sigue ERROR. Antes de repetir una operación ERROR/PENDIENTE, revise `last_error` y `log`, corrija la causa y respete cualquier `Retry-After`. Un timeout puede significar que Graph creó el evento aunque no recibimos respuesta: conserve la operación original y su transactionId; no cree otra acta para recuperarla. No borre IDs externos para forzar POST. Los errores de persistencia posteriores al envío requieren reconciliación operativa con el calendario antes de reintentos prolongados.

## Microsoft 365: configuración pendiente de autorización

Se eligió flujo de aplicación (`client_credentials`) con buzón organizador fijo. El login WordPress de SigoCalco no produce consentimiento ni token delegado de Microsoft. Usar permisos delegados requeriría un login/consentimiento Microsoft adicional y gestión de tokens de usuario; no se introdujo ese cambio. [Flujo oficial de Entra](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow).

Requisitos que debe preparar un administrador:

1. Aplicación de un solo tenant en Microsoft Entra ID, con service principal, Tenant ID, Client ID y credencial de aplicación vigente. Esta implementación usa client secret; guárdelo localmente o en el gestor de secretos del despliegue, nunca en Git.
2. Buzón de usuario corporativo en Exchange Online con capacidad/licencia y políticas de Teams habilitadas. Su calendario debe incluir `teamsForBusiness` en `allowedOnlineMeetingProviders`. Puede verificarse con `GET /v1.0/users/{organizador}/calendar?$select=allowedOnlineMeetingProviders,defaultOnlineMeetingProvider`. [Calendario y reuniones online](https://learn.microsoft.com/en-us/graph/outlook-calendar-online-meetings).
3. Acceso de aplicación al calendario. Recomendación para limitar el buzón: **Exchange Online RBAC for Applications**, service principal registrado en Exchange, un resource scope que incluya únicamente el organizador, y rol **Application Calendars.ReadWrite** asignado a ese scope por un administrador autorizado. Verificar con `Test-ServicePrincipalAuthorization` para el buzón permitido y para otro excluido. Este acceso es independiente de los permisos amplios de Entra; no dejar además una concesión global equivalente porque ambas autorizaciones se suman. [Guía oficial y comandos RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac).
4. Si el administrador elige el modelo de permisos Graph de Entra, el permiso requerido es **Microsoft Graph → Application → Calendars.ReadWrite**, con **consentimiento administrativo**. Esa concesión por sí sola cubre todos los buzones: el administrador debe resolver y comprobar su restricción antes de habilitar el proveedor. No basta con configurar `OrganizerEmail` en esta aplicación para restringir una credencial comprometida. Preferir RBAC con alcance en lugar de una concesión global innecesaria.
5. Para este flujo de creación de eventos no se solicita `Mail.Send` ni `OnlineMeetings.ReadWrite.All`: se usa la API de calendario con `isOnlineMeeting:true`, `onlineMeetingProvider:"teamsForBusiness"` y `attendees`. Crear un evento con asistentes envía invitaciones automáticamente; no hay aquí una segunda llamada de correo. [Permisos y comportamiento de Create event](https://learn.microsoft.com/en-us/graph/api/user-post-events?view=graph-rest-1.0).

El cliente llama únicamente a `login.microsoftonline.com` y `graph.microsoft.com/v1.0`, usando UTC para Graph y conservando la zona original en MySQL. Recupera `onlineMeeting.joinUrl` y usa `transactionId` para reintentos. [Contrato de event](https://learn.microsoft.com/en-us/graph/api/resources/event?view=graph-rest-1.0).

Agregue esta sección, conservando las secciones existentes, a `01.Meetings.Api/Meetings.Api/appsettings.Local.json` (ignorado por Git y excluido de publicación), o utilice User Secrets / variables de entorno equivalentes:

```json
{
  "Integrations": {
    "Calendar": {
      "Mode": "Graph",
      "OrganizerEmail": "SU_BUZON_CORPORATIVO",
      "TenantId": "GUID_DEL_TENANT",
      "ClientId": "GUID_DE_LA_APLICACION",
      "ClientSecret": "VALOR_LOCAL_NO_VERSIONAR",
      "AllowExternalInvitations": false
    }
  }
}
```

La configuración versionada mantiene `Mode: "Simulated"`, un correo `.invalid` y `AllowExternalInvitations: false`. En Development se carga el archivo local, luego User Secrets y variables de entorno; fuera de Development utilice el gestor de secretos/variables del despliegue. Los nombres de entorno son, por ejemplo, `Integrations__Calendar__ClientSecret`. No pegue secretos en el chat, archivos `.http`, argumentos compartidos ni logs.

Para la primera prueba real falta: aprobación de los tres permisos de actas que se necesiten, configuración administrativa de Entra/Exchange, verificación del buzón Teams y autorización expresa del envío. Después, habilitar localmente `AllowExternalInvitations:true`, iniciar Meetings, usar únicamente su cuenta corporativa autorizada como asistente de prueba y crear una nueva acta virtual con UUID nuevo. Verificar evento, invitación, enlace y estado guardado; volver a deshabilitar envíos al terminar. Nunca reutilizar los correos `.invalid` de estos ejemplos en modo Graph.

## Ejecución y verificación

Desde la raíz `C:\Users\juan.zapata\source\repos\SigoCalco`, en terminales separadas:

```powershell
dotnet run --project .\SigoCalco.Authentication\01.Authentication.Api\Authentication.Api\Authentication.Api.csproj --launch-profile Authentication.Api
dotnet run --project .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj -c Release --no-build --launch-profile https
```

Swagger Meetings: `https://localhost:7298/swagger`. Swagger Authentication: `https://localhost:64845/swagger`. En Meetings use Authorize con el JWT que obtenga manualmente de Authentication. No se realizó un login nuevo ni se solicitó un token de usuario. La compatibilidad JWT existente se conserva; validación de sesiones por inactividad/logout/revocación continúa pendiente del contrato aprobado con Authentication.

```powershell
dotnet build .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release --no-restore
dotnet test .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release --no-build --no-restore
```

Validación realizada: Release con 0 advertencias/0 errores; 91 pruebas aprobadas, 0 omitidas al habilitar expresamente el caso MySQL. Incluye las 59 pruebas anteriores y 32 adicionales; ninguna llamada real a Graph ni correo. Las nuevas creaciones y fallos transaccionales se ejercitan en SQLite efímero; MySQL valida el esquema, los cuatro endpoints previos, su concurrencia y el 403 del nuevo POST con los permisos reales todavía vacíos. No se afirma una creación exitosa de actas en MySQL antes de autorizar esos permisos.

La prueba MySQL utiliza solo su conjunto temporal previamente autorizado (una plantilla, dos versiones y dos secciones) y los elimina por ID. No se repitió la migración, no hubo SQL de esquema ni concesiones nuevas. Authentication y Reservations no se modificaron; no se hizo commit ni push.

Swagger se verificó en ejecución real: UI HTTP 200 y cinco operaciones con el wrapper documentado. Se detuvo el proceso de prueba al finalizar. Reporte de pruebas: `artifacts/test-results/actas-final.trx`.

Comparación con el respaldo histórico de la migración: las ocho estructuras de Authentication coinciden, normalizando únicamente el contador AUTO_INCREMENT. Seis tablas coinciden también por SHA256 completo. `log` y `sessions` contienen actividad posterior a aquel respaldo y anterior a esta ampliación (última actividad de sesión: 11/11/2026 22:42 UTC; primeros cambios de esta ampliación: 12/11/2026 00:10 UTC). No se revirtieron esos datos ni se afirma igualdad histórica de las ocho copias. También se preservó la plantilla existente del usuario; solo se eliminaron los IDs creados por la prueba autorizada.
