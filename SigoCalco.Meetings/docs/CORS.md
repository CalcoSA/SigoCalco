# CORS de Meetings — 24/11/2026

## Referencia y diferencias

Authentication registra una política llamada `SigoCalcoCorsPolicy` con `WithOrigins`, `AllowAnyHeader` y `AllowAnyMethod`. Lee la lista de `Cors:AllowedOrigins`. No habilita credenciales por cookies. Su middleware se encuentra después de HTTPS y antes de autenticación/autorización.

La configuración observada de Authentication tiene únicamente `http://localhost:4200` en appsettings.json. appsettings.Development.json no la sobrescribe: la lista base se hereda también en otros entornos si el despliegue no proporciona otra. El equipo confirmó que React utiliza **`http://localhost:5173`**. Meetings permite ese origen y conserva `http://localhost:4200` por compatibilidad con la configuración existente. Authentication no se modificó: autorizar 5173 en Meetings no cambia la política independiente de Authentication.

Meetings no tenía registro, sección de configuración ni middleware CORS. Ahora reutiliza el nombre y el enfoque de Authentication, con separación explícita entre desarrollo y configuración base.

## Configuración

- `appsettings.json`: `Cors:AllowedOrigins` es `[]`. Sin configuración explícita no se concede acceso CORS a ningún origen.
- `appsettings.Development.json`: permite `http://localhost:5173` (React) y `http://localhost:4200` (compatibilidad existente).
- `appsettings.Local.example.json`: documenta la misma lista local para otro desarrollador. Este archivo es versionable y no se carga automáticamente.
- `appsettings.Local.json`: permanece sin modificaciones e ignorado por Git. Meetings conserva su carga únicamente en Development y las prioridades de User Secrets/variables de entorno existentes.

No se añadieron localhost:3000, 127.0.0.1, HTTPS del frontend ni dominios externos. El esquema, host y puerto deben coincidir con el Origin que realmente envía el navegador; React en `http://localhost:5173` consume la API HTTPS en `https://localhost:7298`.

La política permite los métodos y encabezados solicitados **solo para los orígenes de la lista**. Incluye Authorization, Content-Type e Idempotency-Key y admite los GET, POST, PUT y PATCH existentes. No utiliza AllowAnyOrigin, AllowCredentials ni comodines de subdominio. El JWT viaja en Authorization: Bearer; no hace falta activar `credentials: 'include'` o `withCredentials: true` para cookies.

Se valida la configuración al iniciar: cada elemento debe ser un origen HTTP/HTTPS concreto, sin rutas, query, fragmentos, credenciales, comodines ni barra final. Un valor inválido impide arrancar con una política accidentalmente permisiva. Los cambios requieren reiniciar el proceso.

Para QA o producción, cuando se conozcan los dominios reales:

1. Seleccionar el entorno correcto, por ejemplo Staging o Production.
2. Definir la sección `Cors:AllowedOrigins` en `appsettings.<Environment>.json`, con un arreglo de los orígenes reales completos. También se puede establecer `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc. mediante variables de entorno del despliegue.
3. No incluir rutas `/api`, barras finales ni comodines. Mantener únicamente los orígenes necesarios y reiniciar Meetings.
4. Si se combinan varios proveedores de configuración, recordar que los arrays se combinan por índice; mantener una lista autoritativa por entorno y revisar todas las entradas efectivas. La lista base vacía evita heredar localhost en QA/producción.

No se crearon archivos ni dominios de QA/producción ficticios. El dominio `.invalid` de las pruebas existe únicamente como fixture automatizado, no como origen autorizado de despliegue.

## Orden del middleware y respuestas

```text
UseHttpsRedirection → UseRouting → UseCors → UseAuthentication → UseAuthorization → MapControllers
```

Swagger conserva su ubicación y disponibilidad en Development. El orden explícito de CORS sigue la [documentación de ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0).

Un preflight válido responde 204 sin exigir JWT; incluye Access-Control-Allow-Origin y los métodos/encabezados solicitados. Las solicitudes reales siguen validando JWT, rol y permisos. Para un origen autorizado, las respuestas 401 y 403 conservan el formato `isSuccess/message/result` y los encabezados CORS, incluidos los errores funcionales procesados por el manejador de excepciones.

Un origen no permitido no recibe encabezados Access-Control-Allow-*. El servidor puede devolver 204 al OPTIONS o una respuesta normal al cliente HTTP, pero el navegador no autoriza su lectura mediante CORS. CORS no reemplaza autenticación ni es un filtro para clientes ajenos al navegador.

React debe llamar directamente a la URL HTTPS de la API (`https://localhost:7298` en el perfil local actual). Se conserva la redirección HTTPS existente; enviar el preflight al puerto HTTP y depender de la redirección puede fallar en el navegador. El certificado HTTPS local debe estar confiado.

## Verificación

`Meetings.Api.http` incluye preflight autorizado, GET con JWT, 401 con origen autorizado y preflight no autorizado. Comprobación equivalente:

```powershell
curl.exe -i -X OPTIONS https://localhost:7298/api/v1/actas -H "Origin: http://localhost:5173" -H "Access-Control-Request-Method: POST" -H "Access-Control-Request-Headers: authorization,content-type,idempotency-key"
```

Las pruebas de integración HTTP usan el middleware real y SQLite aislado; prueban el emisor JWT original de Authentication con una clave exclusiva de pruebas. No ejecutan login, SMTP real ni operaciones en MySQL. Las pruebas MySQL anteriores con escrituras requieren activación explícita y no se habilitan para este cambio.

Archivos funcionales modificados: Program.cs, appsettings.json, appsettings.Development.json y appsettings.Local.example.json. Se agregan CorsTests, opciones de entorno/configuración en MeetingsApiFactory y documentación/ejemplos HTTP. No se cambian JWT, permisos, actas, plantillas, SMTP, esquema ni datos; Authentication, Reservations, frontend y configuración privada permanecen intactos.

Validación de la implementación inicial del 24/11/2026: **245 pruebas aprobadas, 0 fallidas y 3 omitidas** (las pruebas MySQL con escrituras, no habilitadas en esta tarea). Incluye las **34 pruebas de CORS**: preflight, origen permitido/no permitido, JWT emitido por Authentication, creación con Idempotency-Key, errores 401/403, Swagger y aislamiento de entornos/configuración inválida. Compilación Release: **0 errores y 0 advertencias**. Evidencia local ignorada por Git: `artifacts/cors-20261124/final.trx` y `final-tests.log`.

El ajuste posterior a React 5173 solo amplía las listas de desarrollo/ejemplo y actualiza pruebas y documentación. Program.cs y la configuración base de QA/producción permanecen sin cambios. Los preflight verifican ambos orígenes; las pruebas de JWT, 401/403 y creación utilizan ahora 5173. Ambos orígenes siguen excluidos de Staging/Production salvo configuración explícita. Reiniciar Meetings para cargar la lista actualizada.

Validación del ajuste a 5173: **34 pruebas CORS aprobadas, 0 fallidas y 0 omitidas**; Release con **0 errores y 0 advertencias**. Resultados en `artifacts/cors-20261124/react-5173.trx`. No se ejecutaron pruebas con escrituras en MySQL.
