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
    public async Task<MeetingsIdentity> ResolveAsync(int userId, int roleId, string roleName, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT r.nameRole, c.solution_center_code
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
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var actualRole = TemplateApplication.NormalizeRole(reader.GetString(0));
                if (actualRole != TemplateApplication.NormalizeRole(roleName)) continue;
                roles.Add(actualRole);
                centers.Add(reader.GetString(1));
            }
            if (roles.Count == 0) throw MeetingsRequestException.Forbidden();
            return new(userId, roles, centers);
        }
        finally { if (opened) await connection.CloseAsync(); }
    }
}

