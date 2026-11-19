using System.Text;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Services;

// Administrative access is explicitly authorized by Authentication's real role, never by a functional permission.
public static class PermissionAdministration
{
    public static bool IsAdministrator(MeetingsIdentity actor) =>
        actor.Roles.Any(r => MeetingsPermissions.NormalizeRole(r) == "ADMINISTRADOR");

    public static string[] ParseRoles(string? value) => (value ?? "")
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .DistinctBy(MeetingsPermissions.NormalizeRole).ToArray();

    public static string SerializeRoles(IEnumerable<string> roles)
    {
        var value = string.Join(',', roles);
        if (value.Length > 2000) throw MeetingsRequestException.Invalid("La lista de roles supera los 2000 caracteres permitidos.");
        return value;
    }
}

public sealed class PermissionApplication(IPermissionStore store, IAuthenticationDirectory directory, IMeetingsIdentity identity)
    : IPermissionApplication
{
    public async Task<IReadOnlyList<PermissionDto>> GetAsync(CancellationToken ct)
    {
        await RequireAdministratorAsync(ct);
        return (await store.GetAsync(ct)).Select(ToDto).ToArray();
    }

    public async Task<PermissionDto> AddRoleAsync(string key, AddPermissionRoleDto request, CancellationToken ct)
    {
        await RequireAdministratorAsync(ct);
        await RequirePermissionAsync(key, ct);
        var roles = await ValidateRolesAsync([request.RoleName], ct);
        return ToDto(await store.AddRoleAsync(key, roles[0], ct));
    }

    public async Task<PermissionDto> UpdateRolesAsync(string key, UpdatePermissionRolesDto request, CancellationToken ct)
    {
        await RequireAdministratorAsync(ct);
        await RequirePermissionAsync(key, ct);
        if (request.Roles is null) throw MeetingsRequestException.Invalid("Debe proporcionar el arreglo roles; utilice [] para vaciarlo.");
        var roles = await ValidateRolesAsync(request.Roles, ct);
        return ToDto(await store.ReplaceRolesAsync(key, roles, ct));
    }

    private async Task RequireAdministratorAsync(CancellationToken ct)
    {
        if (!PermissionAdministration.IsAdministrator(await identity.GetAsync(ct))) throw MeetingsRequestException.Forbidden();
    }

    private async Task RequirePermissionAsync(string key, CancellationToken ct)
    {
        if (!MeetingsPermissions.Keys.Contains(key, StringComparer.Ordinal) || !await store.ExistsAsync(key, ct))
            throw MeetingsRequestException.NotFound("No se encontró el permiso.");
    }

    private async Task<string[]> ValidateRolesAsync(IReadOnlyList<string> roles, CancellationToken ct)
    {
        if (roles.Count > 1000) throw MeetingsRequestException.Invalid("La lista contiene demasiados roles.");
        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role) || role.Length > 100 || role.Any(char.IsControl) ||
                role.Normalize(NormalizationForm.FormKC).Contains(','))
                throw MeetingsRequestException.Invalid("Cada rol debe ser un nombre válido de hasta 100 caracteres, sin comas ni caracteres de control.");
            if (!requested.Add(MeetingsPermissions.NormalizeRole(role)))
                throw MeetingsRequestException.Invalid("La lista contiene roles duplicados.");
        }
        if (requested.Count == 0) return [];
        var catalog = await directory.GetActiveRoleNamesAsync(ct);
        var result = new List<string>();
        foreach (var role in roles)
        {
            var matches = catalog.Where(name => MeetingsPermissions.NormalizeRole(name) == MeetingsPermissions.NormalizeRole(role)).ToArray();
            if (matches.Length != 1) throw MeetingsRequestException.Invalid("Uno de los roles no existe, está inactivo o es ambiguo en Authentication.");
            var canonical = matches[0].Trim();
            if (canonical.Any(char.IsControl) || canonical.Normalize(NormalizationForm.FormKC).Contains(','))
                throw MeetingsRequestException.Invalid("El nombre del rol no puede representarse en la lista de permisos.");
            result.Add(canonical);
        }
        PermissionAdministration.SerializeRoles(result);
        return result.ToArray();
    }

    private static PermissionDto ToDto(MeetingsPermission p) => new(p.PermissionId, p.PermissionKey, PermissionAdministration.ParseRoles(p.PermissionValue));
}
