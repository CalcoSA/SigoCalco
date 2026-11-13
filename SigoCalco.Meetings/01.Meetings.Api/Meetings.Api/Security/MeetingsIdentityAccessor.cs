using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;

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
        cached = await directory.ResolveAsync(userId, roleId, user.FindFirst("nameRole")!.Value, ct);
        return cached;
    }
}

