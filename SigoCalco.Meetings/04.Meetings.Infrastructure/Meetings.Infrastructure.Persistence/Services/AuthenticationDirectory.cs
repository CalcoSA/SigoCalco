using System.Data;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Exceptions;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Meetings.Infrastructure.Persistence.Services;

// Temporary shared-database adapter. SELECT only; no Authentication entities are mapped in EF.
public sealed class AuthenticationDirectory(MeetingsDbContext context) : IAuthenticationDirectory
{
    public async Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(CancellationToken ct) =>
        await context.Database.SqlQueryRaw<string>("SELECT nameRole AS Value FROM sigocalco_authentication_role WHERE statusRole = 1 ORDER BY IdRole").ToListAsync(ct);

    public async Task<MeetingsIdentity> ResolveAsync(int userId, int roleId, string roleName, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT r.nameRole, c.solution_center_code, u.userName, u.email, u.userLogin
                FROM sigocalco_authentication_users u
                JOIN sigocalco_authentication_userrole ur ON ur.IdUser = u.IdUser
                JOIN sigocalco_authentication_role r ON r.IdRole = ur.IdRole
                JOIN sigocalco_authentication_solution_center c ON c.solution_center_id = ur.solution_center_id
                WHERE u.IdUser = @userId AND r.IdRole = @roleId AND u.statusUser = 1 AND r.statusRole = 1
                """;
            var user = command.CreateParameter(); user.ParameterName = "@userId"; user.Value = userId; command.Parameters.Add(user);
            var role = command.CreateParameter(); role.ParameterName = "@roleId"; role.Value = roleId; command.Parameters.Add(role);
            var roles = new HashSet<string>(StringComparer.Ordinal);
            var centers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? userName = null, email = null, userLogin = null;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var actualRole = MeetingsPermissions.NormalizeRole(reader.GetString(0));
                if (actualRole != MeetingsPermissions.NormalizeRole(roleName)) continue;
                roles.Add(actualRole);
                centers.Add(reader.GetString(1));
                userName = reader.IsDBNull(2) ? null : reader.GetString(2);
                email = reader.IsDBNull(3) ? null : reader.GetString(3);
                userLogin = reader.IsDBNull(4) ? null : reader.GetString(4);
            }
            if (roles.Count == 0) throw MeetingsRequestException.Forbidden();
            return new(userId, roles, centers, userName, email, userLogin);
        }
        finally { if (opened) await connection.CloseAsync(); }
    }
}

