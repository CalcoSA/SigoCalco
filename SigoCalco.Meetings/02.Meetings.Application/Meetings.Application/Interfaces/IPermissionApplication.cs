using Meetings.Application.Dtos;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Interfaces;

public interface IPermissionApplication
{
    Task<IReadOnlyList<PermissionDto>> GetAsync(CancellationToken ct);
    Task<PermissionDto> AddRoleAsync(string key, AddPermissionRoleDto request, CancellationToken ct);
    Task<PermissionDto> UpdateRolesAsync(string key, UpdatePermissionRolesDto request, CancellationToken ct);
}

public interface IPermissionStore
{
    Task<IReadOnlyList<MeetingsPermission>> GetAsync(CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
    Task<MeetingsPermission> AddRoleAsync(string key, string role, CancellationToken ct);
    Task<MeetingsPermission> ReplaceRolesAsync(string key, IReadOnlyList<string> roles, CancellationToken ct);
}
