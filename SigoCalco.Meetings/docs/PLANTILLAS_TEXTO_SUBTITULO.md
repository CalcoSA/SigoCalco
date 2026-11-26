# Plantillas: TEXTO, SUBTITULO y nombres únicos

Entrega del 25/11/2026. Implementación y pruebas terminadas; **migración 08 pendiente de aprobación y de ejecución en `bdcalco_sigocalco`**. Aplicarla antes de ejecutar este backend sobre esa base: el modelo EF ya consulta la columna calculada `name_normalized`. No hay migración automática al iniciar la API.

## Contrato de las secciones

| Tipo/configuración | Comportamiento en creación y edición de actas |
|---|---|
| TEXTO `{"mode":"FIXED","defaultText":"Texto institucional"}` | El servidor guarda el texto de la versión original. Se puede omitir la sección, enviar null o devolver exactamente ese string. Otro contenido devuelve 400. |
| TEXTO `{"mode":"EDITABLE","defaultText":"Borrador"}` | Admite un string JSON como contenido. Ausencia/null usa el valor inicial; `""` permite vaciar una sección opcional. Si es obligatoria, el valor efectivo no puede estar vacío ni contener solo espacios. |
| TEXTO histórico con configuración null o sin `mode` | Se interpreta como editable. Un `defaultText` string puede precargar contenido. Se conserva el JSON histórico sin reescribirlo; otras propiedades históricas sin `mode` no se interpretan. |
| SUBTITULO | El texto visible es `title`, en su `sortOrder`. Requiere `isRequired:false` y `configurationJson:null`; no admite contenido ni crea una fila de contenido del acta. |

Los modos nuevos son exactamente `FIXED` y `EDITABLE`, con `defaultText` obligatorio y de tipo string. FIXED exige texto no vacío. Configuraciones nuevas con modo inválido, campos adicionales o tipos incorrectos devuelven 400. El JSON de TEXTO es, por ejemplo, `"contentJson":"Contenido escrito"`, no un objeto `{"text":"..."}` ni un JSON doblemente serializado.

LISTA, TABLA, FORMULARIO y PARTICIPANTES conservan sus reglas anteriores. Los límites de tamaño y la validación de propiedades JSON duplicadas siguen vigentes.

Los GET de versiones, detalle de versión, plantillas disponibles para actas y detalle del acta conservan los mismos DTO. Devuelven `contentType`, `configurationJson`, `title`, `sortOrder` e `isRequired`. El detalle del acta incluye SUBTITULO con contenido null desde la definición de la versión, sin crear contenido editable. Crear una versión posterior no cambia configuraciones, secciones ni actas de versiones anteriores.

## Ejemplos para Swagger

Usar un JWT válido y permisos funcionales existentes. Estos ejemplos son manuales y crean datos; las pruebas automatizadas no cambian concesiones de la base real. Sustituir IDs y centro por valores autorizados. Más solicitudes completas en `01.Meetings.Api/Meetings.Api/Meetings.Api.http`.

### Crear una plantilla con los tres tipos de sección

`POST /api/v1/templates/with-version` → 201:

```json
{
  "actaTypeId": 1,
  "name": "Acta de ejemplo con textos",
  "scope": "GLOBAL",
  "solutionCenterCode": null,
  "sections": [
    {
      "title": "Introducción",
      "contentType": "TEXTO",
      "sortOrder": 1,
      "isRequired": true,
      "configurationJson": {
        "mode": "FIXED",
        "defaultText": "En CALCO S.A. se deja constancia de los siguientes hechos."
      }
    },
    {
      "title": "DESARROLLO DE LA REUNIÓN",
      "contentType": "SUBTITULO",
      "sortOrder": 2,
      "isRequired": false,
      "configurationJson": null
    },
    {
      "title": "Observaciones",
      "contentType": "TEXTO",
      "sortOrder": 3,
      "isRequired": false,
      "configurationJson": {"mode":"EDITABLE","defaultText":"Borrador inicial"}
    }
  ]
}
```

Para `POST /api/v1/templates/{templateId}/versions`, enviar únicamente `{"sections":[...]}` con las secciones completas de la nueva versión. El nombre de la propia plantilla no bloquea esta operación. Consultar `GET /api/v1/templates/{templateId}/versions` y `GET /api/v1/template-versions/{templateVersionId}` para recuperar los IDs reales y verificar orden/configuración.

### Crear o editar contenido del acta

`POST /api/v1/actas`, con un nuevo UUID en el encabezado `Idempotency-Key` → 201. En este ejemplo 30 es la versión y 103 la sección EDITABLE:

```json
{
  "templateVersionId": 30,
  "title": "Seguimiento",
  "documentDate": "2026-11-25",
  "solutionCenterCode": "CENTRO_ASIGNADO",
  "sections": [{"templateSectionId":103,"contentJson":"Acuerdos registrados durante la reunión."}],
  "participants": [],
  "meeting": null
}
```

FIXED se incorpora desde la versión; SUBTITULO no necesita enviarse. Para editar un borrador propio, `PUT /api/v1/actas/{actaId}` → 200:

```json
{
  "title": "Seguimiento actualizado",
  "documentDate": "2026-11-25",
  "sections": [{"templateSectionId":103,"contentJson":"Observaciones actualizadas."}]
}
```

La lista `sections` conserva la semántica existente de sustitución del contenido del acta; incluir todas las secciones editables que se quieran conservar. Para comprobar el bloqueo, enviar en cualquiera de esos bodies `{"templateSectionId":101,"contentJson":"Texto fijo manipulado"}` (101 = sección FIXED) devuelve 400, sin escrituras parciales. Enviar contenido para SUBTITULO también devuelve 400. Los ejemplos no envían correos al tener `participants:[]`.

### Comprobar nombre duplicado

Después de crear el primer ejemplo, `POST /api/v1/templates`:

```json
{
  "actaTypeId": 1,
  "name": "  ACTA DE EJEMPLO CON TEXTOS  ",
  "scope": "GLOBAL",
  "solutionCenterCode": null
}
```

Responde 409, al igual que repetir el POST completo:

```json
{
  "isSuccess": false,
  "message": "Ya existe una plantilla con ese nombre, independientemente de su ámbito o centro.",
  "result": null
}
```

La unicidad es global, también entre tipos de acta y centros distintos. Se ignoran mayúsculas/minúsculas y espacios externos; no se eliminan acentos ni se colapsan espacios internos. Se aplica a ambas creaciones y a la actualización de nombre, excluyendo la propia plantilla al renombrarla. El índice único MySQL es la garantía final ante concurrencia, incluso si se omite la consulta previa. Los errores de ese índice se traducen a 409; la transacción existente evita plantillas/versiones/secciones parciales.

## Migración e impacto

La inspección de solo lectura de `127.0.0.1:3306/bdcalco_sigocalco` no encontró nombres duplicados conforme a `LOWER(TRIM(name)) COLLATE utf8mb4_bin`. Las cuatro secciones TEXTO existentes tienen configuración null. No se cambiaron esos datos ni se aplicó DDL sobre la base real.

`scripts/sql/08_template_text_subtitle_unique_name.sql` es incremental y realiza exactamente:

1. Consulta de nombres duplicados e IDs y comprobaciones de esquema.
2. Agrega a `sigocalco_meetings_template` la columna calculada almacenada `name_normalized`, `varchar(200) COLLATE utf8mb4_bin`, expresión `lower(trim(name))`, y el índice único `UX_meetings_template_name_normalized`.
3. Agrega SUBTITULO **al final** del ENUM `content_type` en `sigocalco_meetings_template_section`, conservando los valores y posiciones anteriores.
4. Elimina la rutina auxiliar creada para validar/ejecutar la migración.

No crea nuevas tablas, no cambia permisos ni Authentication, no actualiza JSON o contenido histórico y no elimina/renombra/fusiona registros. El total seguirá siendo 26 tablas: 18 Meetings y 8 Authentication. La columna almacenada y su índice requieren espacio y pueden reconstruir/bloquear la tabla durante el ALTER.

Antes de una ejecución autorizada: reconfirmar servidor/base, detener escrituras de plantillas, obtener y verificar un respaldo actualizado y volver a comprobar duplicados. Seleccionar explícitamente la base en un cliente MySQL que interprete `DELIMITER` y ejecutar el archivo completo. No usar `07_current_model.sql` sobre una base existente: es el esquema completo para instalaciones nuevas y ya incluye esta evolución.

La migración aborta antes del primer ALTER si detecta duplicados. No los resuelve automáticamente. También rechaza una ejecución ya aplicada o un esquema parcial/inesperado. Los dos ALTER tienen commit implícito independiente: ante un fallo, inspeccionar columna, índice y ENUM y preparar la recuperación de la operación pendiente, sin repetir el archivo a ciegas. Si el cliente se detiene en un error de CALL, la rutina auxiliar puede permanecer; retirarla solo después de revisar el error. Aplicar 08 antes de desplegar este backend.

## Adaptación de React pendiente

React no se modificó. Debe representar SUBTITULO como encabezado usando `title` y `sortOrder`, sin input. TEXTO FIXED se muestra desde `defaultText`, sin permitir edición; la API igualmente rechaza manipulaciones. TEXTO EDITABLE utiliza un campo de texto con precarga, y envía un string en `contentJson`. Configuración histórica null/sin mode se trata como editable. Usar siempre los IDs de sección de la versión elegida y mostrar el mensaje estándar de 409 para nombres repetidos.

## Verificación y archivos

- Release: compilación sin errores ni advertencias.
- Suite completa: 274 aprobadas, 0 fallidas y 1 omitida de 275. Se omitió deliberadamente la prueba que escribe en la base real.
- MySQL: migración y persistencia probadas en bases temporales independientes, eliminadas al terminar; rechazo previo a DDL ante duplicados históricos, conservación de secciones/versiones históricas, protección FIXED, SUBTITULO, defaults, versiones posteriores y renombrado.
- Concurrencia MySQL: 12 POST simultáneos con nombre equivalente → 1 creación y 11 conflictos, sin registros parciales. También se verificó el índice con escritores directos que omiten la validación de aplicación.
- Esquema exportado `07_current_model.sql` comparado con el modelo EF; pruebas anteriores, incluidas autorización y CORS, conservadas.
- Base real: las exportaciones completas antes/después coinciden por SHA256, y las ocho tablas Authentication se verificaron también por separado. Sigue sin columna `name_normalized` ni SUBTITULO en el ENUM; cero duplicados y cero bases temporales de pruebas remanentes. Respaldo y resultados privados en `artifacts/templates-20261125/`, ignorados por Git.

Para repetir la suite completa con MySQL aislado, desde la raíz `SigoCalco` en PowerShell:

```powershell
$env:MEETINGS_APPROVED_TEMPLATE_MYSQL = '1'
$env:MEETINGS_APPROVED_ACTAS_MYSQL = '1'
$env:MEETINGS_APPROVED_PERMISSION_MYSQL = '1'
$env:MEETINGS_APPROVED_MYSQL_VERIFICATION = '0'
$env:MEETINGS_MYSQL_LOCAL_SETTINGS = (Resolve-Path 'SigoCalco.Meetings/01.Meetings.Api/Meetings.Api/appsettings.Local.json').Path
dotnet build SigoCalco.Meetings/SigoCalco.Meetings.sln -c Release --no-restore
dotnet test SigoCalco.Meetings/SigoCalco.Meetings.sln -c Release --no-build --no-restore
```

Estas pruebas aisladas requieren permiso MySQL para crear y eliminar sus propias bases temporales. No activar `MEETINGS_APPROVED_MYSQL_VERIFICATION`: esa prueba anterior usa la base real. En esta ejecución se necesitó ejecutar las pruebas MySQL fuera del aislamiento de Windows por un error TLS del entorno; no se deshabilitó TLS ni se cambiaron credenciales.

Código: `TextSectionValidator.cs`, `SectionJsonValidator.cs`, `TemplateApplication.cs`, `ActaApplication.cs`, `TemplateStore.cs`, `ActasModel.cs`, `TemplatesController.cs` y dos registros DI en `Program.cs`. Los DTO y rutas existentes se conservan.

Pruebas: `TemplateSectionEvolutionTests.cs`, `TemplateNameTests.cs`, `TemplateEvolutionMySqlTests.cs`; adaptación del soporte SQLite en `MeetingsApiFactory.cs`, fixtures con nombres distintos en `TemplateApiTests.cs`, expectativas de TEXTO en `SectionValidationTests.cs` y migración de la base aislada de `MyActasMySqlTests.cs`. SQL: 07 y 08. Ejemplos: `Meetings.Api.http` y esta guía; enlace desde `MIS_PLANTILLAS_Y_SMTP.md`.

No se modificaron Authentication, Reservations, React, administración de permisos, CORS, SMTP ni configuraciones privadas. No se hicieron commit, push ni merge.
