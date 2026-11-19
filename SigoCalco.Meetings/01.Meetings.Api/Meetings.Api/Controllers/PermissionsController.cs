using Meetings.Api.Responses;
using Meetings.Api.Security;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Authorize(Policy = PermissionAdministrationRequirement.Policy)]
[Route("api/v1/permissions")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public sealed class PermissionsController(IPermissionApplication application) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Consultar los seis permisos de Meetings")]
    [EndpointDescription("Solo Administrador validado en Authentication. Devuelve roles como arreglo; valores vacíos o NULL se representan con [].")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetAsync(ct), "Permisos consultados correctamente."));

    [HttpPost("{permissionKey}/roles")]
    [EndpointSummary("Agregar un rol existente a un permiso")]
    [EndpointDescription("Solo Administrador validado en Authentication. Conserva los roles anteriores; un rol ya asignado produce 409. El rol debe existir y estar activo en Authentication.")]
    [ProducesResponseType(typeof(ApiResponse<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddRole(string permissionKey, [FromBody] AddPermissionRoleDto request, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.AddRoleAsync(permissionKey, request, ct), "Rol agregado correctamente."));

    [HttpPut("{permissionKey}")]
    [EndpointSummary("Reemplazar los roles de un permiso")]
    [EndpointDescription("Solo Administrador validado en Authentication. Sustituye toda la lista de forma atómica; [] deja el permiso sin roles. No cambia su ID ni su clave.")]
    [ProducesResponseType(typeof(ApiResponse<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRoles(string permissionKey, [FromBody] UpdatePermissionRolesDto request, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.UpdateRolesAsync(permissionKey, request, ct), "Roles actualizados correctamente."));
}
