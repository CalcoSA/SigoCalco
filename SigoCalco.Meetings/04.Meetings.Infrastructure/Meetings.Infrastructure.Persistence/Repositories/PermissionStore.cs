using System.Data;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Meetings.Infrastructure.Persistence.Repositories;

public sealed class PermissionStore(MeetingsDbContext context) : IPermissionStore
{
    public async Task<IReadOnlyList<MeetingsPermission>> GetAsync(CancellationToken ct) =>
        await context.Set<MeetingsPermission>().FromSqlRaw("""
            SELECT permission_id, permission_key, COALESCE(permission_value, '') AS permission_value
            FROM sigocalco_meetings_permission
            """).AsNoTracking().OrderBy(p => p.PermissionId).ToListAsync(ct);

    public Task<bool> ExistsAsync(string key, CancellationToken ct) =>
        context.Set<MeetingsPermission>().AnyAsync(p => p.PermissionKey == key, ct);

    public Task<MeetingsPermission> AddRoleAsync(string key, string role, CancellationToken ct) => ChangeAsync(key, current =>
    {
        var roles = PermissionAdministration.ParseRoles(current);
        if (roles.Any(r => MeetingsPermissions.NormalizeRole(r) == MeetingsPermissions.NormalizeRole(role)))
            throw new MeetingsRequestException(409, "El rol ya está asignado al permiso.");
        return PermissionAdministration.SerializeRoles(roles.Append(role));
    }, ct);

    public Task<MeetingsPermission> ReplaceRolesAsync(string key, IReadOnlyList<string> roles, CancellationToken ct) =>
        ChangeAsync(key, _ => PermissionAdministration.SerializeRoles(roles), ct);

    private async Task<MeetingsPermission> ChangeAsync(string key, Func<string, string> change, CancellationToken ct)
    {
        if (!MeetingsPermissions.Keys.Contains(key, StringComparer.Ordinal)) throw MeetingsRequestException.NotFound("No se encontró el permiso.");
        var isolation = context.Database.IsMySql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable;
        await using var transaction = await context.Database.BeginTransactionAsync(isolation, ct);
        try
        {
            // Both POST and PUT lock the same existing row. The merge reads the last committed value after obtaining the lock.
            var query = context.Database.IsMySql()
                ? context.Set<MeetingsPermission>().FromSqlInterpolated($"SELECT permission_id, permission_key, COALESCE(permission_value, '') AS permission_value FROM sigocalco_meetings_permission WHERE permission_key = {key} FOR UPDATE")
                : context.Set<MeetingsPermission>().Where(p => p.PermissionKey == key);
            var permission = (await query.AsNoTracking().ToListAsync(ct)).SingleOrDefault()
                ?? throw MeetingsRequestException.NotFound("No se encontró el permiso.");
            var value = change(permission.PermissionValue);
            await context.Set<MeetingsPermission>().Where(p => p.PermissionId == permission.PermissionId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.PermissionValue, value), ct);
            await transaction.CommitAsync(ct);
            permission.PermissionValue = value;
            return permission;
        }
        catch (MySqlException ex) when (ex.Number is 1205 or 1213)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new MeetingsRequestException(409, "Conflicto concurrente al actualizar el permiso; vuelva a intentar.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
