# Inventario de archivos

Todos los archivos de esta carpeta son nuevos respecto al repositorio SigoCalco. Los archivos con procedencia reutilizan el original, adaptando namespaces, nombres y referencias; los cambios funcionales se describen en ../README.md. El origen no se modificó. No se listan bin, obj, cachés ni resultados de compilación.

| Archivo de destino | Procedencia dentro de sitraCalco.Authentication |
|---|---|
| .gitignore | Nuevo |
| 01.Authentication.Api/Authentication.Api/appsettings.json | Plantilla nueva sin valores del origen |
| 01.Authentication.Api/Authentication.Api/Authentication.Api.csproj | Authentication.Api/Authentication.Api.csproj |
| 01.Authentication.Api/Authentication.Api/Controllers/AuthController.cs | Authentication.Api/Controllers/AuthController.cs |
| 01.Authentication.Api/Authentication.Api/Controllers/LogController.cs | Authentication.Api/Controllers/LogController.cs |
| 01.Authentication.Api/Authentication.Api/Controllers/MenuOptionController.cs | Authentication.Api/Controllers/MenuOptionController.cs |
| 01.Authentication.Api/Authentication.Api/Controllers/RoleController.cs | Authentication.Api/Controllers/RoleController.cs |
| 01.Authentication.Api/Authentication.Api/Controllers/SolutionCentersController.cs | Authentication.Api/Controllers/SolutionCentersController.cs |
| 01.Authentication.Api/Authentication.Api/Controllers/UserController.cs | Authentication.Api/Controllers/UserController.cs |
| 01.Authentication.Api/Authentication.Api/Controllers/WordpressUserController.cs | Authentication.Api/Controllers/WordpressUserController.cs |
| 01.Authentication.Api/Authentication.Api/Extensions/ClaimsPrincipalExtensions.cs | Authentication.Api/Extensions/ClaimsPrincipalExtensions.cs |
| 01.Authentication.Api/Authentication.Api/Extensions/SessionJwtBearerEvents.cs | Authentication.Api/Extensions/SessionJwtBearerEvents.cs |
| 01.Authentication.Api/Authentication.Api/Program.cs | Authentication.Api/Program.cs |
| 02.Authentication.Application/Authentication.Application/ApplicationExtensions.cs | Authentication.Application/ApplicationExtensions.cs |
| 02.Authentication.Application/Authentication.Application/Authentication.Application.csproj | Authentication.Application/Authentication.Application.csproj |
| 02.Authentication.Application/Authentication.Application/Interfaces/IAuthApplication.cs | Authentication.Application/Interfaces/IAuthApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/IJwtTokenApplication.cs | Authentication.Application/Interfaces/IJwtTokenApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/ILogApplication.cs | Authentication.Application/Interfaces/ILogApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/IMenuOptionApplication.cs | Authentication.Application/Interfaces/IMenuOptionApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/IRoleApplication.cs | Authentication.Application/Interfaces/IRoleApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/ISolutionCenterApplication.cs | Authentication.Application/Interfaces/ISolutionCenterApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/ISSOSignatureApplication.cs | Authentication.Application/Interfaces/ISSOSignatureApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/IUserApplication.cs | Authentication.Application/Interfaces/IUserApplication.cs |
| 02.Authentication.Application/Authentication.Application/Interfaces/IWordpressUserApplication.cs | Authentication.Application/Interfaces/IWordpressUserApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/AuthApplication.cs | Authentication.Application/Services/AuthApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/JwtTokenApplication.cs | Authentication.Application/Services/JwtTokenApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/LogApplication.cs | Authentication.Application/Services/LogApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/MenuOptionApplication.cs | Authentication.Application/Services/MenuOptionApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/RoleApplication.cs | Authentication.Application/Services/RoleApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/SolutionCenterApplication.cs | Authentication.Application/Services/SolutionCenterApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/SSOSignatureApplication.cs | Authentication.Application/Services/SSOSignatureApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/UserApplication.cs | Authentication.Application/Services/UserApplication.cs |
| 02.Authentication.Application/Authentication.Application/Services/WordpressUserApplication.cs | Authentication.Application/Services/WordpressUserApplication.cs |
| 03.Authentication.Domain/Authentication.Domain/Authentication.Domain.csproj | Authentication.Domain/Authentication.Domain.csproj |
| 03.Authentication.Domain/Authentication.Domain/Dtos/AuditResultDto.cs | Authentication.Domain/Dtos/AuditResultDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/AuthUserDto.cs | Authentication.Domain/Dtos/AuthUserDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/CreateLogDto.cs | Authentication.Domain/Dtos/CreateLogDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/CreateRoleDto.cs | Authentication.Domain/Dtos/CreateRoleDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/CreateUserDto.cs | Authentication.Domain/Dtos/CreateUserDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/IntranetAccessDto.cs | Authentication.Domain/Dtos/IntranetAccessDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/LogDto.cs | Authentication.Domain/Dtos/LogDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/LoginDto.cs | Authentication.Domain/Dtos/LoginDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/MenuOptionDto.cs | Authentication.Domain/Dtos/MenuOptionDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/PagedDto.cs | Authentication.Domain/Dtos/PagedDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/RefreshTokenDto.cs | Authentication.Domain/Dtos/RefreshTokenDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/RoleDetailDto.cs | Authentication.Domain/Dtos/RoleDetailDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/RoleDto.cs | Authentication.Domain/Dtos/RoleDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/SolutionCenterDto.cs | Authentication.Domain/Dtos/SolutionCenterDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/UpdateRoleDto.cs | Authentication.Domain/Dtos/UpdateRoleDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/UpdateUserDto.cs | Authentication.Domain/Dtos/UpdateUserDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/UserAdministrationDto.cs | Authentication.Domain/Dtos/UserAdministrationDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/UserAssignmentResultDto.cs | Authentication.Domain/Dtos/UserAssignmentResultDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/UserDto.cs | Authentication.Domain/Dtos/UserDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Dtos/WordpressUserDto.cs | Authentication.Domain/Dtos/WordpressUserDto.cs |
| 03.Authentication.Domain/Authentication.Domain/Exceptions/BusinessRuleException.cs | Authentication.Domain/Exceptions/BusinessRuleException.cs |
| 03.Authentication.Domain/Authentication.Domain/Exceptions/SessionException.cs | Authentication.Domain/Exceptions/SessionException.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/ILogRepository.cs | Authentication.Domain/Interfaces/ILogRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/IMenuOptionRepository.cs | Authentication.Domain/Interfaces/IMenuOptionRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/IRepository.cs | Authentication.Domain/Interfaces/IRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/IRoleRepository.cs | Authentication.Domain/Interfaces/IRoleRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/ISessionRepository.cs | Authentication.Domain/Interfaces/ISessionRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/ISolutionCenterRepository.cs | Authentication.Domain/Interfaces/ISolutionCenterRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/IUserRepository.cs | Authentication.Domain/Interfaces/IUserRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Interfaces/IWordpressUserRepository.cs | Authentication.Domain/Interfaces/IWordpressUserRepository.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/AuthenticationLog.cs | Authentication.Domain/Models/AuthenticationLog.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/AuthenticationSession.cs | Authentication.Domain/Models/AuthenticationSession.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/Menuoption.cs | Authentication.Domain/Models/Menuoption.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/Role.cs | Authentication.Domain/Models/Role.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/Rolemenuoption.cs | Authentication.Domain/Models/Rolemenuoption.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/SolutionCenter.cs | Authentication.Domain/Models/SolutionCenter.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/User.cs | Authentication.Domain/Models/User.cs |
| 03.Authentication.Domain/Authentication.Domain/Models/Userrole.cs | Authentication.Domain/Models/Userrole.cs |
| 03.Authentication.Domain/Authentication.Domain/Options/SessionOptions.cs | Authentication.Domain/Options/SessionOptions.cs |
| 03.Authentication.Domain/Authentication.Domain/Responses/ResponseApi.cs | Authentication.Domain/Responses/ResponseApi.cs |
| 03.Authentication.Domain/Authentication.Domain/Validators/CreateRoleDtoValidator.cs | Authentication.Domain/Validators/CreateRoleDtoValidator.cs |
| 03.Authentication.Domain/Authentication.Domain/Validators/CreateUserDtoValidator.cs | Authentication.Domain/Validators/CreateUserDtoValidator.cs |
| 03.Authentication.Domain/Authentication.Domain/Validators/UpdateRoleDtoValidator.cs | Authentication.Domain/Validators/UpdateRoleDtoValidator.cs |
| 03.Authentication.Domain/Authentication.Domain/Validators/UpdateUserDtoValidator.cs | Authentication.Domain/Validators/UpdateUserDtoValidator.cs |
| 03.Authentication.Domain/Authentication.Domain/Validators/UserEmail.cs | Nuevo |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Authentication.Infrastructure.Persistence.csproj | Authentication.Infrastructure.Persistance/Authentication.Infrastructure.Persistance.csproj |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/AuthenticationLogConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/AuthenticationLogConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/AuthenticationSessionConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/AuthenticationSessionConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/MenuoptionConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/MenuoptionConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/RoleConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/RoleConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/RolemenuoptionConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/RolemenuoptionConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/SolutionCenterConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/SolutionCenterConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/UserConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/UserConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Configurations/UserroleConfiguration.cs | Authentication.Infrastructure.Persistance/Data/Configurations/UserroleConfiguration.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/EFExtensions.cs | Authentication.Infrastructure.Persistance/Data/EFExtensions.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/Scripts/CreateSigoCalcoAuthentication.sql | Nuevo |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Data/SigoCalcoContext.cs | Authentication.Infrastructure.Persistance/Data/SitraCalcoContext.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Mappings/AutoMapperProfile.cs | Authentication.Infrastructure.Persistance/Mappings/AutoMapperProfile.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Properties/AssemblyInfo.cs | Nuevo |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/LogRepository.cs | Authentication.Infrastructure.Persistance/Repositories/LogRepository.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/MenuOptionRepository.cs | Authentication.Infrastructure.Persistance/Repositories/MenuOptionRepository.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/Repository.cs | Authentication.Infrastructure.Persistance/Repositories/Repository.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/RoleRepository.cs | Authentication.Infrastructure.Persistance/Repositories/RoleRepository.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/SessionRepository.cs | Authentication.Infrastructure.Persistance/Repositories/SessionRepository.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/SolutionCenterRepository.cs | Authentication.Infrastructure.Persistance/Repositories/SolutionCenterRepository.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/UserRepository.cs | Authentication.Infrastructure.Persistance/Repositories/UserRepository.cs |
| 04.Authentication.Infrastructure/Authentication.Infrastructure.Persistence/Repositories/WordpressUserRepository.cs | Authentication.Infrastructure.Persistance/Repositories/WordpressUserRepository.cs |
| 05.Authentication.Test/Authentication.Test/AuditDescriptionTests.cs | Authentication.Test/AuditDescriptionTests.cs |
| 05.Authentication.Test/Authentication.Test/AuthApplicationSessionTests.cs | Authentication.Test/AuthApplicationSessionTests.cs |
| 05.Authentication.Test/Authentication.Test/Authentication.Test.csproj | Authentication.Test/Authentication.Test.csproj |
| 05.Authentication.Test/Authentication.Test/EmailAndCenterTests.cs | Nuevo |
| 05.Authentication.Test/Authentication.Test/PointOfSaleAssignmentTests.cs | Authentication.Test/PointOfSaleAssignmentTests.cs |
| 05.Authentication.Test/Authentication.Test/SchemaTests.cs | Nuevo |
| 05.Authentication.Test/Authentication.Test/SessionApiTests.cs | Authentication.Test/SessionApiTests.cs |
| 05.Authentication.Test/Authentication.Test/SessionPersistenceTests.cs | Authentication.Test/SessionPersistenceTests.cs |
| 05.Authentication.Test/Authentication.Test/SessionPolicyTests.cs | Authentication.Test/SessionPolicyTests.cs |
| 05.Authentication.Test/Authentication.Test/WordpressEmailTests.cs | Nuevo |
| docs/ARCHIVOS.md | Nuevo |
| docs/VERIFICACION.md | Nuevo |
| README.md | Nuevo |
| scripts/Verify.ps1 | Nuevo |
| SigoCalco.Authentication.sln | Nuevo |

Inventario inicial: 116 archivos. El SQL completo deriva de las configuraciones EF y de la estructura SQL original; reemplaza los scripts incrementales históricos del origen.

Archivos adicionales registrados en la revisión del 6 de noviembre de 2026:

| Archivo de destino | Procedencia |
|---|---|
| 01.Authentication.Api/Authentication.Api/Properties/launchSettings.json | Configuración ya presente al iniciar esta revisión; conservada |
| 01.Authentication.Api/Authentication.Api/Properties/AssemblyInfo.cs | Nuevo; acceso de las pruebas HTTP a Program |
| 05.Authentication.Test/Authentication.Test/HttpSecurityTests.cs | Nuevo; pruebas de la canalización HTTP y sesiones persistidas |
| 05.Authentication.Test/Authentication.Test/StatusUserTests.cs | Nuevo; regresiones de estado y advertencia EF |
| docs/REVISION_TECNICA_FINAL.md | Nuevo; matriz final y evidencia |

Total actual: 121 archivos candidatos a Git. `appsettings.Development.json` contiene configuración local, está ignorado y no forma parte de esta entrega ni de la publicación Release. Tampoco se incluyen bin, obj ni .artifacts.
