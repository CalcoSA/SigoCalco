# Mis compartidas — 23/11/2026

## Contratos HTTP

`GET /api/v1/actas/shared-with-me?page=1&pageSize=20`, sin body, usa el DTO paginado existente de Mis actas. Predeterminados 1/20; page >= 1, pageSize 1..100, con validación del desbordamiento del offset. Orden `created_at DESC, acta_id DESC` y todos los estados: BORRADOR, PUBLICADA y ANULADA.

```json
{
  "isSuccess": true,
  "message": "Actas compartidas consultadas correctamente.",
  "result": {
    "items": [],
    "page": 1,
    "pageSize": 20,
    "totalCount": 0,
    "totalPages": 0
  }
}
```

Cuando hay resultados, cada elemento conserva `ActaListItemDto`: actaId, actaCode, title, documentDate, actaTypeId, actaTypeName, actaStatusId, status, solutionCenterCode, createdByUser, createdAt, updatedAt, hasMeeting y modality. Una colección vacía devuelve 200, no 404.

`GET /api/v1/actas/{actaId:int}` conserva su ruta y DTO completo: `result.acta`, templateId, templateName, templateVersionId, versionNumber, sections, participants y meeting (con reservation opcional). No se crea otro detalle ni se cambia la versión original. El message sigue siendo `Acta consultada correctamente.`

| Situación | Respuesta |
|---|---|
| Listado válido o detalle accesible | 200, isSuccess:true |
| Paginación inválida, body o query no documentada | 400, isSuccess:false, result:null |
| JWT ausente/inválido/vencido o login no contrastable para estas lecturas | 401 |
| Creador sin ACTAS_GESTION | 403 |
| Persona ajena o acta inexistente | 404, `No se encontró el acta.` |
| Fallo inesperado | 500 sin información interna |

Ambas lecturas rechazan body y parámetros de identidad (userLogin, userId, idUser, createdByUser). El listado solo admite page/pageSize; el detalle no recibe query. Las rutas numéricas conservan `:int`, por lo que no hay conflicto con shared-with-me.

## Identidad y autorización

Authentication ya emite el claim `userLogin`: no se añadió ni cambió ningún claim. Meetings mantiene la validación JWT de firma, issuer, audience y expiración, comprueba `idUser == sub` y resuelve el usuario/rol activo y sus centros mediante el adaptador de solo lectura `AuthenticationDirectory`.

El adaptador ahora también obtiene `u.userLogin` del mismo registro. `MeetingsIdentityAccessor` acepta para participación exactamente un claim userLogin no vacío, de longitud máxima 60 y sin controles; normaliza trim + minúsculas y lo contrasta con el login del catálogo para ese ID. Si falta, está duplicado, mal formado o no coincide, deja UserLogin sin validar y estas dos lecturas responden 401. No deriva un login de userName, email, el body, el query ni solamente del catálogo. Se conserva el comportamiento de los demás endpoints existentes.

La comparación de participación usa igualdad completa de `LOWER(TRIM(userLogin))`. MySQL usa explícitamente `utf8mb4_bin` después de normalizar para no ampliar una identidad validada mediante equivalencias de acentos de la collation predeterminada. Se toleran mayúsculas y espacios exteriores normales; no se usa LIKE, coincidencia parcial, nombre visible ni email.

| Operación | Regla |
|---|---|
| Mis actas | Creador e inclusión de su rol en ACTAS_GESTION |
| Mis compartidas | JWT/identidad/login válidos, participante INTERNO asociado, creador diferente |
| Detalle propio | Creador con ACTAS_GESTION, aunque también sea participante |
| Detalle de otra persona | Asociación interna válida por login; sin exigir ACTAS_GESTION ni ACTAS_LECTURA |
| PUT contenido/participantes/asistencia | Creador + ACTAS_GESTION + BORRADOR, sin ampliar su política |
| PATCH estado | Creador + ACTAS_GESTION y transiciones existentes |

Administrador o pertenencia al centro no habilitan documentos ajenos. Las seis claves y sus asignaciones se conservan; no se modificaron sus tres endpoints administrativos. No se usan las tablas de permisos por usuario/centro para ampliar este flujo. Retirar la asociación de participación elimina el acceso en la siguiente consulta; no hay caché de concesiones documentales.

## Persistencia y participantes

La consulta relaciona `participant` (solo INTERNO y login exacto) → `acta_participant` por participant_id → `acta` por acta_id. Usa una subconsulta IN/EXISTS sobre actas y excluye `created_by_user == actor.UserId`; varias personas con el mismo login no duplican actas. COUNT, filtros, orden, LIMIT y OFFSET se ejecutan en la base de datos, con snapshot consistente entre total y página. El detalle aplica propiedad/participación dentro de su consulta y transacción antes de cargar contenido. Las escrituras siguen usando las entradas owner-only y bloqueos ya existentes.

EXTERNO con login se rechazaba y sigue rechazándose al escribir. Los registros externos históricos, incluso mal formados con login, nunca otorgan acceso. Para INTERNO se exige login no vacío, máximo 60 y sin caracteres de control. La existencia autenticable se verifica cuando el lector se identifica contra Authentication; no se crean usuarios ni se modifica ese catálogo.

Se corrigió un riesgo del catálogo reutilizado por correo: si un correo existente representa otro tipo o login, POST/PUT de participantes responde 409 y revierte toda la operación. Antes podía asociar silenciosamente una identidad distinta de la solicitada. Se conservan el nombre histórico y la asistencia de asociaciones retenidas. No se corrigen registros históricos automáticamente ni se actualiza una identidad global desde un acta.

No hay cambios de esquema ni script SQL nuevo. Inspección real: MySQL 8.0.46, 26 tablas (8 Authentication y 18 Meetings), cuatro participantes. Existe UNIQUE(acta_id, participant_id) e índice participant_id en la relación, PK de acta e índice de propietario. No hay índice userLogin. Un índice simple sobre el valor crudo no optimiza la igualdad normalizada con TRIM/LOWER; con el catálogo actual no se justifica una migración funcional de índices. Si el volumen crece, evaluar el plan y un índice de expresión compatible con esta misma comparación. No se repitió ninguna migración en la base real; la prueba histórica existente aplica scripts exclusivamente en su base efímera.

Las consultas no crean notificaciones, no llaman SMTP y no llaman Reservations. No se alteró la configuración real, sus credenciales ni la lógica de envío. Las pruebas sustituyen proveedores o usan Simulated.

## Swagger: procedimiento manual

En dos terminales, desde `C:\Users\juan.zapata\source\repos\SigoCalco`:

```powershell
dotnet run --project .\SigoCalco.Authentication\01.Authentication.Api\Authentication.Api\Authentication.Api.csproj --launch-profile Authentication.Api
dotnet run --project .\SigoCalco.Meetings\01.Meetings.Api\Meetings.Api\Meetings.Api.csproj -c Release --no-build --launch-profile https
```

Swagger: Authentication https://localhost:64845/swagger y Meetings https://localhost:7298/swagger. Cada usuario obtiene su propio JWT mediante el flujo existente; no compartir contraseñas ni tokens entre usuarios.

Preparar usuarios internos existentes A (creador con ACTAS_GESTION y plantilla visible), B (participante) y, para el control negativo, C (no participante). La configuración observada el 23/11 incluye Administrador en ACTAS_GESTION; no se cambió durante esta entrega. Seleccionar usuarios autorizados existentes, sin modificar permisos reales. Para evitar envíos en la prueba manual, usar un proceso local de prueba con `Smtp__Enabled=false` en su entorno, sin editar appsettings ni credenciales; la creación normal mantiene su comportamiento SMTP.

1. Como A, seleccionar una versión de `/api/v1/actas/available-templates`. Crear un acta con el contrato existente: UUID nuevo en Idempotency-Key, versión/centro propios, secciones válidas y meeting:null. En participants incluir el login **real de B**, no su nombre visible:

   ```json
   {"userName":"Nombre de B","email":"correo-de-B@example.invalid","participantType":"INTERNO","userLogin":"login.real.de.b"}
   ```

   Sustituir los ejemplos por los datos del usuario de prueba. Si su correo ya existe como EXTERNO u otro login, se devuelve 409: no se convierte silenciosamente ese registro. Verificar que el acta devuelta está en BORRADOR y aparece en Mis actas de A.

2. En Authorize de Meetings, cambiar al JWT propio de B. Ejecutar:

   ```http
   GET /api/v1/actas/shared-with-me?page=1&pageSize=20
   Authorization: Bearer <JWT de B>

   GET /api/v1/actas/<actaId creado por A>
   Authorization: Bearer <JWT de B>
   ```

   Ambos devuelven 200, con contenido y versión original. El acta no pertenece a Mis actas de B; si B carece de ACTAS_GESTION, ese listado propio devuelve 403.

3. Como B, intentar PUT de contenido/participantes/asistencia y PATCH PUBLICADA/ANULADA con cuerpos válidos del archivo `.http`: 403 si no tiene ACTAS_GESTION o 404 si lo tiene pero no es el creador. Ninguna operación debe modificar el acta.
4. Como C, abrir el mismo ID: 404, incluso siendo Administrador o del mismo centro. Su lista compartida no incluye el acta.
5. Como A, retirar a B mediante el PUT de participantes en BORRADOR, conservando los demás participantes deseados. Como B, repetir listado/detalle: no aparece y el detalle devuelve 404. Las consultas y este reemplazo no generan correos.

El archivo `Meetings.Api.http` incluye los dos GET y el intento de suplantación rechazado. No se ejecutó ningún login manual ni envío real durante el desarrollo.

## Archivos y verificación

API: ActasController, MeetingsIdentityAccessor y Meetings.Api.http. Application: IMeetingsIdentity, IActaStore/IActaApplication, ActaApplication y ActaApplication.Management. Domain: ParticipantIdentity. Persistence: AuthenticationDirectory, ActaStore, ActaStore.Management y ActaStore.Shared. Tests: SharedActasTests, MyActasMySqlTests, MySqlVerificationTests, MeetingsApiFactory, MyActasTests, AuthenticationJwtCompatibilityTests, CommandCounter y ResponseEnvelopeTests. Documentación: este archivo y MIS_ACTAS.md.

Se conserva la suite anterior y se agregan escenarios de usuarios distintos, múltiples registros del mismo login, todos los estados, centros distintos, nombres iguales, externos mal formados, orden/paginación SQL, detalle original, retirada de participación, rechazo de escritura con/sin permiso, JWT/login inválidos, suplantación por parámetros/body, consistencia del catálogo y ausencia de efectos externos. El escenario compartido se ejecuta también con EF/MySQL real en la base efímera de pruebas; la compatibilidad usa el emisor JWT original de Authentication con clave exclusiva de pruebas.

Evidencias locales (ignoradas por Git): `artifacts/mis-compartidas-20261123/`, incluyendo respaldo anterior, resultados TRX y comprobación de preservación. No se modifican Authentication, Reservations ni frontend; no hay commit/push/merge.

Resultado final: compilación Release con **0 errores y 0 advertencias**; **214 pruebas aprobadas, 0 fallidas y 0 omitidas**, incluidas las tres pruebas MySQL existentes y el nuevo escenario compartido en MySQL. El primer intento en sandbox no pudo iniciar TLS de Windows; la ejecución fuera del aislamiento completó la suite sin cambiar TLS ni credenciales.

Los ocho dumps de Authentication coinciden exactamente por SHA256 antes/después. El dump completo de las 26 tablas conserva esquema y filas, incluidas las seis concesiones; la única diferencia son los contadores AUTO_INCREMENT de Meetings consumidos por los fixtures temporales existentes. No quedan bases efímeras ni registros temporales. Los cuatro participantes reales siguen siendo EXTERNO sin login, por lo que no generan acceso por participación; requieren futuras asociaciones internas explícitas, nunca una conversión automática de históricos.
