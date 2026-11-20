# Entrega: seis permisos, Mis plantillas y SMTP

Actualización del 20/11/2026: [MIS_ACTAS.md](MIS_ACTAS.md) describe los estados, edición/asistencia y reservas opcionales. Sustituye la regla histórica de correo de las secciones 9 y 11: ahora se notifica a los participantes explícitos al crear cualquier acta, incluso sin reunión, y el creador no se añade automáticamente como destinatario.

Actualización del 19/11/2026: la administración de las asignaciones de estos seis permisos mediante GET/POST/PUT está documentada en [PERMISSIONS_API.md](PERMISSIONS_API.md). Solo el rol real Administrador puede utilizarla; no cambia las definiciones ni introduce nuevas claves.

Esta entrega sustituye los contratos anteriores de permisos por tipo de acta y la integración Graph. Solo cambia Meetings. No modifica Authentication, Reservations ni React. No ejecuta login ni envía correos reales. Todos los endpoints requieren el JWT existente de Authentication y conservan `isSuccess`, `message`, `result` y los códigos HTTP 200/201/400/401/403/404/409/500.

## 1. Componentes

Se reutilizan `TemplateApplication`, `TemplateStore`, `ActaApplication`, `ActaStore`, validación JSON, JWT, directorio Authentication de solo lectura, inmutabilidad de versiones, transacciones y reserva simulada. `MeetingsPermissions` concentra las seis claves y las reglas de alcance; no contiene nombres de roles. Los valores se consultan en cada operación, sin recompilación ni caché de concesiones.

Se añaden creación completa de plantilla, consultas de versiones, actualización de nombre, composición de notificaciones y proveedor SMTP MailKit/MimeKit. Se retiran `GraphCalendarProvider`, `GraphAccessTokenProvider`, `IMeetingCalendarProvider`, `SimulatedCalendarProvider`, `CalendarOptions` y sus pruebas exclusivas. `MeetingIntegrationService` conserva únicamente reservas presenciales.

## 2. Migración aplicada

Instancia autorizada: `127.0.0.1:3306/bdcalco_sigocalco`, equipo `COMDEATECL33`, MySQL 8.0.46. Scripts:

- `scripts/sql/04_add_meeting_notifications.sql`: una tabla nueva y tres índices; FK únicamente a Meetings.
- `scripts/sql/05_six_permissions_and_active_types.sql`: seis claves, retiro de 19 anteriores y eliminación del tipo disciplinario solo cuando no tiene referencias.
- `scripts/Invoke-NextDelivery.ps1`: verifica instancia, estado inicial, respaldo, dependencias y resultados; rechaza estados parciales y no repite una entrega ya aplicada.
- `scripts/Verify-Meetings.ps1`: comprobación de las 26 tablas, catálogos y seis claves.

Respaldo privado: `artifacts/mysql-before-six-permissions-smtp-20261118/`. Se verificó mediante una segunda exportación completa idéntica por SHA256, presencia de las 25 tablas originales y comparación separada de las ocho tablas Authentication. No se restauró el respaldo sobre otra base ni se ejecutó DDL remoto.

Resultado: **26 tablas: 8 Authentication y 18 Meetings**. La tabla adicional `sigocalco_meetings_meeting_notification` es necesaria para registrar atómicamente los destinatarios, el contenido y el estado antes de SMTP; un simple log posterior no permitiría detectar un envío interrumpido ni evitar duplicados. Índices únicos por `(meeting_id, recipient_email)` y `message_id`.

`sigocalco_meetings_meeting_calendar` se conserva por su registro histórico. No se consulta ni escribe en los flujos operativos nuevos. Tampoco se crean notificaciones retroactivas para reuniones antiguas. Las tres actas y demás registros previos se preservan. MySQL confirma DDL implícitamente: ante una ejecución parcial, inspeccionar qué objetos existen y recuperar solo la operación pendiente; nunca repetir la sustitución completa ni asumir rollback del DDL.

Los scripts 01/02 y `migration-manifest.json` pertenecen a la migración histórica. `Invoke-Migration.ps1` queda deshabilitado para evitar reconstrucciones accidentales. El seed 03 fue actualizado para instalaciones nuevas: dos tipos y seis claves; no se ejecutó para reconstruir esta base.

## 3. Tipos de acta

| ID conservado | Código | Alcances |
|---|---|---|
| 1 | REUNION | GLOBAL / CENTRO |
| 3 | ADMINISTRATIVA | GLOBAL / CENTRO |

DISCIPLINARIA tenía cero referencias en plantillas, versiones y actas y se retiró del catálogo. No se renumeraron IDs. El backend también excluye tipos retirados si otra instalación tuviera historia que impidiera borrarlos; en ese caso la eliminación requeriría una decisión específica.

## 4. Permisos actuales

| Número | Clave exacta | Valor en MySQL |
|---|---|---|
| 1 | PLANTILLAS_GLOBALES_GESTION | Administrador |
| 2 | PLANTILLAS_GLOBALES_LECTURA | vacío |
| 3 | PLANTILLAS_CENTRO_GESTION | Administrador |
| 4 | PLANTILLAS_CENTRO_LECTURA | vacío |
| 5 | ACTAS_GESTION | vacío |
| 6 | ACTAS_LECTURA | vacío |

Solo existe el rol activo `Administrador`. `Administrador Centro` no existe y no se creó ni se concedió. Cuando se defina en Authentication, su inclusión en el permiso 3 necesitará la configuración autorizada correspondiente.

Los nueve permisos antiguos de plantillas se sustituyen por gestión GLOBAL/CENTRO según el alcance. Las tres concesiones anteriores de creación de actas no se transfieren a ACTAS_GESTION, que tiene un significado más amplio. Los permisos antiguos de lectura/edición y VIEW_ALL_SOLUTION_CENTERS también se retiran. No había FK, vistas, triggers ni rutinas que referenciaran la tabla de permisos. Las concesiones anteriores permanecen recuperables en el respaldo.

Consecuencia deliberada: `Administrador` puede gestionar plantillas GLOBAL y las CENTRO de sus centros asignados, y leer plantillas de todos los centros; actualmente recibe 403 al crear actas porque ACTAS_GESTION está vacío. No se asignaron valores a los permisos 2, 4, 5 o 6.

## 5. Endpoints

| Método | Ruta | Estado y resultado |
|---|---|---|
| GET | `/api/v1/acta-types` | Existente adaptado; 200, los dos tipos vigentes |
| GET | `/api/v1/templates` | Existente adaptado; 200, filtros y paginación autorizada |
| POST | `/api/v1/templates` | Conservado por compatibilidad; 201, plantilla sin primera versión |
| POST | `/api/v1/templates/with-version` | Nuevo; 201, plantilla + versión 1 + secciones |
| GET | `/api/v1/templates/{templateId}/versions` | Nuevo; 200, todas las versiones con secciones |
| GET | `/api/v1/template-versions/{templateVersionId}` | Nuevo; 200, plantilla, versión y configuración |
| POST | `/api/v1/templates/{templateId}/versions` | Adaptado; 201, siguiente versión y sus secciones |
| PUT | `/api/v1/templates/{templateId}` | Nuevo; 200, metadatos actualizados |
| POST | `/api/v1/actas` | Adaptado; 201 si ACTAS_GESTION autoriza; 403 con la configuración actual |

PUT acepta exclusivamente `{ "name": "Nuevo nombre" }`. Rechaza propiedades desconocidas como `actaTypeId`, `scope`, `solutionCenterCode` o `sections` con 400. No modifica el tipo, el centro, el alcance, los creadores ni versiones históricas.

## 6. Creación completa

Consultar primero el catálogo y usar sus IDs reales. Todos los POST llevan `Content-Type: application/json` y `Authorization: Bearer <JWT>`. Ejemplo:

```http
POST /api/v1/templates/with-version
Authorization: Bearer <JWT_DE_AUTHENTICATION>
Content-Type: application/json

{
  "actaTypeId": 1,
  "name": "Acta general",
  "scope": "GLOBAL",
  "solutionCenterCode": null,
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

Respuesta 201 ilustrativa:

```json
{
  "isSuccess":true,
  "message":"Plantilla y primera versión creadas correctamente.",
  "result":{
    "templateId":20,
    "templateVersionId":30,
    "versionNumber":1,
    "sections":[{
      "templateSectionId":40,
      "title":"Información general",
      "contentType":"FORMULARIO",
      "sortOrder":1,
      "isRequired":true,
      "configurationJson":{"fields":[
        {"key":"fecha","label":"Fecha","type":"date","required":true},
        {"key":"hora","label":"Hora","type":"time","required":true},
        {"key":"lugar","label":"Lugar","type":"text","required":true}
      ]}
    }]
  }
}
```

Para CENTRO, enviar `scope:"CENTRO"` y un código incluido en los centros validados del actor. Por ejemplo `19` solo si está realmente asignado. Un código enviado por el cliente no concede acceso. El creador siempre se obtiene del JWT validado. Se comparte la validación de plantilla/secciones con los endpoints existentes y una transacción guarda todo; cualquier fallo revierte plantilla, versión y secciones.

## 7. Versiones

```http
GET /api/v1/templates/20/versions
Authorization: Bearer <JWT_DE_AUTHENTICATION>

GET /api/v1/template-versions/30
Authorization: Bearer <JWT_DE_AUTHENTICATION>
```

El primer GET devuelve `result` como lista ordenada por `versionNumber`. Cada elemento tiene `templateVersionId`, `versionNumber`, `createdByUser`, `createdAt` y `sections` con el contrato del ejemplo anterior.

El detalle devuelve `result` con `templateId`, `name`, `actaTypeId`, `actaTypeName`, `scope`, `solutionCenterCode`, `templateVersionId`, `versionNumber`, `createdByUser`, `createdAt` y `sections`. Ambas rutas comprueban el permiso y el alcance de la plantilla, incluyendo el centro.

Nueva versión usando el contrato FORMULARIO existente:

```http
POST /api/v1/templates/20/versions
Authorization: Bearer <JWT_DE_AUTHENTICATION>
Content-Type: application/json

{
  "sections":[{
    "title":"Información y restricciones",
    "contentType":"FORMULARIO",
    "sortOrder":1,
    "isRequired":true,
    "configurationJson":{"fields":[
      {"key":"detalle","label":"Detalle","type":"text","required":true}
    ]}
  }]
}
```

Respuesta 201: `result` incluye el nuevo ID, `versionNumber:2`, fecha, creador y secciones con sus IDs. El número se asigna bajo bloqueo de la fila padre y transacción; no se modifica la versión 1 ni las actas que la usan. No se inventan formatos para TEXTO, LISTA, TABLA o PARTICIPANTES: siguen admitiendo solo configuración/contenido nulos y sección no obligatoria hasta definir sus contratos.

## 8. Mis plantillas y diagramas

`GET /api/v1/templates?page=1&pageSize=20&scope=CENTRO&actaTypeId=1` filtra dentro del conjunto autorizado. `page >= 1`, `pageSize` entre 1 y 100; tipos retirados/inexistentes y scope inválido producen 400.

| Permiso | Listado visible | Gestión |
|---|---|---|
| 1 | GLOBAL y CENTRO de todos los centros | Solo GLOBAL; CENTRO requiere además permiso 3 y pertenencia |
| 2 | GLOBAL | Ninguna |
| 3 | CENTRO de los centros asignados al actor | Solo esos centros |
| 4 | CENTRO de los centros asignados al actor | Ninguna |

Se combinan permisos mediante una condición de visibilidad única, sin duplicados. `totalCount` y `totalPages` cuentan solo las plantillas autorizadas después de filtros. El listado conserva versiones/secciones anidadas por compatibilidad y usa consultas de cantidad constante, sin N+1. Respuesta vacía válida:

```json
{"isSuccess":true,"message":"Plantillas consultadas correctamente.","result":{"items":[],"page":1,"pageSize":20,"totalCount":0,"totalPages":0}}
```

Los diagramas quedan cubiertos: filtros GLOBAL/CENTRO y tipo, paginación, visibilidad por permiso, Crear completo, Versiones, Nueva versión y detalle por ID. Mis actas/Mis compartidas se mantienen como navegación futura; no se implementa frontend ni CRUD adicional.

## 9. Actas

POST `/api/v1/actas` conserva DTO, participantes reutilizables, JSON, centro, transacción, reunión opcional e `Idempotency-Key` UUID. Requiere ACTAS_GESTION, incluso para Administrador. ACTAS_LECTURA nunca permite crear; ninguno implica acceso universal a actas privadas. No se modifican las tablas de compartición ni se introducen nuevas operaciones de lectura/edición de actas.

La respuesta conserva `actaId`, `actaCode`, `meetingId`, `modality`, `integration` y añade `notifications`. Sin reunión, ambos campos son nulos y no hay registros de notificación. Presencial conserva `integration.provider:"RESERVATIONS"`, `state:"SIMULADO"`. Virtual devuelve `integration:null`: no se crea calendario ni enlace. Ejemplo del resultado de una reunión virtual en simulación:

```json
{
  "isSuccess":true,
  "message":"Acta creada. Consulte integration y notifications: una simulación no envía correos ni confirma reservas; ENVIADO indica aceptación SMTP, no entrega final.",
  "result":{
    "actaId":50,"actaCode":"ACT-11111111111141118111111111111111","meetingId":60,"modality":"VIRTUAL","integration":null,
    "notifications":{"state":"SIMULADO","simulated":true,"smtpAccepted":false,"items":[{"notificationId":70,"state":"SIMULADO","attempts":1,"error":null}]}
  }
}
```

Una clave de idempotencia repetida devuelve 409 sin nueva acta, reunión ni correo. Si SMTP falla después del commit, se conserva 201 para la creación local y `notifications` informa ERROR, INCIERTO o PARCIAL; `smtpAccepted` no será true si no fueron aceptados todos los mensajes. No confundir `isSuccess:true` del acta con éxito del correo.

## 10. Graph retirado

No quedan clientes HTTP, adquisición de tokens Entra, eventos Outlook, simulación Graph ni generación de enlaces Teams. Se retira `Integrations:Calendar` de las configuraciones versionables. La configuración local existente no contenía esa sección y se conservó. La entidad/mapping `MeetingCalendar` queda únicamente para compatibilidad estructural y conservación del registro histórico, fuera de DTO y servicios operativos.

## 11. SMTP

Implementado con MailKit 5.0.0 y MimeKit.Core transitivo. Conecta con `SecureSocketOptions.StartTls`, puerto 587 y timeout de 15 segundos, propaga cancelación y mantiene la validación normal de certificados. No hay callbacks para aceptar certificados inválidos. [API MailKit](https://mimekit.net/docs/html/M_MailKit_Net_Smtp_SmtpClient_ConnectAsync_2.htm).

Configuración de ejemplo, ya incluida en `appsettings.Local.example.json`:

```json
{
  "Smtp":{
    "Enabled":false,
    "Host":"smtp.gmail.com",
    "Port":587,
    "Username":"notificaciones@crepesywafflesantioquia.com",
    "Password":"",
    "FromEmail":"notificaciones@crepesywafflesantioquia.com",
    "FromName":"SigoCalco - Actas y Reuniones",
    "ReplyTo":"",
    "UseStartTls":true,
    "TimeoutSeconds":15
  }
}
```

`Enabled:false` selecciona el proveedor simulado: registra SIMULADO sin conexión SMTP. No necesita contraseña. El proveedor real también comprueba ese interruptor antes de abrir una conexión. Para una prueba real posterior, configure una credencial autorizada exclusivamente en `appsettings.Local.json`, User Secrets o `Smtp__Password`; habilite el envío solo con autorización y destinatarios acordados. El archivo local sigue ignorado y excluido de publicación; el ejemplo puede versionarse. User Secrets y variables tienen prioridad sobre el archivo local en Development.

Los destinatarios son el organizador obtenido del directorio Authentication validado (SELECT de nombre y correo, sin cambiar JWT) y los participantes explícitos del acta. Se validan y normalizan correos, se eliminan duplicados y no se agregan terceros. Si falta un correo válido, no se inventa; sin destinatarios la respuesta informa SIN_DESTINATARIOS. Se genera un mensaje por destinatario, sin exponer los demás en To/Cc/Bcc.

Texto plano y HTML incluyen título, código de acta, inicio/fin en la zona horaria, modalidad, lugar presencial y organizador disponible. El HTML escapa contenido del usuario. Se guarda una copia estable del contenido y Message-ID para mantener trazabilidad.

| Estado persistido | Significado |
|---|---|
| PENDIENTE | Guardado en la transacción local, todavía no procesado |
| PROCESANDO | Una instancia adquirió el envío mediante UPDATE condicional atómico |
| SIMULADO | Prueba sin red; no se convierte automáticamente en envío real |
| ENVIADO | SMTP aceptó el mensaje; **no acredita entrega al destinatario** |
| ERROR | Fallo conocido; puede reintentarse de forma controlada tras corregir la causa |
| INCIERTO | No se puede saber si SMTP aceptó; no se reenvía automáticamente |

Cada destinatario tiene registro único y contador de intentos. La transacción del acta termina antes del envío. El cambio a PROCESANDO se confirma antes de SMTP; ENVIADO solo se registra después de aceptación. Un cierre interrumpido permanece PROCESANDO y exige reconciliación manual, igual que INCIERTO. SMTP no ofrece una garantía general de entrega exactamente una vez; un Message-ID estable ayuda a identificar mensajes, pero no se presenta como garantía de deduplicación del servidor.

Recuperación explícita para un registro PENDIENTE/ERROR (no inicia el servidor HTTP):

```powershell
dotnet run --project .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj -c Release --no-build --launch-profile https -- --retry-notification 70
```

ENVIADO/SIMULADO/PROCESANDO/INCIERTO no se reenvían con ese comando. Para PROCESANDO o INCIERTO primero debe comprobarse la aceptación con el administrador del correo; no hay reset automático ni endpoint de reenvío masivo. La recuperación anterior `--retry-meeting ID` permanece solo para reservas presenciales. No se introduce broker ni servicio externo de notificaciones.

## 12. Pruebas

Se conservan los casos vigentes, se adaptan los afectados por catálogo/permisos y se eliminan solo los específicos de Graph. Se prueban las seis claves, visibilidad y escritura por alcance, combinaciones sin duplicados, permisos dinámicos/vacíos/ausentes, filtros, conteos, endpoints por ID, creación completa y rollback, versiones históricas, metadatos protegidos, actas, simulación, STARTTLS obligatorio, MIME, destinatarios, fallos y recuperación sin duplicados.

MySQL se verifica contra el modelo EF completo: columnas, tipos, nulabilidad, FK e índices. Las pruebas HTTP recorren los nueve endpoints (POST actas devuelve 403 con el permiso real vacío). Las pruebas de persistencia crean temporalmente actas sin reunión/presenciales/virtuales y notificaciones simuladas directamente mediante los servicios de persistencia, sin otorgar ACTAS_GESTION en MySQL. Comprueban rollback real por una restricción de notificaciones y reutilización de participantes. Todos los registros temporales se limpian por IDs propios y correo único de prueba. No se afirma un POST de actas exitoso con los permisos reales actuales.

Resultado final: Release sin errores ni advertencias; **116 pruebas aprobadas, 0 fallidas y 0 omitidas**, incluida MySQL. Swagger publicado respondió 200, mostró nueve operaciones y contratos de éxito con `isSuccess/message/result`; las nueve rutas respondieron 401 uniforme sin JWT. El host temporal se detuvo al terminar. La comprobación HTTP tuvo que ejecutarse fuera del aislamiento porque Windows impedía escribir en EventLog; no se modificó la configuración real para resolverlo.

Authentication conserva las ocho tablas y datos exactamente por SHA256. Además, se compararon las 23 tablas previas ajenas a los dos catálogos migrados: datos idénticos y estructura idéntica, excluyendo únicamente los siguientes valores AUTO_INCREMENT consumidos por las pruebas. Se conservan las tres actas y el registro histórico de calendario; no quedaron notificaciones temporales.

Resultados y evidencia: `artifacts/test-results/six-permissions-smtp-final.trx`, `artifacts/http-final-verification.json`, `artifacts/preserved-tables-final.json` y `artifacts/six-permissions-smtp-verification.json`. Ninguna prueba envía correo real ni realiza llamadas a Graph.

## 13. Swagger y ejecución

Desde `C:\Users\juan.zapata\source\repos\SigoCalco`:

```powershell
dotnet build .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release
dotnet test .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release
dotnet run --project .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj -c Release --no-build --launch-profile https
```

Swagger: `https://localhost:7298/swagger`. Configurar conexión a la base migrada y Secret/Issuer/Audience compatibles con Authentication; usar Authorize con un JWT obtenido por el usuario. No se solicita ni almacena un token real en los ejemplos. `Meetings.Api.http` incluye los ocho flujos solicitados, el POST legado y las tres variantes de acta. Usar los IDs devueltos y centros realmente asignados.

## 14. Pendientes externos

- Roles autorizados para permisos 2, 4, 5 y 6, actualmente vacíos. Sin decisión adicional no se concede ACTAS_GESTION.
- Existencia/configuración de `Administrador Centro`, fuera de Meetings.
- Credencial SMTP autorizada y autorización de la primera prueba real con destinatarios acordados; SMTP permanece deshabilitado.
- Contratos definitivos de los tipos de sección distintos de FORMULARIO.
- Si se decide eliminar el calendario histórico, autorización específica y tratamiento de su registro; conservarlo no bloquea esta entrega.
- Inactividad/logout/revocación de sesiones sigue dependiendo del contrato pendiente con Authentication; la validación JWT actual se conserva.
