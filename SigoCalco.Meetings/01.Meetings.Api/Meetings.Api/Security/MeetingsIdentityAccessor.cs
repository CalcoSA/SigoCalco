using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;

namespace Meetings.Api.Security;

public sealed class MeetingsIdentityAccessor(IHttpContextAccessor accessor, IAuthenticationDirectory directory) : IMeetingsIdentity
{
    private MeetingsIdentity? cached;
    public async Task<MeetingsIdentity> GetAsync(CancellationToken ct)
    {
        if (cached is not null) return cached;
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true ||
            !int.TryParse(user.FindFirst("idUser")?.Value, out var userId) || userId <= 0 ||
            user.FindFirst("sub")?.Value != userId.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            !int.TryParse(user.FindFirst("idRole")?.Value, out var roleId) || roleId <= 0 ||
            string.IsNullOrWhiteSpace(user.FindFirst("nameRole")?.Value))
            throw new MeetingsRequestException(401, "La identidad autenticada no contiene usuario y rol válidos.");
        var resolved = await directory.ResolveAsync(userId, roleId, user.FindFirst("nameRole")!.Value, ct);
        var claims = user.FindAll("userLogin").ToArray();
        var login = claims.Length == 1 ? ParticipantIdentity.NormalizeLogin(claims[0].Value) : null;
        // A login is usable for document participation only when BOTH the signed claim and
        // the current Authentication catalog identify the same user. No catalog/email fallback.
        cached = resolved with { UserLogin = login is not null &&
            login == ParticipantIdentity.NormalizeLogin(resolved.UserLogin) ? login : null };
        return cached;
    }
}

