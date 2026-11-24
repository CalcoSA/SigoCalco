# Mis actas: entrega del 20/11/2026

Actualización del 23/11/2026: el GET por ID también admite participantes internos de otros creadores en modo lectura. Véase [MIS_COMPARTIDAS.md](MIS_COMPARTIDAS.md) para la autorización y el procedimiento vigente. Los valores de permisos y conteos descritos abajo corresponden a la entrega del 20/11, no sustituyen la configuración dinámica actual.

Se reutilizan JWT e identidad validada, las seis claves y su administración, las plantillas/versiones inmutables, validación JSON, creación transaccional, participantes reutilizables, Idempotency-Key, MailKit y los proveedores simulados. No se modificaron Authentication, Reservations, React ni las configuraciones privadas. No se enviaron correos reales.

## Autorización y estados

La gestión y el listado propio requieren `ACTAS_GESTION`. El listado filtra por `created_by_user` en SQL y sus totales corresponden al usuario. Las mutaciones verifican propiedad. El GET por ID admite al creador autorizado o al participante interno de otro creador; una persona ajena recibe 404, igual que ante un acta inexistente. El centro y el rol Administrador no sustituyen estas condiciones. `ACTAS_LECTURA` no concede acceso general ni es necesario para leer por participación.

Las seis asignaciones reales permanecen sin cambios: gestión de plantillas GLOBAL/CENTRO contiene Administrador; los otros cuatro permisos, incluido ACTAS_GESTION, continúan vacíos. No se necesita otra decisión para administrar permisos: los tres endpoints anteriores siguen reservados al Administrador real, sin cambios en esta entrega. Un administrador deberá realizar la asignación funcional autorizada antes de las pruebas manuales de actas.

Catálogo aplicado: **1 BORRADOR, 5 ANULADA, 6 PUBLICADA**. Se conservaron los IDs 1 y 5. ABIERTA, EN_REVISION y CERRADA no tenían actas asociadas y se retiraron. Las tres actas existentes siguen en BORRADOR.

| Origen | Destino permitido |
|---|---|
| BORRADOR | PUBLICADA o ANULADA |
| PUBLICADA | ANULADA |
| ANULADA | Ninguno |

La creación fija BORRADOR en persistencia; el cliente no puede elegir estado. Una transición repetida, regresar a BORRADOR o salir de ANULADA devuelve 409. Un nombre de estado inválido devuelve 400. Edición, participantes, asistencia y estado bloquean la misma fila del acta en una transacción MySQL. El estado se vuelve a validar después del bloqueo, de modo que publicar/anular y editar no pueden saltarse las restricciones concurrentemente.

## Endpoints

| Método/ruta | Comportamiento |
|---|---|
| POST `/api/v1/actas` | Existente adaptado; 201, BORRADOR, reunión/reserva opcionales y notificaciones |
| GET `/api/v1/actas?page=1&pageSize=20` | Nuevo; listado propio, paginación y total autorizado |
| GET `/api/v1/actas/{actaId}` | Detalle propio autorizado o compartido por participación, con plantilla/versión original, secciones, participantes y reunión/reserva |
| GET `/api/v1/actas/shared-with-me` | Actas de otros creadores donde el usuario es participante interno; solo lectura |
| PUT `/api/v1/actas/{actaId}` | Nuevo; reemplaza título, fecha y contenido en BORRADOR |
| PUT `/api/v1/actas/{actaId}/participants` | Nuevo; reemplaza asociaciones en BORRADOR |
| PUT `/api/v1/actas/{actaId}/participants/{participantId}/attendance` | Nuevo; true/false/null en BORRADOR |
| PATCH `/api/v1/actas/{actaId}/status` | Nuevo; transición atómica |
| GET `/api/v1/actas/available-templates` | Nuevo; plantillas visibles con última versión, filtros y paginación |

Los siete endpoints nuevos devuelven 200 con `isSuccess`, `message`, `result`. Los errores usan `isSuccess:false`, `result:null`: 400 para solicitudes inválidas, 401 para JWT inválido, 403 para falta de permiso, 404 para recurso inexistente/ajeno o participante no asociado, 409 para estado/concurrencia/idempotencia y 500 para un fallo inesperado sin detalles internos.

El listado devuelve `result.items`, `page`, `pageSize`, `totalCount`, `totalPages`. Cada elemento contiene `actaId`, `actaCode`, `title`, `documentDate`, `actaTypeId`, `actaTypeName`, `actaStatusId`, `status`, `solutionCenterCode`, `createdByUser`, `createdAt`, `updatedAt`, `hasMeeting` y `modality`. Orden descendente por actaId, page >= 1, pageSize entre 1 y 100, snapshot consistente para listado/conteos.

El detalle tiene `result.acta` con esos datos generales, `templateId`, `templateName`, `templateVersionId`, `versionNumber`, `sections`, `participants` y `meeting`. Cada sección incluye configuración y `contentJson` de la versión original; las opcionales sin contenido aparecen con null. La reunión muestra modalidad, horarios UTC, zona, organizador, lugar y `reservation` opcional; una reserva simulada tiene `simulated:true` y no representa una confirmación real.

## Edición, participantes y asistencia

PUT de contenido admite únicamente:

```json
{
  "title":"Seguimiento actualizado",
  "documentDate":"2026-12-02",
  "sections":[{
    "templateSectionId":123,
    "contentJson":{"fecha":"2026-12-02","hora":"11:00","lugar":"Oficina"}
  }]
}
```

Usar IDs reales del detalle y contenido compatible con su configuración. Se reemplazan las secciones del acta dentro de la misma transacción, conservando la versión original. No admite ID, código, creador, fecha de creación, estado, centro, reunión, participantes ni otra versión en ese body. Una sección inválida no produce una edición parcial. La reunión y la reserva no se reprograman al cambiar título/fecha del documento: esa operación no forma parte de este contrato.

Reemplazo de participantes:

```json
{"participants":[{"userName":"Invitado","email":"person@example.invalid","participantType":"EXTERNO","userLogin":null}]}
```

Se reutiliza el catálogo por correo validado, sin sobrescribir la identidad histórica de una persona existente. Se cambia la pertenencia al acta; las relaciones conservadas mantienen su asistencia. Las nuevas relaciones empiezan pendientes. `participants:[]` elimina únicamente las asociaciones de esta acta; no borra personas ni modifica otras actas. Los correos inválidos/duplicados se rechazan conforme a la validación existente. No se generan notificaciones nuevas al editar contenido, participantes o asistencia.

Asistencia, usando el `participantId` del detalle:

```json
{"attended":true}
```

`true` asistió, `false` no asistió, `null` pendiente. La propiedad es obligatoria aunque su valor pueda ser null. La información se guarda en `sigocalco_meetings_acta_participant.attended`, nunca en el catálogo general. Reincorporar una persona previamente retirada crea una asociación pendiente, sin inventar su asistencia.

Publicar/anular:

```json
{"status":"PUBLICADA"}
```

```json
{"status":"ANULADA"}
```

Cambiar estado no modifica secciones, participantes, versiones ni reserva. Anular no ejecuta una cancelación automática de sala o correos, porque no se ha definido esa regla.

## Cuatro modalidades de creación

El body base de POST conserva `templateVersionId`, `title`, `documentDate`, `solutionCenterCode`, `sections`, `participants`, `meeting` y el encabezado UUID `Idempotency-Key`. Añade `status:"BORRADOR"` en la respuesta. Se rechazan campos ajenos al contrato, incluidos estado/creador enviados por el cliente.

| Caso | meeting/reserva | Resultado |
|---|---|---|
| Sin reunión | `meeting:null` | Sin reunión ni reserva; notifica participantes |
| Virtual | `modality:"VIRTUAL"`, sin sala | Con reunión, sin reserva/Graph/Teams |
| Presencial sin reserva | `reserveRoom:false`, `externalRoomId:null` | Lugar descriptivo opcional, sin fila de reserva ni llamada al proveedor |
| Presencial con reserva | `reserveRoom:true`, `externalRoomId` seleccionado | Fila de reserva e integración simulada existente |

Si se omite `reserveRoom`, un `externalRoomId` presente conserva el comportamiento anterior de solicitar reserva; sin sala no se solicita. `reserveRoom:true` requiere modalidad PRESENCIAL y sala. `reserveRoom:false` con una sala enviada se rechaza como contradictorio. Una reunión virtual no admite reserva. No se inventan salas ni enlaces. Horarios y zona mantienen sus validaciones.

POST exige también centro autorizado y visibilidad de la plantilla seleccionada; enviar su ID directamente no elude los permisos de lectura GLOBAL/CENTRO. La edición de una acta propia mantiene la versión histórica aunque aparezcan versiones nuevas.

## Plantillas disponibles

Reutiliza la consulta existente y sus reglas de visibilidad. Acepta page/pageSize, scope y actaTypeId; requiere ACTAS_GESTION y permiso de plantilla adecuado. La carga de versiones usa un Include limitado a la última versión por plantilla. El GET general de plantillas conserva su comportamiento y admite `latestVersionOnly=true` como opción adicional.

`result.items` incluye templateId, name, actaTypeId, scope, solutionCenterCode, templateVersionId, versionNumber, sections/configurationJson y canCreateActa. Una plantilla sin versiones aparece explícitamente con IDs de versión nulos, sections vacío y canCreateActa=false. No se inventan configuraciones ni se sustituyen las versiones de actas antiguas.

## Correo al crear

Se reutilizan el compositor, servicio de estados y MailKit. Los destinatarios son **solo los participantes explícitos con correo válido**, deduplicados; el creador no se añade automáticamente si no es participante. El mensaje HTML y texto informa creación, título, código, fecha del documento y creador; agrega datos de reunión cuando existe. El HTML conserva el escape de contenido. No incluye Graph, Outlook ni enlaces Teams.

Acta, secciones, participantes, reunión/reserva solicitada y notificaciones se guardan primero en una transacción. El procesamiento SMTP ocurre después del commit. Un fallo conserva el acta y responde 201 con `notifications` en ERROR/INCIERTO/PARCIAL; ENVIADO significa aceptación SMTP, no entrega final. Se mantienen Message-ID estable, Idempotency-Key y recuperación controlada sin reenvío de estados inciertos.

Sin destinatarios se guarda `ACTA_SIN_DESTINATARIOS` en el log existente, dentro de la transacción, y se devuelve `notifications.state:"SIN_DESTINATARIOS"`. No se crean destinatarios ficticios. Con `Smtp:Enabled=false` los mensajes quedan SIMULADO sin conexiones SMTP. Las notificaciones son una copia de la creación: editar contenido/asistencia no cambia ni reenvía mensajes anteriores. No se generaron mensajes retroactivos para actas existentes.

## Migración y conservación

Se ejecutó **06_mis_actas_incremental.sql una vez**, sobre `127.0.0.1:3306/bdcalco_sigocalco` después de verificar el respaldo. Cambios:

- `acta_participant`: `attended tinyint(1) NULL`; relaciones previas quedan pendientes.
- `acta`: índice `(created_by_user, acta_id)` para Mis actas.
- `meeting_notification`: `acta_id` obligatorio con FK e índice único `(acta_id, recipient_email)`; `meeting_id` nullable. La FK de reunión se reemplaza en el mismo ALTER por `FK_meetings_notification_meeting`. Cualquier notificación previa toma su acta desde la reunión existente.
- `acta_status`: PUBLICADA nueva y eliminación exclusiva de estados antiguos sin referencias. No se reasigna estado de ninguna acta.

No se añaden tablas: siguen **26 = 8 Authentication + 18 Meetings**. Se conservan las tres actas, dos reuniones, participantes y el calendario histórico. No quedan notificaciones temporales ni bases efímeras. El único cambio de datos del entorno existente fue el catálogo autorizado y el NULL inicial de la nueva columna.

El ejecutor `scripts/Invoke-MisActasMigration.ps1` verifica instancia/UUID, estado inicial, respaldo SHA256, dependencias y permisos. Detecta una migración parcial y se detiene sin repetir DDL. Los estados históricos referenciados se conservan y requieren una decisión específica. MySQL confirma DDL implícitamente: nunca asumir rollback total. En esta instancia no hubo estados retirados referenciados ni ejecución parcial.

Respaldo verificado: `artifacts/mis-actas-20261120/backup/`; una segunda exportación completa idéntica validó el contenido antes del DDL. Authentication se comprobó por ocho hashes exactos antes/después y después de las pruebas. No se hizo una restauración de ensayo sobre la base real.

`07_current_model.sql` es el esquema completo revisado para instalaciones nuevas y comparación con EF; **no se ejecutó para reconstruir la base real**. `03_seed_catalogs_permissions.sql` se actualizó para instalaciones nuevas; **no se ejecutó en esta base**. Los scripts históricos 01/02/04/05 permanecen históricos. No repetirlos sobre el esquema actualizado.

## Pruebas y Swagger

Release: **0 errores y 0 advertencias**. Suite final: **189 aprobadas, 0 fallidas, 0 omitidas**. Incluye el esquema completo real contra EF, los flujos anteriores, las tres verificaciones MySQL, migración con datos históricos en una base aislada, concurrencia, SMTP simulado, propiedad, estados y 19 operaciones documentadas en Swagger. Las concesiones para operaciones autorizadas de Mis actas se probaron exclusivamente en fixtures aisladas; los seis valores reales no cambiaron.

Evidencia privada: `artifacts/mis-actas-20261120/final.trx`, `preserved-tables.json` y `verification.json`. Los respaldos/artefactos y appsettings.Local.json siguen ignorados. No hubo commit, push ni merge.

Desde la raíz de SigoCalco:

```powershell
dotnet build .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release
dotnet test .\SigoCalco.Meetings\SigoCalco.Meetings.sln -c Release
dotnet run --project .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj -c Release --no-build --launch-profile https
```

Swagger: `https://localhost:7298/swagger`. El comando de pruebas normal omite las tres pruebas MySQL hasta activar sus variables de consentimiento; la ejecución final de esta entrega las activó todas. Los nombres son `MEETINGS_APPROVED_MYSQL_VERIFICATION`, `MEETINGS_APPROVED_PERMISSION_MYSQL` y `MEETINGS_APPROVED_ACTAS_MYSQL` con valor `1`, más `MEETINGS_MYSQL_LOCAL_SETTINGS` apuntando al archivo local privado.

Secuencia manual: mantener SMTP deshabilitado; el Administrador realiza la asignación autorizada de ACTAS_GESTION mediante los endpoints existentes; Authorize con JWT; consultar plantillas disponibles; utilizar sus IDs/secciones y un centro asignado; crear los cuatro casos con UUID distintos; consultar/listar; editar en BORRADOR; editar participantes y asistencia; publicar; comprobar el 409 al editar; anular; comprobar que no regresa a BORRADOR/PUBLICADA. `Meetings.Api.http` contiene las doce solicitudes completas requeridas, respuestas y variantes. Usar los IDs devueltos de las actas creadas para la prueba, no los de registros históricos.

Pendientes externos: asignación funcional autorizada, prueba SMTP real posterior con credenciales/destinatarios acordados, contrato de sesiones con Authentication pendiente de entregas anteriores y formatos de secciones distintos de FORMULARIO. El texto menciona un diagrama que no venía adjunto en este turno: esta entrega cubre los casos escritos; cualquier regla adicional de ese diagrama deberá contrastarse cuando esté disponible. No se implementan Mis compartidas, reprogramación/cancelación de reservas ni nuevos envíos por edición sin una regla aprobada.
