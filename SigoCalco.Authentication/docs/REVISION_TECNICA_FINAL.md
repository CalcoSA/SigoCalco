# Revisión técnica final — 6 de noviembre de 2026

La compilación Release terminó con **0 errores y 0 advertencias**. La ejecución final de `dotnet test` terminó con **164 aprobadas, 0 fallidas y 0 omitidas**. Se corrigieron dos defectos reproducidos y la exposición de configuración local en un archivo candidato a Git. Esta revisión no certifica el despliegue en producción: licencia, integración con consumidores y pruebas con MySQL real siguen pendientes.

## Alcance y evidencia ejecutada

- Origen de solo lectura: `C:\Users\juan.zapata\source\repos\sitraCalco`.
- Repositorio de destino: `C:\Users\juan.zapata\source\repos\SigoCalco`.
- Único directorio modificado: `SigoCalco.Authentication`, dentro del destino.
- SDK: .NET 10.0.401. Se ejecutó primero la suite existente de 132 pruebas: todas aprobadas.
- Después se añadieron 32 casos: 28 HTTP, 3 de `StatusUser` y 1 de comparación exhaustiva SQL/modelo.
- La última ejecución completa terminó el 2026-11-06 a las 19:03:56, UTC-05:00. Los contadores se verificaron también leyendo el TRX.
- Reporte local: [authentication.trx](../.artifacts/test-results/authentication.trx), excluido de Git; corresponde a la última ejecución y reemplaza los resultados anteriores.
- No se ejecutó el script de instalación ni se abrió una conexión real con MySQL o WordPress. Las pruebas de persistencia usan SQLite en memoria; WordPress se sustituye por un servicio simulado.
- Las comprobaciones manuales con WordPress comunicadas por el usuario son antecedentes; no se repitieron contra el entorno real en esta revisión.

Comando ejecutado desde la carpeta del servicio:

```powershell
.\scripts\Verify.ps1 -Action Test
```

El script ejecutó realmente:

```powershell
dotnet build SigoCalco.Authentication.sln --configuration Release --nologo
dotnet test SigoCalco.Authentication.sln --configuration Release --no-build --no-restore --nologo --logger 'trx;LogFileName=authentication.trx' --results-directory .artifacts/test-results
```

También se ejecutó una publicación local, sin despliegue, para comprobar la exclusión de configuración sensible:

```powershell
dotnet publish 01.Authentication.Api/Authentication.Api/Authentication.Api.csproj --configuration Release --no-build --no-restore --nologo --output .artifacts/publish-review
```

La publicación terminó correctamente y no incluyó `appsettings.Development.json`. Sus artefactos permanecen dentro del servicio y excluidos de Git.

## Matriz de verificaciones

| Verificación solicitada | Resultado final | Evidencia y límites |
|---|---|---|
| 1. Suite existente y compilación | Aprobada | Las 132 pruebas existentes pasaron antes de corregir. Release final: 0 errores/advertencias. Suite ampliada: 164/164. |
| 2. Renovación y revocación | Aprobada en pruebas locales | Login HTTP, hash persistido, rotación, rechazo del refresh anterior, mismo `sid`, sin prolongar inactividad tras avanzar el reloj un minuto; logout invalida access y refresh. Bloqueos concurrentes reales de MySQL pendientes. |
| 3. Centro válido para todos los roles | Aprobada | Alta y actualización, roles PDV, otros y recién creados; centro omitido, cero, negativo o inexistente; conservación de asignación y rollback; FK restrictiva y rechazo de eliminar roles asignados. |
| 4. Endpoints protegidos | Aprobada | Los 17 endpoints devuelven 401 sin cabecera o con JWT malformado, sin consultar persistencia. Metadatos confirman únicamente login, intranet-access y refresh como anónimos. También se prueban firma, emisor, audiencia, vencimiento y propietario incorrectos. |
| 5. Sesiones revocadas/vencidas | Aprobada en pruebas locales | Rechazo de acceso, activity, refresh y nuevo logout; rechazo del JWT anterior al refresh después de logout. Expiración en el límite, borrado del hash y auditoría única; una corrección posterior del reloj no reactiva la sesión. Sesiones independientes del mismo usuario permanecen separadas. |
| 6. Advertencia EF `StatusUser` | Falló inicialmente; corregida y aprobada | Antes de corregir, `false` se guardaba como `true` y el modelo MySQL emitía `BoolWithDefaultWarning`. Tres regresiones pasan después del cambio a `HasDefaultValue(true)`. |
| 7. AutoMapper y licencia | Versión verificada; licencia pendiente | La API resuelve 16.1.1. Application declara 16.1.1 e Infrastructure 15.1.3. Falta acreditar la modalidad de licencia aplicable y su configuración cuando corresponda. |
| 8. Secretos en candidatos a Git | Falló inicialmente; corregida y aprobada en el alcance inspeccionado | JWT, SSO y conexiones estaban configurados en `appsettings.json`. Se trasladaron a configuración local ignorada; plantilla con valores vacíos. No se detectaron sus valores, contraseñas conocidas, claves privadas ni JWT literales en candidatos a Git; ningún archivo de Authentication está en staging. Configuración local excluida también del paquete publicado. |
| 9. Meetings y Reservations | Contrato local aprobado; integración pendiente | Un validador JWT independiente valida el token renovado y sus claims `email`, `idUser`, `sid`. Los consumidores aún deben configurar validación JWT y validación de sesión. No se probaron llamadas entre servicios. |
| 10. SQL frente a entidades/mappings | Diferencia inicial corregida; comparación estática aprobada | Se comparan las 8 tablas, todas las columnas, tipos, longitudes, nulabilidad, valores predeterminados, PK, índices, unicidad, FK y acciones referenciales. Se alineó `created_at` con `CURRENT_TIMESTAMP(6)`. Ejecución DDL, collation efectiva y concurrencia en MySQL quedan pendientes. |

No quedan pruebas automatizadas fallidas. Los pendientes de la matriz no se consideran aprobados para producción.

## Defectos confirmados y correcciones

1. **Alta de usuario inactivo.** `HasDefaultValueSql("'1'")` no comunicaba a EF el valor booleano predeterminado. Una prueba nueva creó `statusUser: false` y obtuvo `true` al recargarlo. Otra convirtió la advertencia del modelo MySQL en excepción y falló. Se cambió únicamente el mapping a `HasDefaultValue(true)`. La columna continúa siendo obligatoria y el SQL conserva su default verdadero; la solicitud explícita `false` se persiste correctamente.
2. **Precisión predeterminada de auditoría.** La comparación completa detectó `DATETIME(6) DEFAULT CURRENT_TIMESTAMP` en el modelo frente a `DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6)` en el SQL. Se corrigió `AuthenticationLogConfiguration` para declarar ambos con precisión 6. Las dos clases de contexto SQLite adaptan exclusivamente ese default a la sintaxis que SQLite admite; la comparación MySQL utiliza el mapping real sin esa adaptación.
3. **Credenciales en configuración versionable.** Se conservaron los valores locales en `01.Authentication.Api/Authentication.Api/appsettings.Development.json`, ignorado por Git, y se vaciaron las conexiones y secretos en la plantilla base. Se añadió `CopyToPublishDirectory="Never"` para el archivo local y se verificó mediante `dotnet publish`. Los valores sensibles no se imprimieron en los informes ni se agregaron al índice.

La infraestructura de pruebas HTTP usa `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 y acceso interno a `Program` desde el ensamblado de pruebas. Arranca la canalización real de middleware/controladores/aplicación/repositorios, sustituyendo únicamente configuración, reloj, WordPress y contexto de base de datos. El interceptor SQLite comprueba que los bloqueos solicitados estén dentro de una transacción y retira su sintaxis específica; esto no demuestra la semántica de bloqueo de MySQL.

También se verifica que un fallo de escritura de auditoría durante logout devuelve error y revierte la revocación; al recuperarse, logout produce una única auditoría exitosa.

## Pruebas que realmente se ejecutaron

| Clase en el TRX | Casos aprobados |
|---|---:|
| AuditDescriptionTests | 27 |
| AuthApplicationSessionTests | 9 |
| HttpSecurityTests | 28 |
| PointOfSaleAssignmentTests, incluidas sus partes EmailAndCenter y StatusUser | 49 |
| SchemaTests | 2 |
| SessionApiTests | 11 |
| SessionPersistenceTests | 7 |
| SessionPolicyTests | 15 |
| WordpressEmailTests | 16 |
| **Total** | **164** |

El correo local sigue teniendo prioridad: creación con valor inicial WordPress, edición parcial, login, SSO y refresh se verifican con datos simulados y SQLite. El nuevo flujo HTTP edita el correo, renueva incluso si la cabecera contiene un access token vencido y valida el claim `email` actualizado con un validador independiente.

## AutoMapper: versión y requisito de producción

Se verificaron los `PackageReference` y `01.Authentication.Api/Authentication.Api/obj/project.assets.json`: **16.1.1 es la versión efectiva de la API**, aunque Infrastructure conserva su referencia directa 15.1.3. No se cambiaron versiones para esta revisión.

El `LICENSE.md` del paquete 16.1.1 declara las alternativas RPL 1.5 y acuerdo Lucky Penny. La documentación oficial indica que AutoMapper requiere licencia desde la versión 15 y muestra su configuración mediante `cfg.LicenseKey`. Para un producto de código cerrado, corresponde comprobar la licencia Lucky Penny aplicable; Community tiene criterios de elegibilidad y no se puede asumir que la organización los cumple. La revisión no acredita ni descarta una licencia de la organización. [Guía oficial de versión 15](https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html), [modalidades y elegibilidad](https://automapper.io/), [acuerdo del proveedor](https://luckypennysoftware.com/license).

Actualmente `EFExtensions.AddPersistence` solo registra el perfil; no asigna `LicenseKey`. Tampoco se encontró una clave en la configuración local inspeccionada ni en las variables de licencia revisadas. Si se elige una modalidad con clave, falta suministrarla mediante configuración externa y asignarla a esa propiedad, sin incorporarla a Git. En el código de la versión 16.1.1 revisado, `LicenseAccessor` toma la propiedad de configuración; no se debe asumir que definir una variable de entorno sin conectarla a esa propiedad resuelve el requisito. [LicenseAccessor 16.1.1](https://github.com/AutoMapper/AutoMapper/blob/v16.1.1/src/AutoMapper/Licensing/LicenseAccessor.cs), [configuración 16.1.1](https://github.com/AutoMapper/AutoMapper/blob/v16.1.1/src/AutoMapper/Configuration/MapperConfigurationExpression.cs).

No se compró, emitió ni validó una licencia. El resultado positivo del build y de las pruebas no sustituye esa comprobación.

## Integración que queda pendiente

- **Meetings/Reservations:** configurar firma HS256, mismo emisor/audiencia acordados, clave distribuida de forma segura, validación de vigencia y lectura de claims sin remapeo. Meetings tiene autorización declarada, pero su arranque inspeccionado no registra JWT Bearer ni `UseAuthentication`; Reservations conserva un arranque básico sin configuración JWT. Se leyeron, pero no se modificaron.
- **Estado de sesión compartido:** acordar cómo cada consumidor verifica `sid`, revocación e inactividad. Authentication comprueba la sesión en su middleware; validar únicamente el JWT en otro microservicio permite usarlo hasta `exp` incluso después de logout. No existe una integración de sesión entre servicios verificada en esta revisión.
- **MySQL:** ejecutar el DDL en un entorno de integración autorizado, contrastar el esquema instalado, y probar refresh concurrentes, carreras con logout/activity, bloqueo y collation. La comparación estática no afirma que la base instalada coincida con el archivo.
- **Entorno final:** verificar aprovisionamiento de secretos, permisos, certificados, CORS, usuarios/catálogos aprobados y llamadas reales desde frontend y consumidores. Las pruebas locales no certifican estos valores.
- **Autorización funcional:** se conserva `[Authorize]`; no se incorporó ni certificó una nueva política de permisos administrativos por rol.

## Conservación del origen y de los demás servicios

Se compararon rutas y SHA-256 de los archivos rastreados y no ignorados antes y después. Se incluyen archivos previamente eliminados como `MISSING`; se excluyen metadatos Git, archivos ignorados y salidas de compilación.

| Alcance protegido | Archivos | SHA-256 idéntico al inicial |
|---|---:|---|
| sitraCalco completo | 438 | `08B83615BD90217599E5A669F1ACD3824A0705042FFF060BCB40DB4363504620` |
| SigoCalco fuera de SigoCalco.Authentication | 86 | `5396D7116EB0BA08E5889857691917E49572ED7A26940E32E222936ACBBD0CC9` |

El Git de sitraCalco permanece limpio. Se mantuvieron `feature/jp2` / `66b2a5dd8417cee16877446faec7602c90ba2821` en el origen y `feature/initproject` / `eb45f48be73a06b2c9bd2e66a19884233314b32c` en el destino. No hubo commit, push, cambios de rama ni modificaciones de bases reales. Los cambios preexistentes de Meetings permanecen intactos.
